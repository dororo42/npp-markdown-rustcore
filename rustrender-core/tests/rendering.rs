//! rustrender-core integration tests: feature parity, sanitization, anchors.

use rustrender_core::{inject_line_anchors, render, RenderOptions};

fn render_default(md: &str) -> String {
    render(md, None, &RenderOptions::default())
        .unwrap()
        .html_body
}

// ---------------------------------------------------------------- GFM basics

#[test]
fn gfm_tables() {
    let h = render_default("| a | b |\n|---|---|\n| 1 | 2 |\n");
    assert!(h.contains("<table"), "{h}");
    assert!(h.contains("<th"), "{h}");
    assert!(h.contains(">1</td>"), "{h}");
    assert!(h.contains(">2</td>"), "{h}");
}

#[test]
fn gfm_tasklist_renders_checkbox() {
    let h = render_default("- [x] done\n- [ ] todo\n");
    assert!(h.contains("<input type=\"checkbox\""), "{h}");
    assert!(h.contains("checked"), "{h}");
}

#[test]
fn gfm_strikethrough_autolink_superscript() {
    let h = render_default("~del~ and https://example.com and e=mc^2^ and ~~gone~~\n");
    assert!(h.contains(">del</del>"), "{h}");
    assert!(h.contains(">gone</del>"), "{h}");
    assert!(h.contains("href=\"https://example.com\""), "{h}");
    assert!(h.contains(">2</sup>"), "{h}");
}

#[test]
fn footnotes() {
    let h = render_default("Text[^1]\n\n[^1]: note\n");
    assert!(h.contains("footnote-ref"), "{h}");
    assert!(h.contains("href=\"#fn-1\""), "{h}");
}

#[test]
fn description_lists() {
    let h = render_default("Term\n: Definition\n");
    assert!(h.contains("<dl"), "{h}");
    assert!(h.contains("<dt"), "{h}");
    assert!(h.contains("<dd"), "{h}");
}

#[test]
fn wikilink_obsidian_semantics() {
    // [[target|display]] — title after the pipe.
    let h = render_default("See [[My Page|the page]] now\n");
    assert!(h.contains("data-wikilink=\"true\""), "{h}");
    assert!(h.contains(">the page</a>"), "{h}");
}

#[test]
fn callout_github_alert() {
    let h = render_default("> [!NOTE]\n> Useful info\n");
    assert!(h.contains("markdown-alert markdown-alert-note"), "{h}");
    assert!(h.contains("markdown-alert-title"), "{h}");
    assert!(h.contains(">Useful info</p>"), "{h}");
    // Marker text must not leak into the output.
    assert!(!h.contains("[!NOTE]"), "{h}");
}

#[test]
fn frontmatter_does_not_leak() {
    let h = render_default("---\ntitle: secret-key\ntags: [a, b]\n---\n\nBody\n");
    assert!(!h.contains("secret-key"), "{h}");
    assert!(!h.contains("frontmatter"), "{h}");
    assert!(h.contains(">Body</p>"), "{h}");
}

#[test]
fn heading_ids_user_content_prefix() {
    let h = render_default("# Hello World\n");
    assert!(h.contains("id=\"user-content-hello-world\""), "{h}");
}

#[test]
fn mermaid_fence_keeps_language_class() {
    let h = render_default("```mermaid\ngraph TD; A-->B;\n```\n");
    assert!(h.contains("language-mermaid"), "{h}");
}

#[test]
fn code_inline_escapes() {
    let h = render_default("Use `<div>` & `\"quotes\"`\n");
    assert!(h.contains("&lt;div&gt;"), "{h}");
    assert!(h.contains("&amp;"), "{h}");
}

// ------------------------------------------------------------------ headings

