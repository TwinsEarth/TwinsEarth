"""周期 Agent 基类与通用数据结构。"""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any, Dict, Optional


@dataclass
class AgentState:
    """Agent 当前状态快照。"""

    layer: int
    name: str
    year: int
    # 核心变量
    variables: Dict[str, float] = field(default_factory=dict)
    # 周期相位：0.0 = 上行起点，0.5 = 顶峰，1.0 = 下行终点
    phase: float = 0.0
    # 周期阶段标签
    stage: str = "ascending"  # ascending | peak | descending | trough
    # 上层传入的约束
    upstream_constraint: Dict[str, float] = field(default_factory=dict)
    # 向下层施加的约束
    downstream_signal: Dict[str, float] = field(default_factory=dict)
    # 自下而上的反馈（从下层累积）
    feedback_from_below: float = 0.0

    def to_dict(self) -> Dict[str, Any]:
        return {
            "layer": self.layer,
            "name": self.name,
            "year": self.year,
            "variables": self.variables,
            "phase": round(self.phase, 4),
            "stage": self.stage,
            "feedback_from_below": round(self.feedback_from_below, 4),
        }


class CycleAgent:
    """周期 Agent 基类。

    每个 Agent 维护自身状态，每步响应上层约束，计算自身演化，
    并产生向下层的信号和向上层的反馈。
    """

    LAYER: int = -1
    NAME: str = "base"
    # 典型周期长度（年）
    CYCLE_LENGTH: float = 100.0

    def __init__(self, start_year: int = 0, **kwargs):
        self.state = AgentState(
            layer=self.LAYER,
            name=self.NAME,
            year=start_year,
        )
        self._init_variables(**kwargs)

    def _init_variables(self, **kwargs) -> None:
        """初始化核心变量。子类覆盖。"""
        self.state.variables = {}

    def step(self, years: int = 1) -> AgentState:
        """推进 years 年，返回新状态。"""
        for _ in range(years):
            self.state.year += 1
            self._evolve()
        return self.state

    def _evolve(self) -> None:
        """内部演化逻辑。子类覆盖。"""
        raise NotImplementedError

    def receive_upstream(self, constraint: Dict[str, float]) -> None:
        """接收上层传入的约束。"""
        self.state.upstream_constraint.update(constraint)

    def emit_downstream(self) -> Dict[str, float]:
        """向下层发出信号。子类覆盖。"""
        return dict(self.state.downstream_signal)

    def receive_feedback(self, feedback: float) -> None:
        """接收下层的累积反馈。"""
        self.state.feedback_from_below += feedback

    def emit_feedback(self) -> float:
        """向上层发出反馈（归一化到 [-1, 1]）。子类覆盖。"""
        return 0.0

    def _update_stage(self) -> None:
        """根据 phase 更新阶段标签。"""
        p = self.state.phase % 1.0
        if p < 0.25:
            self.state.stage = "ascending"
        elif p < 0.5:
            self.state.stage = "peak"
        elif p < 0.75:
            self.state.stage = "descending"
        else:
            self.state.stage = "trough"

    def summary(self) -> Dict[str, Any]:
        return self.state.to_dict()
