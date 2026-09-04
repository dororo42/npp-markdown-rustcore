//! # rustrender-core
//!
//! Shared Markdown rendering core for the NppMarkdownPanel Rust fork.
//!
//! Pipeline: **comrak parse → AST walk (headings) → HTML render (syntect,
//! feature-gated) → block line anchors (`data-line` / `data-src-line`) →
//! ammonia sanitize**.
//!
//! Design notes (v4.0 定稿):
//! - Raw HTML is passed through by comrak (`render.unsafe_ = true`) and then
//!   sanitized by ammonia — robust allowlist filtering, not escaping.
//! - `data-line` (block start line) is injected for every block that carries
//!   a `data-sourcepos` start line. This is the contract consumed by the
//!   upstream Webview2 control (scroll sync + checkbox/radio callbacks).
//! - Headings additionally receive `data-src-line` (v4.0 contract).
//! - Relative image/link URLs are rewritten to absolute `file:///` URLs by a
//!   comrak `URLRewriter` — no post-processing regex passes.
//! - Frontmatter is enabled (`---`) and comrak emits nothing for it, so YAML
//!   never leaks into the preview.
//! - Mermaid/KaTeX: the core keeps `class="language-mermaid"` / math as-is;
//!   rendering is a front-end concern (`web/` assets, route C).

pub mod highlight;
pub mod resolve;
pub mod sanitize;

use std::sync::Arc;

use comrak::nodes::{AstNode, NodeValue};
use comrak::{parse_document, Arena, Options};

use crate::resolve::PathResolver;

/// Render options (FFI/WASM-facing; mirrors the v4.0 contract).
#[derive(Debug, Clone)]
pub struct RenderOptions {
    /// `true` → dark syntect theme + dark styling decisions downstream.
    pub dark_mode: bool,
    /// GitHub-style callouts (`> [!NOTE]`) — comrak `alerts` extension.
    pub enable_callout: bool,
    /// `[[Page]]` / `[[Page|alias]]` wikilinks.
    pub enable_wikilink: bool,
    /// Reserved: mermaid fences stay `language-mermaid` code blocks;
    /// actual rendering happens in the front-end (route C).
    pub enable_mermaid: bool,
    /// Reserved: KaTeX rendering happens in the front-end (route C).
    pub enable_katex: bool,
    /// Emit `data-line` / `data-src-line` / `data-sourcepos` block anchors.
    pub source_line_anchors: bool,
    /// Native syntect highlighting (no-op on builds without `syntax-highlight`).
    pub highlight: bool,
    /// Syntect highlight theme class (FFI bits 7-9). `0` = auto (the legacy
    /// `dark_mode` pair); `1..=6` pin an explicit theme so hosts can match
    /// code-block colors to the preview palette (warm palette → warm theme).
    pub highlight_theme: u8,
}

impl Default for RenderOptions {
    fn default() -> Self {
        RenderOptions {
            dark_mode: false,
            enable_callout: true,
            enable_wikilink: true,
            enable_mermaid: true,
            enable_katex: true,
            source_line_anchors: true,
            highlight: true,
            highlight_theme: 0,
        }
    }
}

impl RenderOptions {
    /// Preset used by the bench harness: everything on, line anchors on.
    pub fn source_anchors() -> Self {
        RenderOptions::default()
    }

    /// Bit flags used by the FFI/WASM boundary (bit order = v4.0 contract;
    /// bits 7-9 = highlight theme class, see `RenderOptions::highlight_theme`).
    pub fn from_bits(bits: u32) -> Self {
        RenderOptions {
            dark_mode: bits & 1 != 0,
            enable_callout: bits & (1 << 1) != 0,
            enable_wikilink: bits & (1 << 2) != 0,
            enable_mermaid: bits & (1 << 3) != 0,
            enable_katex: bits & (1 << 4) != 0,
            source_line_anchors: bits & (1 << 5) != 0,
            highlight: bits & (1 << 6) != 0,
            highlight_theme: ((bits >> 7) & 0x7) as u8,
        }
    }
}

/// A document heading with its source line (scroll-sync TOC + anchors).
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Heading {
    pub level: u8,
    /// 1-based line of the heading start in the Markdown source.
    pub line: usize,
    pub text: String,
}

/// Render result.
#[derive(Debug, Clone)]
pub struct RenderOutput {
    /// Sanitized HTML body fragment (no `<html>`/`<body>` wrapper).
    pub html_body: String,
    /// Headings in document order.
    pub headings: Vec<Heading>,
    /// Non-fatal diagnostics.
    pub warnings: Vec<String>,
}

/// Render error (message suitable for surfacing in the preview pane).
#[derive(Debug, Clone)]
pub struct RenderError(pub String);

impl std::fmt::Display for RenderError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.write_str(&self.0)
    }
}

