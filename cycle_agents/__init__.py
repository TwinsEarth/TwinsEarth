"""
TwinsEarth V1.0.1 — 四层嵌套周期 Agent 框架

零级祖周期：气候 Agent（10万年级）
一级母周期：人口 Agent（数百年级）
二级子周期：制度/财政 Agent（数十年到数百年）
三级孙周期：事件/经济 Agent（数年到数十年）

每层 Agent 是一个独立的状态机，响应上层约束，向下层施加约束。
层级间存在双向反馈：自上而下的压力传导 + 自下而上的扰动反馈。
"""

from .base import CycleAgent, AgentState
from .l0_climate import ClimateAgent
from .l1_population import PopulationAgent
from .l2_institution import InstitutionAgent
from .l3_event import EventAgent
from .hierarchy import NestedCycleSystem

__version__ = "1.0.1"
__all__ = [
    "CycleAgent",
    "AgentState",
    "ClimateAgent",
    "PopulationAgent",
    "InstitutionAgent",
    "EventAgent",
    "NestedCycleSystem",
]
