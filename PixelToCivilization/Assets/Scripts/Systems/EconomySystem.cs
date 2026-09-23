using System.Collections.Generic;
using UnityEngine;
using PixelToCivilization.Core;
using PixelToCivilization.Data;

namespace PixelToCivilization.Systems
{
    /// <summary>
    /// 经济系统 —— 1:1 对齐 v5.9.9 updateEconomy：
    /// 建筑产出 + 人口基础采集 + 政策/电力/AI/两宋加成；木石粮金钢消耗；资源转换；饥荒/民心/朝代兴衰；科技研究。
    /// </summary>
    public class EconomySystem : GameSystemBase
    {
        // 每秒产出（供UI显示）
        public Dictionary<string, float> ProductionRate { get; } = new();
        private readonly string[] _stoneBuildings = { "market","temple","well","rich_house","noble_palace","mine","altar","wall","great_wall","watchtower","barracks","palace","pagoda","granary","bank","porcelain_kiln","arsenal","grand_hall","brick_works","factory_pre","factory_modern","power_plant","data_center","skyscraper","ai_lab","space_elevator","fusion_plant","lunar_base","mars_colony","orbital_station","dyson_swarm" };
        private float _warnCd;

        public override void Init(GameManager gm)
        {
            base.Init(gm);
            foreach (var k in ResourceDatabase.Order) ProductionRate[k] = 0;
        }

