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

use rustrender_core::{render as core_render, RenderOptions};

const RC_OK: c_int = 0;
const RC_INVALID_INPUT: c_int = 1;
const RC_PANIC: c_int = 2;
const RC_RENDER_ERROR: c_int = 3;
const RC_NULL_POINTER: c_int = 4;

/// Build metadata, exposed for diagnostics (shown in the plugin About box).
#[no_mangle]
pub extern "C" fn rustrender_version() -> *const c_char {
    concat!("rustrender ", env!("CARGO_PKG_VERSION"), " (comrak 0.54 / syntect)\0")
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

    // Panic + stack-overflow isolation (v4.0 requirement).
    // catch_unwind alone cannot stop a stack overflow — deep blockquote/heading
    // nesting recurses inside comrak's formatter (and in AST drop glue). The
    // render runs on a dedicated 512 MiB-stack thread (stack is reserved
    // virtual memory; only touched pages commit), so hostile documents can at
    // worst produce an error code — never a host crash. Debug builds have
    // several-fold larger frames than release, hence the generous budget.
    const RENDER_STACK_SIZE: usize = 512 * 1024 * 1024;

    let md_owned = md.to_string();
    let joined = match std::thread::Builder::new()
        .stack_size(RENDER_STACK_SIZE)
        .spawn(move || {
            catch_unwind(AssertUnwindSafe(|| {
                core_render(&md_owned, cwd.as_deref(), &opts)
            }))
        }) {
        Ok(handle) => handle.join(),
        // Thread could not be spawned — treat like a caught panic.
        Err(_) => Err(Box::new("render thread spawn failed")
            as Box<dyn std::any::Any + Send>),
    };

    let rendered = match joined {
        Ok(Ok(Ok(output))) => output,
        Ok(Ok(Err(_))) => return RC_RENDER_ERROR,
        // Panic escaped catch_unwind (or the thread could not be spawned).
        Ok(Err(_)) | Err(_) => return RC_PANIC,
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
