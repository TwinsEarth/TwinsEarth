using System.Collections.Generic;
using UnityEngine;
using PixelToCivilization.Core;
using PixelToCivilization.Data;
using PixelToCivilization.World;
using PixelToCivilization.Rendering;

namespace PixelToCivilization.Systems
{
    /// <summary>
    /// 建筑系统 —— 对齐 v5.9.9：水域/沙滩/时代/科技/资源/间距校验，建造、升级(最高3级)、拆除、年久老化、AI自动建造、时代风格刷新。
    /// </summary>
    public class BuildingSystem : GameSystemBase
    {
        public const float Tile = 4f;
        public const int MaxLevel = 3;

        // 建筑→所需科技
        public static readonly Dictionary<string,string> TechGate = new()
        {
            {"bronze_forge","bronze_casting"},{"iron_smelter","iron_smelting"},{"great_wall","great_wall_tech"},
            {"canal","canal_engineering"},{"printing_house","movable_type"},{"gunpowder_mill","gunpowder"},
            {"compass_shop","compass"},{"treasure_shipyard","treasure_ship"},{"modern_arsenal","modern_military"},
            {"railway_pre","railway"},{"power_plant","electricity"},{"data_center","computer"},
            {"ai_lab","ai_tech"},{"high_speed_rail","high_speed_rail_tech"},{"airport","aviation"},
            {"space_elevator","space_elevator_tech"},{"fusion_plant","fusion_power"},
            {"dyson_swarm","dyson_swarm_tech"},{"lunar_base","lunar_landing"},{"mars_colony","mars_landing"},
        };
        // 沙滩允许建筑
        static readonly HashSet<string> BeachAllowed = new()
        { "sea_port","treasure_shipyard","dockyard_modern","shipyard_pre","canal","lumbermill" };

        public WorldGenerator Terrain;
        public Buildings.BuildingMeshFactory MeshFactory;
        public Transform BuildingRoot;

        public override void Init(GameManager gm)
        {
            base.Init(gm);
            Terrain = Object.FindObjectOfType<WorldGenerator>();
            MeshFactory = Object.FindObjectOfType<Buildings.BuildingMeshFactory>();
            if (BuildingRoot==null)
            {
                var rootGo=new GameObject("Buildings");
                rootGo.transform.SetParent(gm.transform);
                BuildingRoot=rootGo.transform;
            }
        }

        // ===== 可建校验 =====
        public bool CanBuild(string type, float x, float z, out string reason)
        {
            reason = null;
            var def = GM.Def(type);
            if (def == null) { reason = "无此建筑"; return false; }
            if (def.Era > S.Era) { reason = "时代未到"; return false; }
            if (Terrain != null)
            {
                if (!Terrain.InsideFrontier(x,z)) { reason = "疆域尚未扩展到此处"; return false; } // V6.3.7(真扩展) 边疆外不可建造
                if (Terrain.IsWater(x,z) && type != "canal") { reason = "水域只能修运河"; return false; }
                if (Terrain.IsBeach(x,z) && !BeachAllowed.Contains(type)) { reason = "滩涂只能建码头类"; return false; }
            }
            if (TechGate.TryGetValue(type, out var tech) && !S.ResearchedTechs.Contains(tech))
            { reason = "需要科技：" + (GM.Techs.TryGetValue(tech,out var td)?td.Name:tech); return false; }
            if (S.Buildings.Count >= GameConstants.MaxBuildings) { reason = "建筑数量已达上限"; return false; }
            if (!S.CanAfford(def.Cost)) { reason = "资源不足"; return false; }
            foreach (var b in S.Buildings)
                if (Mathf.Abs(b.X-x) < Tile*1.5f && Mathf.Abs(b.Z-z) < Tile*1.5f)
                { reason = "距离其他建筑太近"; return false; }
            return true;
        }

        public bool PlaceBuilding(string type, float x, float z, string mapId = "home")
        {
            if (!CanBuild(type,x,z,out var reason)) { if (reason!=null) GM.AddEvent("bad","🚫 "+reason); return false; }
            var def = GM.Def(type);
            S.Pay(def.Cost);
            var e = new BuildingEntity { Type=type, Def=def, X=x, Z=z, Level=1, Hp=100, MapId=mapId };
            S.Buildings.Add(e);
            SpawnView(e);
            OnBuilt(e);
            return true;
        }

        /// <summary>开局村落免费放置：绕过时代/资源/间距检查，不扣资源（仍生成实体、视图与建成效果）</summary>
        public BuildingEntity PlaceInitial(string type, float x, float z, string mapId = "home")
        {
            var def = GM.Def(type);
            if (def == null) return null;
            if (Terrain != null && Terrain.IsWater(x,z) && type != "canal") return null;
            var e = new BuildingEntity { Type=type, Def=def, X=x, Z=z, Level=1, Hp=100, MapId=mapId };
            S.Buildings.Add(e);
            SpawnView(e);
            OnBuilt(e);
            return e;
        }

        public void SpawnView(BuildingEntity e)
        {
            if (MeshFactory == null || e.MapId != "home") return;
            float y = Terrain != null ? Terrain.HeightAt(e.X,e.Z) : 0;
            e.View = MeshFactory.Create(e, GM.Era, new Vector3(e.X,y,e.Z), BuildingRoot);
            // V6.2.2 三档 LOD：完全体/简化体/像素点，按屏幕占比由 LODGroup 自动切换
            LODKit.Attach(e.View, 4.2f + (e.Level-1)*1.3f);
        }

