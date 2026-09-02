<!-- 标题格式：<type>: <一句话描述>，type ∈ feat|fix|docs|perf|test|refactor -->

## 变更说明

<!-- 做了什么，为什么 -->

## 关联 Issue

<!-- 例如：Closes #12；无则填 N/A -->

## 自查

- [ ] `cargo test -p rustrender-core -p rustrender-native` 全绿
- [ ] WASM 构建零警告（`cargo build -p rustrender-wasm --target wasm32-unknown-unknown --release`）
- [ ] 涉及 FFI/净化/安全的行为已附带测试
- [ ] 渲染输出变化已附 before/after（如适用）
- [ ] README/文档同步更新（如适用）
