using System;
using UnityEngine;

namespace PixelToCivilization.Core
{
    /// <summary>
    /// V6.1.3 国家/势力实体：一处聚落对应一个方国；天下在「分裂并立(split)」与「大一统(unify)」间周期演化。
    /// 颜色用十六进制字符串存储（JsonUtility 友好），运行时经 Color 访问器解析。
    /// </summary>
    [Serializable]
    public class NationEntity
    {
        public int Id;
        public string Name = "";
        public string ColorHex = "d9402f";   // 无 # 前缀的十六进制 RGB
        public float Cx, Cz;                 // 国都（聚落中心）世界坐标
        public int VillageId;                // 对应聚落序号
        public bool IsPlayer;                // 是否玩家势力
        public bool Alive = true;            // 是否仍存续（被吞并则 false，分裂时可于旧村址复国）
        public int ContinentId = 1;          // V6.1.7 国都所在大陆（1=玩家主大陆），航海前异大陆互不兼并
        public int Pop;                      // 人口
        public float Power;                  // 国力（人口×浮动系数）
        public string Note = "";

        /// <summary>势力旗帜颜色（解析失败回退朱红）</summary>
        public Color Color => HexToColor(ColorHex);

        public static Color HexToColor(string hex)
        {
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString("#" + hex, out var c)) return c;
            return new Color(0.85f, 0.25f, 0.18f);
        }
    }
}
