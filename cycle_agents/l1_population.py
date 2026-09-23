"""一级母周期 Agent：人口周期律（数百年）。

核心机制：人口增长 → 资源压力 → 崩溃 → 重置。
只有社会制度/技术的大版本升级（农业革命、工业革命、数字革命）才能切换此周期。
当前形态：从"人口过剩→崩溃"切换为"人口萎缩→老龄化→停滞"。
"""

from __future__ import annotations

import math

from .base import CycleAgent


class PopulationAgent(CycleAgent):
    """人口周期 Agent。

    输入：气候承载力（来自零级）、技术水平、社会制度
    输出：人口规模、年龄结构、劳动力供给
    周期：~300年（农业帝国）；现代形态约100年
    """

    LAYER = 1
    NAME = "population"
    CYCLE_LENGTH = 300.0

    def _init_variables(
        self,
        population: float = 50.0,       # 百万
        tech_level: float = 1.0,          # 技术乘数（1=农业，5=工业，20=数字）
        institution_version: int = 1,     # 制度大版本（1=农业，2=工业，3=数字）
    ) -> None:
        self.state.variables = {
            "population": population,          # 百万
            "birth_rate": 35.0,                 # 千分率
            "death_rate": 30.0,                 # 千分率
            "labor_ratio": 0.55,                # 劳动年龄人口占比
            "aging_ratio": 0.08,                # 老龄化比例
            "tech_level": tech_level,
            "institution_version": institution_version,
            "pressure_ratio": 0.0,              # 人口/承载力比
            "carrying_capacity_base": 100.0,    # 基础承载力（百万）
        }

    def _evolve(self) -> None:
        v = self.state.variables

        # 上层约束：气候承载力
        climate_cap = self.state.upstream_constraint.get("carrying_capacity", 1.0)
        climate_stress = self.state.upstream_constraint.get("climate_stress", 0.0)

        # 实际承载力 = 基础承载力 × 技术乘数 × 气候因子
        effective_capacity = (
            v["carrying_capacity_base"]
            * (1 + math.log1p(v["tech_level"]))
            * climate_cap
        )

        v["pressure_ratio"] = v["population"] / effective_capacity

        # 人口动力学：逻辑斯蒂增长 + 崩溃阈值
        if v["pressure_ratio"] < 0.8:
            # 增长期：出生率 > 死亡率
            v["birth_rate"] = 35.0 - v["aging_ratio"] * 20.0
            v["death_rate"] = 25.0 + climate_stress * 10.0
        elif v["pressure_ratio"] < 1.0:
            # 顶峰期：增长放缓
            v["birth_rate"] = 20.0 - v["aging_ratio"] * 15.0
            v["death_rate"] = 22.0 + climate_stress * 15.0
        else:
            # 崩溃期：死亡率飙升
            v["birth_rate"] = 12.0
            v["death_rate"] = 45.0 + (v["pressure_ratio"] - 1.0) * 50.0 + climate_stress * 20.0

        # 人口变化（千分率 → 年变化）
        net_growth = (v["birth_rate"] - v["death_rate"]) / 1000.0
        v["population"] *= (1 + net_growth)
        v["population"] = max(1.0, v["population"])

        # 年龄结构演化
        if net_growth > 0.01:
            v["aging_ratio"] = max(0.05, v["aging_ratio"] - 0.001)
            v["labor_ratio"] = min(0.65, v["labor_ratio"] + 0.001)
        elif net_growth < -0.005:
            v["aging_ratio"] = min(0.35, v["aging_ratio"] + 0.002)
            v["labor_ratio"] = max(0.40, v["labor_ratio"] - 0.002)
        else:
            # 低生育稳态：老龄化加深
            v["aging_ratio"] = min(0.40, v["aging_ratio"] + 0.0005)

        # 相位推进
        self.state.phase = (self.state.phase + 1.0 / self.CYCLE_LENGTH) % 1.0
        self._update_stage()

    def emit_downstream(self) -> dict:
        """向制度 Agent 输出：人口压力、劳动力供给、年龄结构。"""
        v = self.state.variables
        self.state.downstream_signal = {
            "population_pressure": v["pressure_ratio"],
            "labor_supply": v["population"] * v["labor_ratio"],
            "aging_burden": v["aging_ratio"],
            "net_growth_rate": (v["birth_rate"] - v["death_rate"]) / 1000.0,
        }
        return dict(self.state.downstream_signal)

    def emit_feedback(self) -> float:
        """向上层（气候）反馈：人口压力导致碳排放和资源消耗。"""
        v = self.state.variables
        # 人口越多、技术越高，碳排越多 → 气候反馈越强
        carbon_pressure = v["population"] * math.log1p(v["tech_level"]) / 100.0
        return min(1.0, carbon_pressure)
