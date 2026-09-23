using UnityEngine;
using PixelToCivilization.Core;
using PixelToCivilization.UI;

namespace PixelToCivilization.Systems
{
    /// <summary>
    /// 诸子百家系统 —— 对齐策划书 era2「诸子百家系统」：
    /// 玩家选择一家思想作为治国理念（可改换），学派为全局经济/军事/民生提供修正。
    /// 儒+文化稳定 / 法+军事行政 / 道+民心人口 / 墨+科技城防 / 兵+战力 / 纵横+贸易外交。
    /// </summary>
    public class PhilosophySystem : GameSystemBase
    {
        public static readonly (string id, string name, string desc, long color)[] Schools =
        {
            ("ru",       "儒家",   "+文化25% · 民心缓升 · 军事-10%", 0xC41E3A),
            ("fa",       "法家",   "+军事20% · 建造加速15% · 民心缓降", 0x8B0000),
            ("dao",      "道家",   "+民心缓升 · 人口增长15% · 税收-10%", 0x228B22),
            ("mo",       "墨家",   "+研究20% · 城防+15% · 商业-10%", 0x4169E1),
            ("bing",     "兵家",   "+战力25% · 火力持续增长", 0x696969),
            ("zongheng", "纵横家", "+贸易金15% · 战争意愿降低", 0x9932CC),
        };

        /// <summary>择学派（改换立即生效）</summary>
        public void Adopt(string id)
        {
            foreach (var s in Schools)
                if (s.id == id)
                {
                    bool changed = S.Philosophy != id;
                    S.Philosophy = id;
                    if (changed)
                    {
                        GM.AddEvent("god", "百家争鸣：以「" + s.name + "」治国 —— " + s.desc);
                        UIManager.Instance?.Toast("国策更张：" + s.name);
                    }
                    return;
                }
        }

        // ---- 供经济/军事系统读取的倍率（缺省1） ----
        public float ResearchMult => S.Philosophy switch { "mo" => 1.20f, "fa" => 1.05f, _ => 1f };
        public float CultureMult  => S.Philosophy switch { "ru" => 1.25f, "dao" => 1.10f, _ => 1f };
        public float GoldMult     => S.Philosophy switch { "zongheng" => 1.15f, "dao" => 0.90f, "mo" => 0.90f, _ => 1f };
        public float FoodMult     => S.Philosophy switch { "dao" => 1.15f, _ => 1f };
        public float MilitaryMult => S.Philosophy switch { "bing" => 1.25f, "fa" => 1.20f, "ru" => 0.90f, _ => 1f };
        public float BuildSpeedMult => S.Philosophy switch { "fa" => 1.15f, _ => 1f };

        public override void Tick(float dt)
        {
            // 学派的持续性效果（民心/武备缓变，经系统 Tick 频率驱动）
            switch (S.Philosophy)
            {
                case "ru": S.Happiness = Mathf.Min(100, S.Happiness + 0.06f * dt); break;
                case "dao": S.Happiness = Mathf.Min(100, S.Happiness + 0.08f * dt); break;
                case "fa": S.Happiness = Mathf.Max(0, S.Happiness - 0.04f * dt); break;
                case "bing": S.MilFirepower += 0.05f * dt; break;
                case "mo": S.MilDefense += 0.04f * dt; break;
            }
        }
    }
}
