//! FFI round-trip tests. The tests link the crate as an rlib and call the
//! exported `extern "C"` functions directly in-process — identical ABI and
//! calling convention to the C# P/Invoke surface.

use std::ffi::{CStr, CString};
use std::os::raw::{c_char, c_int, c_uint};

use rustrender::{free_html, render_markdown, rustrender_version};

const RC_OK: c_int = 0;
const RC_INVALID_INPUT: c_int = 1;
const RC_NULL_POINTER: c_int = 4;

const OPT_SOURCE_LINE_ANCHORS: c_uint = 1 << 5;
const OPT_HIGHLIGHT: c_uint = 1 << 6;

fn call(md: &str, cwd: Option<&str>, options: c_uint) -> Result<String, c_int> {
    let cmd = CString::new(md).unwrap();
    let cwd_c = cwd.map(|s| CString::new(s).unwrap());
    let (cw, cl) = match &cwd_c {
        Some(s) => (s.as_ptr(), s.as_bytes().len()),
        None => (std::ptr::null(), 0),
    };
    let mut out: *mut c_char = std::ptr::null_mut();
    let mut len: usize = 0;
    let rc = unsafe {
        render_markdown(
            cmd.as_ptr(),
            cmd.as_bytes().len(),
            cw,
            cl,
            options,
            &mut out,
            &mut len,
        )
    };
    if rc != RC_OK {
        return Err(rc);
    }
    assert!(!out.is_null(), "success must set out_html");
    let s = unsafe { CStr::from_ptr(out) }
        .to_string_lossy()
        .into_owned();
    unsafe { free_html(out) };
    Ok(s)
}

#[test]
fn roundtrip_basic_render() {
    let html = call("# Hello\n\nworld\n", None, 0).expect("rc 0");
    assert!(html.contains("<h1"), "{html}");
    assert!(html.contains("user-content-hello"), "{html}");
    assert!(html.contains("<p>world</p>"), "{html}");
}

#[test]
fn anchors_with_source_line_option() {
    let html = call("# T\n\n- [ ] task\n", None, OPT_SOURCE_LINE_ANCHORS).unwrap();
    assert!(html.contains("data-line=\"1\""), "{html}");
    assert!(html.contains("data-src-line=\"1\""), "{html}");
    assert!(html.contains("data-line=\"3\""), "{html}");
}

#[test]
fn cwd_resolves_relative_images() {
    let html = call("![x](a/b.png)\n", Some("C:/docs"), 0).unwrap();
    assert!(html.contains("file:///C:/docs/a/b.png"), "{html}");
}

#[test]
fn invalid_utf8_rejected() {
    let bad: Vec<u8> = vec![0x23, 0x20, 0xFF, 0xFE]; // "# " + invalid
    let mut out: *mut c_char = std::ptr::null_mut();
    let mut len: usize = 0;
    let rc = unsafe {
        render_markdown(
            bad.as_ptr() as *const c_char,
            bad.len(),
            std::ptr::null(),
            0,
            0,
            &mut out,
            &mut len,
        )
    };
    assert_eq!(rc, RC_INVALID_INPUT);
    assert!(out.is_null());
}

#[test]
fn embedded_nul_rejected() {
    // Raw bytes with an interior NUL (CString::new would panic building this).
    let cmd: Vec<u8> = vec![b'a', 0, b'b'];
    let mut out: *mut c_char = std::ptr::null_mut();
    let mut len: usize = 0;
    let rc = unsafe {
        render_markdown(
            cmd.as_ptr() as *const c_char,
            cmd.len(),
            std::ptr::null(),
            0,
            0,
            &mut out,
            &mut len,
        )
    };
    assert_eq!(rc, RC_INVALID_INPUT);
}

#[test]
fn null_pointers_rejected() {
    let mut out: *mut c_char = std::ptr::null_mut();
    let mut len: usize = 0;
    let rc = unsafe {
        render_markdown(
            std::ptr::null(),
            0,
            std::ptr::null(),
            0,
            0,
            &mut out,
            &mut len,
        )
    };
    assert_eq!(rc, RC_NULL_POINTER);

    let md = CString::new("x").unwrap();
    let rc = unsafe {
        render_markdown(
            md.as_ptr(),
            1,
            std::ptr::null(),
            0,
            0,
            std::ptr::null_mut(),
            &mut len,
        )
    };
    assert_eq!(rc, RC_NULL_POINTER);
}

#[test]
fn hostile_nesting_does_not_crash() {
    // 50k-deep blockquote nesting must not panic the boundary: at worst a
    // contained panic (rc 2), never a process crash. Reaching the assert is
    // itself the survival proof — a stack overflow would abort the process
    // before it gets here.
    let md = "> ".repeat(50_000) + "x";
    match call(&md, None, 0) {
        Ok(html) => assert!(html.contains("<p>x</p>") || html.contains("x"), "{html}"),
        Err(rc) => assert_eq!(rc, 2, "unexpected error code: {rc}"),
    }
    // Also survive 100k "#" on one line (hostile ATX heading prefix).
    let md2 = "#".repeat(100_000) + " x";
    let _ = call(&md2, None, 0);
}

#[test]
fn highlight_theme_bits_flow_through_ffi() {
    // Bits 7-9 carry the syntect theme class across the C ABI: 0 = auto
    // (legacy dark_mode pair), 1..=6 pin a palette. Bit 6 gates highlighting.
    let md = "```rust\nfn main() { let x = 1; }\n```\n";
    let auto = call(md, None, OPT_HIGHLIGHT).unwrap();
    let warm = call(md, None, OPT_HIGHLIGHT | (4 << 7)).unwrap();
    assert_ne!(auto, warm, "theme class 4 must change highlight HTML");
    // A pinned warm-light class (2) also differs from the warm dark one (4).
    let light_warm = call(md, None, OPT_HIGHLIGHT | (2 << 7)).unwrap();
    assert_ne!(light_warm, warm);
}

#[test]
fn version_nonempty() {
    let v = unsafe { CStr::from_ptr(rustrender_version()) }
        .to_string_lossy()
        .into_owned();
    assert!(v.starts_with("rustrender "), "{v}");
}

#[test]
fn huge_document_smoke() {
    let mut md = String::with_capacity(1 << 20);
    for i in 0..5000 {
        md.push_str(&format!(
            "## Section {i}\n\ntext **bold** `code`\n\n| a | b |\n|---|---|\n| 1 | 2 |\n\n"
        ));
    }
    let html = call(&md, None, OPT_SOURCE_LINE_ANCHORS).unwrap();
    assert!(html.len() > 100_000);
}
