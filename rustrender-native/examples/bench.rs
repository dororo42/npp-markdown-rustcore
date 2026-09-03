//! Cross-platform benchmark harness for the native render path.
//! (The Windows-oriented `bench/run.ps1` wraps this binary.)
//!
//! Usage: cargo run --release -p rustrender-native --example bench -- <file.md> [iters]

use std::time::Instant;

fn main() {
    let args: Vec<String> = std::env::args().collect();
    let path = args.get(1).map(String::as_str).unwrap_or("bench/100KB.md");
    let iters: u32 = args.get(2).and_then(|s| s.parse().ok()).unwrap_or(10);

    let md = std::fs::read_to_string(path).unwrap_or_else(|e| {
        eprintln!("cannot read {path}: {e}");
        std::process::exit(2);
    });

    // Optional 3rd arg: raw FFI option bits (default: all on).
    let bits: u32 = args
        .get(3)
        .and_then(|s| u32::from_str_radix(s, 16).ok())
        .unwrap_or(0x7F);

    // Warm-up (also validates the render).
    let out = rustrender_core::render(&md, None, &rustrender_core::RenderOptions::from_bits(bits))
        .unwrap_or_else(|e| {
            eprintln!("render failed: {e}");
            std::process::exit(1);
        });
    eprintln!(
        "input {} bytes -> html {} bytes, {} headings; {} iters (bits={bits:#x}):",
        md.len(),
        out.html_body.len(),
        out.headings.len(),
        iters
    );

    let mut total = 0u128;
    let mut worst = 0u128;
    for _ in 0..iters {
        let t0 = Instant::now();
        let o =
            rustrender_core::render(&md, None, &rustrender_core::RenderOptions::from_bits(bits))
                .expect("render");
        let dt = t0.elapsed().as_micros();
        total += dt;
        worst = worst.max(dt);
        std::hint::black_box(&o);
    }
    let avg = total / iters as u128;
    println!(
        "{{\"file\":\"{path}\",\"bytes\":{},\"iters\":{},\"avg_us\":{avg},\"worst_us\":{worst}}}",
        md.len(),
        iters
    );
}
