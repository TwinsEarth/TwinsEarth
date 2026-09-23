using System.Collections.Generic;

namespace PixelToCivilization.Data
{
    /// <summary>
    /// V6.8.0 世界奇观静态定义：每时代一座、全世界唯一、不可拆除的国家工程。
    /// 成本对齐四级锚点 T4 量级；效果为全局永久乘数/加数，由 WonderSystem 汇总后
    /// 在 EconomySystem 乘数链末端 / PopulationSystem 住房处唯一叠加。
    /// </summary>
    [System.Serializable]
    public class WonderDefinition
    {
        public string Id;
        public string Name;
        public int Era;                 // 解锁时代 0..7
        public string Icon;             // emoji
        public string Shape;            // 丰碑造型键（WonderSystem 程序化建模）
        public string Desc;
        public Dictionary<string, int> Cost = new();

        // —— 全局永久效果（加成分数，乘数=1+Σ；住房为绝对加数）——
        public float ResearchAdd, GoldAdd, CultureAdd, FoodAdd, GoodsAdd;
        public float HousingAdd;
        public float FireCapMul = 1f;   // 军事火力软上限倍率（>1）
        public bool ForcePower;         // 现代电力拉满（电力倍率恒取高档）
        public long ColorHex;           // 主体色
        public long TrimHex;            // 金饰色

        public int CostOf(string k) => Cost != null && Cost.TryGetValue(k, out var v) ? v : 0;
        public string CostText()
        {
            if (Cost == null || Cost.Count == 0) return "无";
            var sb = new System.Text.StringBuilder();
            foreach (var kv in Cost) { if (sb.Length > 0) sb.Append(' '); sb.Append(kv.Value).Append(ResName(kv.Key)); }
            return sb.ToString();
        }
        public static string ResName(string id) => id switch
        {
            "wood" => "木", "stone" => "石", "food" => "粮", "gold" => "金", "iron" => "铁",
            "bronze" => "青铜", "goods" => "货", "steel" => "钢", "concrete" => "水泥",
            "fusion" => "聚变", "helium3" => "氦3", _ => id
        };
    }

    public static partial class WonderDatabase
    {
        public static List<WonderDefinition> CreateAll()
        {
            return new List<WonderDefinition>
            {
                new WonderDefinition{ Id="lingtai", Name="灵台观象", Era=0, Icon="🔭", Shape="observatory",
                    Desc="观星授时、推演历法，文明第一次抬头仰望星空。",
                    Cost=new(){ ["wood"]=300,["stone"]=200 },
                    ResearchAdd=0.15f, CultureAdd=0.10f, ColorHex=0x6B5640, TrimHex=0xFFD700 },

                new WonderDefinition{ Id="terracotta", Name="兵马俑阵", Era=1, Icon="⚔️", Shape="army",
                    Desc="甲士如林、军阵如山，帝国武力的永恒陈列。",
                    Cost=new(){ ["wood"]=300,["stone"]=300,["bronze"]=120 },
                    FireCapMul=1.30f, ColorHex=0x8A5A3B, TrimHex=0xC9A227 },

                new WonderDefinition{ Id="daminggong", Name="大明宫", Era=2, Icon="🏯", Shape="hall",
                    Desc="万国来朝的盛世正殿，礼制与富庶的顶点。",
                    Cost=new(){ ["wood"]=500,["stone"]=400,["gold"]=200 },
                    GoldAdd=0.20f, FoodAdd=0.10f, ColorHex=0x9E2B25, TrimHex=0x3FA34D },

                new WonderDefinition{ Id="astronomic", Name="水运仪象台", Era=3, Icon="🕰️", Shape="tower",
                    Desc="集观测、演示、报时于一体的古代天文钟塔。",
                    Cost=new(){ ["wood"]=400,["stone"]=400,["iron"]=150,["gold"]=200 },
                    ResearchAdd=0.25f, ColorHex=0x7A6A58, TrimHex=0xB8860B },

                new WonderDefinition{ Id="forbidden_city", Name="紫禁城", Era=4, Icon="🏛️", Shape="palace",
                    Desc="红墙黄瓦、中轴恢弘的帝王之都，四海归一。",
                    Cost=new(){ ["wood"]=700,["stone"]=600,["iron"]=200,["gold"]=500 },
                    ResearchAdd=0.10f, GoldAdd=0.10f, CultureAdd=0.10f, FoodAdd=0.10f, GoodsAdd=0.10f,
                    HousingAdd=120f, ColorHex=0x8C1A1A, TrimHex=0xFFD700 },

                new WonderDefinition{ Id="jingzhang", Name="京张铁路", Era=5, Icon="🚂", Shape="rail",
                    Desc="中国人自建的第一条铁路，近代工业的脊梁。",
                    Cost=new(){ ["stone"]=500,["steel"]=300,["gold"]=400 },
                    GoldAdd=0.15f, GoodsAdd=0.30f, ColorHex=0x5A6470, TrimHex=0xD9B36A },

                new WonderDefinition{ Id="three_gorges", Name="三峡大坝", Era=6, Icon="🏗️", Shape="dam",
                    Desc="横断大江的混凝土巨坝，电力泽被万里。",
                    Cost=new(){ ["steel"]=700,["concrete"]=600,["gold"]=800 },
                    GoodsAdd=0.20f, ForcePower=true, ColorHex=0x9AA3AD, TrimHex=0xF2C019 },

                new WonderDefinition{ Id="dyson", Name="戴森云", Era=7, Icon="🛰️", Shape="dyson",
                    Desc="环绕恒星采集能量的轨道云团，I 型文明迈向 II 型的丰碑。",
                    Cost=new(){ ["fusion"]=400,["helium3"]=200,["steel"]=600,["gold"]=1500 },
                    ResearchAdd=0.30f, GoldAdd=0.30f, GoodsAdd=0.30f, ForcePower=true,
                    ColorHex=0x2BD4FF, TrimHex=0x9BE8FF },
            };
        }
    }
}
