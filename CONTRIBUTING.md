# 贡献指南

感谢关注 NppMarkdownPanel · rustcore！

## 开发环境

- Rust stable（1.85+，`rustup` 安装即可）
- 测试/开发核心：任意平台（Linux/macOS/Windows）
- 构建插件成品：Windows + .NET Framework 4.7.2 Developer Pack + msbuild
- 交叉编译（可选）：`pip install ziglang && cargo install cargo-zigbuild`

## 提交前检查

```bash
cargo test -p rustrender-core -p rustrender-native   # 全部测试通过
cargo build -p rustrender-wasm --target wasm32-unknown-unknown --release  # WASM 零警告
cargo clippy --workspace -- -D warnings              # lint 干净
```

## 约定

- 提交信息使用祈使句：`feat: …` / `fix: …` / `docs: …` / `perf: …` / `test: …`
- Rust 代码遵循 `rustfmt` 默认风格；C# 代码遵循上游 NppMarkdownPanel 现有风格
- 涉及 FFI/净化/安全的行为变更，必须附带对应测试
- 渲染输出变更请在 PR 中附 before/after 片段，便于评审肉眼比对

## 报告问题

安全相关问题（净化绕过、崩溃、panic 泄漏）请勿公开 Issue——通过
SECURITY 私密报告或联系维护者。其他问题使用 Issue 模板即可。
