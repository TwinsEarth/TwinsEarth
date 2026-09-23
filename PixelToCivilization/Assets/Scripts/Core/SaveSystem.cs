using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PixelToCivilization.Core;
using PixelToCivilization.Data;
using PixelToCivilization.World;

namespace PixelToCivilization.Core
{
    /// <summary>可序列化存档数据（JsonUtility不支持Dictionary，用平行数组）。V6.1.2 对齐 v5.9.9 全量快照。</summary>
    [Serializable]
    public class SaveData
    {
        public string Version="7.0.2";
        public string SlotName="手动存档";
        public string DynastyName="";
        public int Year; public float Day; public int Era, DynastyIdx;
        public int Pop,MaxPop; public float Happiness, Housing, DynastyMorale;
        public float Speed; public int DebugLevel;
        public float Children,Young,Middle,Old;
        // 军事 / 研究 / 社会
        public float MilSoldiers,MilCavalry,MilFirepower,MilDefense;
        public bool WarActive,Victory; public string VictoryType;
        public float ResearchProgress; public string CurrentResearch;
        public string[] SocialKeys; public float[] SocialVals;
        public string[] ResKeys; public float[] ResVals;
        public string[] Techs; public string[] Policies;
        // 运河 / 潮汐 / 电力
        public int CanalSegments; public float CanalBonus,AiBonus,ElectricGrid,PowerCoverage;
        public bool CanalAutoBuild; public float CanalBuildTimer,TidePhase,TideLevel; public bool TideHigh; public int[] CanalCells;
        public int[] BridgeCells; public int[] BridgeRuns;   // V6.3.9 桥梁
    public float[] GrownLands;   // V6.5.4 运行时实时增陆（每块7浮点 cx,cz,br,kind,p1,p2,p3）
        // 太空
        public float SpElevator,SpShips,SpDyson,SpLunar,SpMars;
        // 海洋 / 太空副本
        public bool OceanUnlocked,SpaceUnlocked;
        public string[] OceanDiscovered;
        public string[] OceanResKeys,SpaceResKeys; public float[] OceanResVals,SpaceResVals;
        // 船只（我方）与车辆
        public int ShipCount; public string[] ShipType; public float[] ShipX,ShipZ; public int[] ShipLvl; public bool[] ShipMil;
        public int CartCount; public string[] CartType; public float[] CartX,CartZ; public int[] CartLvl;
        // 人口（位置+家园+阶层+职业）
        public int AgentCount; public float[] AgentX,AgentZ,AgentHX,AgentHZ; public string[] AgentClass,AgentJob; public int[] AgentAge,AgentSeed; // V7.0.2
        // 建筑
        public int BuildingCount;
        public string[] BType; public float[] BX,BZ; public int[] BLvl; public int[] BAge; public float[] BHp;
        // V6.1.3 全要素：地形种子（读档还原同一张大地图）/ 散树 / 地图延展 / 大航海·宇宙里程碑
        public int TerrainSeed;
        public int TreeCount; public float[] TreeX,TreeZ; public int[] TreeStage,TreeAge;
        public float WorldExpansion; public bool AgeOfSail,AgeOfSpace;
        // V6.1.3 多聚落 / 天下分合（JsonUtility 用平行数组）
        public string WorldPhase; public int PhaseYearsLeft,PlayerNationId,VilCount,NationCount;
        public float[] VilX,VilZ;
        public int[] NId; public string[] NName,NHex; public float[] NX,NZ; public int[] NPop; public bool[] NPlayer,NAlive;
        // V6.1.3 全要素补全：诸子百家学派 / 科举 / 吏治腐败与君主 / 已触发历史事件(防重复领奖) / 船员与船血量 / 农田阶段
        public string Philosophy, MonarchName;
        public bool SchoolFounded, MonarchWise;
        public float Corruption;
        public string[] FiredEvents;
        public int[] ShipCrew, BFarmStage;
        public float[] ShipHp;
        // V6.1.4 我方步骑机动部队（视图读档后由军事系统重建）
        public int[] FuKind; public float[] FuX,FuZ,FuHp;
        public int FactionAnnexTimer;
        // V6.1.5 殖民地
        public int ColCount; public string[] ColId,ColName,ColRes; public int[] ColLvl;
        public float[] ColPop,ColLoy,ColX,ColZ; public bool ColonialAge;
        // V6.1.6 海洋/太空探索副本网格（嵌套可序列化，含迷雾/节点/位置/战力补给）
        public ExpeditionState OceanExpData, SpaceExpData;
        // V6.1.8 九智能体共治（开关/模式/Token/议政节奏/兜底次数/各神运行态）
        public bool AIEnabled=true, AIOnline; public long AITokens;
        public int AIInterval,AILastCouncil,AISafety; public string AIApiKey,AIModel;
        public int[] AIGodLast; public long[] AIGodAct;
        // V6.1.9 加速冷冻运行态（累计年数/是否冷冻/剩余现实秒）
        public float CryoAccum,CryoRemain; public bool CryoActive;
        // V6.8.0 世界奇观 / 文明成就（平行数组，视图读档后由 WonderSystem 自愈重建）
        public int WonderCount; public string[] WId; public int[] WYear; public float[] WX,WZ;
        public string[] Achievements; public bool WonderAuto;
        public long SaveTime;
    }