impl std::error::Error for RenderError {}

/// Render Markdown → sanitized HTML.
///
/// `cwd` is the document directory used to resolve relative image/link paths.
pub fn render(
    markdown: &str,
    cwd: Option<&str>,
    opts: &RenderOptions,
) -> Result<RenderOutput, RenderError> {
    let mut options = Options::default();

    // ---- GFM extensions ----------------------------------------------------
    options.extension.strikethrough = true;
    // NOTE: tagfilter stays OFF. With render.unsafe_ the tagfilter merely
    // escapes <script>/<style> into visible text; ammonia (next stage)
    // removes them together with their content — closer to GitHub parity.
    options.extension.table = true;
    options.extension.autolink = true;
    options.extension.tasklist = true;
    options.extension.superscript = true;
    options.extension.footnotes = true;
    options.extension.description_lists = true;

    // Heading anchors (`user-content-` prefix, GitHub parity).
    options.extension.header_id_prefix = Some("user-content-".to_string());
    options.extension.header_id_prefix_in_href = true;

    // Frontmatter: parsed and rendered as *nothing* (no YAML leakage).
    options.extension.front_matter_delimiter = Some("---".to_string());

    if opts.enable_callout {
        options.extension.alerts = true;
    }
    if opts.enable_wikilink {
        // Obsidian semantics: [[target|display title]].
        options.extension.wikilinks_title_after_pipe = true;
    }

    // ---- render options ----------------------------------------------------
    // Raw HTML goes through ammonia afterwards; escaping here would make
    // allowlist sanitization impossible.
    options.render.r#unsafe = true;
    options.render.sourcepos = opts.source_line_anchors;
    // NOTE: github_pre_lang stays OFF so `<code>` keeps `class="language-x"`
    // (required by mermaid/katex/highlight.js front-ends).

    // ---- path resolution ---------------------------------------------------
    if let Some(dir) = cwd {
        let img = Arc::new(PathResolver::new(dir)) as Arc<dyn comrak::options::URLRewriter>;
        let link = Arc::new(PathResolver::new(dir)) as Arc<dyn comrak::options::URLRewriter>;
        options.extension.image_url_rewriter = Some(img);
        options.extension.link_url_rewriter = Some(link);
    }

    // ---- parse + headings --------------------------------------------------
    let arena = Arena::new();
    let root = parse_document(&arena, markdown, &options);

    let mut headings: Vec<Heading> = Vec::new();
    if opts.source_line_anchors {
        extract_headings(root, &mut headings);
    }

    // ---- render ------------------------------------------------------------
    let plugins = highlight::make_plugins(opts);
    let mut html = String::with_capacity(markdown.len() * 2 + 64);
    comrak::format_html_with_plugins(root, &options, &mut html, &plugins)
        .map_err(|e| RenderError(format!("render failed: {e}")))?;

    // ---- block anchors -----------------------------------------------------
    if opts.source_line_anchors {
        html = inject_line_anchors(&html);
    }

    // ---- sanitize ----------------------------------------------------------
    let html = sanitize::sanitize(&html).map_err(RenderError)?;

    Ok(RenderOutput {
        html_body: html,
        headings,
        warnings: Vec::new(),
    })
}

/// Pre-order AST walk collecting headings (level, source line, text).
fn extract_headings<'a>(node: &'a AstNode<'a>, out: &mut Vec<Heading>) {
    let hit = {
        let d = node.data.borrow();
        match &d.value {
            NodeValue::Heading(h) => Some((h.level, d.sourcepos.start.line)),
            _ => None,
        }
    };
    if let Some((level, line)) = hit {
        out.push(Heading {
            level,
            line,
            text: extract_text(node),
        });
    }
    for child in node.children() {
        extract_headings(child, out);
    }
}

/// Plain text of a node's inline content (Text + inline Code).
fn extract_text<'a>(node: &'a AstNode<'a>) -> String {
    let owned = {
        let d = node.data.borrow();
        match &d.value {
            NodeValue::Text(s) => Some(s.to_string()),
            NodeValue::Code(c) => Some(c.literal.clone()),
            NodeValue::SoftBreak | NodeValue::LineBreak => Some(" ".to_string()),
            _ => None,
        }
    };
    if let Some(s) = owned {
        return s;
    }
    let mut s = String::new();
    for child in node.children() {
        s.push_str(&extract_text(child));
    }
    s
}

const DATA_SOURCEPOS: &str = "data-sourcepos=\"";
const DATA_LINE: &str = "data-line=\"";