#[test]
fn headings_extracted_with_lines() {
    let md = "# First\n\ntext\n\n## Second `code` heading\n";
    let out = render(md, None, &RenderOptions::default()).unwrap();
    assert_eq!(out.headings.len(), 2, "{:?}", out.headings);
    assert_eq!(out.headings[0].level, 1);
    assert_eq!(out.headings[0].line, 1);
    assert_eq!(out.headings[0].text, "First");
    assert_eq!(out.headings[1].level, 2);
    assert_eq!(out.headings[1].line, 5);
    assert_eq!(out.headings[1].text, "Second code heading");
}

// ------------------------------------------------------------------- anchors

#[test]
fn data_line_injected_on_blocks() {
    let md = "# Title\n\nPara one.\n\n- item\n- item2\n";
    let h = render_default(md);
    assert!(h.contains("data-line=\"1\""), "{h}"); // h1
    assert!(h.contains("data-line=\"3\""), "{h}"); // p
    assert!(h.contains("data-line=\"5\""), "{h}"); // li
}

#[test]
fn data_src_line_on_headings_only() {
    let md = "# Title\n\nPara.\n";
    let h = render_default(md);
    assert!(h.contains("data-src-line=\"1\""), "{h}");
    assert!(!h.contains("data-src-line=\"3\""), "{h}");
    assert!(h.contains("data-line=\"3\""), "{h}");
}

#[test]
fn anchors_off_disables_injection() {
    let opts = RenderOptions {
        source_line_anchors: false,
        ..RenderOptions::default()
    };
    let out = render("# Hi\n\nBody\n", None, &opts).unwrap();
    assert!(!out.html_body.contains("data-line"), "{}", out.html_body);
    assert!(
        !out.html_body.contains("data-sourcepos"),
        "{}",
        out.html_body
    );
    assert!(out.headings.is_empty());
}

#[test]
fn inject_handles_non_ascii_attrs() {
    let h = inject_line_anchors("<p title=\"中文 test\">x</p>");
    assert!(h.contains("中文 test"), "{h}");
}

#[test]
fn inject_does_not_touch_sourcepos_text_in_code() {
    let h = render_default("```\ndata-sourcepos=\"3:1-3:2\"\n```\n");
    // The literal is plain text inside <span> (syntect output); the tag-scoped
    // injector must never treat it as an attribute.
    assert!(h.contains("<pre"), "{h}");
    assert!(!h.contains("data-line=\"3\""), "{h}");
}

// --------------------------------------------------------------- sanitization

#[test]
fn sanitize_strips_script_and_onerror() {
    let h = render_default(
        "Hello <script>alert(1)</script> world\n\n<img src=\"x\" onerror=\"alert(1)\">\n",
    );
    assert!(!h.contains("<script"), "{h}");
    assert!(!h.contains("alert(1)"), "{h}");
    assert!(!h.contains("onerror"), "{h}");
    assert!(h.contains("Hello"), "{h}");
}

#[test]
fn sanitize_neutralizes_javascript_urls() {
    let h = render_default("[click](javascript:alert(1))\n");
    assert!(!h.contains("href=\"javascript:"), "{h}");
    // ammonia removes the href entirely, keeping the link text
    assert!(h.contains("click"), "{h}");
}

#[test]
fn sanitize_blocks_data_and_vbscript_urls() {
    let h = render_default("[a](data:text/html;base64,AAAA) [b](vbscript:msgbox)\n");
    assert!(!h.contains("data:text/html"), "{h}");
    assert!(!h.contains("vbscript:"), "{h}");
}

#[test]
fn sanitize_strips_iframe_object_embed() {
    let h = render_default(
        "<iframe src=\"https://evil.example\"></iframe>\n\n<object data=\"x\"></object>\n\n<embed src=\"y\">\n",
    );
    assert!(!h.contains("<iframe"), "{h}");
    assert!(!h.contains("<object"), "{h}");
    assert!(!h.contains("<embed"), "{h}");
}

#[test]
fn sanitize_drops_style_tag_and_comments() {
    let h =
        render_default("<style>body{display:none}</style>\n\n<!-- secret note -->\n\nvisible\n");
    assert!(!h.contains("display:none"), "{h}");
    assert!(!h.contains("secret note"), "{h}");
    assert!(h.contains("visible"), "{h}");
}

