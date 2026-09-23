using System.Collections.Generic;
using UnityEngine;

namespace PixelToCivilization.Data
{
    /// <summary>朝代定义 —— 对齐 v5.9.9 DYNASTIES（16朝代，游戏年份+公历双轨）</summary>
    [System.Serializable]
    public class DynastyDefinition
    {
        public string Name;
        public int Era;          // 所属时代
        public int YearStart;    // 游戏年份起
        public int YearEnd;      // 游戏年份止
        public int GregStart;    // 公历起（负数=公元前）
        public int GregEnd;      // 公历止
    }

    /// <summary>朝代数据库 + 公历换算（以游戏年份为准线性插值）</summary>
    public static class DynastyDatabase
    {
        public static List<DynastyDefinition> CreateAll() => new()
        {
            D("三皇五帝",0,1,1000,-3000,-2070),
            D("夏",0,1000,1400,-2070,-1600),
            D("商",0,1400,2000,-1600,-1046),
            D("西周",0,2000,2300,-1046,-770),
            D("东周",1,2300,2800,-770,-221),
            D("秦",1,2800,2816,-221,-202),
            D("汉",1,2816,3200,-202,220),
            D("三国晋南北朝",1,3200,3581,220,581),
            D("隋唐五代",2,3581,3960,581,960),
            D("两宋",3,3960,4271,960,1271),
            D("元",3,4271,4368,1271,1368),
            D("明",4,4368,4636,1368,1636),
            D("清",4,4636,4912,1636,1912),
            D("民国",5,4912,4949,1912,1949),
            D("新中国",6,4949,5050,1949,2050),
            D("地球联盟",7,5050,9999,2050,9999),
        };

        private static DynastyDefinition D(string n,int era,int y0,int y1,int g0,int g1) => new()
        { Name=n, Era=era, YearStart=y0, YearEnd=y1, GregStart=g0, GregEnd=g1 };

        /// <summary>按游戏年份取朝代索引（年份驱动）</summary>
        public static int GetIndexByYear(int year, List<DynastyDefinition> all)
        {
            for (int i = 0; i < all.Count; i++)
                if (year >= all[i].YearStart && year < all[i].YearEnd) return i;
            return all.Count - 1;
        }

        /// <summary>按游戏年份取时代索引</summary>
        public static int GetEraByYear(int year, List<EraDefinition> eras)
        {
            for (int i = 0; i < eras.Count; i++)
                if (year >= eras[i].StartYear && year < eras[i].EndYear) return i;
            return eras.Count - 1;
        }

        /// <summary>游戏年份→公历年（在当前朝代区间内线性插值），负数=公元前</summary>
        public static int GetGregorian(int gameYear, DynastyDefinition d)
        {
            if (d == null) return -3000;
            float span = Mathf.Max(1, d.YearEnd - d.YearStart);
            float t = (gameYear - d.YearStart) / span;
            return d.GregStart + Mathf.RoundToInt(t * (d.GregEnd - d.GregStart));
        }

        /// <summary>公历格式化：负数=公元前xxxx年，0=公元1年</summary>
        public static string FormatGregorian(int gy)
        {
            if (gy < 0) return "公元前" + (-gy) + "年";
            if (gy == 0) return "公元1年";
            return "公元" + gy + "年";
        }
    }
}