        /// <summary>建成特殊效果</summary>
        private void OnBuilt(BuildingEntity e)
        {
            var type = e.Type;
            switch (type)
            {
                case "canal": GM.Canal.AddSegment(); GM.AddEvent("good","🌊 大运河段已建成！贸易+"+S.CanalBonus+"%"); break;
                case "barracks": S.MilSoldiers += 5; GM.AddEvent("good","⚔️ 军营建成，可训练士兵"); break;
                case "power_plant": case "fusion_plant": GM.Infra.OnBuildingBuilt(type); break;
                case "ai_lab": case "dyson_swarm": GM.Infra.OnBuildingBuilt(type); break;
                case "mars_colony":
                    S.Victory = true; GM.AddEvent("good","🎉 火星移民成功！人类文明迈向星际！"); break;
                case "space_elevator":
                    S.SpElevator=100; GM.AddEvent("good","🛗 太空电梯建成！太空建设速度翻倍"); break;
                case "lunar_base":
                    S.SpLunar=100; GM.AddEvent("good","🌙 月球基地建成！开始开采氦-3"); break;
                case "treasure_shipyard":
                    GM.AddEvent("good","⛵ 宝船厂建成，可建造宝船舰队"); break;
            }
            if (e.Def.GetFunc("housing") > 0) GM.Population.OnResidenceBuilt(type);
            if (e.Def.GetFunc("soldiers") > 0) S.MilSoldiers += e.Def.GetFunc("soldiers");
            if (e.Def.GetFunc("cavalry") > 0) S.MilCavalry += e.Def.GetFunc("cavalry");
            if (e.Def.GetFunc("defense") > 0) S.MilDefense += e.Def.GetFunc("defense");
        }

        // ===== 升级 =====
        public Dictionary<string,int> UpgradeCost(BuildingEntity b)
        {
            var cost = new Dictionary<string,int>();
            if (b.Def.Cost == null) return cost;
            foreach (var kv in b.Def.Cost) cost[kv.Key] = Mathf.CeilToInt(kv.Value*b.Level*1.5f);
            return cost;
        }
        public bool CanUpgrade(BuildingEntity b)
        {
            if (b == null || b.Level >= MaxLevel) return false;
            return S.CanAfford(UpgradeCost(b));
        }
        public bool Upgrade(BuildingEntity b)
        {
            if (!CanUpgrade(b)) return false;
            S.Pay(UpgradeCost(b));
            b.Level++;
            if (b.View != null && MeshFactory != null) { Object.Destroy(b.View); SpawnView(b); }
            GM.AddEvent("good","⬆️ "+b.Def.Name+" 升级到 "+b.Level+" 级");
            return true;
        }

        public void Demolish(BuildingEntity b)
        {
            S.Refund(b.Def.Cost, 0.5f);
            if (b.View != null) Object.Destroy(b.View);
            S.Buildings.Remove(b);
            GM.AddEvent("info","🧹 拆除"+b.Def.Name+"，返还50%资源");
        }

        /// <summary>被敌军/灾害摧毁：只移除实体与视图，绝不返还资源（区别于玩家主动拆除 Demolish 的 50% 返还）</summary>
        public void DestroyByEnemy(BuildingEntity b)
        {
            if (b==null) return;
            string nm=b.Def!=null?b.Def.Name:"建筑";
            if (b.View != null) Object.Destroy(b.View);
            S.Buildings.Remove(b);
            GM.AddEvent("bad","🔥 敌军摧毁了一座"+nm);
        }

        // ===== 老化 =====
        public override void OnYear(int year)
        {
            for (int i=S.Buildings.Count-1;i>=0;i--)
            {
                var b = S.Buildings[i];
                b.Age++;
                int startAge = 50 + b.Level*30;
                float decay = Mathf.Max(1, 5-b.Level);
                if (b.Age > startAge + Random.value*50)
                {
                    b.Hp -= decay;
                    if (b.Hp <= 0)
                    {
                        if (b.View!=null) Object.Destroy(b.View);
                        S.Buildings.RemoveAt(i);
                        GM.AddEvent("bad","一座"+b.Def.Name+"因年久失修倒塌");
                    }
                }
                // 农田四阶段
                if (b.Type=="farm")
                {
                    b.FarmStage = (b.FarmStage+1)%4;
                    MeshFactory?.UpdateFarmStage(b);
                }
            }
        }

        /// <summary>时代切换：全部建筑按新风格重建外观</summary>
        public void RefreshAllStyles()
        {
            foreach (var b in S.Buildings)
            {
                if (b.View == null) { SpawnView(b); continue; }
                Object.Destroy(b.View);
                SpawnView(b);
            }
        }

        // ===== AI自动建造 =====
        private float _autoTimer;
        public bool AutoBuildEnabled;
        public override void Tick(float dt)
        {
            if (!AutoBuildEnabled) return;
            _autoTimer -= dt;
            if (_autoTimer > 0) return;
            _autoTimer = 2f;
            AutoBuildOnce();
        }

        public void AutoBuildOnce()
        {
            // 优先补住房与食物，再按时代解锁补特色建筑
            string[] priority = { "hut","farm","lumbermill","mine","market","well","granary" };
            foreach (var type in priority)
            {
                if (GM.Def(type)==null) continue;
                if (FindAutoPosition(type, out var x, out var z) && PlaceBuilding(type,x,z)) return;
            }
        }

        public bool FindAutoPosition(string type, out float x, out float z)
        {
            x=0;z=0;
            float half = Terrain!=null?Terrain.ActiveHalf*0.92f:GameConstants.WorldSize*0.46f; // V6.3.7(真扩展) 只在当前活动疆域内自动选址
            for (int tries=0;tries<8;tries++)
            {
                float px=(Random.value-0.5f)*2f*half;
                float pz=(Random.value-0.5f)*2f*half;
                if (CanBuild(type,px,pz,out _)) { x=px;z=pz;return true; }
            }
            return false;
        }

        public int CountByType(string type) => S.CountBuilding(type);
    }
}
