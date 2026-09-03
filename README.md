# NppMarkdownPanel · rustcore

[![build](https://github.com/dororo42/npp-markdown-rustcore/actions/workflows/build.yml/badge.svg)](./.github/workflows/build.yml)
[![license](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![rust](https://img.shields.io/badge/rust-1.85%2B-orange.svg)](https://www.rust-lang.org)
[![comrak](https://img.shields.io/badge/comrak-0.54-green.svg)](https://github.com/kivikakk/comrak)

**Notepad++ Markdown 实时预览插件的 Rust 重构**——共享渲染核心 + 双出口架构（Native DLL / WASM），基于 [NppMarkdownPanel 0.9.3](https://github.com/mohzy83/NppMarkdownPanel) fork。

> 目标：把渲染管线从 C#（Markdig + HtmlSanitizer）下沉到纯 Rust 核心，
> 获得 3~10× 渲染性能、内存安全、跨出口复用（同一核心同时驱动原生面板与 WebView2 内嵌 WASM）。

---

## ✨ 特性

- **完整 GFM**：表格、任务列表、删除线、自动链接、脚注、描述列表、上标
- **GitHub Alerts / Obsidian Callout**：`> [!NOTE]` 原生渲染（comrak `alerts`）
- **Obsidian 双链**：`[[Page|别名]]`（`data-wikilink` 属性供宿主拦截跳转）
- **代码高亮**：syntect（onig）内联样式 + **进程级代码块缓存**，主题随明暗模式切换
- **本地图片/链接**：comrak `URLRewriter` 阶段解析为绝对 `file:///` URL（无正则后处理）
- **安全净化**：ammonia 白名单（默认禁 `data:`/`javascript:`，保留 syntect 受控内联样式）
- **滚动同步锚点**：全块级 `data-line` + 标题 `data-src-line`（与上游 Webview2 控件契约兼容）
- **崩溃隔离**：FFI 边界 `catch_unwind` + 512MB 大栈渲染线程，恶意文档最多返回错误码
- **双出口**：同一核心编译为 Windows 原生 DLL（路线 A'）或 WASM（路线 C 实验）

## 🏗 架构

```
                    ┌───────────────────────────────┐
                    │      rustrender-core (纯 Rust) │
                    │  comrak 0.54 · ammonia 4.1     │
                    │  syntect 5(+缓存) · url        │
                    └───────────┬───────────────────┘
             ┌──────────────────┴──────────────────┐
   ┌─────────▼──────────┐               ┌──────────▼─────────┐
   │  rustrender-native │               │  rustrender-wasm   │
   │  cdylib → rustrender.dll           │  wasm-bindgen → .wasm
   │  (路线 A' · FFI)    │               │  (路线 C · 实验)    │
   └─────────┬──────────┘               └──────────┬─────────┘
   ┌─────────▼──────────┐               ┌──────────▼─────────┐
   │ RustRenderWrapper  │               │ web/ worker.js      │
   │ (C# P/Invoke)      │               │ preview.html        │
   │ NppMarkdownPanel   │               │ mermaid · KaTeX     │
   └────────────────────┘               └─────────────────────┘
```

渲染管线：`comrak 解析 → AST heading 提取 → syntect 高亮(缓存) → data-line 锚点注入 → ammonia 净化`

## 📁 项目结构

```
npp-markdown-rustcore/
├── Cargo.toml                  # workspace（含 release/LTO profile）
├── rustrender-core/            # 共享渲染核心（feature: syntect-onig）
│   └── src/{lib,sanitize,resolve,highlight}.rs
├── rustrender-native/          # FFI 出口 → rustrender.dll
│   ├── src/lib.rs              # render_markdown / free_html / rustrender_version
│   ├── tests/ffi.rs            # FFI 回环 + 恶意输入测试
│   └── examples/bench.rs       # 性能 harness
├── rustrender-wasm/            # WASM 出口（无 syntect，前端高亮）
├── NppMarkdownPanel/           # fork 自上游 0.9.3（C#）
│   ├── RustRenderWrapper/      # ★ 新增：NativeRenderer / RustMarkdownGenerator
│   │                           #   ImagePathFixer / ThemeManager / ScrollingSynchronizer
│   └── …（上游原结构：MarkdigWrapper 作为回落保留）
├── web/                        # 路线 C 前端（worker/main/preview + bindings）
├── bench/                      # 样例生成器 + run.ps1 + bench harness
├── .github/workflows/build.yml # CI（Windows 双架构 + 发布）
└── LICENSE / README.md
```

## 🚀 快速开始

> 📘 **完整 Windows 构建与安装手册**（含插件目录组装、安装步骤、故障排查表）：
> [docs/windows-build-guide.md](docs/windows-build-guide.md)

### 📦 直接安装（二进制包，推荐）

从 [Releases](https://github.com/dororo42/npp-markdown-rustcore/releases)（或 Actions
工件 `npp-markdown-rustcore-packages`）下载对应架构的 zip：

- `NppMarkdownPanel-rustcore-x64.zip` → 64 位 Notepad++
- `NppMarkdownPanel-rustcore-x86.zip` → 32 位 Notepad++

zip 根目录即 `NppMarkdownPanel\` 文件夹，**直接解压到 Notepad++ 的 `plugins\` 目录**，
重启 Notepad++ 即可。

插件支持两种布局：依赖 DLL 平铺在插件根目录，或集中在 `lib\` 子目录
（AssemblyResolve 双路径探测）。**rustrender.dll 缺失或加载失败时自动回落 Markdig
管线，功能不丢失**；正常加载时走 Rust 渲染核心（大文档显著提速）。

**升级/替换插件时提示"文件夹被占用"**：先完全退出 Notepad++，并在任务管理器确认
`notepad++.exe` 进程已消失（窗口关闭 ≠ 进程退出，渲染收尾被拖住时进程会短暂存活，
插件已为收尾路径设置 3 秒超时上限）；若仍有 `msedgewebview2.exe` 残留（命令行包含
`MarkdownPanel` 的用户数据目录 `--user-data-dir=...\MarkdownPanel\webview2`），可安全
结束该进程后替换 `plugins\NppMarkdownPanel\`。插件退出时按确定性顺序关闭 WebView2
（解绑事件 → 有界等初始化 → Close → 有限泵消息），正常情况下不会残留进程。

预览样式基于 GitHub 风格现代化排版：Segoe UI 正文 + Cascadia Mono 代码字体（均为
Windows 自带，零额外分发）、无边框浮层式行内代码与代码块、柔和表格边框、淡灰引用块、
GFM alerts 彩色提示框；暗色主题对齐 GitHub Dark（`#0d1117`）色板。

### Rust 侧

```bash
# 测试（Linux/Windows/macOS 均可）
cargo test -p rustrender-core -p rustrender-native

# 构建 Windows 原生 DLL
#   方式一（Windows）：MSVC 工具链
cargo build --release -p rustrender-native --target x86_64-pc-windows-msvc
#   方式二（跨平台交叉编译）：cargo-zigbuild
pip install ziglang && cargo install cargo-zigbuild
cargo zigbuild -p rustrender-native --target x86_64-pc-windows-gnu --release
# 产物: target/<triple>/release/rustrender.dll

# WASM 出口（路线 C）
cargo build -p rustrender-wasm --target wasm32-unknown-unknown --release
wasm-bindgen --target web --out-dir web/bindings \
  target/wasm32-unknown-unknown/release/rustrender_wasm.wasm
```

### C# 插件（Windows）

```powershell
# 推荐：一键双平台构建（脚本内置 nuget restore；运行机无 .NET 4.7.2
#       Targeting Pack 时自动改用 NuGet 参考程序集，用法见脚本头注释）
powershell -ExecutionPolicy Bypass -File NppMarkdownPanel\build.ps1

# 本地组装发布 zip（含 lib\ 布局与可选 rustrender.dll）
powershell -ExecutionPolicy Bypass -File NppMarkdownPanel\makerelease.ps1
```

安装：执行上述 `makerelease.ps1` 得到 `Release\NppMarkdownPanel-*-x86/x64.zip`，
解压到 Notepad++ 的 `plugins\` 目录即可。手动组装时将 `NppMarkdownPanel.dll`、
各类库 `bin\Release\*.dll` 与对应架构的 `rustrender.dll` 放入
`plugins\NppMarkdownPanel\`。**rustrender.dll 缺失时插件自动回落 Markdig 管线，
功能不丢失。**

### 性能基准

```powershell
powershell -ExecutionPolicy Bypass -File bench\run.ps1        # Windows
# 或直接（跨平台）:
cargo run --release -p rustrender-native --example bench -- bench/100KB.md 10
```

实测参考（release，含全部特性）：1KB ≈ 0.8ms · 100KB ≈ 45ms · 1MB ≈ 520ms

## 🔌 FFI 契约

```c
int  render_markdown(const char* md, size_t md_len,
                     const char* cwd, size_t cwd_len,
                     uint32_t options,
                     char** out_html, size_t* out_len);   // 0=成功
void free_html(char* html);                               // 必须调用
const char* rustrender_version(void);
```

`options` 位定义与错误码：

| bit | 含义 | | rc | 含义 |
|-----|------|---|----|------|
| 0 | dark_mode（syntect 暗色主题） | | 0 | 成功 |
| 1 | enable_callout（GitHub Alerts） | | 1 | 输入非法（非 UTF-8 / 含 NUL） |
| 2 | enable_wikilink | | 2 | panic 已拦截（恶意文档） |
| 3 | enable_mermaid（前端渲染占位） | | 3 | 渲染失败 |
| 4 | enable_katex（前端渲染占位） | | 4 | 空指针 |
| 5 | source_line_anchors | | | |
| 6 | highlight（syntect） | | | |

## 📋 与设计文档 v4.0 的差异

实现以 comrak **0.54** 真实 API 为准（文档基于 0.21 编写）：

| 文档假设 | 实际实现 |
|----------|----------|
| `extension.callout` | `extension.alerts`（原生 GitHub Alerts） |
| `WikilinksMode::TitleFirst` | `wikilinks_title_after_pipe = true` |
| 手写 syntect `highlight()` | comrak 插件接口 + 自研缓存适配器 |
| HTML 正则注入锚点 | 原生 `data-sourcepos` + 标签级扫描注入 `data-line` |
| 二次替换 `javascript:` | ammonia scheme 白名单（不破坏代码块文本） |

## 🗺 路线图

- [x] Phase 0-1：共享核心 + 34 项测试全绿
- [x] Phase 2：Native DLL（x64/x86）+ C# 集成
- [x] Phase 4-E1：WASM 出口构建 + JS 粘合层
- [ ] Phase 2.5：滚动同步三阶段打磨（方向锁已就绪，接 Webview2 控件）
- [ ] Phase 4-E2：syntect-fancy 纯 Rust 后端实验（WASM 高亮）
- [ ] Phase 3：Mermaid/KaTeX 官方支持（core 占位位已预留）

## 🤝 参与贡献

见 [CONTRIBUTING.md](CONTRIBUTING.md)。提交 PR 前请确保 `cargo test` 全绿并遵循
`.editorconfig`/`.gitattributes` 的格式约定。

## 📄 许可

[MIT](LICENSE)。`NppMarkdownPanel/` 目录为上游 [mohzy83/NppMarkdownPanel](https://github.com/mohzy83/NppMarkdownPanel) 0.9.3 的 fork，同样以 MIT 发布。

致谢：[comrak](https://github.com/kivikakk/comrak) · [syntect](https://github.com/trishume/syntect) · [ammonia](https://github.com/rust-ammonia/ammonia) · [cargo-zigbuild](https://github.com/rust-cross/cargo-zigbuild)
