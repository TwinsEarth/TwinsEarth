using System.Collections.Generic;
using UnityEngine;
using PixelToCivilization.Core;
using PixelToCivilization.Data;
using PixelToCivilization.UI;

namespace PixelToCivilization.Systems
{
    /// <summary>
    /// 历史事件系统 —— 对齐策划书各时代「特殊事件」：
    /// 大禹治水/青铜礼器/甲骨占卜/百家争鸣/商鞅变法/张骞通西域/科举开设/玄奘西行/
    /// 贞观之治/活字印刷/交子发行/郑和下西洋/永乐大典/一条鞭法/鸦片战争/洋务运动/
    /// 辛亥革命/两弹一星/改革开放/载人航天/AI革命/第一类接触。
    /// 年份窗口内触发一次（FiredEvents 防重），效果直接改写状态并写入编年史。
    /// </summary>
    public class HistoryEventSystem : GameSystemBase
    {
        private class HistEvent
        {
            public string Id, Name;
            public int Era, Y0, Y1;
            public HistEvent(string id, int era, int y0, int y1, string name)
            { Id = id; Era = era; Y0 = y0; Y1 = y1; Name = name; }
        }

        // 策划书事件表（年份窗口对齐各时代区间）
        private static readonly List<HistEvent> Events = new()
        {
            // ---- era0 夏商周 (1-2300) ----
            new("dayu_zhishui",   0,   100,  400, "大禹治水"),
            new("jiagu_zhanbu",   0,   500, 2300, "甲骨占卜"),
            new("qingtong_liqi",  0,   800, 1600, "青铜礼器"),
            new("wangchao_gengdi",0,  1900, 2300, "商汤灭夏·武王伐纣"),
            // ---- era1 春秋战国·秦汉 (2300-3581) ----
            new("baijia_zhengming",1, 2400, 3000, "百家争鸣"),
            new("xiu_changcheng", 1, 2600, 3581, "修筑长城"),
            new("shang_yang_bianfa",1,2800, 3400, "商鞅变法"),
            new("zhang_qian_xiyu",1, 3200, 3581, "张骞通西域"),
            // ---- era2 隋唐 (3581-3960) ----
            new("zhen_guan_zhi_zhi",2,3620, 3800, "贞观之治·开元盛世"),
            new("keju_kaishe",    2, 3650, 3960, "科举开设"),
            new("xuanzang_xixing",2, 3760, 3960, "玄奘西行"),
            // ---- era3 两宋 (3960-4368) ----
            new("huozi_yinshua",  3, 4000, 4200, "活字印刷"),
            new("jiaozi_faxing",  3, 4050, 4300, "交子发行"),
            new("huoyao_wuqi",    3, 4100, 4368, "火药武器化"),
            // ---- era4 明朝 (4368-4912) ----
            new("zhenghe_xixia",  4, 4380, 4600, "郑和下西洋"),
            new("yongle_dadian",  4, 4400, 4700, "永乐大典"),
            new("yitiaobianfa",   4, 4650, 4912, "一条鞭法"),
            // ---- era5 清·民国 (4912-4949) ----
            new("yapian_zhanzheng",5,4920, 4935, "鸦片战争"),
            new("yangwu_yundong", 5, 4925, 4945, "洋务运动"),
            new("xinhai_geming",  5, 4945, 4949, "辛亥革命"),
            // ---- era6 新中国·冷战 (4949-5050) ----
            new("liangdan_yixing",6, 4960, 4990, "两弹一星"),
            new("gaige_kaifang",  6, 4980, 5010, "改革开放"),
            new("zairen_hangtian",6, 4990, 5050, "载人航天"),
            // ---- era7 地球文明联盟 (5050+) ----
            new("ai_geming",      7, 5050, 5150, "AI革命"),
            new("diyilei_jiechu", 7, 5150, 99999, "第一类接触"),
        };

        public override void OnYear(int year)
        {
            foreach (var e in Events)
            {
                if (e.Era != S.Era || year < e.Y0 || year > e.Y1) continue;
                if (!S.FiredEvents.Add(e.Id)) continue;   // 每局一次
                Apply(e);
            }
        }

