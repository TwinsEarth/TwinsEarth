using System.Collections.Generic;

namespace PixelToCivilization.Data
{
    /// <summary>
    /// 建筑静态定义 —— 对齐 v5.9.9 BUILDING_DEFS（67种）
    /// </summary>
    [System.Serializable]
    public class BuildingDefinition
    {
        public string Id;            // 类型id，如 hut
        public string Name;          // 中文名
        public string Cat;           // 分类：居住/基础/食物/资源/经济/文化/工业/军事/海洋/能源/科技/太空
        public string Icon;          // emoji图标
        public int Era;              // 所属时代 0-7
        public string Desc;          // 描述
        public Dictionary<string, int> Cost;          // 建造成本
        public Dictionary<string, float> Production;  // 资源产出
        public Dictionary<string, float> Func;        // 功能数值(housing/defense/attack/range/soldiers...)
        public string[] ClassReq;    // 居住建筑允许的社会阶层

        public float GetProd(string key) => Production != null && Production.TryGetValue(key, out var v) ? v : 0f;
        public float GetFunc(string key) => Func != null && Func.TryGetValue(key, out var v) ? v : 0f;
        public int GetCost(string key) => Cost != null && Cost.TryGetValue(key, out var v) ? v : 0;
        public bool HasCost => Cost != null && Cost.Count > 0;
    }
}
