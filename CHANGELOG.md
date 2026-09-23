# 更新日志 (Changelog)

本项目遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

## TwinsEarth 融合仓库

### v1.0.0 — Genesis（2026-09-23）

三大开源项目首次融合为「孪生地球 Twins Earth」数字生命系统。

**新增**
- 三大子项目完整纳入：
  - `agent-universe/` — 智能体宇宙（Rust + JS + Python），v2.3.4 智能体市场
  - `udos-reasoning-engine/` — UDOS 推演引擎（Python），v7.5.0
  - `PixelToCivilization/` — 从像素到文明（C#/Unity），V7.0.2
- 顶层文档：README、LICENSE（MIT）、CONTRIBUTING、SECURITY
- `docs/`：ARCHITECTURE、INTEGRATION、VISION、ROADMAP
- `.github/`：CI 流水线（4 job）、Issue 模板、PR 模板
- `desktop/`：Tauri 2 macOS 桌面客户端
- `client/`：Tauri 2 五平台跨平台客户端（macOS/Windows/Linux/iOS/Android）

**CI**
- Rust gsn-core：cargo build + test
- JS SDK：node test
- Python AIP SDK：pytest
- Python UDOS：pytest（含 PyTorch CPU）

---

## 智能体宇宙 (agent-universe)

### v2.3.4 — Agent Market（2026-09-23）
- 新增智能体市场：注册/发现/匹配/执行/验证/结算/信誉
- BFT-lite QA 委员会（n≥3f+1）
- 守恒账本 + 多维信誉（不可转让）+ 质押罚没
- 证据分级（verified/cpu-proto/unverified）
- Tauri 桌面客户端 + 跨平台客户端
- 公共 npm 发布：`@twinsearth/agent-universe`

### v2.3.3 — P2P Net（2026-09）
- Agent 协作网络（MCP Agent → Route Agent → End Agent）
- 分布式推理与算力调度
- 去中心化任务众包
- NAT 穿透、安全防御（Sybil/Eclipse/污染攻击）

### v2.3.2 — MCP + ACA（2026-09）
- MCP（Model Context Protocol）兼容：工具/资源/提示
- ACA（Agent Communication API）兼容：Manifest/Envelope/Receipt/Reputation
- 五级验证分层（L0 抽样 → L4 仲裁）

### v2.3.1 — Swarm（2026-09）
- 群体智能：网络结构的 Scaling Law
- 涌现检测、轻量共识
- 五个可证伪条件

### v2.3.0 — P2P（2026-09）
- 分布式网络基础设施
- libp2p + Kademlia DHT + GossipSub

### v2.2.0 — Mesh（2026-08）
- 全对等网络
- 根种子降级逻辑
- CRDT 状态同步 + 纠删码冗余

### v2.1.0 — Bridge（2026-08）
- 跨链桥接
- 经济层：信誉、贡献证明、动态定价

### v2.0.0 — Shard（2026-08）
- 分片存储
- DHT 路由

### v1.0.0 — Genesis（2026-07）
- 初始版本
- DID 身份、AgentCard、基础 P2P

---

## UDOS 推演引擎

### v7.5.0
- 统一世界模型引擎 v7（单一残差循环预测器 + 校准不确定性）
- udos7/ 新架构；udos/ v5.5.5 冻结

### v5.4.4
- 精细生物物理（Hodgkin–Huxley、NMDA）
- 类脑树突区室模型
- KV Cache 多级类比

### v5.4.3
- 树突门控、DHS 分层调度

### v5.1
- KV Cache：HBM→DDR→SSD→remote 多级

### v5.0.x
- 安全治理（PBKDF2、RBAC、加密备份）
- AGI/ASI 情报（奇点门、S 曲线 ETA）

### v4.x
- 自治/协作：自规划课程、自训练、多智能体

### v3.x
- 具身/世界模型：物理闭环、形态重定向、数字孪生

### v2.x
- 可信推演：PAVA 保序校准、因果决策
- CTM 时序同步 + GPM 场景内化双引擎

### v0.1
- PCE-Format 物理 Token 数据层

---

## 从像素到文明 (PixelToCivilization)

### V7.0.2
- Q 版低多边形 4X 文明模拟
- 九位 AI 神灵共治
- 世界生成与真实扩展（每 100 年新陆地）
- 完整中国朝代年表
- 大航海、殖民、海洋开发、太空探索
- Tuanjie 2022.3 LTS / URP / WebGL
