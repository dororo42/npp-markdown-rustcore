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
//! renders drop to the no-highlight baseline.) The cache guards (entry
//! count / total bytes) evict least-recently-used entries — tripping a
//! guard costs a few cold blocks, not the whole warm cache.

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
/// The heavyweight assets are shared process-wide (see [`shared_syntax_set`]),
/// so an adapter only owns its per-theme highlight cache.
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

/// Process-wide shared `SyntaxSet` — `load_defaults_newlines()` costs tens of
/// ms and tens of MB, and every theme lexes with the same grammar set, so all
/// six adapters share one copy instead of each pinning its own (PERF-4: the
/// per-adapter copies peaked at ~5x redundant resident memory).
#[cfg(feature = "syntax-highlight")]
fn shared_syntax_set() -> &'static syntect::parsing::SyntaxSet {
    static SHARED: OnceLock<syntect::parsing::SyntaxSet> = OnceLock::new();
    SHARED.get_or_init(syntect::parsing::SyntaxSet::load_defaults_newlines)
}

/// Process-wide shared `ThemeSet` — `load_defaults()` deserializes every
/// built-in theme at once, so per-adapter copies were pure duplication.
#[cfg(feature = "syntax-highlight")]
fn shared_theme_set() -> &'static syntect::highlighting::ThemeSet {
    static SHARED: OnceLock<syntect::highlighting::ThemeSet> = OnceLock::new();
    SHARED.get_or_init(syntect::highlighting::ThemeSet::load_defaults)
}

#[cfg(feature = "syntax-highlight")]
struct CachedAdapter {
    theme: &'static str,
    /// Cache state under a single lock: entries + byte accounting + the
    /// monotonically increasing LRU tick.
    cache: Mutex<CacheState>,
}

/// Mutable cache bookkeeping, all under one lock.
#[cfg(feature = "syntax-highlight")]
struct CacheState {
    map: HashMap<u64, CacheEntry>,
    tick: u64,
    bytes: usize,
}

