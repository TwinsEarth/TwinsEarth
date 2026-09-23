using UnityEngine;
using UnityEngine.UI;
using PixelToCivilization.Data;

namespace PixelToCivilization.UI
{
    /// <summary>V6.8.0 世界奇观·文明丰碑 弹窗（右栏「奇观」入口）</summary>
    public partial class UIManager
    {
        private GameObject _wonderModal;

        private void OpenWonderModal()
        {
            if (GM.Wonder == null) { Toast("奇观系统未就绪", false); return; }
            if (_wonderModal == null)
            {
                _wonderModal = MakeModal("WonderModal", "世界奇观 · 文明丰碑", out _, false);
                _wonderModal.transform.SetParent(_modalLayer, false);
            }
            FillWonder(_wonderModal);
            Open(_wonderModal);
        }

        private void FillWonder(GameObject modal)
        {
            var body = ModalBody(modal); Clear(body);
            UITheme.VerticalScroll("WonderScroll", body.transform, out var content, 6);

            // 顶部说明 + 九神自动援建开关
            UITheme.Label("hint", content,
                "每个时代可建一座世界奇观，全世界唯一、不可拆除，提供永久全局加成。",
                12, TextAnchor.UpperLeft).gameObject.AddComponent<LayoutElement>().preferredHeight = 32;
            var top = Row(content, 34);
            var autoBtn = UITheme.Btn("auto", top.transform,
                S.WonderAuto ? "◉ 九神自动援建：开" : "○ 九神自动援建：关", 12);
            autoBtn.onClick.AddListener(() => { S.WonderAuto = !S.WonderAuto; FillWonder(modal); });
            UITheme.Label("cnt", top.transform, $"已建 {S.Wonders.Count}/{GM.Wonder.Defs.Count}", 12,
                TextAnchor.MiddleRight, UITheme.Gold);

            // 八座奇观
            foreach (var d in GM.Wonder.Defs)
            {
                bool built = GM.Wonder.Built(d.Id);
                int builtYear = -1;
                foreach (var w in S.Wonders) if (w.Id == d.Id) builtYear = w.BuiltYear;
                bool eraOk = GM.Wonder.IsAvailable(d);
                bool afford = S.CanAfford(d.Cost);
                var row = UITheme.Panel("w_" + d.Id, content,
                    UITheme.HexA(built ? 0x2e7d32 : (eraOk ? 0xFFD700 : 0xffffff), built ? 0.16f : (eraOk ? 0.10f : 0.04f)));
                row.AddComponent<LayoutElement>().preferredHeight = 78;

                string eraName = GM.Eras[d.Era].Name;
                string info = $"{d.Icon} {d.Name}（{eraName}）\n{d.Desc}\n成本：{d.CostText()}　效果：{EffectText(d)}";
                var lab = UITheme.Label("info", row.transform, info, 12, TextAnchor.MiddleLeft);
                lab.rectTransform.anchorMin = new Vector2(0, 0); lab.rectTransform.anchorMax = new Vector2(0.72f, 1);
                lab.rectTransform.offsetMin = new Vector2(8, 2); lab.rectTransform.offsetMax = new Vector2(-4, -2);

                var b = UITheme.Btn("build", row.transform,
                    built ? $"已建成·{builtYear}年" : (!eraOk ? "时代未到" : (!afford ? "资源不足" : "建 造")), 12);
                var brt = b.GetComponent<RectTransform>();
                brt.anchorMin = new Vector2(0.74f, 0.2f); brt.anchorMax = new Vector2(0.99f, 0.8f);
                brt.offsetMin = brt.offsetMax = Vector2.zero;
                b.interactable = !built && eraOk && afford;
                string id = d.Id;
                b.onClick.AddListener(() =>
                {
                    var (ok, msg) = GM.Wonder.TryBuild(id);
                    Toast(ok ? $"奇观「{msg}」落成" : msg, ok);
                    if (ok) FillWonder(modal);
                });
            }

            // 文明成就
            UITheme.Label("achT", content, "—— 文明成就 ——", 14, TextAnchor.MiddleCenter, UITheme.Gold, FontStyle.Bold)
                .gameObject.AddComponent<LayoutElement>().preferredHeight = 24;
            if (S.Achievements.Count == 0)
                UITheme.Label("ach0", content, "尚未取得里程碑成就", 12, TextAnchor.MiddleCenter).gameObject
                    .AddComponent<LayoutElement>().preferredHeight = 26;
            // 去重后按年份排序展示
            var seen = new System.Collections.Generic.HashSet<string>();
            var list = new System.Collections.Generic.List<(int yr, string txt)>();
            foreach (var a in S.Achievements)
            {
                var parts = a.Split('|');
                if (parts.Length < 3 || !seen.Add(parts[0])) continue;
                int.TryParse(parts[1], out var yr);
                list.Add((yr, parts[2]));
            }
            list.Sort((x, y) => x.yr.CompareTo(y.yr));
            foreach (var a in list)
            {
                var r = UITheme.Panel("ach", content, UITheme.HexA(0xffffff, 0.05f));
                r.AddComponent<LayoutElement>().preferredHeight = 30;
                UITheme.Label("t", r.transform, $"🏅 {a.txt}　·　第 {a.yr} 年", 12, TextAnchor.MiddleLeft, UITheme.Good)
                    .SetInset(8, 0.1f);
            }
        }

        private static string EffectText(WonderDefinition d)
        {
            var sb = new System.Text.StringBuilder();
            void Pct(string name, float v) { if (v > 0.001f) { if (sb.Length > 0) sb.Append('、'); sb.Append(name).Append('+').Append(Mathf.RoundToInt(v * 100)).Append('%'); } }
            Pct("研究", d.ResearchAdd); Pct("金", d.GoldAdd); Pct("文化", d.CultureAdd);
            Pct("粮", d.FoodAdd); Pct("货", d.GoodsAdd);
            if (d.HousingAdd > 0) { if (sb.Length > 0) sb.Append('、'); sb.Append("住房+").Append(Mathf.RoundToInt(d.HousingAdd)); }
            if (d.FireCapMul > 1.001f) { if (sb.Length > 0) sb.Append('、'); sb.Append("火力上限×").Append(d.FireCapMul.ToString("0.##")); }
            if (d.ForcePower) { if (sb.Length > 0) sb.Append('、'); sb.Append("电力拉满"); }
            return sb.ToString();
        }
    }
}