#[test]
fn sanitize_keeps_benign_raw_html() {
    let h = render_default("<details>\n<summary>More</summary>\n\nhidden text\n\n</details>\n");
    assert!(h.contains("<details>"), "{h}");
    assert!(h.contains("<summary>More</summary>"), "{h}");
}

#[test]
fn sanitize_syntect_styles_are_whitelisted_only() {
    let h = render_default("```rust\nfn main() {}\n```\n");
    // with syntect-onig (default) the code is highlighted with inline styles
    if h.contains("style=") {
        assert!(!h.contains("position"), "{h}");
        assert!(!h.contains("width"), "{h}");
    }
    assert!(h.contains("fn"), "{h}");
}

#[test]
fn sanitize_keeps_wikilink_attr() {
    let h = render_default("[[Page]]\n");
    assert!(h.contains("data-wikilink=\"true\""), "{h}");
    assert!(h.contains("<a"), "{h}");
}

// --------------------------------------------------------------- path fixing

#[test]
fn path_rewrite_images() {
    let out = render(
        "![alt](./img/pic.png)\n\n[link](sub/doc.md#anchor)\n",
        Some("C:/docs"),
        &RenderOptions::default(),
    )
    .unwrap();
    let h = &out.html_body;
    assert!(
        h.contains("file:///C:/docs/img/pic.png"),
        "image not resolved: {h}"
    );
    assert!(
        h.contains("file:///C:/docs/sub/doc.md#anchor"),
        "link not resolved: {h}"
    );
}

#[test]
fn absolute_urls_untouched() {
    let out = render(
        "[gh](https://github.com) and [mail](mailto:a@b.c) and [anchor](#sec)\n",
        Some("C:/docs"),
        &RenderOptions::default(),
    )
    .unwrap();
    let h = &out.html_body;
    assert!(h.contains("href=\"https://github.com\""), "{h}");
    assert!(h.contains("href=\"mailto:a@b.c\""), "{h}");
    assert!(h.contains("href=\"#sec\""), "{h}");
}

// ------------------------------------------------------- highlight theming

#[test]
fn from_bits_parses_highlight_theme_class() {
    // bits 7-9 carry the syntect theme class; 0 = auto (legacy pair).
    assert_eq!(RenderOptions::from_bits(0).highlight_theme, 0);
    assert_eq!(RenderOptions::from_bits(1).highlight_theme, 0);
    assert_eq!(RenderOptions::from_bits(1 << 7).highlight_theme, 1);
    assert_eq!(RenderOptions::from_bits(6 << 7).highlight_theme, 6);
    assert_eq!(RenderOptions::from_bits(7 << 7).highlight_theme, 7);
    // bits beyond the class field must not leak into it.
    assert_eq!(RenderOptions::from_bits(1 << 10).highlight_theme, 0);
}

#[test]
fn highlight_theme_class_selects_palette() {
    // An explicit class (warm dark = base16-eighties) must render code
    // blocks with different inline colors than the default light pair.
    let md = "```rust\nfn main() { let x = 1; }\n```\n";
    let warm = RenderOptions {
        highlight_theme: 4,
        ..RenderOptions::default()
    };
    let a = render(md, None, &RenderOptions::default())
        .unwrap()
        .html_body;
    let b = render(md, None, &warm).unwrap().html_body;
    assert_ne!(a, b);
}

#[test]
fn highlight_theme_auto_matches_legacy_dark_pair() {
    // Class 0 + dark_mode must equal explicitly pinning class 3
    // (both resolve to base16-ocean.dark).
    let md = "```rust\nfn main() {}\n```\n";
    let auto_dark = RenderOptions {
        dark_mode: true,
        ..RenderOptions::default()
    };
    let pinned = RenderOptions {
        highlight_theme: 3,
        ..RenderOptions::default()
    };
    let a = render(md, None, &auto_dark).unwrap().html_body;
    let b = render(md, None, &pinned).unwrap().html_body;
    assert_eq!(a, b);
}
