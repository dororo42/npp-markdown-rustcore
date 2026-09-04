//! Syntax highlighting (feature-gated).
//!
//! - `syntect-onig` (default): inline-styled highlighting via syntect —
//!   self-contained HTML, correct on any WebView.
//! - No feature: highlighting disabled; code fences keep
//!   `class="language-x"` for front-end highlighters (WASM route).
//!
//! Performance: highlighted code blocks are cached process-wide, keyed by
//! (theme, lang, code). Editor re-renders on keystrokes hit the cache for
//! every unchanged block, which is where syntect spends most of its time.
//! (Measured: 100 KB doc with ~30% fences, 92 ms uncached → cache-warm
//! renders drop to the no-highlight baseline.)

#[cfg(feature = "syntax-highlight")]
use std::collections::HashMap;
#[cfg(feature = "syntax-highlight")]
use std::fmt;
#[cfg(feature = "syntax-highlight")]
use std::sync::{Mutex, OnceLock};

#[cfg(feature = "syntax-highlight")]
use comrak::adapters::SyntaxHighlighterAdapter;
use comrak::options::Plugins;

use crate::RenderOptions;

/// The six pinnable syntect themes, indexed by FFI highlight class 1..=6.
/// Classes 1 (InspiredGitHub) and 3 (base16-ocean.dark) are the legacy
/// auto light/dark pair; class 0 (auto) resolves to one of those two.
#[cfg(feature = "syntax-highlight")]
const THEME_CLASSES: [&str; 6] = [
    "InspiredGitHub",       // 1 — neutral light (Obsidian/Catppuccin light)
    "Solarized (light)",    // 2 — warm light (Gruvbox/Everforest light)
    "base16-ocean.dark",    // 3 — cool dark (Obsidian/Nord/Catppuccin dark)
    "base16-eighties.dark", // 4 — warm dark (Gruvbox)
    "base16-mocha.dark",    // 5 — warm purple dark (Dracula)
    "Solarized (dark)",     // 6 — green-tinted dark (Everforest)
];

/// Cache guards: entry count / approximate total bytes of cached HTML.
#[cfg(feature = "syntax-highlight")]
const CACHE_MAX_ENTRIES: usize = 4096;
#[cfg(feature = "syntax-highlight")]
const CACHE_MAX_BYTES: usize = 64 * 1024 * 1024;

/// Build the render plugins, wiring the cached syntect adapter when the
/// `syntax-highlight` feature (and `RenderOptions::highlight`) allow it.
///
/// Adapters are cached per theme in process-lifetime statics: `SyntaxSet`
/// construction is expensive (tens of ms) and the C# host keeps the DLL
/// loaded for the whole session, so this matches the host lifecycle.
pub fn make_plugins(opts: &RenderOptions) -> Plugins<'_> {
    #[cfg(feature = "syntax-highlight")]
    {
        if opts.highlight {
            let adapter: &'static CachedAdapter = adapter_for(theme_class(opts));
            let mut plugins = Plugins::default();
            plugins.render.codefence_syntax_highlighter = Some(adapter);
            return plugins;
        }
    }
    #[cfg(not(feature = "syntax-highlight"))]
    let _ = opts;
    Plugins::default()
}

/// Resolve the syntect theme class for a render: an explicit class
/// (`opts.highlight_theme` in 1..=6, set from FFI bits 7-9) pins the theme
/// so code-block colors follow the preview palette; class 0 keeps the
/// legacy behavior where `dark_mode` picks the light/dark pair.
#[cfg(feature = "syntax-highlight")]
fn theme_class(opts: &RenderOptions) -> usize {
    match opts.highlight_theme {
        1..=6 => opts.highlight_theme as usize,
        _ => {
            if opts.dark_mode {
                3 // DARK_THEME == THEME_CLASSES[2]
            } else {
                1 // LIGHT_THEME == THEME_CLASSES[0]
            }
        }
    }
}

#[cfg(feature = "syntax-highlight")]
fn adapter_for(class: usize) -> &'static CachedAdapter {
    static CELLS: [OnceLock<CachedAdapter>; 6] = [const { OnceLock::new() }; 6];
    let idx = (class - 1) % THEME_CLASSES.len();
    CELLS[idx].get_or_init(|| CachedAdapter::new(THEME_CLASSES[idx]))
}

#[cfg(feature = "syntax-highlight")]
struct CachedAdapter {
    theme: &'static str,
    syntax_set: syntect::parsing::SyntaxSet,
    theme_set: syntect::highlighting::ThemeSet,
    cache: Mutex<HashMap<u64, (ExactKey, String)>>,
    cached_bytes: Mutex<usize>,
}

/// Exact input identity stored alongside the hash key, so a cache hit can be
/// verified against the original (lang, code). The 64-bit FNV key alone is
/// only a fast path — a hash collision must never return a mis-lexed or
/// mis-themed block. (`theme` is constant per adapter and needs no check.)
#[cfg(feature = "syntax-highlight")]
struct ExactKey {
    lang: Option<String>,
    code: String,
}

#[cfg(feature = "syntax-highlight")]
impl CachedAdapter {
    fn new(theme: &'static str) -> Self {
        CachedAdapter {
            theme,
            syntax_set: syntect::parsing::SyntaxSet::load_defaults_newlines(),
            theme_set: syntect::highlighting::ThemeSet::load_defaults(),
            cache: Mutex::new(HashMap::new()),
            cached_bytes: Mutex::new(0),
        }
    }

