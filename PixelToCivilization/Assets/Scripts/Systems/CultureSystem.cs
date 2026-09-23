using UnityEngine;
using PixelToCivilization.Core;
using PixelToCivilization.Data;

namespace PixelToCivilization.Systems
{
    /// <summary>
    /// 文化与朝代系统 —— 对齐 v5.9.9：
    /// 朝代士气崩溃事件、时代切换过场与特殊初始化（era4解锁海洋、era6解锁太空）、文化/民心联动。
    /// </summary>
    public class CultureSystem : GameSystemBase
    {
        private float _collapseCheckCd;

        // 策划书·继承人：明君/昏君名字池
        private static readonly string[] WiseNames = { "文王再世","中兴之主","雄才大略","仁宣之治","励精图治" };
        private static readonly string[] FoolNames  = { "骄奢淫逸","宠信佞臣","穷兵黩武","怠于政事","刚愎自用" };

        public override void Tick(float dt)
        {
            // 君主品性：明君安抚、昏君扰民（每帧按已乘速度的 dt 累加；旧实现误置于 1 秒冷却块内又乘单帧 dt，近乎无效）
            if (S.MonarchWise) S.Happiness = Mathf.Min(100, S.Happiness + 0.02f * dt);
            else S.Happiness = Mathf.Max(0, S.Happiness - 0.015f * dt);

            // 朝代气数（士气由经济系统持续消耗）；概率事件按 1 秒节流
            _collapseCheckCd -= dt;
            if (_collapseCheckCd <= 0)
            {
                _collapseCheckCd = 1f;
                if (S.DynastyMorale < 20 && Random.value < 0.01f) DynastyTurmoil();
                // 策划书·吏治：腐败度过高 → 官逼民变
                if (S.Corruption > 75 && Random.value < 0.004f)
                {
                    S.Happiness = Mathf.Max(0, S.Happiness - 2);
                    GM.AddEvent("bad", "⛓️ 吏治腐败，官逼民变 —— 民心-2（推行变革可澄清吏治）");
                }
            }
        }

        /// <summary>王朝动荡（年份驱动下朝代仍由年份决定，此处表现为动乱事件并重置士气）</summary>
        private void DynastyTurmoil()
        {
            var dyn = GM.Time.DynastyName;
            GM.AddEvent("bad","💥 "+dyn+"王朝动荡！民变四起，国力受损");
            S.DynastyMorale = 60 + Random.value*30f;
            S.Happiness = Mathf.Max(0, S.Happiness-10);
        }

        /// <summary>改革澄清吏治（历史事件/政策调用）：降低腐败度</summary>
        public void Reform(float amount)
        {
            float before = S.Corruption;
            S.Corruption = Mathf.Max(0, S.Corruption - amount);
            if (before > 0 && S.Corruption <= 0)
                GM.AddEvent("good", "⚖️ 吏治为之一清！腐败度归零");
        }

        /// <summary>新君即位：70%明君 / 30%昏君，影响民心与腐败增速</summary>
        public void RollMonarch()
        {
            S.MonarchWise = Random.value < 0.7f;
            var pool = S.MonarchWise ? WiseNames : FoolNames;
            S.MonarchName = pool[Random.Range(0, pool.Length)];
            GM.AddEvent(S.MonarchWise ? "good" : "bad",
                S.MonarchWise ? "👑 新君即位：" + S.MonarchName + " —— 朝野振奋"
                              : "⚠️ 新君即位：" + S.MonarchName + " —— 朝纲隐忧");
        }

        public override void OnDynasty(int idx, int year)
        {
            var d = Dynasties[idx];
            GM.AddEvent("good","📜 朝代更迭：进入"+d.Name+"（第"+year+"年，"+DynastyDatabase.FormatGregorian(
                DynastyDatabase.GetGregorian(year,d))+"）");
            // 策划书·朝代生命周期：更迭掷新君；文明成果延续（建筑/科技保留），腐败度部分清偿
            RollMonarch();
            S.Corruption = Mathf.Max(0, S.Corruption * 0.6f);
        }

        public override void OnEra(int newEra, int oldEra)
        {
            var era = Eras[newEra];
            GM.AddEvent("god","🌟 进入"+era.Name+"！"+era.Feature);

            // 策划书·吏治：腐败随年代缓涨（明君缓、昏君急），在年份推进时结算
            // 建筑风格随时代更新
            GM.Building?.RefreshAllStyles();
            // 船只随时代升级
            GM.Naval?.UpgradeShipsByEra(newEra);

            switch (newEra)
            {
                case 1: GM.AddEvent("good","⚔️ 封建时代来临！建造军营以备战。"); break;
                case 2: GM.AddEvent("good","🌊 隋唐盛世！可修建大运河、开学堂。"); break;
                case 3: GM.AddEvent("good","🔬 两宋科技繁荣！科技研究速度翻倍！"); break;
                case 4:
                    GM.AddEvent("good","🚢 大明航海时代！建造宝船开启海洋副本！");
                    GM.Ocean?.UnlockExpansion(); break;
                case 5: GM.AddEvent("bad","⚠️ 清民时期！警惕列强入侵！"); break;
                case 6:
                    GM.AddEvent("good","🏙️ 新中国成立！建设现代化强国！");
                    GM.Space?.UnlockExploration(); break;
                case 7:
                    GM.AddEvent("good","🚀 地球联盟！迈向星际文明！"); break;
            }
        }

        /// <summary>年份推进：腐败度增长（明君+0.15/年，昏君+0.45/年）</summary>
        public override void OnYear(int year)
        {
            S.Corruption = Mathf.Min(100, S.Corruption + (S.MonarchWise ? 0.15f : 0.45f));
        }
    }
}
