# 许可证说明（License Guide）

孪生地球（TwinsEarth）是一个融合了三个独立开源项目的 monorepo。由于各子项目采用不同的开源许可证，本仓库**不使用单一顶层许可证**，而是由各子目录保留并遵循其原始许可证。

## 子项目许可证一览

| 子项目 | 目录 | 许可证 | 许可证文件 |
|--------|------|--------|-----------|
| 🧠 UDOS 推演引擎 | `udos-reasoning-engine/` | **Apache License 2.0** | `udos-reasoning-engine/LICENSE` |
| 🌍 从像素到文明 | `PixelToCivilization/` | **MIT License** | `PixelToCivilization/LICENSE` |
| 🕸️ 智能体宇宙 | `agent-universe/` | **MIT License** | `agent-universe/LICENSE` |

## 你可以做什么

- **MIT 部分**（智能体宇宙、从像素到文明）：可自由使用、复制、修改、合并、发布、再许可与销售，只需在副本中保留原始版权声明与许可声明。
- **Apache-2.0 部分**（UDOS）：可自由使用、修改与分发，需保留版权、许可与 NOTICE 声明；对修改文件需标注变更；并提供明确的专利授权。详见 `udos-reasoning-engine/NOTICE`。

## 使用与再分发须知

1. 当你使用或再分发某个子项目（或其代码）时，请遵循**该子项目对应目录下的许可证**。
2. 顶层 `docs/` 与 `README.md` 等整合文档，版权归 TwinsEarth 贡献者所有，按 **MIT License** 条款提供。
3. 若你将整个 monorepo 整体再分发，需同时保留全部三个子项目的 LICENSE / NOTICE 文件，不得移除任何版权声明。
4. 各子项目中可能包含第三方资源（如引擎、字体、素材、依赖），其版权归各自所有者，使用前请查阅对应子项目的说明。

## 商标

本项目名称"孪生地球 / TwinsEarth"及相关标识，不随开源许可证授予商标使用权。

---

如对许可证有疑问，请在 [Issues](https://github.com/TwinsEarth/TwinsEarth/issues) 中提出。
