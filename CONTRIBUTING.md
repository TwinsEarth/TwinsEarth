# 贡献指南

感谢你对孪生地球（TwinsEarth）的兴趣！我们欢迎任何形式的贡献——代码、文档、测试、设计、想法，或只是一个 Issue。

## 我们欢迎什么

- 🐛 报告 Bug
- ✨ 新功能提案
- 📝 文档改进
- 🧪 测试补充
- 🐳 翻译
- 🎨 UI/UX 改进

## 开始之前

1. 浏览 [Issues](https://github.com/TwinsEarth/TwinsEarth/issues)，看看是否有人已经提出。
2. 对于新功能，先开一个 Issue 讨论，避免做了大量工作后被拒绝。
3. 阅读 [docs/VISION.md](docs/VISION.md) 与 [docs/ROADMAP.md](docs/ROADMAP.md)，了解项目方向。

## 开发环境

```bash
# 克隆
git clone https://github.com/TwinsEarth/TwinsEarth.git
cd TwinsEarth

# 按子项目分别设置开发环境（见各子目录 README）
# - udos-reasoning-engine/   Python, 纯 CPU
# - agent-universe/gsn-core/  Rust
# - agent-universe/js/        Node.js
# - PixelToCivilization/      Tuanjie/Unity
```

## 提交规范

- Commit message 使用祈使句（如 `fix: 修复结算重复支付`）
- 一个 commit 做一件事
- 保持 CI 绿色：提交前本地跑过相关测试

## 代码风格

- **Rust**：`cargo fmt` + `cargo clippy`
- **Python**：遵循 PEP 8，优先类型标注
- **JS/TS**：无分号，2 空格缩进
- **C#**：遵循 Unity 官方风格

## 许可证

提交代码即表示你同意你的贡献按各子项目对应许可证发布（见 [LICENSES-README.md](LICENSES-README.md)）。

---

**让科技造福全人类！** 🌱
