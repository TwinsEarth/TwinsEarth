using System;
using System.Collections.Generic;
using UnityEngine;
using PixelToCivilization.Data;

namespace PixelToCivilization.Core
{
    /// <summary>
    /// 全局游戏状态 —— 1:1 对齐 v5.9.9 的 G 对象
    /// </summary>
    [System.Serializable]
    public class GameState
    {
        // ---- 运行 ----
        public bool Running, Paused;
        public float Speed = 1f;
        public int DebugLevel;                 // 0普通/1Debug/2密码解锁

        // ---- V6.1.9 加速冷冻（加速累计100游戏年→冷冻300现实秒，期间倍速封顶10，解冻后清零重算）----
        public float CryoAccumYears;           // 加速状态下已累计推进的游戏年数
        public bool CryoActive;                // 是否处于冷冻冷却中
        public float CryoRemainSec;            // 冷冻剩余现实秒

        // ---- 时间（游戏年份驱动）----
        public int Year = 1;
        public float Day;
        public int Era;
        public int DynastyIdx;

        // ---- 15种资源 ----
        public Dictionary<string, float> Res = ResourceDatabase.InitialResources();

        // ---- 人口/社会 ----
        public int Pop = GameConstants.StartPop;
        public int MaxPop = 100;
        public float Housing;
        public float Happiness = GameConstants.StartHappiness;
        // 年龄结构（百分比）
        public float Children = 25, Young = 35, Middle = 30, Old = 10;
        // 社会阶层（百分比）：slave/commoner/rich/noble
        public Dictionary<string, float> SocialClasses = new()
        { {"slave",50},{"commoner",35},{"rich",10},{"noble",5} };

        // ---- 实体 ----
        public List<BuildingEntity> Buildings = new();
        public List<AgentEntity> Agents = new();
        public List<TreeEntity> Trees = new();
        public List<ShipEntity> Ships = new();
        public List<ProjectileEntity> Projectiles = new();
        public List<ResourceNode> ResourceNodes = new();

        // ---- 选择/工具 ----
        public string SelectedBuildType;
        public BuildingEntity SelectedBuilding;
        public string Tool = "select";
        public string BuildCat = "居住";

        // ---- 科技/政策 ----
        public HashSet<string> ResearchedTechs = new();
        public string CurrentResearch;
        public float ResearchProgress;
        public HashSet<string> Policies = new();

        // ---- 朝代 ----
        public float DynastyMorale = 100;
        public float DynastyTimer;
        // 策划书·朝代生命周期：吏治腐败度(0-100)与明君/昏君
        public float Corruption;
        public bool MonarchWise = true;
        public string MonarchName = "禅让贤者";

        // ---- V6.1.3 多聚落 / 天下分合（分裂 3-7 国 ↔ 大一统王朝）----
        public List<NationEntity> Nations = new();
        public List<float> VillageX = new(), VillageZ = new();   // 全部聚落中心（含邻村），供分裂复国与存档
        public string WorldPhase = "split";                      // split=列国并立 / unify=大一统
        public int PhaseYearsLeft;                               // 距下一次天下变局的游戏年数
        public int PlayerNationId;

        // ---- 策划书·诸子百家/历史事件/胜利 ----
        public string Philosophy;                       // 当前学派 id（空=未择）
        public HashSet<string> FiredEvents = new();     // 已触发历史事件 id
        public bool SchoolFounded;                      // 科举已开（研究永久+15%）
        public string VictoryType;                      // 胜利类型

        // ---- 军事 ----
        public float MilSoldiers, MilCavalry, MilFirepower, MilDefense;
        public bool WarActive, NavyBattleActive;
        public List<string> BattleLog = new();
        public List<object> EnemyFactions = new();
        public List<FriendlyUnit> FriendlyUnits = new();   // V6.1.4 我方步骑机动部队（实体队）
        public int FactionAnnexTimer;                       // V6.1.4 群雄互伐结算计时（游戏年）

