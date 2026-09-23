"""四层嵌套周期系统：组合所有 Agent，实现双向反馈。

压力传导链：
零级气候 → 一级人口 → 二级制度 → 三级事件

双向反馈：
- 自上而下：climate.capacity → population.pressure → institution.crisis → event.amplitude
- 自下而上：event.social_unrest → institution.reform → population.carbon → climate.temp
"""

from __future__ import annotations

from typing import Dict, List

from .base import AgentState
from .l0_climate import ClimateAgent
from .l1_population import PopulationAgent
from .l2_institution import InstitutionAgent
from .l3_event import EventAgent


class NestedCycleSystem:
    """四层嵌套周期系统。

    用法:
        system = NestedCycleSystem(start_year=1850)
        for year in range(1850, 2025):
            snapshot = system.step(1)
        print(system.report())
    """

    def __init__(self, start_year: int = 0, **kwargs):
        self.climate = ClimateAgent(start_year=start_year, **kwargs.get("climate", {}))
        self.population = PopulationAgent(
            start_year=start_year, **kwargs.get("population", {})
        )
        self.institution = InstitutionAgent(
            start_year=start_year, **kwargs.get("institution", {})
        )
        self.event = EventAgent(start_year=start_year, **kwargs.get("event", {}))
        self.history: List[Dict] = []

    def step(self, years: int = 1) -> Dict:
        """推进 years 年，执行完整的上下传导循环。"""
        for _ in range(years):
            # 1. 气候推进（最慢层，每年也推进但变化很小）
            self.climate.step(1)
            climate_signal = self.climate.emit_downstream()

            # 2. 人口接收气候约束，推进
            self.population.receive_upstream(climate_signal)
            self.population.step(1)
            pop_signal = self.population.emit_downstream()
            pop_feedback = self.population.emit_feedback()

            # 3. 制度接收人口约束，推进
            self.institution.receive_upstream(pop_signal)
            self.institution.step(1)
            inst_signal = self.institution.emit_downstream()
            inst_feedback = self.institution.emit_feedback()

            # 4. 事件接收制度约束，推进
            self.event.receive_upstream(inst_signal)
            self.event.step(1)
            event_feedback = self.event.emit_feedback()

            # 5. 自下而上反馈
            # 事件 → 制度
            self.institution.receive_feedback(event_feedback)
            # 制度 → 人口（制度韧性影响人口承载力感知）
            self.population.receive_feedback(inst_feedback * 0.5)
            # 人口 → 气候（碳排压力）
            self.climate.receive_feedback(pop_feedback)

            # 记录快照
            self.history.append(self.snapshot())

        return self.snapshot()

    def snapshot(self) -> Dict:
        return {
            "year": self.climate.state.year,
            "climate": self.climate.summary(),
            "population": self.population.summary(),
            "institution": self.institution.summary(),
            "event": self.event.summary(),
        }

    def report(self, last_n: int = 10) -> str:
        """生成人类可读的周期报告。"""
        if not self.history:
            return "尚无运行数据。"

        lines = [
            "=" * 60,
            "TwinsEarth V1.0.1 — 四层嵌套周期 Agent 报告",
            "=" * 60,
            f"模拟年份: {self.climate.state.year}",
            f"总步数: {len(self.history)}",
            "",
        ]

        # 最终状态
        snap = self.history[-1]
        lines.append("【当前状态】")
        lines.append(f"  气候: 温度异常={snap['climate']['variables'].get('temp_anomaly', 0):.2f}°C, "
                      f"承载力={snap['climate']['variables'].get('carrying_capacity', 0):.2f}")
        lines.append(f"  人口: {snap['population']['variables'].get('population', 0):.1f}M, "
                      f"压力比={snap['population']['variables'].get('pressure_ratio', 0):.2f}, "
                      f"阶段={snap['population']['stage']}")
        lines.append(f"  制度: 财政健康={snap['institution']['variables'].get('fiscal_health', 0):.2f}, "
                      f"危机={snap['institution']['variables'].get('crisis_level', 0):.2f}")
        lines.append(f"  事件: 经济增长={snap['event']['variables'].get('economic_growth', 0):.2f}%, "
                      f"动荡={snap['event']['variables'].get('social_unrest', 0):.2f}")
        lines.append("")

        # 最近 N 步趋势
        if len(self.history) >= last_n:
            lines.append(f"【最近 {last_n} 步趋势】")
            for h in self.history[-last_n:]:
                lines.append(
                    f"  年{h['year']:>6}: "
                    f"人口={h['population']['variables'].get('population', 0):>6.1f}M "
                    f"财政={h['institution']['variables'].get('fiscal_health', 0):.2f} "
                    f"增长={h['event']['variables'].get('economic_growth', 0):>+6.2f}% "
                    f"动荡={h['event']['variables'].get('social_unrest', 0):.2f}"
                )

        lines.append("=" * 60)
        return "\n".join(lines)