        public override void Tick(float dt)
        {
            // ===== 产出 =====
            float food=0,gold=0,wood=0,stone=0,iron=0,research=0,culture=0,bronze=0,goods=0,trade=0;
            bool wonderPower = GM.Wonder != null && GM.Wonder.ForcePower;
            float powerMult = S.Era >= 6 ? ((S.PowerCoverage >= 50 || wonderPower) ? 1.5f : 1f) : 1f;
            float aiMult = 1f + S.AiBonus;
            float songMult = S.Era == 3 ? 2f : 1f; // 两宋研究翻倍
            // 火力软上限：由兵力与建筑规模决定，避免火力建筑/学派/事件只增不减导致后期数值无限膨胀
            float fireCap = (80f + S.MilSoldiers*1.5f + S.Buildings.Count*2f) * (GM.Wonder!=null?GM.Wonder.FireCapMul:1f);

            foreach (var b in S.Buildings)
            {
                var d = b.Def; if (d == null) continue;
                float m = b.LevelMult;
                food += d.GetProd("food")*m;
                gold += d.GetProd("gold")*m;
                wood += d.GetProd("wood")*m;
                stone += d.GetProd("stone")*m;
                iron += d.GetProd("iron")*m;
                bronze += d.GetProd("bronze")*m;
                research += d.GetProd("research")*songMult*m;
                culture += d.GetProd("culture")*m;
                goods += d.GetProd("goods")*m;
                trade += d.GetProd("trade")*m;
                if (d.GetProd("water")>0) food += 2*m;
                if (d.GetProd("navigation")>0) research += 1*m;
                if (d.GetProd("firepower")>0) S.MilFirepower = Mathf.Min(fireCap, S.MilFirepower + 0.01f*m*dt);
            }
            // 人口基础采集
            wood += S.Pop * 0.01f;
            stone += S.Pop * 0.005f;
            // 运河加成
            trade *= 1f + S.CanalBonus / 100f;
            // 海洋贸易
            gold += S.OceanDiscovered.Count * 2f;
            // 政策加成
            if (S.Policies.Contains("land_reform")) food *= 1.15f;
            if (S.Policies.Contains("imperial_exam_p")) research *= 1.25f;
            if (S.Policies.Contains("open_sea")) gold *= 2f;
            if (S.Policies.Contains("five_year_plan")) { goods *= 1.8f; iron *= 1.5f; }
            if (S.Policies.Contains("reform_opening")) { gold *= 2f; research *= 1.3f; }
            // 策划书·诸子百家学派加成
            var phil = GM.Philosophy;
            if (phil != null)
            {
                research *= phil.ResearchMult;
                culture *= phil.CultureMult;
                gold *= phil.GoldMult;
                food *= phil.FoodMult;
            }
            // 策划书·科举开设（永久研究加成）
            if (S.SchoolFounded) research *= 1.15f;
            // V6.8.0 世界奇观全局乘数（链末端唯一叠加，不与上面各环节重复相加）
            if (GM.Wonder != null) {
                research *= GM.Wonder.ResearchMul; culture *= GM.Wonder.CultureMul;
                gold *= GM.Wonder.GoldMul; food *= GM.Wonder.FoodMul; goods *= GM.Wonder.GoodsMul;
            }
            // 电力与AI
            food *= powerMult*aiMult; gold *= powerMult*aiMult; research *= powerMult*aiMult;

            S.AddRes("food", food*dt*0.5f);
            S.AddRes("gold", gold*dt*0.3f + trade*dt*0.2f);
            S.AddRes("wood", wood*dt*0.3f);
            S.AddRes("stone", stone*dt*0.2f);
            S.AddRes("iron", iron*dt*0.2f);
            S.AddRes("bronze", bronze*dt*0.1f);
            S.AddRes("research", research*dt*0.5f);
            S.AddRes("culture", culture*dt*0.3f);
            S.AddRes("goods", goods*dt*0.2f);

            ProductionRate["food"]=food;ProductionRate["gold"]=gold+trade*0.66f;ProductionRate["wood"]=wood;
            ProductionRate["stone"]=stone;ProductionRate["iron"]=iron;ProductionRate["research"]=research;
            ProductionRate["culture"]=culture;ProductionRate["goods"]=goods;ProductionRate["bronze"]=bronze;

            // ===== 消耗 =====
            float woodC = S.Buildings.Count*0.003f + S.Ships.Count*0.02f + S.Carts.Count*0.01f;
            foreach (var b in S.Buildings)
                if (b.Def!=null && (b.Def.Cat=="居住"||b.Def.Cat=="经济"||b.Def.Cat=="基础")) woodC += 0.002f;
            S.AddRes("wood", -woodC*dt);

            float stoneC = S.Buildings.Count*0.001f;
            var stoneSet = new HashSet<string>(_stoneBuildings);
            foreach (var b in S.Buildings) if (stoneSet.Contains(b.Type)) stoneC += 0.005f;
            S.AddRes("stone", -stoneC*dt);

            float foodC = S.Pop*0.02f + S.MilSoldiers*0.05f;
            foreach (var b in S.Buildings)
                if (b.Type=="school"||b.Type=="academy_pre"||b.Type=="printing_house") foodC += 0.02f;
            S.AddRes("food", -foodC*dt*0.5f);

            float goldC = S.MilSoldiers*0.01f + (S.CurrentResearch!=null?0.05f:0) + (S.WarActive?0.1f:0);
            foreach (var b in S.Buildings)
            {
                if (b.Type=="power_plant"||b.Type=="data_center"||b.Type=="ai_lab"||b.Type=="space_elevator"||
                    b.Type=="fusion_plant"||b.Type=="lunar_base"||b.Type=="mars_colony"||b.Type=="orbital_station"||
                    b.Type=="dyson_swarm"||b.Type=="spaceship_yard") goldC += 0.03f;
                if (b.Type=="factory_pre"||b.Type=="factory_modern"||b.Type=="modern_arsenal"||b.Type=="dockyard_modern") goldC += 0.02f;
            }
            S.AddRes("gold", -goldC*dt*0.3f);

            float steelC = S.Carts.Count*0.005f;
            if (S.Era >= 6)
            {
                steelC += S.MilSoldiers*0.005f + (S.WarActive?0.05f:0);
                foreach (var b in S.Buildings)
                {
                    if (b.Type=="factory_modern"||b.Type=="power_plant"||b.Type=="data_center"||b.Type=="ai_lab"||
                        b.Type=="high_speed_rail"||b.Type=="airport"||b.Type=="highway_modern") steelC += 0.01f;
                    if (b.Type=="space_elevator"||b.Type=="fusion_plant"||b.Type=="lunar_base"||b.Type=="mars_colony"||
                        b.Type=="orbital_station"||b.Type=="dyson_swarm"||b.Type=="spaceship_yard") steelC += 0.02f;
                }
            }
            S.AddRes("steel", -steelC*dt*0.2f);

            // ===== 资源转换 =====
            if (S.Era >= 6 && S.GetRes("iron")>50 && S.GetRes("stone")>30)
            { S.AddRes("steel", dt*0.3f); S.AddRes("concrete", dt*0.5f); }
            if (S.Era >= 7 && S.GetRes("helium3")>10)
            { S.AddRes("fusion", dt*0.5f); S.AddRes("carbon", dt*0.2f); }

            // ===== 耗尽警告（限频）=====
            _warnCd -= dt;
            if (_warnCd <= 0)
            {
                if (S.GetRes("wood")<=0) GM.AddEvent("bad","🪵 木材耗尽！建筑无法维修");
                if (S.GetRes("stone")<=0) GM.AddEvent("bad","⛰️ 石材耗尽！石质建筑老化加速");
                if (S.GetRes("gold")<=0) GM.AddEvent("bad","💰 国库空虚！科技/军事停滞");
                if (S.GetRes("steel")<=0 && S.Era>=6) GM.AddEvent("bad","🔩 钢铁短缺！工业/太空发展受阻");
                _warnCd = 8f;
            }

            // ===== 饥荒 / 人口增长（一律按已乘速度的游戏时间 dt，禁止每帧固定 ±1 的帧率相关崩溃）=====
            if (S.GetRes("food") <= 0)
            {
                // 1 倍速约 2 秒掉 1 人，留出补救窗口；高倍速按游戏时间快速消耗（旧实现每帧 -1，60fps 每秒掉 60 人秒崩）
                int starve = Mathf.Max(1, Mathf.CeilToInt(dt*0.5f));
                S.Pop = Mathf.Max(10, S.Pop-starve);
                // 民心下降统一由下方「民心」段按 dt 结算（food≤0 自然落入下降分支），此处不再重复 -1
            }
            else if (S.GetRes("food")>100 && S.Pop < S.Housing && S.Pop < GameConstants.MaxPop)
            {
                S.Pop = Mathf.Min(GameConstants.MaxPop, Mathf.FloorToInt(Mathf.Min(S.Housing, S.Pop + dt*0.5f)));
            }

            // ===== 民心 =====
            if (S.GetRes("food") > 50) S.Happiness = Mathf.Min(100, S.Happiness+dt*0.1f);
            else S.Happiness = Mathf.Max(0, S.Happiness-dt*0.2f);

            // ===== 朝代兴衰 =====
            S.DynastyMorale -= dt*0.05f;

            // ===== 科技研究 =====
            if (S.CurrentResearch != null && GM.Techs.TryGetValue(S.CurrentResearch, out var tech))
            {
                S.ResearchProgress += research*dt;
                if (S.ResearchProgress >= tech.Cost)
                {
                    string finishedId=S.CurrentResearch;
                    S.ResearchedTechs.Add(finishedId);
                    GM.AddEvent("good","🔬 研究完成：" + tech.Name);
                    S.CurrentResearch = null; S.ResearchProgress = 0;
                    // V6.1.1 科技特殊效果：运河工程启动自动开凿+潮汐（对齐 v5.9.9）
                    if (finishedId=="canal_engineering") GM.Canal.StartAuto();
                }
            }
        }
    }
}
