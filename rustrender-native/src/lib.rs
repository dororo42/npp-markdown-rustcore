//! # rustrender-native
//!
//! FFI boundary of `rustrender-core` for the C# host (route A').
//!
//! Contract (v4.0 定稿):
//! - `render_markdown(md, md_len, cwd, cwd_len, options, out_html, out_len) -> c_int`
//!   `0` success, `1` invalid input (UTF-8/NUL), `2` panic caught,
//!   `3` internal render error, `4` null pointer.
//! - `out_html` receives a NUL-terminated UTF-8 string allocated by Rust;
//!   the caller MUST release it with `free_html`.
//! - Panics are caught with `catch_unwind`; the process never crashes from
//!   a rendering panic — a hostile document at most yields error code 2.
#![deny(clippy::all)]

use std::os::raw::{c_char, c_int};
use std::panic::{catch_unwind, AssertUnwindSafe};
use std::slice;
use std::sync::mpsc;
use std::sync::OnceLock;

use rustrender_core::{render as core_render, RenderOptions, RenderOutput};

const RC_OK: c_int = 0;
const RC_INVALID_INPUT: c_int = 1;
const RC_PANIC: c_int = 2;
const RC_RENDER_ERROR: c_int = 3;
const RC_NULL_POINTER: c_int = 4;

// ---------------------------------------------------------------------------
// Persistent render worker
//
// Panic + stack-overflow isolation (v4.0 requirement): catch_unwind alone
// cannot stop a stack overflow — deep blockquote/heading nesting recurses
// inside comrak's formatter (and in AST drop glue). Renders run on a
// dedicated big-stack worker whose stack is reserved ONCE and reused for
// every document (the previous design spawned a fresh 512 MiB-stack thread
// per render: thread create/destroy on every debounced keystroke render, and
// a 512 MiB address-space reserve that 32-bit hosts cannot afford).
//
// Worker stack budget: virtual reserve, only touched pages commit. Debug
// builds have several-fold larger frames than release, hence the headroom.
#[cfg(target_pointer_width = "64")]
const RENDER_STACK_SIZE: usize = 256 * 1024 * 1024;
#[cfg(target_pointer_width = "32")]
const RENDER_STACK_SIZE: usize = 64 * 1024 * 1024;

struct RenderJob {
    md: String,
    cwd: Option<String>,
    opts: RenderOptions,
    tx: mpsc::Sender<RenderOutcome>,
}

enum RenderOutcome {
    Done(RenderOutput),
    Failed,
    Panicked,
}

static WORKER_TX: OnceLock<mpsc::Sender<RenderJob>> = OnceLock::new();

/// The persistent render worker handle (spawned lazily on first render).
/// Panics are contained per job via `catch_unwind`, so one hostile document
/// cannot take the worker down. A true stack overflow still aborts the
/// process — that property is unchanged from the per-render-thread design.
fn worker() -> &'static mpsc::Sender<RenderJob> {
    WORKER_TX.get_or_init(|| {
        let (tx, rx) = mpsc::channel::<RenderJob>();
        std::thread::Builder::new()
            .stack_size(RENDER_STACK_SIZE)
            .name("rustrender-worker".to_string())
            .spawn(move || {
                for job in rx {
                    let outcome = match catch_unwind(AssertUnwindSafe(|| {
                        core_render(&job.md, job.cwd.as_deref(), &job.opts)
                    })) {
                        Ok(Ok(output)) => RenderOutcome::Done(output),
                        Ok(Err(_)) => RenderOutcome::Failed,
                        Err(_) => RenderOutcome::Panicked,
                    };
                    let _ = job.tx.send(outcome);
                }
            })
            .expect("spawn rustrender worker");
        tx
    })
}

/// Build metadata, exposed for diagnostics (shown in the plugin About box).
#[no_mangle]
pub extern "C" fn rustrender_version() -> *const c_char {
    concat!(
        "rustrender ",
        env!("CARGO_PKG_VERSION"),
        " (comrak 0.54 / syntect)\0"
    )
    .as_ptr() as *const c_char
}

/// Render Markdown (UTF-8) to sanitized HTML (UTF-8, NUL-terminated).
///
/// # Safety
/// - `md_ptr`/`md_len` must describe a valid UTF-8 buffer of `md_len` bytes.
/// - `cwd_ptr`/`cwd_len` may be null/0 to skip path resolution.
/// - `out_html`/`out_len` must be valid writable pointers; the caller owns
///   the returned buffer and must free it via [`free_html`].
#[no_mangle]
pub unsafe extern "C" fn render_markdown(
    md_ptr: *const c_char,
    md_len: usize,
    cwd_ptr: *const c_char,
    cwd_len: usize,
    options: u32,
    out_html: *mut *mut c_char,
    out_len: *mut usize,
) -> c_int {
    if md_ptr.is_null() || out_html.is_null() || out_len.is_null() {
        return RC_NULL_POINTER;
    }

    let md_bytes = slice::from_raw_parts(md_ptr as *const u8, md_len);
    let cwd: Option<String> = if cwd_ptr.is_null() || cwd_len == 0 {
        None
    } else {
        let cwd_bytes = slice::from_raw_parts(cwd_ptr as *const u8, cwd_len);
        match std::str::from_utf8(cwd_bytes) {
            Ok(s) => Some(s.to_string()),
            Err(_) => return RC_INVALID_INPUT,
        }
    };

    let md = match std::str::from_utf8(md_bytes) {
        Ok(s) => s,
        Err(_) => return RC_INVALID_INPUT,
    };

    if md.contains('\0') {
        return RC_INVALID_INPUT;
    }

    let opts = RenderOptions::from_bits(options);

    // Hand the job to the persistent worker and wait for the outcome.
    // The blocking wait is bounded by the render itself; the caller (a
    // .NET ThreadPool thread) is free while this runs.
    let (tx, rx) = mpsc::channel();
    if worker()
        .send(RenderJob {
            md: md.to_string(),
            cwd,
            opts,
            tx,
        })
        .is_err()
    {
        // Worker is gone — treat like a caught panic.
        return RC_PANIC;
    }

    let rendered = match rx.recv() {
        Ok(RenderOutcome::Done(output)) => output,
        Ok(RenderOutcome::Failed) => return RC_RENDER_ERROR,
        // A panic was caught inside the worker, or the worker channel broke.
        Ok(RenderOutcome::Panicked) | Err(_) => return RC_PANIC,
    };

    let html = rendered.html_body;
    if html.contains('\0') {
        return RC_INVALID_INPUT;
    }

    let chtml = match std::ffi::CString::new(html) {
        Ok(s) => s,
        Err(_) => return RC_INVALID_INPUT,
    };

    unsafe {
        *out_len = chtml.as_bytes().len();
        *out_html = chtml.into_raw() as *mut c_char;
    }
    RC_OK
}

/// Free a buffer previously returned by [`render_markdown`].
///
/// # Safety
/// `ptr` must be a pointer returned by [`render_markdown`] that has not been
/// freed yet (passing null is a no-op).
#[no_mangle]
pub unsafe extern "C" fn free_html(ptr: *mut c_char) {
    if !ptr.is_null() {
        drop(std::ffi::CString::from_raw(ptr));
    }
}