        private void Apply(HistEvent e)
        {
            switch (e.Id)
            {
                // ===== era0 =====
                case "dayu_zhishui":
                    S.Happiness = Mathf.Min(100, S.Happiness + 10);
                    S.AddRes("culture", 100);
                    GM.Culture?.Reform(10);
                    S.Pop += 20;
                    Log(e, "组织人力疏九河、决九川 —— 凝聚力大增！民心+10 文化+100 人口+20");
                    break;
                case "jiagu_zhanbu":
                    {   // 随机加成/减益
                        float r = Random.value;
                        if (r < 0.4f) { S.AddRes("food", 120); Log(e, "灼龟观兆：吉！神示丰穰，粮食+120"); }
                        else if (r < 0.8f) { S.AddRes("research", 80); Log(e, "灼龟观兆：吉！先王启示，研究+80"); }
                        else { S.Happiness = Mathf.Max(0, S.Happiness - 5); Log(e, "灼龟观兆：兆象不吉，民心-5"); }
                    }
                    break;
                case "qingtong_liqi":
                    S.AddRes("culture", 200); S.AddRes("bronze", 60); S.AddRes("gold", 80);
                    S.Happiness = Mathf.Min(100, S.Happiness + 5);
                    Log(e, "铸鼎簋、作礼乐 —— 青铜礼器文化光耀四方！文化+200");
                    break;
                case "wangchao_gengdi":
                    S.MilFirepower += 15; S.MilDefense += 10;
                    S.DynastyMorale = Mathf.Min(100, S.DynastyMorale + 10);
                    Log(e, "鸣条之战·牧野之战 —— 新王朝定鼎！军事提升，气数更新");
                    break;
                // ===== era1 =====
                case "baijia_zhengming":
                    S.AddRes("culture", 150); S.AddRes("research", 120);
                    Log(e, "稷下学宫，百家争鸣！可于右侧「百家」择学派治国（儒/法/道/墨/兵/纵横）");
                    UIManager.Instance?.Toast("解锁：诸子百家");
                    break;
                case "xiu_changcheng":
                    if (S.CountBuilding("great_wall") > 0)
                    { S.Happiness = Mathf.Min(100, S.Happiness + 10); S.MilDefense += 20; Log(e, "长城巍峨连九塞 —— 御敌千里！国防+20 民心+10"); }
                    else Log(e, "边患频仍，宜筑长城以御之（可建造「长城」）");
                    break;
                case "shang_yang_bianfa":
                    GM.Culture?.Reform(30); S.MilFirepower += 10;
                    S.Happiness = Mathf.Max(0, S.Happiness - 8);
                    Log(e, "徙木立信，废井田开阡陌 —— 吏治澄清腐败-30，军事+10，民心-8");
                    break;
                case "zhang_qian_xiyu":
                    S.AddRes("gold", 500); S.AddRes("goods", 300);
                    Log(e, "凿空西域，丝绸之路开通！金+500 货物+300");
                    break;
                // ===== era2 =====
                case "zhen_guan_zhi_zhi":
                    S.Happiness = Mathf.Min(100, S.Happiness + 15); S.AddRes("gold", 800);
                    S.DynastyMorale = Mathf.Min(100, S.DynastyMorale + 15);
                    Log(e, "贞观之治/开元盛世 —— 万国来朝的黄金时代！民心+15 金+800");
                    UIManager.Instance?.Toast("黄金时代！", good: true);
                    break;
                case "keju_kaishe":
                    if (S.CountBuilding("school") > 0)
                    { S.SchoolFounded = true; S.AddRes("culture", 200); Log(e, "设科取士，天下英才入彀中 —— 研究+15%（永久）文化+200"); }
                    else { S.FiredEvents.Remove(e.Id); return; } // 未办学堂则延后重试
                    break;
                case "xuanzang_xixing":
                    S.AddRes("culture", 300); S.AddRes("research", 150);
                    Log(e, "玄奘西行取真经 —— 中外文化交流盛事！文化+300");
                    break;
                // ===== era3 =====
                case "huozi_yinshua":
                    S.AddRes("research", 400); S.AddRes("culture", 150);
                    Log(e, "毕昇活字，文明传播革命！研究+400");
                    break;
                case "jiaozi_faxing":
                    S.AddRes("gold", 1000); S.Happiness = Mathf.Min(100, S.Happiness + 5);
                    Log(e, "益州交子，世界最早纸币 —— 商业繁荣！金+1000");
                    break;
                case "huoyao_wuqi":
                    S.MilFirepower += 15; S.MilDefense += 10;
                    Log(e, "突火枪·震天雷列装 —— 军事革命！火力+15 防御+10");
                    break;
                // ===== era4 =====
                case "zhenghe_xixia":
                    S.AddRes("gold", 600);
                    if (!S.OceanUnlocked) { S.OceanUnlocked = true; GM.Ocean?.UnlockExpansion(); }
                    if (S.CountBuilding("sea_port") > 0) { S.AddRes("culture", 200); Log(e, "宝船七下西洋，万国来朝！金+600 文化+200"); }
                    else Log(e, "宝船下水，宜建「海港」开启海洋大开发！金+600");
                    break;
                case "yongle_dadian":
                    S.AddRes("culture", 500);
                    Log(e, "《永乐大典》成书 —— 旷世百科！文化+500");
                    break;
                case "yitiaobianfa":
                    S.AddRes("gold", 800); GM.Culture?.Reform(20);
                    Log(e, "赋役折银，一条鞭法推行 —— 财政充裕金+800，腐败-20");
                    break;
                // ===== era5 =====
                case "yapian_zhanzheng":
                    S.Happiness = Mathf.Max(0, S.Happiness - 15); S.AddRes("gold", -500);
                    Log(e, "国门被坚船利炮轰开 —— 三千年未有之大变局！民心-15 金-500");
                    UIManager.Instance?.Toast("鸦片战争", good: false);
                    break;
                case "yangwu_yundong":
                    GM.Culture?.Reform(25); S.AddRes("research", 300); S.MilFirepower += 10;
                    Log(e, "师夷长技以制夷 —— 洋务运动兴起！腐败-25 研究+300");
                    break;
                case "xinhai_geming":
                    S.Happiness = Mathf.Min(100, S.Happiness + 10); GM.Culture?.Reform(40);
                    S.AddRes("research", 200);
                    Log(e, "武昌枪响，帝制终结 —— 走向共和！腐败-40 研究+200");
                    break;
                // ===== era6 =====
                case "liangdan_yixing":
                    S.MilFirepower += 30; S.MilDefense += 30;
                    S.Happiness = Mathf.Min(100, S.Happiness + 10);
                    Log(e, "两弹一星成功 —— 大国地位奠基！火力+30 防御+30");
                    break;
                case "gaige_kaifang":
                    S.AddRes("gold", 2000); S.AddRes("research", 500);
                    Log(e, "改革开放 —— 春风吹拂大地！金+2000 研究+500");
                    UIManager.Instance?.Toast("改革开放", good: true);
                    break;
                case "zairen_hangtian":
                    if (!S.SpaceUnlocked) { S.SpaceUnlocked = true; GM.Space?.UnlockExploration(); }
                    S.SpElevator = Mathf.Min(100, S.SpElevator + 10);
                    S.Happiness = Mathf.Min(100, S.Happiness + 10);
                    Log(e, "载人航天成功 —— 太空时代来临！太空电梯进度+10");
                    break;
                // ===== era7 =====
                case "ai_geming":
                    S.AddRes("research", 1000); S.AiBonus += 0.1f;
                    Log(e, "强人工智能问世 —— 科技奇点临近！研究+1000 AI加成+10%");
                    break;
                case "diyilei_jiechu":
                    S.AddRes("culture", 1000); S.Happiness = Mathf.Min(100, S.Happiness + 15);
                    Log(e, "「我们并不孤独。」—— 第一类接触建立！文明翻开新篇");
                    UIManager.Instance?.ShowEraTransition("第一类接触", "群星之间，文明的回响终于得到了回应。");
                    break;
            }
        }

        private void Log(HistEvent e, string text) => GM.AddEvent("god", "【" + e.Name + "】" + text);
    }
}
