//! # rustrender-wasm
//!
//! WASM outlet of `rustrender-core` (route C experiment).
//!
//! Built for `wasm32-unknown-unknown` **without** syntect (pure-Rust core
//! only); code fences keep `class="language-x"` so the WebView2 front-end
//! (highlight.js) performs highlighting. See `web/worker.js`.
//!
//! Build:
//! ```sh
//! cargo build -p rustrender-wasm --target wasm32-unknown-unknown --release
//! wasm-bindgen --target web --out-dir web/bindings \
//!   target/wasm32-unknown-unknown/release/rustrender_wasm_bg.wasm
//! ```

use serde::{Deserialize, Serialize};
use wasm_bindgen::prelude::*;

/// Options accepted from JS (snake_case fields; all optional).
#[derive(Debug, Clone, Default, Deserialize)]
#[serde(default)]
pub struct JsRenderOptions {
    pub dark_mode: bool,
    pub enable_callout: bool,
    pub enable_wikilink: bool,
    pub enable_mermaid: bool,
    pub enable_katex: bool,
    pub source_line_anchors: bool,
    #[serde(default = "default_true")]
    pub highlight: bool,
}

fn default_true() -> bool {
    true
}

/// Result payload returned to JS.
#[derive(Debug, Serialize)]
pub struct JsRenderOutput {
    pub html_body: String,
    pub headings: Vec<JsHeading>,
    pub warnings: Vec<String>,
    /// Milliseconds spent in the core render (diagnostics).
    pub render_ms: f64,
}

#[derive(Debug, Serialize)]
pub struct JsHeading {
    pub level: u8,
    pub line: usize,
    pub text: String,
}

/// Render Markdown to sanitized HTML.
///
/// JS: `render_markdown(text, { dark_mode: false, ... })` → object.
#[wasm_bindgen]
pub fn render_markdown(markdown: &str, options: JsValue) -> Result<JsValue, JsValue> {
    let opts: JsRenderOptions = if options.is_undefined() || options.is_null() {
        JsRenderOptions::default()
    } else {
        serde_wasm_bindgen::from_value(options).map_err(|e| JsValue::from_str(&e.to_string()))?
    };

    let core_opts = rustrender_core::RenderOptions {
        dark_mode: opts.dark_mode,
        enable_callout: opts.enable_callout,
        enable_wikilink: opts.enable_wikilink,
        enable_mermaid: opts.enable_mermaid,
        enable_katex: opts.enable_katex,
        source_line_anchors: opts.source_line_anchors,
        // WASM builds carry no syntect; highlight flag intentionally ignored.
        highlight: false,
    };

    let t0 = js_sys::Date::now();
    // cwd is not resolvable inside WASM: path fixing stays a host/core concern.
    let out = rustrender_core::render(markdown, None, &core_opts)
        .map_err(|e| JsValue::from_str(&e.to_string()))?;
    let render_ms = js_sys::Date::now() - t0;

    let payload = JsRenderOutput {
        html_body: out.html_body,
        headings: out
            .headings
            .into_iter()
            .map(|h| JsHeading {
                level: h.level,
                line: h.line,
                text: h.text,
            })
            .collect(),
        warnings: out.warnings,
        render_ms,
    };
    serde_wasm_bindgen::to_value(&payload).map_err(|e| JsValue::from_str(&e.to_string()))
}

/// Convenience JSON-in/JSON-out variant (same options/result schema).
#[wasm_bindgen]
pub fn render_markdown_json(markdown: &str, options_json: &str) -> Result<String, JsValue> {
    let opts: JsRenderOptions = if options_json.is_empty() {
        JsRenderOptions::default()
    } else {
        serde_json::from_str(options_json)
            .map_err(|e| JsValue::from_str(&format!("bad options: {e}")))?
    };

    let core_opts = rustrender_core::RenderOptions {
        dark_mode: opts.dark_mode,
        enable_callout: opts.enable_callout,
        enable_wikilink: opts.enable_wikilink,
        enable_mermaid: opts.enable_mermaid,
        enable_katex: opts.enable_katex,
        source_line_anchors: opts.source_line_anchors,
        highlight: false,
    };

    let out = rustrender_core::render(markdown, None, &core_opts)
        .map_err(|e| JsValue::from_str(&e.to_string()))?;

    let payload = JsRenderOutput {
        html_body: out.html_body,
        headings: out
            .headings
            .into_iter()
            .map(|h| JsHeading {
                level: h.level,
                line: h.line,
                text: h.text,
            })
            .collect(),
        warnings: out.warnings,
        render_ms: 0.0,
    };
    serde_json::to_string(&payload).map_err(|e| JsValue::from_str(&e.to_string()))
}

#[wasm_bindgen]
pub fn version() -> String {
    format!("rustrender-wasm {}", env!("CARGO_PKG_VERSION"))
}
