# 安全策略

## 报告安全漏洞

如果你发现安全漏洞，**不要创建公开 Issue**。

请通过以下方式私下报告：

1. 通过 GitHub [Private Vulnerability Reporting](https://github.com/TwinsEarth/TwinsEarth/security/advisories/new)
2. 或发送邮件到 security@twinsearth.dev

## 响应时间

- **48 小时内**：确认收到报告
- **7 天内**：提供初步评估和修复计划
- **修复后**：在 Release Notes 中致谢

## 已知风险边界

本项目当前为 MVP/原型阶段，以下风险**已知存在**，不构成新漏洞报告：

- P2P 网络的 Sybil/Eclipse 攻击防御依赖准入 + 质押，非开放经济安全
- gsn-daemon 的网络层仍在演进，部分模块为内存模拟
- 智能体市场结算为内部记账，不涉及真实货币
- 桌面客户端未代码签名，首次运行需手动信任

## 不要做

- 不要在公开 Issue、PR 或讨论中泄露漏洞细节
- 不要利用漏洞访问他人数据
- 不要尝试绕过沙箱、隔离或认证机制
