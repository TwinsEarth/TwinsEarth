using System.Collections.Generic;
using UnityEngine;
using PixelToCivilization.Data;

namespace PixelToCivilization.Core
{
    /// <summary>建筑运行时实例（对应 v5.9.9 的 b 对象）</summary>
    [System.Serializable]
    public class BuildingEntity
    {
        public string Type;
        public BuildingDefinition Def;
        public float X, Z;
        public int Level = 1;
        public float Hp = 100f;
        public int Age;
        public int FarmStage;            // 农田0耕地/1幼苗/2生长/3成熟
        public float AttackCooldown;     // 攻击型建筑冷却
        public GameObject View;          // 场景物体
        public string MapId = "home";    // home/ocean/space

        public float LevelMult => 1f + (Level - 1) * 0.5f; // 1级100% 2级150% 3级200%
        public Vector3 Pos => new(X, 0, Z);
    }

    /// <summary>人口个体（对应 agent）</summary>
    [System.Serializable]
    public class AgentEntity
    {
        public float X, Z, Vx, Vz;
        public float HomeX, HomeZ;      // 家园锚点：围绕所属村落小范围活动
        public int Age;                 // 岁
        public int LifeStage = 1;       // V7.0.2 年龄阶段：0幼年/1壮年/2老年
        public int LifeSpan = 72;       // V7.0.2 寿命（60-88随机），寿尽轮回为孩童
        public int ColorSeed = 1;       // V7.0.2 个体稳定颜色种子（肤色/发色/衣色微调/饰色）
        public int ViewSig = -1;       // V7.0.2 当前外观签名(阶段*100+时代)，用于跳年后按需重建
        public string SocialClass = "commoner"; // slave/commoner/rich/noble
        public string Job = "idle";     // 职业：farmer/woodcutter/miner/worker/soldier/merchant/idle
        public GameObject View;
        public float WanderTimer;
        public bool Boarded;            // V6.3.7 是否已登乘车船（登乘期间停止陆地游走、视图隐藏）
        public PixelToCivilization.Actors.HumanoidAnimator Anim; // 运行时人形动画器（不存档，懒加载，读档视图重建后自动重取）
        public Vector3 Pos => new(X, 0, Z);
    }

    /// <summary>树木</summary>
    [System.Serializable]
    public class TreeEntity
    {
        public float X, Z;
        public int Age;
        public int Stage;               // 0幼苗/1成长/2成熟
        public GameObject Trunk, Leaves;
    }

    /// <summary>战船/商船（对应 ship）</summary>
    [System.Serializable]
    public class ShipEntity
    {
        public string ShipTypeId = "small_boat";
        public string Name = "战船";
        public string Side = "ours";    // ours/enemy
        public float X, Z, Vx, Vz;
        public float HomeX, HomeZ;      // 民用船巡游锚点
        public int Level = 1;
        public int Capacity = 1, Crew;
        public int Passengers;            // V6.3.7 Lv2+ 自动登乘的平民载客（居住），战船作战载人走 Crew
        public int Housing;               // 基础居住人数（造船时由船型定义拷入，等级倍率见 EffectiveHousing）
        public float Hp = 100f, MaxHp = 100f;
        public float BaseAttack, Range;
        public bool Military;
        public string AttackType = "arrow";
        public int Age, MaxAge = 200;
        public float AttackCd;
        public GameObject View;
        public Vector3 Pos => new(X, 0, Z);
        public float LevelMult => 1f+(Level-1)*0.5f;
        /// <summary>有效居住人数：普通1.0 / 精良1.5 / 传奇2.0（对齐 getShipHousing）</summary>
        public int EffectiveHousing => Mathf.FloorToInt(Housing*LevelMult);
    }

