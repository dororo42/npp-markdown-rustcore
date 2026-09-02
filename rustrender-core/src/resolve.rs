//! Relative → absolute path resolution for images and links, implemented as
//! a comrak `URLRewriter` (no post-processing regex passes).
//!
//! Windows-first semantics (the core targets Notepad++ on Windows), but
//! host-OS independent so tests run anywhere:
//! - Empty URLs, pure anchors (`#sec`), absolute URLs with a real scheme
//!   (`http:`, `https:`, `mailto:`, …) pass through untouched.
//! - Windows drive paths (`C:\x\y.png`, `C:/x/y.png`) → `file:///C:/x/y.png`.
//! - Rooted paths (`\x`, `/x`) against a drive base are drive-rooted
//!   (`/top.png` + `C:/docs` → `file:///C:/top.png`).
//! - UNC / scheme-relative (`//server/share`) → `file://server/share`.
//! - Everything else is joined against the document directory, then `.` and
//!   `..` segments are normalized.
//! - `path#fragment` keeps its fragment after resolution.
//! - Failure to resolve returns the original URL (never panics).

use std::path::Path;
use url::Url;

pub struct PathResolver {
    base: String,
}

impl PathResolver {
    pub fn new(dir: &str) -> Self {
        PathResolver {
            base: dir.replace('\\', "/"),
        }
    }
}

impl comrak::options::URLRewriter for PathResolver {
    fn to_html(&self, url: &str) -> String {
        resolve_url(Path::new(&self.base), url)
    }
}

/// Pure resolver (exposed for tests and reuse).
pub fn resolve_url(base: &Path, url: &str) -> String {
    let raw = url.trim();
    if raw.is_empty() || raw.starts_with('#') {
        return url.to_string();
    }

    let raw_norm = raw.replace('\\', "/");

    // Windows drive path (check BEFORE Url::parse, which would read `C:` as a
    // single-letter scheme).
    if is_windows_drive(&raw_norm) {
        return abs_to_file_url(&raw_norm)
            .map(|u| u.to_string())
            .unwrap_or_else(|| url.to_string());
    }

    // Absolute URL with a real scheme → untouched.
    if let Ok(parsed) = Url::parse(&raw_norm) {
        if parsed.scheme().len() > 1 {
            return url.to_string();
        }
    }

    // Split fragment before joining.
    let (path_part, fragment) = match raw_norm.find('#') {
        Some(i) => (&raw_norm[..i], Some(&raw_norm[i + 1..])),
        None => (raw_norm.as_str(), None),
    };
    if path_part.is_empty() {
        return url.to_string();
    }

    // UNC / scheme-relative: //server/share → file://server/share
    if let Some(rest) = path_part.strip_prefix("//") {
        return Url::parse(&format!("file://{rest}"))
            .map(|u| u.to_string())
            .unwrap_or_else(|_| url.to_string());
    }

    let base_str = base.to_string_lossy().replace('\\', "/");
    let base_drive = is_windows_drive(&base_str);

    let joined: String = if path_part.starts_with('/') {
        if base_drive {
            // drive-rooted Windows path
            format!("{}:{}", base_trim_drive(&base_str), path_part)
        } else {
            path_part.to_string()
        }
    } else if base_str.is_empty() {
        path_part.to_string()
    } else {
        format!("{}/{}", base_str.trim_end_matches('/'), path_part)
    };

    let normalized = normalize_dots(&joined);

    match abs_to_file_url(&normalized) {
        Some(mut u) => {
            if let Some(frag) = fragment {
                u.set_fragment(Some(frag));
            }
            u.to_string()
        }
        None => url.to_string(),
    }
}

/// `C:/…` or `D:\…` style drive prefix.
fn is_windows_drive(p: &str) -> bool {
    let b = p.as_bytes();
    b.len() >= 2 && b[1] == b':' && b[0].is_ascii_alphabetic()
}

fn base_trim_drive(base: &str) -> &str {
    // base starts with "X:" — return "X"
    &base[..1]
}

/// Absolute (host-agnostic) path → `file:` URL.
///
/// Pure `Url::parse` construction: `Url::from_file_path` is unavailable on
/// some targets (e.g. `wasm32-unknown-unknown`) and would treat `C:/…` as
/// relative on non-Windows hosts anyway.
fn abs_to_file_url(p: &str) -> Option<Url> {
    if is_windows_drive(p) {
        Url::parse(&format!("file:///{}", p)).ok()
    } else if let Some(rest) = p.strip_prefix('/') {
        // "file://" + "/abs/path" → file:///abs/path (parse percent-encodes).
        Url::parse(&format!("file:///{rest}")).ok()
    } else {
        Url::parse(p).ok()
    }
}

/// Collapse `.`, `..` and duplicate slashes in a path string.
/// Preserves a leading `/` and a leading drive prefix (`C:`).
fn normalize_dots(p: &str) -> String {
    let (prefix, rest) = if is_windows_drive(p) {
        let mut c = p.splitn(2, ':');
        let drive = c.next().unwrap_or("");
        let r = c.next().unwrap_or("");
        (format!("{drive}:"), r)
    } else {
        (String::new(), p)
    };

    let absolute = rest.starts_with('/');
    let mut out: Vec<&str> = Vec::new();
    for seg in rest.split('/') {
        match seg {
            "" | "." => {}
            ".." => {
                if out.len() > absolute as usize {
                    out.pop();
                }
            }
            s => out.push(s),
        }
    }
    let mut result = prefix;
    if absolute {
        result.push('/');
    }
    result.push_str(&out.join("/"));
    if result.is_empty() {
        result.push('.');
    }
    result
}

#[cfg(test)]
mod tests {
    use super::*;

    fn r(base: &str, url: &str) -> String {
        resolve_url(Path::new(base), url)
    }

    #[test]
    fn relative_join() {
        assert_eq!(r("C:/docs", "img.png"), "file:///C:/docs/img.png");
        assert_eq!(r("C:/docs", "./img/pic.png"), "file:///C:/docs/img/pic.png");
        assert_eq!(r("C:/docs", "sub/../a.md"), "file:///C:/docs/a.md");
    }

    #[test]
    fn windows_backslashes() {
        assert_eq!(r("C:/docs", "img\\pic.png"), "file:///C:/docs/img/pic.png");
        assert_eq!(r("C:/docs", "D:\\abs\\x.png"), "file:///D:/abs/x.png");
    }

    #[test]
    fn fragment_preserved() {
        assert_eq!(r("C:/docs", "doc.md#intro"), "file:///C:/docs/doc.md#intro");
    }

    #[test]
    fn passthrough() {
        assert_eq!(r("C:/docs", "https://x.io/a.png"), "https://x.io/a.png");
        assert_eq!(r("C:/docs", "mailto:a@b.c"), "mailto:a@b.c");
        assert_eq!(r("C:/docs", "#sec"), "#sec");
        assert_eq!(r("C:/docs", ""), "");
    }

    #[test]
    fn unc_and_rooted() {
        assert_eq!(r("C:/docs", "//srv/share/i.png"), "file://srv/share/i.png");
        assert_eq!(r("C:/docs", "/top.png"), "file:///C:/top.png");
    }

    #[test]
    fn spaces_percent_encoded() {
        assert_eq!(r("C:/my docs", "a b.png"), "file:///C:/my%20docs/a%20b.png");
    }

    #[test]
    fn unix_base_works_too() {
        assert_eq!(r("/home/u", "pic.png"), "file:///home/u/pic.png");
        assert_eq!(r("/home/u", "/etc/x.png"), "file:///etc/x.png");
    }
}
