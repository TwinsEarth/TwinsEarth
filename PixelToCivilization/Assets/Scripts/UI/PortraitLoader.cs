using System.Collections.Generic;
using UnityEngine;

namespace PixelToCivilization.UI
{
    /// <summary>
    /// V6.1.3 浮窗实拍图加载器：Resources/Portraits/{building|ship|cart}/{id}_{level}.jpg
    /// 等级 1 普通 / 2 精良 / 3 传奇，图片随实体等级切换；缺失返回 null（浮窗回退到 Emoji 文本，不报错）。
    /// </summary>
    public static class PortraitLoader
    {
        static readonly Dictionary<string,Texture2D> _cache = new();
        public static Texture2D Load(string category,string id,int level)
        {
            if(string.IsNullOrEmpty(category)||string.IsNullOrEmpty(id)) return null;
            int lv=Mathf.Clamp(level,1,3);
            string key=category+"/"+id+"_"+lv;
            if(_cache.TryGetValue(key,out var cached)) return cached;
            var tex=Resources.Load<Texture2D>("Portraits/"+key);
            _cache[key]=tex; // 缺失也缓存，避免每帧重复 Load
            return tex;
        }
    }
}
