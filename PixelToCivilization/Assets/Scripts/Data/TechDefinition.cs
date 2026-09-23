using System.Collections.Generic;

namespace PixelToCivilization.Data
{
    /// <summary>科技定义 —— 对齐 v5.9.9 TECH_DEFS（37项）</summary>
    [System.Serializable]
    public class TechDefinition
    {
        public string Id;
        public string Name;
        public int Era;
        public int Cost;              // 研究点消耗
        public string Desc;
        public string[] Requires;     // 前置科技

        public bool HasRequirement => Requires != null && Requires.Length > 0;
    }

    /// <summary>政策定义 —— 对齐 v5.9.9 POLICY_DEFS（15项）</summary>
    [System.Serializable]
    public class PolicyDefinition
    {
        public string Id;
        public string Name;
        public int Era;
        public string Desc;
        public string Risk;                 // 风险类型，如 rebellion
        public int Morale;                  // 民心影响
        public Dictionary<string, float> Effect;
    }
}