/// Block tags that receive `data-line` anchors (superset of what the host's
/// scroll-sync and checkbox callbacks query).
const ANCHORED_TAGS: &[&str] = &[
    "h1",
    "h2",
    "h3",
    "h4",
    "h5",
    "h6",
    "div",
    "p",
    "li",
    "blockquote",
    "pre",
    "table",
    "ul",
    "ol",
    "dl",
    "dt",
    "dd",
    "td",
    "th",
    "tr",
    "aside",
    "section",
    "hr",
    "input",
    "a",
    "img",
    "code",
    "span",
];

/// Inject `data-line` on every block tag carrying `data-sourcepos="L:..."`,
/// and additionally `data-src-line` on headings (`<h1>`…`<h6>`).
///
/// Tag scanning is quote-aware and byte-level (only ASCII delimiters are
/// matched; non-ASCII attribute text is copied by slice, UTF-8-safe).
/// Occurrences of the attribute inside code text are never touched because
/// comrak HTML-escapes `"` there.
pub fn inject_line_anchors(html: &str) -> String {
    let bytes = html.as_bytes();
    let n = bytes.len();
    let mut out = String::with_capacity(html.len() + 128);
    let mut i = 0;

    while i < n {
        if bytes[i] == b'<' {
            if let Some((after_name, is_heading, line, has_line)) =
                scan_open_tag(html, i, ANCHORED_TAGS)
            {
                out.push_str(&html[i..after_name]);
                if let Some(line) = line.filter(|_| !has_line) {
                    out.push_str(&format!(" {DATA_LINE}{line}\""));
                    if is_heading {
                        out.push_str(&format!(" data-src-line=\"{line}\""));
                    }
                }
                // Copy the remainder of the open tag verbatim.
                i = copy_through_tag_end(html, after_name, &mut out);
                continue;
            }
        }
        // Copy one UTF-8 scalar.
        let len = utf8_len(bytes[i]);
        let end = (i + len).min(n);
        out.push_str(&html[i..end]);
        i = end;
    }
    out
}

/// Scan an open tag starting at `i` (`bytes[i] == b'<'`).
///
/// Returns `Some((index_after_tag_name, is_heading, sourcepos_start_line,
/// already_has_data_line))` when this is an open tag whose name is in
/// `tags`, else `None`. The scan runs to the tag's `>` (quote-aware) to
/// detect the attributes, but consumes nothing.
fn scan_open_tag(html: &str, i: usize, tags: &[&str]) -> Option<(usize, bool, Option<u64>, bool)> {
    let bytes = html.as_bytes();
    let n = bytes.len();
    let mut j = i + 1;
    if j >= n || !bytes[j].is_ascii_alphabetic() {
        return None;
    }
    let name_start = j;
    while j < n && (bytes[j].is_ascii_alphanumeric() || bytes[j] == b'-') {
        j += 1;
    }
    let name = &html[name_start..j];
    if !tags.contains(&name) {
        return None;
    }
    let is_heading = matches!(name, "h1" | "h2" | "h3" | "h4" | "h5" | "h6");

    // Walk the attribute region to the closing `>`, quote-aware.
    let mut k = j;
    let mut quote = false;
    let mut line: Option<u64> = None;
    let mut has_line = false;
    while k < n {
        let c = bytes[k];
        if quote {
            if c == b'"' {
                quote = false;
            }
        } else if c == b'"' {
            quote = true;
        } else if c == b'>' {
            break;
        } else if c == b'd' {
            if html[k..].starts_with(DATA_SOURCEPOS) {
                let mut m = k + DATA_SOURCEPOS.len();
                let mut v: u64 = 0;
                let mut saw = false;
                while m < n && bytes[m].is_ascii_digit() {
                    v = v * 10 + u64::from(bytes[m] - b'0');
                    m += 1;
                    saw = true;
                }
                if saw && m < n && bytes[m] == b':' {
                    line = Some(v);
                }
            } else if html[k..].starts_with(DATA_LINE) {
                has_line = true;
            }
        }
        k += 1;
    }
    if k >= n {
        return None; // unterminated tag — leave untouched
    }
    Some((j, is_heading, line, has_line))
}

/// Copy from `from` through (and including) the tag's closing `>`.
/// Returns the index just after `>`.
fn copy_through_tag_end(html: &str, from: usize, out: &mut String) -> usize {
    let bytes = html.as_bytes();
    let n = bytes.len();
    let mut j = from;
    let mut quote = false;
    while j < n {
        let c = bytes[j];
        if quote {
            if c == b'"' {
                quote = false;
            }
        } else if c == b'"' {
            quote = true;
        } else if c == b'>' {
            j += 1;
            break;
        }
        j += 1;
    }
    out.push_str(&html[from..j.min(n)]);
    j
}

fn utf8_len(b: u8) -> usize {
    if b < 0x80 {
        1
    } else if b >> 5 == 0b110 {
        2
    } else if b >> 4 == 0b1110 {
        3
    } else {
        4
    }
}