    /// <summary>马车/运输车辆（对应 v5.9.9 cart：小车/马车/大马车，四级锚点 T1-T3）</summary>
    [System.Serializable]
    public class CartEntity
    {
        public string CartTypeId = "small_cart";
        public string Name = "小车";
        public int Level = 1;              // V6.1.3 车辆等级 1普通/2精良/3传奇（浮窗实拍图随等级切换）
        public float X, Z, H;
        public float TargetX, TargetZ;
        public int Capacity, Durability, MaxDurability;
        public int Passengers;            // V6.3.7 Lv2+ 自动登乘乘客（不含驾驶员 HasDriver）
        public bool HasDriver;
        public float WanderTimer;
        public GameObject View;
        public Vector3 Pos => new(X, 0, Z);
    }

    /// <summary>投射物（箭/炮弹）</summary>
    [System.Serializable]
    public class ProjectileEntity
    {
        public Vector3 Pos, Vel;
        public float Damage;
        public float Life;
        public string Kind = "arrow";  // arrow/cannonball/fire
        public GameObject View;
        public object Target;
    }

    /// <summary>太空/海洋资源点</summary>
    [System.Serializable]
    public class ResourceNode
    {
        public string ResId;
        public float X, Z;
        public float Amount;
        public GameObject View;
    }

    /// <summary>事件日志条目</summary>
    [System.Serializable]
    public class LogEntry
    {
        public int Year;
        public string Kind;   // good/bad/info
        public string Text;
        public LogEntry(int y, string k, string t) { Year = y; Kind = k; Text = t; }
    }

    // ================= V6.1.4 我方陆军作战单位（队：1队步兵=5兵 / 1队骑兵=4骑）=================
    [System.Serializable]
    public class FriendlyUnit
    {
        public int Kind;                 // 0步兵 1骑兵
        public float X, Z;
        public float HomeX, HomeZ;       // 待命锚点（聚落/集结点）
        public float Hp, MaxHp;
        public float Attack, Speed;
        public float AtkCd;
        public int State;                // 0待命 1迎敌 2讨伐行军 3回撤
        public string CampaignId;        // 讨伐目标势力 id
        public GameObject View;
        public PixelToCivilization.World.OverheadBillboard OH;   // V6.1.9(i) 头顶旗帜+同色血条
        public Vector3 Pos => new(X, 0, Z);
        public bool IsCavalry => Kind == 1;
    }

    // ================= V6.1.5 殖民地 =================
    [System.Serializable]
    public class Colony
    {
        public string Id, Name;
        public int Level = 1;            // 1贸易站 2殖民地 3领地
        public float Pop = 10;
        public float Loyalty = 100f;     // 忠诚/安定
        public float TributeTimer;       // 上贡计时（游戏年）
        public float UnrestTimer;        // 动乱检定计时
        public string ResId;             // 主要特产
        public float X, Z;
        public GameObject View;          // V6.1.5 海外据点 3D 视图（运行时，不进存档；读档后由 ColonizationSystem.Tick 重建）
    }

    // ================= V6.1.6 副本网格（海洋海图 / 太空星图，统一结构）=================
    [System.Serializable]
    public class ExpeditionState
    {
        public string MapType = "ocean"; // ocean / space
        public int N = 9;                // N×N 网格
        public bool Inited;
        public int Seed;
        public int PosX, PosY;           // 探险队当前格
        public float Power = 100f;       // 战力（舰队/飞船），归零返航
        public float MaxPower = 100f;
        public float Supply = 100f;      // 补给
        public byte[] Seen;              // 0迷雾 1已探索
        public string[] NodeKind;        // 每格节点类型，空=无
        public int[] NodeUsed;           // 节点是否已消耗（资源点可重复则记录剩余）
        public string LastEvent = "";    // 最近一次探险日志

        public int Idx(int x, int y) => y * N + x;
        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < N && y < N;
    }

    /// <summary>V6.8.0 世界奇观运行态（唯一、不可拆；视图由 WonderSystem 按数据自愈重建）</summary>
    [System.Serializable]
    public class WonderRuntime
    {
        public string Id;
        public int BuiltYear;
        public float X, Z;
    }
}