        // ---- V6.1.5 殖民 ----
        public List<Colony> Colonies = new();
        public bool ColonialAge;                            // 殖民时代已开启（大航海后）

        // ---- 大运河 ----
        public int CanalSegments;
        public float CanalBonus;
        public bool CanalAutoBuild;
        public float CanalBuildTimer;

        // ---- 潮汐 ----
        public float TideLevel, TidePhase;
        public bool TideHigh;

        // ---- V6.1.9(i) 月度潮汐 / 天气 / 洋流海风（运行态；读档后由各系统平滑重建）----
        public float MonthlyTide;          // 0~1，月度潮位：1-15 涨至 1，16-30 退回 0
        public bool MonthlyFlooding;       // true=涨潮半程(1-15)，false=退潮半程(16-30)
        public int DayOfMonth = 1;         // 1..30
        public int WeatherKind;            // WeatherSystem.WeatherType 整数
        public float WeatherTimer;         // 当前天气剩余现实秒
        public float WindDir;              // 海风/洋流主方向（弧度）
        public float WindStr = 0.6f;       // 风力 0~1.4
        public List<int> CanalCells = new();   // 已开凿运河格（索引 gz*Size+gx），V6.1.1 潮汐视觉与存档
        // V6.3.9 桥梁：桥面格索引（供车辆越水通行）+ 每座桥 5 整数 [gx0,gz0,gx1,gz1,tier]
        public HashSet<int> BridgeCells = new();
        public List<int> BridgeRuns = new();
        public List<CartEntity> Carts = new(); // 马车/运输车辆，V6.1.1 四级锚点车辆系统

        // ---- 海洋副本 ----
        public List<ShipEntity> OceanFleets = new();
        public List<string> OceanDiscovered = new();
        public bool OceanUnlocked;
        public Dictionary<string, float> OceanResources = new()
        { {"spice",0},{"cotton",0},{"gem",0},{"ivory",0},{"frankincense",0},{"coffee",0} };
        public List<object> OceanTradePosts = new();

        // ---- 电力/AI ----
        public float ElectricGrid, PowerCoverage, AiBonus;

        // ---- 太空副本 ----
        public float SpElevator, SpShips, SpDyson, SpLunar, SpMars; // 太空工程进度
        public bool SpaceUnlocked;
        public Dictionary<string, float> SpaceResources = new()
        { {"helium3",0},{"titanium",0},{"antimatter",0},{"darkenergy",0},{"solarCrystal",0} };
        public List<object> SpaceBases = new();
        // V6.1.6 探索副本网格（海洋海图 / 太空星图）
        public ExpeditionState OceanExp = new(){ MapType="ocean" };
        public ExpeditionState SpaceExp = new(){ MapType="space" };

        // ---- 地图 ----
        public string CurrentMap = "home"; // home/ocean/space
        public bool AgeOfSail;             // V6.1.3 公元1000年·大航海时代已开启（防重复触发）
        public bool AgeOfSpace;            // V6.1.3 公元2000年·宇宙大开发时代已开启
        public float WorldExpansion=1f;    // V6.1.3 地图自然延展倍率（每100年×1.1、每1000年×2）

        // ---- 胜负/日志 ----
        public bool Victory;
        public List<LogEntry> EventLog = new();

        // ---- V6.8.0 世界奇观 / 文明成就 ----
        public List<WonderRuntime> Wonders = new();   // 已建成奇观（唯一）
        public List<string> Achievements = new();     // "id|年份|文案"，只增不减
        public bool WonderAuto;                        // 九神自动援建开关（默认关）

