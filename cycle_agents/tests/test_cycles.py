"""cycle_agents 测试套件。"""

import pytest
from cycle_agents import (
    ClimateAgent,
    PopulationAgent,
    InstitutionAgent,
    EventAgent,
    NestedCycleSystem,
)


class TestClimateAgent:
    def test_init(self):
        c = ClimateAgent(start_year=0)
        assert c.LAYER == 0
        assert c.state.variables["co2_ppm"] == 280.0

    def test_step(self):
        c = ClimateAgent(start_year=1850)
        c.step(10)
        assert c.state.year == 1860
        assert "temp_anomaly" in c.state.variables

    def test_co2_rise_after_industrial(self):
        c = ClimateAgent(start_year=1850)
        c.step(100)
        assert c.state.variables["co2_ppm"] > 280.0


class TestPopulationAgent:
    def test_init(self):
        p = PopulationAgent(start_year=0)
        assert p.LAYER == 1
        assert p.state.variables["population"] > 0

    def test_growth_under_capacity(self):
        p = PopulationAgent(start_year=0)
        p.receive_upstream({"carrying_capacity": 1.5, "climate_stress": 0.0})
        p.step(50)
        # 承载力充足时人口应增长
        assert p.state.variables["population"] > 50.0

    def test_collapse_over_capacity(self):
        p = PopulationAgent(start_year=0, population=200.0)
        p.receive_upstream({"carrying_capacity": 0.3, "climate_stress": 0.5})
        p.step(100)
        # 超过承载力时人口应下降
        assert p.state.variables["population"] < 200.0


class TestInstitutionAgent:
    def test_init(self):
        i = InstitutionAgent(start_year=0)
        assert i.LAYER == 2

    def test_entropy_grows(self):
        i = InstitutionAgent(start_year=0)
        i.receive_upstream({"population_pressure": 0.5, "aging_burden": 0.1})
        i.step(100)
        assert i.state.variables["institution_entropy"] > 0.2

    def test_crisis_under_pressure(self):
        i = InstitutionAgent(start_year=0)
        i.receive_upstream({"population_pressure": 1.5, "aging_burden": 0.3})
        i.step(100)
        assert i.state.variables["crisis_level"] > 0.3


class TestEventAgent:
    def test_init(self):
        e = EventAgent(start_year=0)
        assert e.LAYER == 3

    def test_volatility(self):
        e = EventAgent(start_year=0)
        e.receive_upstream({"crisis_level": 0.5, "fiscal_stress": 0.5, "reform_capacity": 0.3})
        e.step(20)
        assert "economic_growth" in e.state.variables


class TestNestedCycleSystem:
    def test_full_simulation(self):
        system = NestedCycleSystem(start_year=1850)
        system.step(100)
        assert len(system.history) == 100
        snap = system.history[-1]
        assert snap["year"] == 1950
        assert "climate" in snap
        assert "population" in snap
        assert "institution" in snap
        assert "event" in snap

    def test_bidirectional_feedback(self):
        system = NestedCycleSystem(start_year=1850)
        system.step(50)
        # 气候应收到人口反馈
        assert system.climate.state.feedback_from_below != 0.0

    def test_report(self):
        system = NestedCycleSystem(start_year=1850)
        system.step(10)
        report = system.report()
        assert "四层嵌套周期" in report
        assert "气候" in report

    def test_modern_scenario(self):
        """模拟现代：高 CO2、老龄化、高债务。"""
        system = NestedCycleSystem(
            start_year=2000,
            population={"population": 8000.0, "tech_level": 20.0, "institution_version": 3},
            institution={"fiscal_health": 0.5},
        )
        system.step(25)
        snap = system.history[-1]
        # 25年后应有完整状态
        assert snap["year"] == 2025
