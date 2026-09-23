"""二级子周期 Agent：制度/财政/王朝周期（数十年到数百年）。

王朝周期律：制度熵增 → 财政恶化 → 社会矛盾激化 → 改革或革命。
嵌套在人口母周期内，受其约束；同时塑造三级孙周期的运行环境。
"""

from __future__ import annotations

import math

from .base import CycleAgent


class InstitutionAgent(CycleAgent):
    """制度/财政 Agent。

    输入：人口压力、劳动力供给（来自一级）
    输出：财政健康度、制度韧性、社会稳定性、土地集中度
    周期：50-300年（王朝）；10-30年（财政）
    """

    LAYER = 2
    NAME = "institution"
    CYCLE_LENGTH = 250.0

    def _init_variables(
        self,
        fiscal_health: float = 0.8,       # 0=崩溃，1=健康
        institution_entropy: float = 0.2,  # 制度熵增（腐败、僵化）
        land_concentration: float = 0.3,   # 土地兼并程度（0=平均，1=极端集中）
        legitimacy: float = 0.85,          # 统治合法性
    ) -> None:
        self.state.variables = {
            "fiscal_health": fiscal_health,
            "institution_entropy": institution_entropy,
            "land_concentration": land_concentration,
            "legitimacy": legitimacy,
            "tax_base": 100.0,                 # 税基指数
            "debt_ratio": 0.2,                 # 债务/GDP
            "reform_window": 1.0,              # 改革窗口（0=关闭，1=开放）
            "crisis_level": 0.0,               # 社会危机等级
        }

    def _evolve(self) -> None:
        v = self.state.variables

        # 上层约束：人口压力
        pop_pressure = self.state.upstream_constraint.get("population_pressure", 0.5)
        aging = self.state.upstream_constraint.get("aging_burden", 0.1)

        # 制度熵增：随时间自然恶化
        v["institution_entropy"] = min(
            1.0,
            v["institution_entropy"] + 0.0005 + pop_pressure * 0.001
        )

        # 土地兼并：人口压力下加剧
        v["land_concentration"] = min(
            0.95,
            v["land_concentration"] + pop_pressure * 0.002 - 0.001
        )

        # 税基：土地兼并 → 自耕农减少 → 税基萎缩
        v["tax_base"] = 100.0 * (1.0 - v["land_concentration"] * 0.6)

        # 财政健康度：税基 - 支出（养老/军费）- 利息
        spending = 50.0 + aging * 80.0 + pop_pressure * 30.0
        v["debt_ratio"] = min(
            2.0,
            v["debt_ratio"] + (spending - v["tax_base"]) * 0.001
        )
        v["fiscal_health"] = max(
            0.0,
            min(1.0, v["tax_base"] / spending - v["debt_ratio"] * 0.3)
        )

        # 合法性：财政健康 + 低熵增 → 高合法性
        v["legitimacy"] = max(
            0.0,
            min(1.0, v["fiscal_health"] * 0.6 + (1 - v["institution_entropy"]) * 0.4)
        )

        # 改革窗口：合法性高且熵增低 → 改革可行
        v["reform_window"] = max(
            0.0,
            min(1.0, v["legitimacy"] - v["institution_entropy"])
        )

        # 社会危机等级
        v["crisis_level"] = min(
            1.0,
            v["institution_entropy"] * 0.4
            + (1 - v["fiscal_health"]) * 0.4
            + pop_pressure * 0.2
        )

        # 相位推进
        self.state.phase = (self.state.phase + 1.0 / self.CYCLE_LENGTH) % 1.0
        self._update_stage()

    def emit_downstream(self) -> dict:
        """向事件 Agent 输出：制度韧性、财政压力、危机等级。"""
        v = self.state.variables
        self.state.downstream_signal = {
            "crisis_level": v["crisis_level"],
            "fiscal_stress": 1.0 - v["fiscal_health"],
            "reform_capacity": v["reform_window"],
            "institution_entropy": v["institution_entropy"],
        }
        return dict(self.state.downstream_signal)

    def emit_feedback(self) -> float:
        """向上层（人口）反馈：制度韧性影响人口承载力。"""
        v = self.state.variables
        # 好制度可以缓冲人口压力，坏制度加速崩溃
        return v["fiscal_health"] - 0.5  # [-0.5, 0.5]