#[cfg(feature = "syntax-highlight")]
struct CacheEntry {
    exact: ExactKey,
    html: String,
    /// Last-use serial from `CacheState::tick`; the minimum is the LRU
    /// eviction victim.
    last_used: u64,
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
            cache: Mutex::new(CacheState {
                map: HashMap::new(),
                tick: 0,
                bytes: 0,
            }),
        }
    }

    /// Inline-styled highlight of one code block (theme colors baked in).
    fn highlight_inline(&self, lang: Option<&str>, code: &str) -> Result<String, fmt::Error> {
        use syntect::easy::HighlightLines;
        use syntect::html::{append_highlighted_html_for_styled_line, IncludeBackground};
        use syntect::util::LinesWithEndings;

        let syntax_set = shared_syntax_set();
        let syntax = lang
            .and_then(|l| syntax_set.find_syntax_by_token(l))
            .unwrap_or_else(|| syntax_set.find_syntax_plain_text());
        let theme = &shared_theme_set().themes[self.theme];
        let mut highlighter = HighlightLines::new(syntax, theme);
        let bg = theme
            .settings
            .background
            .unwrap_or(syntect::highlighting::Color::WHITE);

        let mut out = String::with_capacity(code.len() * 3 / 2);
        for line in LinesWithEndings::from(code) {
            let regions = highlighter
                .highlight_line(line, syntax_set)
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
impl CacheState {
    /// Insert an entry, evicting least-recently-used entries while either
    /// guard trips. Replaces the old clear-all: a single oversized document
    /// used to nuke the whole warm cache, and the next render of any large
    /// document then paid a full re-highlight CPU spike.
    fn insert_lru(&mut self, key: u64, mut entry: CacheEntry) {
        let incoming = entry.html.len() + entry.exact.code.len();
        while self.map.len() >= CACHE_MAX_ENTRIES || self.bytes + incoming > CACHE_MAX_BYTES {
            let Some(victim) = self
                .map
                .iter()
                .min_by_key(|(_, e)| e.last_used)
                .map(|(k, _)| *k)
            else {
                break; // cache empty: the incoming block alone exceeds the byte guard
            };
            let Some(removed) = self.map.remove(&victim) else {
                break;
            };
            self.bytes = self
                .bytes
                .saturating_sub(removed.html.len() + removed.exact.code.len());
        }
        self.tick += 1;
        entry.last_used = self.tick;
        self.bytes += incoming;
        if let Some(old) = self.map.insert(key, entry) {
            // Replacing a colliding entry: don't double-count its bytes.
            self.bytes = self
                .bytes
                .saturating_sub(old.html.len() + old.exact.code.len());
        }
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
            let mut guard = self.cache.lock().unwrap();
            // Reborrow through the guard once so the map and tick borrows can
            // be split (a hit must refresh the LRU serial).
            let state = &mut *guard;
            if let Some(entry) = state.map.get_mut(&key) {
                if entry.exact.lang.as_deref() == lang && entry.exact.code == code {
                    state.tick += 1;
                    entry.last_used = state.tick;
                    return output.write_str(&entry.html);
                }
            }
        }

        let html = self.highlight_inline(lang, code)?;

        {
            let mut guard = self.cache.lock().unwrap();
            let state = &mut *guard;
            state.insert_lru(
                key,
                CacheEntry {
                    exact: ExactKey {
                        lang: lang.map(str::to_string),
                        code: code.to_string(),
                    },
                    html: html.clone(),
                    last_used: 0,
                },
            );
        }

        output.write_str(&html)
    }

    /// `<pre>` opener, carrying comrak's attributes plus the theme background
    /// (same semantics as comrak's own SyntectAdapter).
    ///
    /// The background style is merged into `attributes` *before* the tag is
    /// written, so the open tag closes exactly once. (An earlier version
    /// appended ` style="..." >` after `write_open_tag` had already emitted
    /// the closing `>`, leaking the style text into the block content.)
    fn write_pre_tag(
        &self,
        output: &mut dyn fmt::Write,
        mut attributes: HashMap<&'static str, std::borrow::Cow<'_, str>>,
    ) -> fmt::Result {
        use syntect::highlighting::Color;
        let colour = shared_theme_set().themes[self.theme]
            .settings
            .background
            .unwrap_or(Color::WHITE);
        let bg_style = format!(
            "background-color:#{:02x}{:02x}{:02x};",
            colour.r, colour.g, colour.b
        );
        match attributes.get_mut("style") {
            Some(existing) => existing.to_mut().push_str(&bg_style),
            None => {
                attributes.insert("style", std::borrow::Cow::Owned(bg_style));
            }
        }
        write_open_tag(output, "pre", &attributes)
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

#[cfg(all(test, feature = "syntax-highlight"))]
mod tests {
    use super::*;

    fn entry(code: &str) -> CacheEntry {
        CacheEntry {
            exact: ExactKey {
                lang: None,
                code: code.to_string(),
            },
            html: format!("<span>{code}</span>"),
            last_used: 0,
        }
    }

    #[test]
    fn evict_is_lru_not_clear_all() {
        let mut state = CacheState {
            map: HashMap::new(),
            tick: 0,
            bytes: 0,
        };
        for i in 0..CACHE_MAX_ENTRIES as u64 {
            state.insert_lru(i, entry(&format!("block-{i}")));
        }
        assert_eq!(state.map.len(), CACHE_MAX_ENTRIES);

        // Refresh key 0 (simulates a cache hit) so key 1 is the LRU victim.
        state.map.get_mut(&0).unwrap().last_used = state.tick;

        state.insert_lru(u64::MAX, entry("new-block"));

        // Exactly the LRU entry was evicted — not the whole cache.
        assert_eq!(state.map.len(), CACHE_MAX_ENTRIES);
        assert!(
            state.map.contains_key(&0),
            "recently-used entry must survive"
        );
        assert!(
            state.map.contains_key(&u64::MAX),
            "new entry must be present"
        );
        assert!(!state.map.contains_key(&1), "LRU entry must be evicted");
    }
}
