//! HTML sanitization via ammonia.
//!
//! The allowlist is tuned to keep exactly what comrak emits plus the
//! block-anchor attributes used by the preview host. Unlike the v4.0 sketch,
//! no blanket string replacement of `javascript:`/`data:`/`vbscript:` is
//! performed — ammonia's URL-scheme filtering handles those correctly without
//! corrupting legitimate occurrences inside code blocks.

use std::collections::{HashMap, HashSet};

/// Sanitize a comrak HTML fragment.
pub fn sanitize(html: &str) -> Result<String, String> {
    let mut b = ammonia::Builder::default();

    // --- tags: ammonia defaults + tasklist checkboxes -----------------------
    let tags: HashSet<&str> = [
        "a", "abbr", "acronym", "area", "article", "aside", "b", "bdi", "bdo", "blockquote", "br",
        "caption", "center", "cite", "code", "col", "colgroup", "data", "dd", "del", "details",
        "dfn", "div", "dl", "dt", "em", "figcaption", "figure", "footer", "h1", "h2", "h3", "h4",
        "h5", "h6", "header", "hgroup", "hr", "i", "img", "input", "ins", "kbd", "li", "map",
        "mark", "nav", "ol", "p", "pre", "q", "rp", "rt", "rtc", "ruby", "s", "samp", "small",
        "span", "strike", "strong", "sub", "summary", "sup", "table", "tbody", "td", "th",
        "thead", "time", "tr", "tt", "u", "ul", "var", "wbr",
    ]
    .into_iter()
    .collect();
    b.tags(tags);

    // Attributes allowed on every element (block anchors + language hints).
    b.generic_attributes(HashSet::from([
        "lang",
        "title",
        "data-sourcepos",
        "data-line",
        "data-src-line",
    ]));

    // Per-tag attributes. `class` is free-form: classes are inert without
    // matching CSS rules, and all preview CSS is host-supplied.
    let mut tag_attributes: HashMap<&str, HashSet<&str>> = HashMap::new();
    let class_only: HashSet<&str> = HashSet::from(["class"]);
    for t in [
        "abbr", "acronym", "article", "aside", "b", "blockquote", "caption", "center", "code",
        "dd", "del", "details", "dfn", "div", "dl", "dt", "em", "figcaption", "figure", "footer",
        "header", "hgroup", "i", "ins", "kbd", "li", "map", "mark", "nav", "ol", "p", "pre", "q",
        "s", "samp", "small", "span", "strike", "strong", "sub", "summary", "sup", "table",
        "tbody", "tr", "u", "ul", "time", "var",
    ] {
        tag_attributes.insert(t, class_only.clone());
    }
    tag_attributes.insert(
        "a",
        HashSet::from(["href", "hreflang", "class", "data-wikilink", "name"]),
    );
    tag_attributes.insert(
        "img",
        HashSet::from(["src", "alt", "height", "width", "align", "class"]),
    );
    tag_attributes.insert("input", HashSet::from(["type", "checked", "disabled"]));
    // Heading ids come from comrak (already `user-content-` prefixed).
    for h in ["h1", "h2", "h3", "h4", "h5", "h6"] {
        let mut s = class_only.clone();
        s.insert("id");
        tag_attributes.insert(h, s);
    }
    tag_attributes.insert(
        "td",
        HashSet::from(["class", "align", "colspan", "rowspan"]),
    );
    tag_attributes.insert(
        "th",
        HashSet::from(["class", "align", "colspan", "rowspan"]),
    );
    tag_attributes.insert(
        "col",
        HashSet::from(["class", "align", "span", "valign", "width"]),
    );
    tag_attributes.insert(
        "colgroup",
        HashSet::from(["class", "align", "span", "valign", "width"]),
    );
    b.tag_attributes(tag_attributes);

    // URL schemes: per v4.0 contract. `file` allows host-resolved local
    // images; ammonia neutralizes javascript:/data:/vbscript: by scheme check.
    b.url_schemes(HashSet::from(["http", "https", "mailto", "file"]));

    // syntect inline styles (native highlighting): strict CSS property whitelist.
    b.filter_style_properties(HashSet::from([
        "color",
        "background-color",
        "font-weight",
        "font-style",
        "text-decoration",
    ]));

    // Kept from ammonia defaults (safe out of the box):
    // - strip_comments = true  (HTML comments removed)
    // - link_rel = "noopener noreferrer"
    // - clean_content_tags = {script, style}  (contents dropped entirely)
    // - url_relative = PassThrough  (paths already resolved by the core)

    Ok(b.clean(html).to_string())
}
