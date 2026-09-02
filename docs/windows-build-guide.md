# Windows 构建与安装到 Notepad++ 操作手册

> 适用对象：NppMarkdownPanel · rustcore（rustrender.dll + NppMarkdownPanel 插件）
> 阅读时间：~10 分钟；按路线 A 操作约 5 分钟可装完。

---

## 0. 两条路线怎么选

| 路线 | 适合谁 | 需要什么 |
|------|--------|----------|
| **A. 预编译产物** | 只想用 | 本仓库 CI 产物（或 `rustrender.x64.dll`/`rustrender.x86.dll`）+ Notepad++ |
| **B. 全量源码构建** | 要改代码 / 自行出包 | Windows 10/11 + VS Build Tools + Rust + .NET 4.7.2 SDK |

> ✅ **运行时依赖实测结论**：`rustrender.dll`（zigbuild/mingw 构建）仅依赖系统组件
> （KERNEL32/ntdll/UCRT/WS2_32/bcryptprimitives）。**不需要** VC++ Redistributable、
> 不需要 libgcc-xx.dll。Windows 10 1709+ 零额外安装；Windows 7/8 需先装
> [KB2999226（UCRT）](https://support.microsoft.com/kb/2999226)。

---

## 1. 前置条件（路线 B）

| 组件 | 版本要求 | 获取方式 |
|------|----------|----------|
| Visual Studio 2022 Build Tools | 含「C++ 桌面开发」与「.NET Framework 4.7.2 目标包」工作负载 | [visualstudio.microsoft.com](https://visualstudio.microsoft.com/zh-hans/downloads/) |
| Rust 工具链 | stable 1.85+（MSVC host） | `winget install Rustlang.Rustup` |
| Git | 任意近期版本 | `winget install Git.Git` |
| Python 3 | 仅 bench 需要 | `winget install Python.Python.3` |
| WebView2 Runtime | Evergreen（Win11 自带） | [WebView2 下载页](https://developer.microsoft.com/microsoft-edge/webview2/) |

安装 Rust 后补两个目标（可选，只装默认目标也能编宿主机版本）：

```powershell
rustup target add x86_64-pc-windows-msvc i686-pc-windows-msvc
```

---

## 2. 构建 Rust 核心（rustrender.dll）

```powershell
# 克隆仓库
git clone https://github.com/<你的用户名>/npp-markdown-rustcore.git
cd npp-markdown-rustcore

# 1) 测试（可选但强烈建议，~1 分钟）
cargo test --release -p rustrender-core -p rustrender-native

# 2) 构建 x64 与 x86 DLL
cargo build --release -p rustrender-native --target x86_64-pc-windows-msvc
cargo build --release -p rustrender-native --target i686-pc-windows-msvc
```

产物位置：

| 架构 | 产物 |
|------|------|
| 64 位 Notepad++ | `target\x86_64-pc-windows-msvc\release\rustrender.dll` |
| 32 位 Notepad++ | `target\i686-pc-windows-msvc\release\rustrender.dll` |

> 💡 自检导出符号：`dumpbin /exports <dll> | findstr render`，
> 应能看到 `render_markdown` / `free_html` / `rustrender_version`。

### 2.1 （可选）跑性能基准

```powershell
python bench\generate_samples.py            # 生成 1KB~10MB 样例
powershell -ExecutionPolicy Bypass -File bench\run.ps1   # 自动构建+跑分
```

预期：100KB 全特性 < 50ms（含代码高亮缓存热身后 ~37ms）。

---

## 3. 构建 C# 插件（NppMarkdownPanel）

```powershell
# 1) 还原 NuGet 包（Markdig/HtmlSanitizer 等，用于回落管线）
nuget restore NppMarkdownPanel\NppMarkdownPanel.sln
# 没装 nuget.exe 的话：msbuild 自带的还原也可以
msbuild NppMarkdownPanel\NppMarkdownPanel.sln -t:Restore

# 2) 构建（位数要和你的 Notepad++ 一致！）
msbuild NppMarkdownPanel\NppMarkdownPanel.sln /p:Configuration=Release /p:Platform=x64 /m
msbuild NppMarkdownPanel\NppMarkdownPanel.sln /p:Configuration=Release /p:Platform=x86 /m
```

主要产物（`NppMarkdownPanel\NppMarkdownPanel\bin\<平台>\Release\`）：

| 文件 | 作用 |
|------|------|
| `NppMarkdownPanel.dll` | 插件主程序（上游 fork） |
| `RustRenderWrapper.dll` | **新增**：rustrender.dll 的 P/Invoke 封装 |
| `MarkdigWrapper.dll` | 保留：rustrender.dll 缺失时的自动回落渲染器 |
| `PanelCommon.dll` / `Webview2Viewer.dll` | 上游基础库 |
| `style.css` / `style-dark.css` | 预览样式（明/暗） |

> ⚠️ 若 `msbuild` 报 `Microsoft.CSharp.targets` 缺失，说明 VS Build Tools
> 未勾选 .NET 桌面开发工作负载。

---

## 4. 组装插件目录

按 Notepad++ 的插件规范，**一个插件一个文件夹**，文件夹名 = 主 DLL 名：

```
<Notepad++安装目录或%ProgramFiles%>\Notepad++\plugins\NppMarkdownPanel\
├── NppMarkdownPanel.dll      ← 插件主程序（必须）
├── RustRenderWrapper.dll     ← 桥接层（必须）
├── MarkdigWrapper.dll        ← 回落渲染器（必须）
├── PanelCommon.dll
├── Webview2Viewer.dll
├── MarkdigWrapper\Markdig\*.dll     ← Markdig 依赖链（照 msbuild 输出拷贝）
├── rustrender.dll            ← ★ 与 Notepad++ 位数一致的那份！
├── style.css
└── style-dark.css
```

> 📌 `rustrender.dll` 不要带 `x64`/`x86` 后缀——C# 端按固定名加载。
> 两种位数都要支持的话，维护两套插件目录（x64 机装 x64 那套）。

---

## 5. 安装到 Notepad++

### 方式一：手动拷贝（推荐，可控）

1. **关闭 Notepad++**。
2. 按上面第 4 节把整套文件拷到 `plugins\NppMarkdownPanel\`。
3. 处理「文件被锁定」标记（从网上下载的 zip 解出来的文件会被 SmartScreen 拦）：

   ```powershell
   Get-ChildItem -Recurse "plugins\NppMarkdownPanel" |
     Unblock-File
   ```

4. 启动 Notepad++ → `插件管理` 里应出现 **NppMarkdownPanel**。
5. 菜单 `插件 → NppMarkdownPanel → Markdown Panel`（或快捷键）打开预览面板。

### 方式二：用 Notepad++ 导入

`插件管理 → 手动安装`（旧版：设置 → 导入 → 导入插件）选择 `NppMarkdownPanel.dll`，
然后**手动**把 `rustrender.dll` 等其余文件补进自动创建的目录。

### 首次渲染验证

1. 新建 `test.md`，输入：

   ````markdown
   # Hello rustcore

   > [!NOTE]
   > 如果你看到这个蓝色标注框，说明 comrak alerts 生效了。

   ```rust
   fn main() { println!("syntect 高亮 + 缓存"); }
   ```

   ![本地图片](./screenshot.png)
   ````

2. 预览应显示：标题锚点、带左侧色条的 NOTE 提示框、着色代码、能加载的本地图片。
3. 编辑任意文字，预览应在 ~百毫秒内跟随（大文档也在半秒内）。

---

## 6. 故障排查

| 现象 | 原因 | 处理 |
|------|------|------|
| 预览正常，但 About 不显示 rustrender 版本 | `rustrender.dll` 未被加载，已自动回落 Markdig | 确认 rustrender.dll 与插件主 DLL 同目录、位数一致；确认未被杀软隔离 |
| 预览完全空白/报脚本错误 | WebView2 Runtime 缺失（Win10 LTSC 常见） | 安装 Evergreen Runtime |
| 加载插件报错「不是有效的 Win32 应用」 | DLL 位数与 Notepad++ 不匹配 | x64 NPP 配 x64 DLL，x86 配 x86 |
| Win7/8 上报 api-ms-win-crt-*.dll 缺失 | 缺 UCRT | 装 KB2999226 / 系统更新 |
| 杀软报 rustrender.dll 风险 | mingw 构建的通用误报 | 用 CI（MSVC 目标）产物，或将文件加入白名单 |
| 代码块没有颜色 | highlight 标志被关 | 检查设置（FFI options bit 6），默认开启 |
| 图片不显示 | 相对路径解析失败 | 确认图片相对当前 .md 所在目录；网络图片需外网 |

**判断当前用的哪条渲染管线**：插件 About 页显示 `rustrender x.y.z (comrak 0.54 / syntect)`
即 Native 路线；不显示则处于 Markdig 回落（功能仍完整，只是没有 Rust 性能与新特性）。

---

## 7. 升级与卸载

**升级**：关闭 Notepad++ → 覆盖 `NppMarkdownPanel.dll`、`RustRenderWrapper.dll`、
`rustrender.dll` 三个文件 → 启动。设置（INI）与样式向后兼容。

**卸载**：关闭 Notepad++ → 删除 `plugins\NppMarkdownPanel\` 整个目录；
配置残留在 `plugins\NppMarkdownPanel\config\`（如有），一并删除即可。

---

## 8. CI 产物（免本地构建）

推送后 GitHub Actions 会产出三平台矩阵构建与打包好的 `npp-markdown-rustcore` 工件
（Actions 页 → 对应 run → Artifacts 下载），布局即第 4 节的插件目录，解压即用。
