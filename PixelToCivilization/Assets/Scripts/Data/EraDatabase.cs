using System.Collections.Generic;

namespace PixelToCivilization.Data
{
    /// <summary>
    /// 时代数据库 —— 对齐 v5.9.9 ERAS（8时代，游戏年份区间）
    /// </summary>
    public static partial class EraDatabase
    {
        public static List<EraDefinition> CreateAll()
        {
            return new List<EraDefinition>
            {
                Era(0,"三皇五帝·夏商周","奴隶社会",1,2300,0x8B4513,
                    "等级制度：贵族与富人住高等级大宅，奴隶住棚屋",
                    "社会分为贵族、富人、平民、奴隶四等。建筑按等级分配，贵族宫殿最大最华丽。",
                    new[]{"hut","rich_house","noble_palace","well","granary","farm","mine","market","altar","bronze_forge","wall"}),
                Era(1,"春秋战国·秦汉","封建社会",2300,3581,0xCD853F,
                    "国家与战争：军营激活，诸侯争霸",
                    "建造军营训练军队，抵御外敌入侵，统一全国。",
                    new[]{"barracks","watchtower","great_wall","stable","road","iron_smelter","palace","academy_pre","workshop","arrow_tower"}),
                Era(2,"隋唐·五代","盛世华章",3581,3960,0xDAA520,
                    "大运河与科举：修运河、开驰道、兴学堂",
                    "分段修建大运河连通南北贸易，开学堂允许所有人读书。",
                    new[]{"canal","highway","school","temple","pagoda","water_mill","shipyard_pre","caravanserai","fire_tower"}),
                Era(3,"两宋·元","科技繁荣",3960,4368,0x4682B4,
                    "科技大发展：研究速度翻倍，商业繁荣",
                    "活字印刷、火药、指南针相继问世，科技研究速度翻倍。",
                    new[]{"printing_house","gunpowder_mill","compass_shop","bank","tea_house","porcelain_kiln","water_clock","cannon_tower"}),
                Era(4,"明·清","航海盛世",4368,4912,0x2E8B57,
                    "海洋大开发：郑和下西洋，独立海洋副本",
                    "建造宝船舰队，开启海洋探索副本，发现新大陆。",
                    new[]{"treasure_shipyard","sea_port","customs_house","arsenal","grand_hall","brick_works"}),
                Era(5,"民国","风雨飘摇",4912,4949,0x708090,
                    "外族外国侵略：鸦片战争、甲午战争、抗日战争",
                    "列强入侵，发展近代工业与新军抵御外侮。",
                    new[]{"modern_arsenal","railway_pre","telegraph","factory_pre","new_army","dockyard_modern","bunker"}),
                Era(6,"新中国","伟大复兴",4949,5050,0xDC143C,
                    "现代化建设：高楼、高铁、飞机、电力、AI",
                    "建设高楼大厦、高铁网络、机场、电站、数据中心和AI实验室。",
                    new[]{"skyscraper","high_speed_rail","airport","power_plant","data_center","ai_lab","factory_modern","highway_modern"}),
                Era(7,"地球联盟","星际文明",5050,9999,0x9400D3,
                    "太空时代：太空电梯、戴森云、火星移民",
                    "建造太空电梯、宇宙飞船、戴森云，移民月球和火星。",
                    new[]{"space_elevator","spaceship_yard","dyson_swarm","lunar_base","mars_colony","orbital_station","fusion_plant"}),
            };
        }

        private static EraDefinition Era(int id, string name, string sub, int y0, int y1, long color,
            string feature, string detail, string[] unlocks)
        {
            var e = new EraDefinition
            {
                Id = id, Name = name, Sub = sub, StartYear = y0, EndYear = y1, ColorHex = color,
                Feature = feature, FeatureDetail = detail,
                Unlocks = new List<string>(unlocks)
            };
            ApplyStyle(e, id);
            return e;
        }

