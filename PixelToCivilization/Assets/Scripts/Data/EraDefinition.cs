using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelToCivilization.Data
{
    /// <summary>
    /// 时代定义 —— 对齐 v5.9.9 ERAS（8时代，游戏年份驱动）
    /// </summary>
    [Serializable]
    public class EraDefinition
    {
        public int Id;
        public string Name;          // 三皇五帝·夏商周 ...
        public string Sub;           // 奴隶社会 ...
        public int StartYear;        // 游戏年份起
        public int EndYear;          // 游戏年份止
        public long ColorHex;        // 主题色
        public List<string> Unlocks = new();
        public string Feature;
        public string FeatureDetail;
        public BuildingStyle Style = new();

        public Color ThemeColor => new(
            ((ColorHex >> 16) & 255) / 255f,
            ((ColorHex >> 8) & 255) / 255f,
            (ColorHex & 255) / 255f);
    }

    /// <summary>建筑风格参数（程序网格生成用）</summary>
    [Serializable]
    public class BuildingStyle
    {
        public Color WallColor = new(0.7f, 0.6f, 0.4f);
        public Color RoofColor = new(0.4f, 0.3f, 0.2f);
        public Color AccentColor = new(0.8f, 0.2f, 0.2f);
        public Color WindowColor = new(0.3f, 0.3f, 0.5f);
        public Color RoadColor = new(0.5f, 0.45f, 0.35f);
        [Range(0.5f, 2f)] public float BuildingScale = 1f;
        [Range(0.5f, 4f)] public float BuildingHeight = 1f;
        public RoofType RoofType = RoofType.Thatched;
        public bool HasPillars, HasWindows, HasSecondFloor, HasBase, HasEaves, HasDome;
        [Range(0f, 1f)] public float EavesCurve;
        public bool IsEmissive, HasForceField;
        public Color EmissiveColor = Color.black;
    }

    public enum RoofType
    {
        Thatched, Hip, Curved, Imperial, Flat, Sawtooth, Gable, Dome, Energy, None
    }
}
