using UnityEngine;
using PixelToCivilization.Core;
using PixelToCivilization.Data;
using PixelToCivilization.UI;

namespace PixelToCivilization.Systems
{
    /// <summary>
    /// 灾害与危机系统 —— 对齐策划书「灾害与危机系统」：
    /// 每个时代有基于真实历史的灾害池（大洪水/蝗灾/旱灾/安史之乱/游牧入侵/列强侵略/金融危机/AI失控/小行星）。
    /// 年份驱动随机触发；对应防御建筑可减免损失（减半并在编年史中体现）。
    /// </summary>
    public class DisasterSystem : GameSystemBase
    {
        private class Disaster
        {
            public string Name, Desc, MitName;
            public string[] MitBuildings;   // 任一存在即减半
            public float HappyDown;         // 民心-
            public float PopRatio;          // 人口损失比例
            public (string res, int amt)[] Loss; // 资源损失
            public Disaster(string n, string d, float hd, float pr, (string, int)[] loss, string[] mit, string mitName)
            { Name = n; Desc = d; HappyDown = hd; PopRatio = pr; Loss = loss; MitBuildings = mit; MitName = mitName; }
        }

        // 时代 → 灾害池（对齐策划书灾害表）
        private static readonly System.Collections.Generic.Dictionary<int, Disaster[]> Pools = new()
        {
            [0] = new[]
            {   // 上古：大洪水、猛兽、瘟疫
                new Disaster("大洪水","洪水滔天，田舍尽没",12f,0.06f,new[]{("food",150),("wood",100)},new[]{"granary","canal"},"粮仓与水利"),
                new Disaster("猛兽侵袭","猛兽下山，袭扰村落",6f,0.02f,new[]{("food",60)},new[]{"wall","watchtower"},"城墙哨塔"),
                new Disaster("瘟疫","疫病流行，人口凋敝",10f,0.05f,new[]{("food",80)},new[]{"well","school"},"清洁与医巫"),
            },
            [1] = new[]
            {   // 古代：蝗灾、旱灾、黄河改道
                new Disaster("蝗灾","飞蝗蔽日，赤地千里",10f,0.03f,new[]{("food",300)},new[]{"granary","water_mill"},"粮仓"),
                new Disaster("大旱","赤日炎炎，河床龟裂",10f,0.03f,new[]{("food",250)},new[]{"canal","water_mill"},"灌溉水利"),
                new Disaster("黄河改道","大河决口，改道淹田",14f,0.05f,new[]{("food",200),("stone",100)},new[]{"canal","great_wall"},"堤防工程"),
            },
            [2] = new[]
            {   // 中古：安史之乱、藩镇割据
                new Disaster("安史之乱","渔阳鼙鼓动地来，盛世倾颓",18f,0.06f,new[]{("gold",400),("goods",150)},new[]{"barracks","great_wall"},"强军镇守"),
                new Disaster("藩镇割据","节度使拥兵自重，号令不出国门",12f,0.03f,new[]{("gold",300)},new[]{"barracks","school"},"中央削藩"),
            },
            [3] = new[]
            {   // 近世：游牧入侵、农民起义
                new Disaster("游牧入侵","北地铁骑南下，边关告急",12f,0.04f,new[]{("gold",300),("food",150)},new[]{"great_wall","cannon_tower"},"长城炮台"),
                new Disaster("农民起义","苛政猛于虎，流民四起",12f,0.04f,new[]{("gold",250)},new[]{"school","temple"},"教化安抚"),
            },
            [4] = new[]
            {   // 近代：列强侵略、内战
                new Disaster("列强侵略","坚船利炮叩关，山河破碎",18f,0.05f,new[]{("gold",600),("steel",80)},new[]{"arsenal","modern_arsenal","dockyard_modern"},"近代军备"),
                new Disaster("军阀混战","烽火连天，生灵涂炭",15f,0.05f,new[]{("gold",400)},new[]{"barracks","school"},"秩序重建"),
            },
            [5] = new[]
            {   // 现代：金融危机、环境灾难
                new Disaster("金融危机","市场崩盘，热钱外逃",12f,0.02f,new[]{("gold",1500)},new[]{"bank","data_center"},"金融调控"),
                new Disaster("环境灾难","极端天气肆虐，生态告急",10f,0.02f,new[]{("food",400),("concrete",100)},new[]{"fusion_plant","data_center"},"科技抗灾"),
            },
            [6] = new[]
            {   // 未来：AI失控、小行星
                new Disaster("AI失控","超级智能越权，系统危机",12f,0.02f,new[]{("research",600),("carbon",50)},new[]{"ai_lab","data_center"},"AI对齐协议"),
                new Disaster("小行星撞击","天外飞星逼近，全球警戒",16f,0.04f,new[]{("concrete",200),("steel",120)},new[]{"space_elevator","orbital_station","dyson_swarm"},"行星防御系统"),
            },
            [7] = new[]
            {   // 星际时代： cosmic 级危机
                new Disaster("恒星耀斑","戴森云单元受损，能源波动",12f,0.02f,new[]{("fusion",30),("helium3",60)},new[]{"dyson_swarm","fusion_plant"},"聚变冗余"),
                new Disaster("殖民地骚乱","远疆殖民地离心，联盟动荡",12f,0.03f,new[]{("gold",1200)},new[]{"orbital_station","spaceship_yard"},"星际治理"),
            },
        };

        private System.Collections.Generic.HashSet<int> _loggedEmptyPools = new();

        public override void OnYear(int year)
        {
            if (!Pools.TryGetValue(S.Era, out var pool) || pool.Length == 0) return;

            // 触发率：基础 8%/年；吏治腐败或昏君加剧天灾人祸
            float chance = 0.08f;
            if (S.Corruption > 60) chance += 0.03f;
            if (!S.MonarchWise) chance += 0.03f;

            if (Random.value > chance) return;
            var d = pool[Random.Range(0, pool.Length)];
            Strike(d);
        }

        private void Strike(Disaster d)
        {
            bool mitigated = false;
            foreach (var b in d.MitBuildings)
                if (S.CountBuilding(b) > 0) { mitigated = true; break; }

            float mult = mitigated ? 0.5f : 1f;
            S.Happiness = Mathf.Max(0, S.Happiness - d.HappyDown * mult);
            int popLoss = Mathf.RoundToInt(S.Pop * d.PopRatio * mult);
            S.Pop = Mathf.Max(20, S.Pop - popLoss);

            var lossTxt = "";
            if (d.Loss != null)
                foreach (var (res, amt) in d.Loss)
                {
                    S.AddRes(res, -amt * mult);
                    lossTxt += $" {res}-{Mathf.RoundToInt(amt * mult)}";
                }

            string head = mitigated ? $"⚠️ {d.Name}来袭 —— {d.MitName}发挥效用，损失减半！"
                                    : $"☠️ {d.Name}！{d.Desc}";
            GM.AddEvent("bad", head + (popLoss > 0 ? $" 人口-{popLoss}" : "") + lossTxt);
            UIManager.Instance?.Toast(mitigated ? d.Name + "（已减灾）" : d.Name + "！", good: false);
        }
    }
}