        /// <summary>各时代建筑视觉风格（墙体/屋顶颜色、屋顶形制）</summary>
        public static void ApplyStyle(EraDefinition e, int id)
        {
            var s = e.Style;
            switch (id)
            {
                case 0: // 夏商周：夯土+茅草
                    s.WallColor = Col(0.96f,0.92f,0.82f); s.RoofColor = Col(0.30f,0.70f,0.58f); // V7.0.1 奶白墙+青绿顶
                    s.RoofType = RoofType.Thatched; s.BuildingScale = 0.85f; s.BuildingHeight = 0.8f; break;
                case 1: // 秦汉：木构+灰瓦
                    s.WallColor = Col(0.95f,0.94f,0.90f); s.RoofColor = Col(0.16f,0.70f,0.66f);
                    s.AccentColor = Col(1.0f,0.54f,0.12f); // V7.0.1 橙
                    s.RoofType = RoofType.Hip; s.HasPillars = s.HasWindows = s.HasBase = s.HasEaves = true;
                    s.BuildingHeight = 1.2f; s.EavesCurve = 0.1f; break;
                case 2: // 隋唐：红墙绿瓦飞檐
                    s.WallColor = Col(0.97f,0.95f,0.92f); s.RoofColor = Col(0.10f,0.74f,0.72f);
                    s.AccentColor = Col(1.0f,0.54f,0.12f); // V7.0.1 橙
                    s.RoofType = RoofType.Curved; s.HasPillars = s.HasWindows = s.HasSecondFloor = s.HasBase = s.HasEaves = true;
                    s.BuildingScale = 1.1f; s.BuildingHeight = 1.5f; s.EavesCurve = 0.3f; break;
                case 3: // 两宋：素雅
                    s.WallColor = Col(0.94f,0.95f,0.95f); s.RoofColor = Col(0.20f,0.66f,0.78f);
                    s.RoofType = RoofType.Curved; s.HasPillars = s.HasWindows = s.HasSecondFloor = s.HasBase = s.HasEaves = true;
                    s.BuildingHeight = 1.3f; s.EavesCurve = 0.25f; break;
                case 4: // 明清：红墙黄瓦宫殿
                    s.WallColor = Col(0.98f,0.94f,0.86f); s.RoofColor = Col(0.12f,0.70f,0.80f); s.AccentColor = Col(1.0f,0.54f,0.12f);
                    s.RoofType = RoofType.Imperial; s.HasPillars = s.HasWindows = s.HasSecondFloor = s.HasBase = s.HasEaves = true;
                    s.BuildingScale = 1.15f; s.BuildingHeight = 1.6f; s.EavesCurve = 0.35f; break;
                case 5: // 民国：青砖灰瓦
                    s.WallColor = Col(0.93f,0.95f,0.96f); s.RoofColor = Col(0.30f,0.60f,0.86f);
                    s.RoofType = RoofType.Gable; s.HasPillars = s.HasWindows = s.HasSecondFloor = s.HasBase = true;
                    s.BuildingHeight = 1.4f; break;
                case 6: // 新中国：现代玻璃幕墙
                    s.WallColor = Col(0.96f,0.97f,0.98f); s.RoofColor = Col(0.25f,0.55f,0.95f);
                    s.RoofType = RoofType.Flat; s.HasWindows = s.HasSecondFloor = true;
                    s.BuildingScale = 1.3f; s.BuildingHeight = 2.5f;
                    s.IsEmissive = true; s.EmissiveColor = ColA(0f,0.4f,0.67f,0.3f); break;
                case 7: // 地球联盟：未来能量体
                    s.WallColor = Col(1f,1f,1f); s.RoofColor = Col(0.10f,0.78f,1.0f); s.AccentColor = Col(0.10f,0.70f,1.0f);
                    s.RoofType = RoofType.Energy; s.HasWindows = s.HasSecondFloor = true;
                    s.BuildingScale = 1.4f; s.BuildingHeight = 3f;
                    s.IsEmissive = true; s.EmissiveColor = ColA(0f,0.533f,1f,0.5f);
                    s.HasForceField = s.HasDome = true; break;
            }
        }

        private static UnityEngine.Color Col(float r, float g, float b) => new(r, g, b);
        private static UnityEngine.Color ColA(float r, float g, float b, float a) => new(r, g, b, a);
    }
}
