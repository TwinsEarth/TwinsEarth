using System.Collections.Generic;

namespace PixelToCivilization.Core
{
    /// <summary>字典取值扩展：键不存在时返回值类型默认值，兼容所有 .NET API 级别（替代 GetValueOrDefault）</summary>
    public static class DictExtensions
    {
        public static V Or<K,V>(this Dictionary<K,V> dict, K key)
        {
            if (dict!=null && dict.TryGetValue(key,out var v)) return v;
            return default;
        }
        public static V Or<K,V>(this Dictionary<K,V> dict, K key, V fallback)
        {
            if (dict!=null && dict.TryGetValue(key,out var v)) return v;
            return fallback;
        }
    }
}
