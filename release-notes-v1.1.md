# v1.1

## 修复

- **代码块 style 泄漏**：`write_pre_tag` 在 `<pre>` 开标签闭合后追加 style 属性，导致每个代码块首行前出现 `style="background-color:#ffffff;">` 字样文本（v1.0 起每个代码块必现）。现合并进属性后一次性输出开标签。
- **语法高亮配色失效**：sanitize 白名单未放行 `style` 属性，syntect 内联着色被 ammonia 全部剥除，`filter_style_properties` CSS 白名单整体无效。现已给 `pre`/`code`/`span` 放行（CSS 值仍受 color / background-color / font-weight / font-style / text-decoration 白名单约束）。
- 回归测试 T1–T4（泄漏消除 / pre 主题背景存活 / span 着色存活 / 表格对齐存活），CI 双架构全绿。

## 新增

- **HTML 源码文件预览**：打开 `.html`/`.htm` 时预览面板按网页直接渲染（跳过 Markdown 管线）。完整文档原样呈现，HTML 片段套轻量模板；`<img>`/`<script>`/`<link>` 本地相对引用按源文件目录解析；导出 HTML 与图片 base64 内嵌链路兼容。
- 开关：设置 ini `Options → HtmlSourcePreview`（默认开启）；`.html/.htm` 不占用 Markdown 扩展名列表。
- ⚠️ **安全提示**：HTML 源码预览不做净化（保真预览），文档内脚本会执行，等同用浏览器打开该文件；请勿预览不可信来源的 HTML。

## 安装

- `NppMarkdownPanel-rustcore-x64.zip` → 64 位 Notepad++
- `NppMarkdownPanel-rustcore-x86.zip` → 32 位 Notepad++
- 解压到 Notepad++ 的 `plugins\` 目录，重启即可

**Full Changelog**: https://github.com/dororo42/npp-markdown-rustcore/compare/v1.0...v1.1