    /// <summary>槽位摘要（供存档列表渲染，不反序列化全部）</summary>
    public class SlotSummary
    {
        public int Slot; public bool Exists,Damaged;
        public string Name,Dynasty; public int Year,BuildingCount; public long Time;
    }

    /// <summary>
    /// 存档系统 —— V6.1.2 对齐 v5.9.9：自动槽(auto,每5分钟现实时间)+5 个手动槽，
    /// 每槽可覆盖/读档/删除，列表显示年份·朝代·建筑数·时间；支持 JSON 导出/导入，全量快照。
    /// 槽位编号：0=自动槽，1..5=手动槽。
    /// </summary>
    public class SaveSystem : MonoBehaviour
    {
        private GameManager _gm;
        private float _autoTimer;
        public const float AutoSaveInterval = 300f;   // v5.9.9：现实 5 分钟
        public const int ManualSlots = 5;
        public float AutoCountdown => Mathf.Max(0f, AutoSaveInterval-_autoTimer);

        public void Init(GameManager gm){ _gm=gm; }

        private static string Key(int slot)=>"PxC_Save_"+(slot==0?"auto":slot.ToString());

        private SaveData Snapshot()
        {
            var s=_gm.State;
            var _gt=UnityEngine.Object.FindObjectOfType<WorldGenerator>();
            var _grown=new List<float>();
            if(_gt!=null) foreach(var L in _gt.GrownLands){_grown.Add(L.Cx);_grown.Add(L.Cz);_grown.Add(L.Br);_grown.Add(L.Kind);_grown.Add(L.P1);_grown.Add(L.P2);_grown.Add(L.P3);}
            float[] grownArr=_grown.ToArray();
            var d=new SaveData
            {
                Year=s.Year,Day=s.Day,Era=s.Era,DynastyIdx=s.DynastyIdx,
                DynastyName=_gm.Time!=null?_gm.Time.DynastyName:"",
                Pop=s.Pop,MaxPop=s.MaxPop,Happiness=s.Happiness,
                Housing=s.Housing,DynastyMorale=s.DynastyMorale,Speed=s.Speed,DebugLevel=s.DebugLevel,
                Children=s.Children,Young=s.Young,Middle=s.Middle,Old=s.Old,
                MilSoldiers=s.MilSoldiers,MilCavalry=s.MilCavalry,MilFirepower=s.MilFirepower,MilDefense=s.MilDefense,
                WarActive=s.WarActive,Victory=s.Victory,VictoryType=s.VictoryType,
                ResearchProgress=s.ResearchProgress,CurrentResearch=s.CurrentResearch,
                SocialKeys=s.SocialClasses.Keys.ToArray(),SocialVals=s.SocialClasses.Values.ToArray(),
                ResKeys=s.Res.Keys.ToArray(),ResVals=s.Res.Values.ToArray(),
                Techs=s.ResearchedTechs.ToArray(),Policies=s.Policies.ToArray(),
                CanalSegments=s.CanalSegments,CanalBonus=s.CanalBonus,AiBonus=s.AiBonus,
                ElectricGrid=s.ElectricGrid,PowerCoverage=s.PowerCoverage,
                SpElevator=s.SpElevator,SpShips=s.SpShips,SpDyson=s.SpDyson,SpLunar=s.SpLunar,SpMars=s.SpMars,
                OceanUnlocked=s.OceanUnlocked,SpaceUnlocked=s.SpaceUnlocked,
                OceanDiscovered=s.OceanDiscovered.ToArray(),
                CanalAutoBuild=s.CanalAutoBuild,CanalBuildTimer=s.CanalBuildTimer,TidePhase=s.TidePhase,
                TideLevel=s.TideLevel,TideHigh=s.TideHigh,
                CanalCells=s.CanalCells.ToArray(),
            BridgeCells=s.BridgeCells.ToArray(), BridgeRuns=s.BridgeRuns.ToArray(),
            GrownLands=grownArr,
                OceanResKeys=s.OceanResources.Keys.ToArray(),OceanResVals=s.OceanResources.Values.ToArray(),
                SpaceResKeys=s.SpaceResources.Keys.ToArray(),SpaceResVals=s.SpaceResources.Values.ToArray(),
                ShipCount=s.Ships.Count,
                ShipType=s.Ships.Select(p=>p.ShipTypeId).ToArray(),
                ShipX=s.Ships.Select(p=>p.X).ToArray(),ShipZ=s.Ships.Select(p=>p.Z).ToArray(),
                ShipLvl=s.Ships.Select(p=>p.Level).ToArray(),ShipMil=s.Ships.Select(p=>p.Military).ToArray(),
                ShipCrew=s.Ships.Select(p=>p.Crew).ToArray(),ShipHp=s.Ships.Select(p=>p.Hp).ToArray(),
                CartCount=s.Carts.Count,
                CartType=s.Carts.Select(c=>c.CartTypeId).ToArray(),
                CartX=s.Carts.Select(c=>c.X).ToArray(),CartZ=s.Carts.Select(c=>c.Z).ToArray(),
                CartLvl=s.Carts.Select(c=>c.Level).ToArray(),
                AgentCount=s.Agents.Count,
                AgentX=s.Agents.Select(a=>a.X).ToArray(),AgentZ=s.Agents.Select(a=>a.Z).ToArray(),
                AgentHX=s.Agents.Select(a=>a.HomeX).ToArray(),AgentHZ=s.Agents.Select(a=>a.HomeZ).ToArray(),
                AgentClass=s.Agents.Select(a=>a.SocialClass).ToArray(),AgentJob=s.Agents.Select(a=>a.Job).ToArray(),
                AgentAge=s.Agents.Select(a=>a.Age).ToArray(),AgentSeed=s.Agents.Select(a=>a.ColorSeed).ToArray(),
                BuildingCount=s.Buildings.Count,
                AgeOfSail=s.AgeOfSail,AgeOfSpace=s.AgeOfSpace,WorldExpansion=s.WorldExpansion,
                // V6.1.3 全要素补全
                Philosophy=s.Philosophy,SchoolFounded=s.SchoolFounded,Corruption=s.Corruption,
                MonarchWise=s.MonarchWise,MonarchName=s.MonarchName,
                FiredEvents=s.FiredEvents.ToArray(),
                SaveTime=DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            // V6.1.3 地形种子（保证读档回到同一张大地图）与玩家散树
            var ter=UnityEngine.Object.FindObjectOfType<PixelToCivilization.World.WorldGenerator>();
            d.TerrainSeed=ter!=null?ter.Seed:0;
            d.TreeCount=s.Trees.Count;
            d.TreeX=s.Trees.Select(t=>t.X).ToArray(); d.TreeZ=s.Trees.Select(t=>t.Z).ToArray();
            d.TreeStage=s.Trees.Select(t=>t.Stage).ToArray(); d.TreeAge=s.Trees.Select(t=>t.Age).ToArray();
            d.BType=s.Buildings.Select(b=>b.Type).ToArray();
            d.BX=s.Buildings.Select(b=>b.X).ToArray();
            d.BZ=s.Buildings.Select(b=>b.Z).ToArray();
            d.BLvl=s.Buildings.Select(b=>b.Level).ToArray();
            d.BAge=s.Buildings.Select(b=>b.Age).ToArray();
            d.BHp=s.Buildings.Select(b=>b.Hp).ToArray();
            d.BFarmStage=s.Buildings.Select(b=>b.FarmStage).ToArray();
            // V6.1.3 聚落中心 + 国家/天下分合
            d.WorldPhase=s.WorldPhase;d.PhaseYearsLeft=s.PhaseYearsLeft;d.PlayerNationId=s.PlayerNationId;
            d.VilCount=s.VillageX.Count; d.VilX=s.VillageX.ToArray(); d.VilZ=s.VillageZ.ToArray();
            d.NationCount=s.Nations.Count;
            d.NId=s.Nations.Select(n=>n.Id).ToArray();
            d.NName=s.Nations.Select(n=>n.Name).ToArray();
            d.NHex=s.Nations.Select(n=>n.ColorHex).ToArray();
            d.NX=s.Nations.Select(n=>n.Cx).ToArray(); d.NZ=s.Nations.Select(n=>n.Cz).ToArray();
            d.NPop=s.Nations.Select(n=>n.Pop).ToArray();
            d.NPlayer=s.Nations.Select(n=>n.IsPlayer).ToArray();
            d.NAlive=s.Nations.Select(n=>n.Alive).ToArray();
            // V6.1.4 我方步骑部队
            d.FuKind=s.FriendlyUnits.Select(u=>u.Kind).ToArray();
            d.FuX=s.FriendlyUnits.Select(u=>u.X).ToArray();
            d.FuZ=s.FriendlyUnits.Select(u=>u.Z).ToArray();
            d.FuHp=s.FriendlyUnits.Select(u=>u.Hp).ToArray();
            d.FactionAnnexTimer=s.FactionAnnexTimer;
            // V6.1.5 殖民地
            d.ColCount=s.Colonies.Count;
            d.ColId=s.Colonies.Select(c=>c.Id).ToArray();
            d.ColName=s.Colonies.Select(c=>c.Name).ToArray();
            d.ColRes=s.Colonies.Select(c=>c.ResId).ToArray();
            d.ColLvl=s.Colonies.Select(c=>c.Level).ToArray();
            d.ColPop=s.Colonies.Select(c=>c.Pop).ToArray();
            d.ColLoy=s.Colonies.Select(c=>c.Loyalty).ToArray();
            d.ColX=s.Colonies.Select(c=>c.X).ToArray();
            d.ColZ=s.Colonies.Select(c=>c.Z).ToArray();
            d.ColonialAge=s.ColonialAge;
            // V6.1.6 副本网格
            d.OceanExpData=CloneExp(s.OceanExp); d.SpaceExpData=CloneExp(s.SpaceExp);
            // V6.1.8 九智能体共治运行态
            var cou=_gm.Council;
            if(cou!=null){
                d.AIEnabled=cou.Enabled;d.AIOnline=cou.Online;d.AITokens=cou.TokensUsed;
                d.AIInterval=cou.IntervalYears;d.AILastCouncil=cou.LastCouncilYear;d.AISafety=cou.SafetyCount;
                d.AIApiKey=cou.ApiKey;d.AIModel=cou.Model;
                d.AIGodLast=cou.Gods.Select(g=>g.LastYear).ToArray();
                d.AIGodAct=cou.Gods.Select(g=>g.Actions).ToArray();
            }
            // V6.1.9 加速冷冻运行态
            d.CryoAccum=s.CryoAccumYears; d.CryoActive=s.CryoActive; d.CryoRemain=s.CryoRemainSec;
            // V6.8.0 奇观 / 成就
            d.WonderCount=s.Wonders.Count;
            d.WId=s.Wonders.Select(w=>w.Id).ToArray();
            d.WYear=s.Wonders.Select(w=>w.BuiltYear).ToArray();
            d.WX=s.Wonders.Select(w=>w.X).ToArray();
            d.WZ=s.Wonders.Select(w=>w.Z).ToArray();
            d.Achievements=s.Achievements.ToArray(); d.WonderAuto=s.WonderAuto;
            return d;
        }

        /// <summary>深拷贝副本状态用于存档（剥离运行时，只留数据）</summary>
        private static ExpeditionState CloneExp(ExpeditionState e)
        {
            if (e==null||!e.Inited) return null;
            return new ExpeditionState
            {
                MapType=e.MapType,N=e.N,Inited=e.Inited,Seed=e.Seed,PosX=e.PosX,PosY=e.PosY,
                Power=e.Power,MaxPower=e.MaxPower,Supply=e.Supply,
                Seen=e.Seen?.ToArray(),NodeKind=e.NodeKind?.ToArray(),NodeUsed=e.NodeUsed?.ToArray(),
                LastEvent=e.LastEvent
            };
        }

        // ---------- 槽位读写 ----------
        public void SaveToSlot(int slot)
        {
            try
            {
                var data=Snapshot();
                data.SlotName = slot==0?"自动存档":"存档"+slot;
                PlayerPrefs.SetString(Key(slot),JsonUtility.ToJson(data));PlayerPrefs.Save();
                _gm.AddEvent("good",(slot==0?"🤖 自动":"💾 已")+"保存到"+(slot==0?"自动槽":"存档位 "+slot));
            }
            catch(Exception e){ Debug.LogError(e);_gm.AddEvent("bad","存档失败："+e.Message); }
        }
        public bool HasSlot(int slot)=>PlayerPrefs.HasKey(Key(slot));

        /// <summary>返回 0(自动)..5(手动) 中存档时间最新的非空槽位，损坏槽跳过；没有任何有效存档返回 -1</summary>
        public int LatestSlot()
        {
            int best=-1; long bestTime=long.MinValue;
            for(int slot=0;slot<=ManualSlots;slot++)
            {
                if(!HasSlot(slot)) continue;
                try
                {
                    var d=JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(Key(slot)));
                    if(d!=null && d.SaveTime>=bestTime){bestTime=d.SaveTime;best=slot;}
                }
                catch{ /* 损坏槽忽略，继续找下一个 */ }
            }
            return best;
        }
        public void DeleteSlot(int slot)
        {
            if(slot==0) return;                 // 自动槽不允许删除
            PlayerPrefs.DeleteKey(Key(slot));PlayerPrefs.Save();
            _gm.AddEvent("info","已删除存档位 "+slot);
        }
        public bool LoadFromSlot(int slot)
        {
            if(!HasSlot(slot)){ _gm.AddEvent("bad",(slot==0?"自动槽":"存档位 "+slot)+" 为空");return false; }
            try
            {
                var d=JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(Key(slot)));
                Apply(d);
                _gm.AddEvent("good","📂 已读取"+(slot==0?"自动存档":"存档位 "+slot));
                return true;
            }
            catch(Exception e){ _gm.AddEvent("bad","读档失败："+e.Message);return false; }
        }

