using UnityEngine;
using PixelToCivilization.Core;
using PixelToCivilization.Data;
using PixelToCivilization.UI;

namespace PixelToCivilization.Systems
{
    /// <summary>
    /// 胜利系统 —— 对齐策划书「胜利条件与终局」六种路径：
    /// 文化/征服/科技(火星已由太空系统结算，此处补戴森云)/经济/外交/超越。
    /// 周期检查，先达成者定胜负类型；胜利写入编年史并弹出过场。
    /// </summary>
    public class VictorySystem : GameSystemBase
    {
        private float _cd = 5f;

        public override void Tick(float dt)
        {
            if (S.Victory) return;
            _cd -= dt;
            if (_cd > 0) return;
            _cd = 5f;
            Check();
        }

        private void Check()
        {
            // 征服胜利：五方势力尽数覆灭
            var mil = GM.Military;
            if (mil != null && mil.FactionsInited && mil.Factions.Count > 0)
            {
                bool allDead = true;
                foreach (var f in mil.Factions) if (!f.Destroyed) { allDead = false; break; }
                if (allDead) { Win("征服", "五方势力尽灭，寰宇一统 —— 世界政府建立！"); return; }
            }
            // 文化胜利：文化影响力覆盖天下
            if (S.Era >= 4 && S.GetRes("culture") >= 30000)
            { Win("文化", "文明之光普照天下，四方来仪 —— 天下大同！"); return; }
            // 科技胜利：戴森云竣工（火星移民胜利已由太空系统结算）
            if (S.SpDyson >= 100)
            { Win("科技", "戴森云环绕恒星，文明跃升卡尔达肖夫II型！"); return; }
            // 经济胜利：富甲天下
            if (S.Era >= 5 && S.GetRes("gold") >= 100000)
            { Win("经济", "通货行于万邦，不战而屈人之兵！"); return; }
            // 外交胜利：地球文明联盟
            if (S.Era >= 7 && S.Happiness >= 90 && !S.WarActive && S.GetRes("culture") >= 20000)
            { Win("外交", "万国同心，地球文明联盟成立 —— 人类走向统一！"); return; }
            // 超越胜利：科技全通 + 意识形态升华
            if (S.Era >= 7 && S.ResearchedTechs.Count >= GM.Techs.Count && S.GetRes("research") >= 30000)
            { Win("超越", "穷尽已知，文明步入数字形态 —— 超越物质世界！"); return; }
        }

        private void Win(string type, string desc)
        {
            S.Victory = true;
            S.VictoryType = type;
            S.Running = false; // 定格胜局（玩家可在Debug面板继续）
            GM.AddEvent("good", "🏆 " + type + "胜利达成！" + desc);
            UIManager.Instance?.Toast("🏆 " + type + "胜利！");
            UIManager.Instance?.ShowEraTransition("🏆 " + type + "胜利", desc + "\n\n「周虽旧邦，其命维新。」—— 文明的故事没有终点。");
            Debug.Log("[VICTORY] " + type + "胜利: " + desc);
        }
    }
}