    /// Inline-styled highlight of one code block (theme colors baked in).
    fn highlight_inline(&self, lang: Option<&str>, code: &str) -> Result<String, fmt::Error> {
        use syntect::easy::HighlightLines;
        use syntect::html::{append_highlighted_html_for_styled_line, IncludeBackground};
        use syntect::util::LinesWithEndings;

        let syntax = lang
            .and_then(|l| self.syntax_set.find_syntax_by_token(l))
            .unwrap_or_else(|| self.syntax_set.find_syntax_plain_text());
        let theme = &self.theme_set.themes[self.theme];
        let mut highlighter = HighlightLines::new(syntax, theme);
        let bg = theme
            .settings
            .background
            .unwrap_or(syntect::highlighting::Color::WHITE);

        let mut out = String::with_capacity(code.len() * 3 / 2);
        for line in LinesWithEndings::from(code) {
            let regions = highlighter
                .highlight_line(line, &self.syntax_set)
                .map_err(|_| fmt::Error)?;
            append_highlighted_html_for_styled_line(
                &regions[..],
                IncludeBackground::IfDifferent(bg),
                &mut out,
            )
            .map_err(|_| fmt::Error)?;
        }
        Ok(out)
    }
}

#[cfg(feature = "syntax-highlight")]
impl SyntaxHighlighterAdapter for CachedAdapter {
    fn write_highlighted(
        &self,
        output: &mut dyn fmt::Write,
        lang: Option<&str>,
        code: &str,
    ) -> fmt::Result {
        // Cache key: FNV-1a over (theme, lang, code) — fast path only. A hit
        // is verified against the stored exact (lang, code) pair, so a 64-bit
        // hash collision can never return a wrong block.
        let mut key = fnv1a(self.theme.as_bytes(), 0xcbf2_9ce4_8422_2325);
        key = fnv1a(lang.unwrap_or("").as_bytes(), key);
        key = fnv1a(code.as_bytes(), key);

        {
            let cache = self.cache.lock().unwrap();
            if let Some((exact, hit)) = cache.get(&key) {
                if exact.lang.as_deref() == lang && exact.code == code {
                    return output.write_str(hit);
                }
            }
        }

        let html = self.highlight_inline(lang, code)?;

        {
            let mut cache = self.cache.lock().unwrap();
            let mut bytes = self.cached_bytes.lock().unwrap();
            if cache.len() >= CACHE_MAX_ENTRIES
                || *bytes + html.len() + code.len() > CACHE_MAX_BYTES
            {
                cache.clear();
                *bytes = 0;
            }
            *bytes += html.len() + code.len();
            cache.insert(
                key,
                (
                    ExactKey {
                        lang: lang.map(str::to_string),
                        code: code.to_string(),
                    },
                    html.clone(),
                ),
            );
        }

        output.write_str(&html)
    }

    /// `<pre>` opener, carrying comrak's attributes plus the theme background
    /// (same semantics as comrak's own SyntectAdapter).
    fn write_pre_tag(
        &self,
        output: &mut dyn fmt::Write,
        mut attributes: HashMap<&'static str, std::borrow::Cow<'_, str>>,
    ) -> fmt::Result {
        use syntect::highlighting::Color;
        let colour = self.theme_set.themes[self.theme]
            .settings
            .background
            .unwrap_or(Color::WHITE);
        let bg_style = format!(
            "background-color:#{:02x}{:02x}{:02x};",
            colour.r, colour.g, colour.b
        );
        let merged = match attributes.get_mut("style") {
            Some(existing) => {
                existing.to_mut().push_str(&bg_style);
                None
            }
            None => Some(bg_style),
        };
        write_open_tag(output, "pre", &attributes)?;
        if let Some(style) = merged {
            // style was absent before — emit it now.
            write!(output, " style=\"{}\"", escape_attr(&style))?;
        }
        output.write_char('>')
    }

    fn write_code_tag(
        &self,
        output: &mut dyn fmt::Write,
        attributes: HashMap<&'static str, std::borrow::Cow<'_, str>>,
    ) -> fmt::Result {
        write_open_tag(output, "code", &attributes)
    }
}

#[cfg(feature = "syntax-highlight")]
fn write_open_tag(
    output: &mut dyn fmt::Write,
    tag: &str,
    attributes: &HashMap<&'static str, std::borrow::Cow<'_, str>>,
) -> fmt::Result {
    write!(output, "<{tag}")?;
    let mut attrs: Vec<_> = attributes.iter().collect();
    attrs.sort_by(|a, b| a.0.cmp(b.0)); // deterministic output
    for (name, value) in attrs {
        write!(output, " {name}=\"{}\"", escape_attr(value))?;
    }
    output.write_char('>')
}

#[cfg(feature = "syntax-highlight")]
fn escape_attr(s: &str) -> String {
    s.replace('&', "&amp;")
        .replace('<', "&lt;")
        .replace('>', "&gt;")
        .replace('"', "&quot;")
}

/// FNV-1a (64-bit).
#[cfg(feature = "syntax-highlight")]
fn fnv1a(bytes: &[u8], mut hash: u64) -> u64 {
    for b in bytes {
        hash ^= u64::from(*b);
        hash = hash.wrapping_mul(0x0000_0100_0000_01b3);
    }
    hash
}