        /// <summary>读取槽位摘要（列表用，损坏也能识别）</summary>
        public SlotSummary Summarize(int slot)
        {
            var sum=new SlotSummary{Slot=slot,Exists=HasSlot(slot)};
            if(!sum.Exists) return sum;
            try
            {
                var d=JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(Key(slot)));
                sum.Year=d.Year;sum.Dynasty=d.DynastyName;sum.BuildingCount=d.BuildingCount;
                sum.Time=d.SaveTime;sum.Name=slot==0?"🤖 自动存档":"💾 存档"+slot;
            }
            catch{ sum.Damaged=true; }
            return sum;
        }

        private void Apply(SaveData d)
        {
            var s=_gm.State;
            // 0) V6.1.3 按存档地形种子还原同一张大地图（植被/村址/相机/小地图同步），保证建筑与单位坐标不漂移
            var ter=UnityEngine.Object.FindObjectOfType<PixelToCivilization.World.WorldGenerator>();
            if(ter!=null && d.TerrainSeed!=0 && ter.Seed!=d.TerrainSeed)
            {
                var village=ter.Regenerate(d.TerrainSeed);
                var veg=UnityEngine.Object.FindObjectOfType<PixelToCivilization.World.VegetationSystem>();
                veg?.Regrow(ter,d.TerrainSeed);
                var rig=UnityEngine.Object.FindObjectOfType<PixelToCivilization.World.CameraRig>();
                rig?.Retarget(village);
                PixelToCivilization.UI.UIManager.Instance?.InvalidateMinimapBase();
            }
            // 1) 清空现有实体视图与数据
            // V6.1.3 先清掉上一局/随机局残留的聚落装饰节点（道路/旗帜/码头；祭坛棚屋属建筑视图，随后按存档重建）
            foreach(Transform child in _gm.transform)
                if (child.name.StartsWith("Settlement")) Destroy(child.gameObject);
            foreach(var b in s.Buildings) if(b.View)Destroy(b.View);
            s.Buildings.Clear();
            foreach(var old in s.Ships) if(old.View)Destroy(old.View);
            s.Ships.Clear();
            foreach(var old in s.Carts) if(old.View)Destroy(old.View);
            s.Carts.Clear();
            foreach(var a in s.Agents) if(a.View)Destroy(a.View);
            s.Agents.Clear();
            // V6.1.3 清空散树（视图+数据），随后按存档重建
            _gm.Env?.ClearTrees();
            // 2) 标量状态
            s.Year=d.Year;s.Day=d.Day;s.Era=d.Era;s.DynastyIdx=d.DynastyIdx;
            s.Pop=d.Pop;s.MaxPop=Mathf.Max(100,d.MaxPop);s.Happiness=d.Happiness;
            s.Housing=d.Housing;s.DynastyMorale=d.DynastyMorale;s.Speed=d.Speed;s.DebugLevel=d.DebugLevel;
            s.Children=d.Children;s.Young=d.Young;s.Middle=d.Middle;s.Old=d.Old;
            s.MilSoldiers=d.MilSoldiers;s.MilCavalry=d.MilCavalry;s.MilFirepower=d.MilFirepower;s.MilDefense=d.MilDefense;
            s.WarActive=d.WarActive;s.Victory=d.Victory;s.VictoryType=d.VictoryType;
            s.ResearchProgress=d.ResearchProgress;s.CurrentResearch=d.CurrentResearch;
            // 3) 字典 / 集合
            s.Res.Clear();
            if(d.ResKeys!=null)for(int i=0;i<d.ResKeys.Length;i++) s.Res[d.ResKeys[i]]=d.ResVals[i];
            s.ResearchedTechs=new HashSet<string>(d.Techs??Array.Empty<string>());
            s.Policies=new HashSet<string>(d.Policies??Array.Empty<string>());
            s.SocialClasses.Clear();
            if(d.SocialKeys!=null)for(int i=0;i<d.SocialKeys.Length;i++) s.SocialClasses[d.SocialKeys[i]]=d.SocialVals[i];
            s.CanalSegments=d.CanalSegments;s.CanalBonus=d.CanalBonus;s.AiBonus=d.AiBonus;
            s.ElectricGrid=d.ElectricGrid;s.PowerCoverage=d.PowerCoverage;
            s.SpElevator=d.SpElevator;s.SpShips=d.SpShips;s.SpDyson=d.SpDyson;s.SpLunar=d.SpLunar;s.SpMars=d.SpMars;
            s.OceanUnlocked=d.OceanUnlocked;s.SpaceUnlocked=d.SpaceUnlocked;
            s.OceanDiscovered=new List<string>(d.OceanDiscovered??Array.Empty<string>());
            RestoreDict(s.OceanResources,d.OceanResKeys,d.OceanResVals);
            RestoreDict(s.SpaceResources,d.SpaceResKeys,d.SpaceResVals);
            s.CanalAutoBuild=d.CanalAutoBuild;s.CanalBuildTimer=d.CanalBuildTimer;s.TidePhase=d.TidePhase;
            s.TideLevel=d.TideLevel;s.TideHigh=d.TideHigh;
            s.CanalCells=new List<int>(d.CanalCells??Array.Empty<int>());
            s.BridgeCells=new HashSet<int>(d.BridgeCells??Array.Empty<int>()); s.BridgeRuns=new List<int>(d.BridgeRuns??Array.Empty<int>());
            // V6.1.3 地图延展 + 大航海/宇宙里程碑
            s.AgeOfSail=d.AgeOfSail;s.AgeOfSpace=d.AgeOfSpace;s.WorldExpansion=Mathf.Max(1f,d.WorldExpansion);
            ter?.SnapExpansion(s.WorldExpansion);
            // V6.1.3 全要素补全：学派 / 科举 / 吏治 / 君主 / 已触发历史事件（防读档后重复发奖）
            s.Philosophy=d.Philosophy; s.SchoolFounded=d.SchoolFounded; s.Corruption=d.Corruption;
            s.MonarchWise=d.MonarchWise; s.MonarchName=string.IsNullOrEmpty(d.MonarchName)?"禅让贤者":d.MonarchName;
            s.FiredEvents=new HashSet<string>(d.FiredEvents??Array.Empty<string>());
            // 4) 重建建筑
            for(int i=0;i<d.BuildingCount;i++)
            {
                var def=_gm.Def(d.BType[i]);
                if(def==null)continue;
                int fs=(d.BFarmStage!=null&&i<d.BFarmStage.Length)?d.BFarmStage[i]:0;
                var b=new BuildingEntity{Type=d.BType[i],Def=def,X=d.BX[i],Z=d.BZ[i],Level=d.BLvl[i],Age=d.BAge[i],Hp=d.BHp[i],FarmStage=fs};
                s.Buildings.Add(b); _gm.Building.SpawnView(b);
            }
            // 5) 重建船只 / 车辆
            if(d.ShipType!=null)
                for(int i=0;i<d.ShipType.Length;i++)
                {
                    var sh=_gm.Naval.RestoreShip(d.ShipType[i],d.ShipX[i],d.ShipZ[i],d.ShipLvl[i],d.ShipMil[i]);
                    if(sh!=null)
                    {   // 回填船员（否则 SpeedOf=0 船不动、攻击加成丢失）与存档血量
                        if(d.ShipCrew!=null&&i<d.ShipCrew.Length) sh.Crew=d.ShipCrew[i];
                        if(d.ShipHp!=null&&i<d.ShipHp.Length&&d.ShipHp[i]>0) sh.Hp=d.ShipHp[i];
                    }
                }
            if(d.CartType!=null)
                for(int i=0;i<d.CartType.Length;i++){
                    int clv=(d.CartLvl!=null&&i<d.CartLvl.Length)?d.CartLvl[i]:1;
                    _gm.Cart.SpawnCart(d.CartType[i],d.CartX[i],d.CartZ[i],clv);
                }
            // 6) 重建人口（按存档阶层/职业）
            if(d.AgentX!=null && _gm.Env!=null)
                for(int i=0;i<d.AgentX.Length;i++)
                {
                    string cls=d.AgentClass!=null&&i<d.AgentClass.Length?d.AgentClass[i]:"commoner";
                    string job=d.AgentJob!=null&&i<d.AgentJob.Length?d.AgentJob[i]:"idle";
                    int age=d.AgentAge!=null&&i<d.AgentAge.Length?d.AgentAge[i]:-1;
                    int seed=d.AgentSeed!=null&&i<d.AgentSeed.Length?d.AgentSeed[i]:0;
                    _gm.Env.SpawnAgent(d.AgentX[i],d.AgentZ[i],d.AgentHX[i],d.AgentHZ[i],cls,job,age,seed);
                }
            // 7) 运河视觉
            _gm.Canal.RebuildViews();
            _gm.Bridge?.RebuildViews();
            // V6.5.4 恢复运行时实时增陆（地形按种子重建后补盖戳）
            if(d.GrownLands!=null){
                var tg=UnityEngine.Object.FindObjectOfType<WorldGenerator>();
                if(tg!=null){var rr=new System.Random(d.TerrainSeed+777);
                    for(int gi=0;gi+6<d.GrownLands.Length;gi+=7)
                        tg.RestoreGrownLand(d.GrownLands[gi],d.GrownLands[gi+1],d.GrownLands[gi+2],(int)d.GrownLands[gi+3],d.GrownLands[gi+4],d.GrownLands[gi+5],d.GrownLands[gi+6],rr);
                    tg.EndRestoreGrown();}}
            // 8) V6.1.3 重建玩家散树 + 恢复飞鸟/鱼群（纯视觉）
            if(d.TreeX!=null && _gm.Env!=null)
                for(int i=0;i<d.TreeX.Length;i++)
                {
                    int st=(d.TreeStage!=null&&i<d.TreeStage.Length)?d.TreeStage[i]:1;
                    _gm.Env.SpawnTree(d.TreeX[i],d.TreeZ[i],st);
                }
            _gm.Env?.ReinitWildlife();
            // 9) V6.1.3 聚落中心 + 国家/天下分合恢复
            s.VillageX.Clear(); s.VillageZ.Clear();
            if (d.VilX!=null) for(int i=0;i<d.VilX.Length;i++){ s.VillageX.Add(d.VilX[i]); s.VillageZ.Add(d.VilZ[i]); }
            s.Nations.Clear();
            if (d.NId!=null)
                for(int i=0;i<d.NId.Length;i++)
                {
                    var n=new NationEntity
                    {
                        Id=d.NId[i],
                        Name=(d.NName!=null&&i<d.NName.Length)?d.NName[i]:"方国",
                        ColorHex=(d.NHex!=null&&i<d.NHex.Length)?d.NHex[i]:"888888",
                        Cx=d.NX[i],Cz=d.NZ[i],
                        Pop=(d.NPop!=null&&i<d.NPop.Length)?d.NPop[i]:0,
                        IsPlayer=d.NPlayer!=null&&i<d.NPlayer.Length&&d.NPlayer[i],
                        Alive=d.NAlive==null||i>=d.NAlive.Length||d.NAlive[i],
                        // V6.1.7 大陆归属由重建后的地形现算（地形按同种子确定性重生成）
                        ContinentId=ter!=null?Mathf.Max(1,ter.ContinentAt(d.NX[i],d.NZ[i])):1,
                    };
                    n.Power=n.Pop; s.Nations.Add(n);
                }
            s.WorldPhase=string.IsNullOrEmpty(d.WorldPhase)?"split":d.WorldPhase;
            s.PhaseYearsLeft=d.PhaseYearsLeft; s.PlayerNationId=d.PlayerNationId;
            // V6.1.4 恢复我方步骑部队（数据；视图由 MilitarySystem.Tick 的 RebuildAndPumpUnits 重建）
            s.FriendlyUnits.Clear();
            if(d.FuKind!=null)
                for(int i=0;i<d.FuKind.Length;i++)
                {
                    bool cav=d.FuKind[i]==1;
                    float x=d.FuX!=null&&i<d.FuX.Length?d.FuX[i]:0, z=d.FuZ!=null&&i<d.FuZ.Length?d.FuZ[i]:0;
                    s.FriendlyUnits.Add(new FriendlyUnit{
                        Kind=d.FuKind[i],X=x,Z=z,HomeX=x,HomeZ=z,State=0,
                        Hp=d.FuHp!=null&&i<d.FuHp.Length&&d.FuHp[i]>0?d.FuHp[i]:(cav?60:50),MaxHp=cav?60:50,
                        Attack=cav?9:6,Speed=cav?3.2f:1.6f});
                }
            s.FactionAnnexTimer=d.FactionAnnexTimer;
            // V6.1.5 恢复殖民地
            s.Colonies.Clear();
            if(d.ColId!=null)
                for(int i=0;i<d.ColId.Length;i++)
                    s.Colonies.Add(new Colony{
                        Id=d.ColId[i],
                        Name=d.ColName!=null&&i<d.ColName.Length?d.ColName[i]:"殖民地",
                        ResId=d.ColRes!=null&&i<d.ColRes.Length?d.ColRes[i]:"spice",
                        Level=d.ColLvl!=null&&i<d.ColLvl.Length?d.ColLvl[i]:1,
                        Pop=d.ColPop!=null&&i<d.ColPop.Length?d.ColPop[i]:10,
                        Loyalty=d.ColLoy!=null&&i<d.ColLoy.Length?d.ColLoy[i]:100,
                        X=d.ColX!=null&&i<d.ColX.Length?d.ColX[i]:0,
                        Z=d.ColZ!=null&&i<d.ColZ.Length?d.ColZ[i]:0});
            s.ColonialAge=d.ColonialAge;
            // V6.1.6 恢复探索副本网格（旧存档无则给空白新图）
            s.OceanExp=d.OceanExpData??new ExpeditionState(){MapType="ocean"};
            s.SpaceExp=d.SpaceExpData??new ExpeditionState(){MapType="space"};
            // V6.1.8 恢复九智能体共治运行态（旧存档无则保持默认开启·离线）
            if(_gm.Council!=null){
                var c=_gm.Council;
                c.Enabled=d.AIEnabled; c.SetOnline(d.AIOnline); c.TokensUsed=d.AITokens;
                c.IntervalYears=d.AIInterval<=0?3:d.AIInterval; c.LastCouncilYear=d.AILastCouncil; c.SafetyCount=d.AISafety;
                if(!string.IsNullOrEmpty(d.AIApiKey))c.ApiKey=d.AIApiKey;
                // V6.1.9：旧档写死的失效模型(seed-1-6-250615)自动升级为实测可用模型并开启联网；其余尊重存档选择
                bool staleModel=string.IsNullOrEmpty(d.AIModel)||d.AIModel=="doubao-seed-1-6-250615";
                if(staleModel){ c.Model="deepseek-v4-flash-ga-260731"; c.SetOnline(true); }
                else c.Model=d.AIModel;
                if(d.AIGodLast!=null)for(int i=0;i<Mathf.Min(d.AIGodLast.Length,c.Gods.Count);i++)c.Gods[i].LastYear=d.AIGodLast[i];
                if(d.AIGodAct!=null)for(int i=0;i<Mathf.Min(d.AIGodAct.Length,c.Gods.Count);i++)c.Gods[i].Actions=d.AIGodAct[i];
            }
            // V6.1.9 恢复加速冷冻运行态（旧存档无字段则默认不冷冻、累计0）
            s.CryoAccumYears=d.CryoAccum; s.CryoActive=d.CryoActive; s.CryoRemainSec=Mathf.Max(0,d.CryoRemain);
            // V6.8.0 恢复奇观 / 成就（旧档无字段给空，不报错；视图由 WonderSystem.Tick 自愈）
            s.Wonders.Clear();
            if(d.WId!=null)
                for(int i=0;i<d.WId.Length;i++)
                    s.Wonders.Add(new WonderRuntime{
                        Id=d.WId[i],
                        BuiltYear=d.WYear!=null&&i<d.WYear.Length?d.WYear[i]:0,
                        X=d.WX!=null&&i<d.WX.Length?d.WX[i]:0,
                        Z=d.WZ!=null&&i<d.WZ.Length?d.WZ[i]:0});
            s.Achievements=new System.Collections.Generic.List<string>(d.Achievements??System.Array.Empty<string>());
            s.WonderAuto=d.WonderAuto;
            s.Running=true;
        }

        private static void RestoreDict(Dictionary<string,float> target,string[] keys,float[] vals)
        {
            target.Clear();
            if(keys==null||vals==null)return;
            int n=Mathf.Min(keys.Length,vals.Length);
            for(int i=0;i<n;i++)target[keys[i]]=vals[i];
        }

        // ---------- 导出 / 导入 ----------
        public string Export()=>JsonUtility.ToJson(Snapshot(),true);
        public bool Import(string json)
        {
            try{ Apply(JsonUtility.FromJson<SaveData>(json));_gm.AddEvent("good","📥 存档导入成功");return true; }
            catch(Exception e){_gm.AddEvent("bad","导入失败："+e.Message);return false;}
        }

        private void Update()
        {
            if(_gm==null||_gm.State==null||!_gm.State.Running||_gm.State.Paused)return;
            _autoTimer+=Time.unscaledDeltaTime;   // 现实时间计时，不受倍速影响
            if(_autoTimer>=AutoSaveInterval){_autoTimer=0;SaveToSlot(0);}
        }
    }
}
