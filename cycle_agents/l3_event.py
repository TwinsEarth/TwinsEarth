"""三级孙周期 Agent：事件/经济/政治周期（数年到数十年）。

经济周期（繁荣-萧条）、政治周期、社会动荡。
嵌套在制度子周期内，是其"表面波动"。
当多个孙周期共振时，可触发子周期的临界点。
"""

from __future__ import annotations

import math
import random

from .base import CycleAgent


class EventAgent(CycleAgent):
    """事件/经济 Agent。

    输入：制度韧性、财政压力（来自二级）
    输出：经济波动、政治事件、社会动荡
    周期：3-10年（商业）；1-5年（政治）
    """

    LAYER = 3
    NAME = "event"
    CYCLE_LENGTH = 7.0

    def _init_variables(
        self,
        economic_growth: float = 2.0,       # 年增长率 %
        market_sentiment: float = 0.5,      # 0=恐慌，1=贪婪
        social_unrest: float = 0.1,         # 社会动荡等级
        political_instability: float = 0.2, # 政治不稳定
    ) -> None:
        self.state.variables = {
            "economic_growth": economic_growth,
            "market_sentiment": market_sentiment,
            "social_unrest": social_unrest,
            "political_instability": political_instability,
            "debt_level": 0.5,                 # 债务杠杆
            "asset_bubble": 0.3,              # 资产泡沫程度
            "event_amplitude": 0.0,            # 当前事件振幅
        }
        self._rng = random.Random(42)  # 固定种子可复现

    def _evolve(self) -> None:
        v = self.state.variables

        # 上层约束：制度危机
        crisis = self.state.upstream_constraint.get("crisis_level", 0.2)
        fiscal_stress = self.state.upstream_constraint.get("fiscal_stress", 0.3)
        reform_cap = self.state.upstream_constraint.get("reform_capacity", 0.5)

        # 经济周期：基钦周期 ~3-4年，朱格拉周期 ~7-10年
        # 叠加正弦波 + 随机冲击
        phase = self.state.phase * 2 * math.pi
        business_cycle = math.sin(phase * 2) * 2.0  # 3-4年周期
        juglar_cycle = math.sin(phase) * 1.5        # 7年周期

        # 随机冲击（外部事件）
        shock = self._rng.gauss(0, 0.5)

        # 经济增长率 = 周期项 + 冲击 + 上层传导
        base_growth = 2.0
        v["economic_growth"] = (
            base_growth
            + business_cycle * (1 - crisis * 0.5)
            + juglar_cycle * (1 - fiscal_stress * 0.3)
            + shock
            - crisis * 3.0
        )

        # 市场情绪：贪婪与恐慌交替
        v["market_sentiment"] = max(
            0.0,
            min(1.0, 0.5 + v["economic_growth"] * 0.15 + shock * 0.1)
        )

        # 债务杠杆：经济好时加杠杆，差时去杠杆
        v["debt_level"] = max(
            0.0,
            min(1.0, v["debt_level"] + v["economic_growth"] * 0.02 - crisis * 0.05)
        )

        # 资产泡沫：情绪 + 杠杆
        v["asset_bubble"] = min(
            1.0,
            v["market_sentiment"] * 0.5 + v["debt_level"] * 0.5
        )

        # 社会动荡：制度危机 + 经济差 → 动荡
        v["social_unrest"] = max(
            0.0,
            min(1.0, crisis * 0.6 + (1 - max(0, v["economic_growth"])) * 0.2 + shock * 0.05)
        )

        # 政治不稳定：社会动荡 + 改革空间小
        v["political_instability"] = max(
            0.0,
            min(1.0, v["social_unrest"] * 0.7 + (1 - reform_cap) * 0.3)
        )

        # 事件振幅 = 所有波动的合成
        v["event_amplitude"] = (
            abs(v["economic_growth"]) * 0.3
            + v["social_unrest"] * 0.4
            + v["political_instability"] * 0.3
        )

        # 相位推进
        self.state.phase = (self.state.phase + 1.0 / self.CYCLE_LENGTH) % 1.0
        self._update_stage()

    def emit_downstream(self) -> dict:
        """三级 Agent 没有下游，返回自身状态摘要。"""
        v = self.state.variables
        return {
            "event_amplitude": v["event_amplitude"],
            "economic_growth": v["economic_growth"],
            "social_unrest": v["social_unrest"],
        }

    def emit_feedback(self) -> float:
        """向上层（制度）反馈：事件冲击可能加速或延缓制度周期。"""
        v = self.state.variables
        # 大动荡 → 制度被迫改革（正反馈）；持续低波动 → 制度僵化
        return v["social_unrest"] - 0.2  # [-0.2, 0.8]
