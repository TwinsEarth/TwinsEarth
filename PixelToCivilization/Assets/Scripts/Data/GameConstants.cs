using System.Collections.Generic;
using UnityEngine;

namespace PixelToCivilization.Data
{
    /// <summary>15种资源定义（对齐 v5.9.9 RES_ICONS）</summary>
    [System.Serializable]
    public class ResourceDef
    {
        public string Id, Name, Icon;
        public int StartValue;
        public ResourceDef(string id, string name, string icon, int start = 0)
        { Id = id; Name = name; Icon = icon; StartValue = start; }
    }

    public static class ResourceDatabase
    {
        public static readonly string[] Order =
            { "wood","stone","food","gold","iron","bronze","goods","culture","research","power","steel","concrete","fusion","carbon","helium3" };

        public static readonly Dictionary<string, string> Names = new()
        {
            {"wood","木材"},{"stone","石料"},{"food","粮食"},{"gold","金币"},{"iron","铁"},
            {"bronze","青铜"},{"goods","货物"},{"culture","文化"},{"research","研究"},{"power","电力"},
            {"steel","钢铁"},{"concrete","混凝土"},{"fusion","聚变能"},{"carbon","碳材料"},{"helium3","氦-3"}
        };

        public static readonly Dictionary<string, string> Icons = new()
        {
            {"wood","🌲"},{"stone","⛰️"},{"food","🌾"},{"gold","💰"},{"iron","⚙️"},
            {"bronze","🥉"},{"goods","📦"},{"culture","📚"},{"research","🔬"},{"power","⚡"},
            {"steel","🔩"},{"concrete","🧱"},{"fusion","☢️"},{"carbon","💎"},{"helium3","🔮"}
        };

        public static Dictionary<string, float> InitialResources() => new()
        {
            {"wood",200},{"stone",100},{"food",150},{"gold",50},{"iron",0},{"bronze",0},
            {"goods",0},{"culture",0},{"research",0},{"power",0},{"steel",0},
            {"concrete",0},{"fusion",0},{"carbon",0},{"helium3",0}
        };
    }

    /// <summary>全局平衡常量（对齐 v5.9.9）</summary>
    public static class GameConstants
    {
        public const int YearDays = 365;          // 每年天数
        // 1:1 对齐 v5.9.9：现实 60 秒 = 1 年（1倍速每秒推进 365/60≈6.083 天，60秒累计365天进1年）
        public const float DaySeconds = 365f / 60f;
        public const int MaxPop = 2000;
        public const int MaxBuildings = 300;
        public const int MaxTrees = 1000;
        public const int MapSize = 240;          // 初始活动网格（=V6.3.6 实际，开局即可玩区域 240x240）
        public const float Tile = 4f;            // 每格4单位
        public const float WorldSize = MapSize * Tile; // 初始活动世界边长960
        // V6.5.8 全量画布上限=长(X)5倍×宽(Z)3倍=面积15倍：初始960×960 → 上限4800×2880。
        // 高度图仍用方形网格(边长取较大轴 MaxMapSize=1200)，X 每格 Tile=4，Z 每格 TileZ=2.4，噪声/BFS 保持方形不越界。
        public const int MaxMapSize = MapSize*5;       // 全量高度图方形网格 1200x1200（按较大轴 X）
        public const float TileZ = (MapSize*3f*Tile)/MaxMapSize; // Z 轴每格世界单位 =2880/1200=2.4
        public const float WorldMaxX = MaxMapSize*Tile;        // X 上限世界长 4800（初始5倍）
        public const float WorldMaxZ = MaxMapSize*TileZ;       // Z 上限世界宽 2880（初始3倍）
        public const float MaxWorldSize = WorldMaxX;    // 兼容旧引用：取较大边长 4800
        public const int TerrainSegments = 96;    // 渲染网格分段
        public const float WaterLevel = -0.2f;     // 低于此高度为水域
        public const float StartHappiness = 70f;
        public const int StartPop = 80;

        // Debug分级：0=普通(最高100倍) 1=Debug(300倍) 2=密码解锁(1000倍)。v5.9.9 为连续倍速，这里仅作键盘循环快捷档
        public const string DebugPassword = "ToFuture";
        public const float SpeedMaxNormal = 100f, SpeedMaxDebug1 = 300f, SpeedMaxDebug2 = 1000f;
        public static readonly float[] SpeedTiersNormal = { 1f, 2f, 3f, 5f, 10f, 30f, 100f };
        public static readonly float[] SpeedTiersDebug1 = { 1f, 2f, 3f, 5f, 10f, 30f, 100f, 300f };
        public static readonly float[] SpeedTiersDebug2 = { 1f, 2f, 3f, 5f, 10f, 30f, 100f, 300f, 1000f };

        // V6.1.9 加速冷冻：加速(倍速>1)累计推进满100游戏年→强制冷冻冷却300现实秒，期间实际倍速封顶10；倒计时结束自动解冻并把累计年数清零重算
        public const float CryoYearThreshold = 100f;   // 触发冷冻的累计游戏年数
        public const float CryoCooldownSec = 300f;     // 冷冻冷却现实秒数
        public const float CryoMaxSpeed = 10f;         // 冷冻期间最高倍速
    }
}