        // ===== 便捷访问器 =====
        public float GetRes(string id) => Res.TryGetValue(id, out var v) ? v : 0f;
        public void AddRes(string id, float delta)
        {
            Res.TryGetValue(id, out var v);
            Res[id] = Mathf.Max(0, v + delta);
        }
        public bool CanAfford(Dictionary<string,int> cost)
        {
            if (cost == null) return true;
            foreach (var kv in cost)
                if (GetRes(kv.Key) < kv.Value) return false;
            return true;
        }
        public void Pay(Dictionary<string,int> cost)
        {
            if (cost == null) return;
            foreach (var kv in cost) AddRes(kv.Key, -kv.Value);
        }
        public void Refund(Dictionary<string,int> cost, float ratio = 0.5f)
        {
            if (cost == null) return;
            foreach (var kv in cost) AddRes(kv.Key, kv.Value * ratio);
        }

        public int CountBuilding(string type)
        {
            int n = 0;
            foreach (var b in Buildings) if (b.Type == type) n++;
            return n;
        }

        public void Reset()
        {
            Running = false; Paused = false; Speed = 1f; DebugLevel = 0;
            CryoAccumYears=0f; CryoActive=false; CryoRemainSec=0f; // V6.1.9 冷冻状态归零
            Year = 1; Day = 0; Era = 0; DynastyIdx = 0;
            Res = ResourceDatabase.InitialResources();
            Pop = GameConstants.StartPop; MaxPop = 100; Housing = 0; Happiness = 70;
            Children=25;Young=35;Middle=30;Old=10;
            SocialClasses = new(){ {"slave",50},{"commoner",35},{"rich",10},{"noble",5} };
            Buildings.Clear(); Agents.Clear(); Trees.Clear(); Ships.Clear(); Carts.Clear();
            Projectiles.Clear(); ResourceNodes.Clear();
            ResearchedTechs.Clear(); CurrentResearch=null; ResearchProgress=0; Policies.Clear();
            DynastyMorale=100; DynastyTimer=0;
            Corruption=0; MonarchWise=true; MonarchName="禅让贤者";
            Philosophy=null; FiredEvents.Clear(); SchoolFounded=false; VictoryType=null;
            MilSoldiers=MilCavalry=MilFirepower=MilDefense=0;
            WarActive=NavyBattleActive=false; BattleLog.Clear();
            // V6.1.4-6.1.6 新增状态清理（防重开残留）
            FriendlyUnits.Clear(); FactionAnnexTimer=0;
            Colonies.Clear(); ColonialAge=false;
            OceanExp=new ExpeditionState(){ MapType="ocean" };
            SpaceExp=new ExpeditionState(){ MapType="space" };
            CanalSegments=0;CanalBonus=0;CanalAutoBuild=false;CanalBuildTimer=0;CanalCells.Clear(); BridgeCells.Clear(); BridgeRuns.Clear();
            TideLevel=0;TidePhase=0;TideHigh=false;
            MonthlyTide=0;MonthlyFlooding=true;DayOfMonth=1;WeatherKind=0;WeatherTimer=0;WindDir=0;WindStr=0.6f;
            OceanFleets.Clear();OceanDiscovered.Clear();OceanUnlocked=false;
            // V6.1.3 修复重开残留：海洋/太空资源字典与贸易站/基地必须归零，避免新局继承上局副本资源
            foreach(var k in new List<string>(OceanResources.Keys)) OceanResources[k]=0;
            OceanTradePosts.Clear();
            ElectricGrid=PowerCoverage=AiBonus=0;
            SpElevator=SpShips=SpDyson=SpLunar=SpMars=0; SpaceUnlocked=false;
            foreach(var k in new List<string>(SpaceResources.Keys)) SpaceResources[k]=0;
            SpaceBases.Clear();
            CurrentMap="home"; Victory=false; EventLog.Clear();
            Wonders.Clear(); Achievements.Clear(); WonderAuto=false; // V6.8.0
            AgeOfSail=false; AgeOfSpace=false; WorldExpansion=1f;
            Nations.Clear(); VillageX.Clear(); VillageZ.Clear();
            WorldPhase="split"; PhaseYearsLeft=0; PlayerNationId=0;
        }
    }
}
