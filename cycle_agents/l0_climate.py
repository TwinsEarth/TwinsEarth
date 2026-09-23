"""零级祖周期 Agent：地球气候周期（10万年级）。

米兰科维奇循环：轨道偏心率（~10万年）、地轴倾角（~4.1万年）、岁差（~2万年）。
决定人类文明的"游戏棋盘"——哪些地区宜居，哪些文明可能诞生。
人类无法跳出此周期，只能适应、延缓或局部调控。
"""

from __future__ import annotations

import math

from .base import CycleAgent


class ClimateAgent(CycleAgent):
    """地球气候 Agent。

    输入：太阳辐射、轨道力学参数、火山活动、CO2浓度
    输出：温度异常、降水模式、海平面、生态系统承载力
    周期：~10万年（冰期-间冰期旋回）
    """

    LAYER = 0
    NAME = "climate"
    CYCLE_LENGTH = 100_000.0  # 年

    def _init_variables(self, start_temp: float = 0.0, co2: float = 280.0) -> None:
        self.state.variables = {
            "temp_anomaly": start_temp,       # 温度异常（°C，相对工业前）
            "precipitation": 100.0,           # 降水指数（%）
            "sea_level": 0.0,                 # 海平面异常（m）
            "co2_ppm": co2,                   # CO2 浓度
            "carrying_capacity": 1.0,         # 生态承载力（归一化）
            # 米兰科维奇参数（简化模型）
            "eccentricity_phase": 0.3,        # 偏心率相位
            "obliquity_phase": 0.5,           # 地轴倾角相位
            "precession_phase": 0.1,          # 岁差相位
        }

    def _evolve(self) -> None:
        v = self.state.variables
        dt = 1  # 每年

        # 米兰科维奇三周期叠加（简化）
        # 偏心率：~10万年，振幅小但决定冰期节奏
        v["eccentricity_phase"] += dt / 100_000.0
        # 地轴倾角：~4.1万年
        v["obliquity_phase"] += dt / 41_000.0
        # 岁差：~2.3万年
        v["precession_phase"] += dt / 23_000.0

        # 温度异常 = 三周期正弦叠加
        milankovitch = (
            0.5 * math.sin(v["eccentricity_phase"] * 2 * math.pi)
            + 0.3 * math.sin(v["obliquity_phase"] * 2 * math.pi)
            + 0.2 * math.sin(v["precession_phase"] * 2 * math.pi)
        )

        # CO2 人类活动扰动（在 1850 年后快速上升）
        if self.state.year > 1850:
            years_since_industrial = self.state.year - 1850
            v["co2_ppm"] = min(
                280.0 + 0.8 * years_since_industrial ** 1.2,
                600.0
            )

        # 温度 = 自然周期 + CO2 温室效应（每 doubling CO2 ~3°C）
        co2_effect = (v["co2_ppm"] - 280.0) * 0.008
        v["temp_anomaly"] = milankovitch * 2.0 + co2_effect

        # 海平面：温度上升 → 冰盖融化 → 海平面上升
        v["sea_level"] = max(0.0, v["temp_anomaly"]) * 2.3

        # 降水：温度异常影响季风和洋流
        v["precipitation"] = 100.0 + v["temp_anomaly"] * 5.0

        # 生态承载力：温度适中时最高，过冷或过热都下降
        optimal = 1.0  # 全新世适宜期
        deviation = abs(v["temp_anomaly"] - optimal)
        v["carrying_capacity"] = max(0.3, 1.0 - deviation * 0.15)

        # 相位推进（以 10 万年为周期）
        self.state.phase = (self.state.year / self.CYCLE_LENGTH) % 1.0
        self._update_stage()

    def emit_downstream(self) -> dict:
        """向人口 Agent 输出：承载力上限和气候适宜度。"""
        v = self.state.variables
        self.state.downstream_signal = {
            "carrying_capacity": v["carrying_capacity"],
            "temp_anomaly": v["temp_anomaly"],
            "climate_stress": max(0.0, abs(v["temp_anomaly"]) - 1.0) * 0.5,
        }
        return dict(self.state.downstream_signal)

    def emit_feedback(self) -> float:
        """人口压力反馈：过度开垦可能局部改变微气候（反馈很小）。"""
        v = self.state.variables
        # 人类对气候的反馈被放大（温室效应），但在零级尺度上仍有限
        return min(0.3, self.state.feedback_from_below * 0.1)
