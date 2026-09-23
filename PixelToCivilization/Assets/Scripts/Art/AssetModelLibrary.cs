using System.Collections.Generic;
using UnityEngine;

namespace PixelToCivilization.Art
{
    /// <summary>
    /// V6.7.0 开源 CC0 真实 3D 模型库（运行时 Resources 加载）。
    /// 资源位于 Assets/Resources/Models3D/{Buildings,Ships,Cars}，全部为 CC0 协议：
    /// ·建筑=Quaternius Medieval Village Pack（House_1/House_3/Inn/Mill/Sawmill）
    /// ·船=Kenney Watercraft Kit（watercraftPack_001..029）
    /// ·车=Kenney Car Kit（sedan/suv/truck...）+ Kenney Fantasy Town Kit（cart 古代马车）
    /// 加载失败一律返回 null，由调用方回退到程序化模型，保证不会因缺资源崩溃。
    /// 注：Quaternius/Kenney 源 MTL 基色偏暗（为配合顶点色/图集），这里对暗色材质做保色相自适应提亮，
    /// 已经够亮的（白帆、彩色金属）保持不变。
    /// </summary>
    public static class AssetModelLibrary
    {
        const string Root = "Models3D/";
        static readonly Dictionary<string, GameObject> _cache = new();
        static readonly HashSet<string> _missing = new();
        static readonly Dictionary<Material, Material> _bright = new();
        static Material _fallback;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int Color = Shader.PropertyToID("_Color");

        /// <summary>按 Models3D 下相对路径（无扩展名）加载导入的模型预制体，带缓存；找不到返回 null（只判定一次）。</summary>
        public static GameObject Load(string relPath)
        {
            if (string.IsNullOrEmpty(relPath)) return null;
            if (_cache.TryGetValue(relPath, out var cached)) return cached;
            if (_missing.Contains(relPath)) return null;
            var prefab = Resources.Load<GameObject>(Root + relPath);
            if (prefab == null) { _missing.Add(relPath); Debug.LogWarning("[Models3D] 缺少模型资源: " + relPath); return null; }
            _cache[relPath] = prefab;
            return prefab;
        }

        /// <summary>
        /// 在 parent 下实例化真实模型，并做归一化：删除碰撞体、按 XZ 足迹等比缩放、XZ 居中、底部贴 yBase。
        /// fitLongest=true 时让模型较长边对齐足迹；false 时取 min 保证完整落在 footX×footZ 内。
        /// 返回承载节点；资源缺失返回 null。
        /// </summary>
        public static bool ToyMode = true; // V7.0.1 统一卡通程序化玩具风，停用写实 CC0 模型
        public static GameObject PlaceReal(Transform parent, string relPath, float footX, float footZ,
            float yBase = 0f, bool fitLongest = true, float yawDeg = 0f, string nodeName = "Real",
            Color? tint = null, float tintStrength = 0.45f)
        {
            if (ToyMode) return null; // V7.0.1
            var prefab = Load(relPath);
            if (prefab == null || parent == null) return null;

            var wrap = new GameObject(nodeName);
            wrap.transform.SetParent(parent, false);
            wrap.transform.localPosition = Vector3.zero;
            wrap.transform.localRotation = Quaternion.identity;
            wrap.transform.localScale = Vector3.one;

            var inst = Object.Instantiate(prefab, wrap.transform);
            inst.name = prefab.name;
            inst.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            inst.transform.localPosition = Vector3.zero;

            // 删除自带碰撞体（点击/移动碰撞由外部统一的 BoxCollider 负责，避免穿模与误触）
            foreach (var col in inst.GetComponentsInChildren<Collider>(true)) Object.Destroy(col);

            var rends = inst.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) { Object.Destroy(wrap); return null; }

            // 以包围盒计算等比缩放（先缩放再平移，保证中心/贴地准确）
            Bounds b = LocalBounds(wrap.transform, rends);
            float sx = footX / Mathf.Max(1e-4f, b.size.x);
            float sz = footZ / Mathf.Max(1e-4f, b.size.z);
            float s = fitLongest ? Mathf.Max(sx, sz) : Mathf.Min(sx, sz);
            if (!float.IsFinite(s) || s <= 0f) s = 1f;
            inst.transform.localScale = Vector3.one * s;

            Bounds b2 = LocalBounds(wrap.transform, rends);
            inst.transform.localPosition += new Vector3(-b2.center.x, yBase - b2.min.y, -b2.center.z);

            // 渲染设置：阴影 + 暗色材质保色相提亮 + 缺材质兜底
            foreach (var r in rends)
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
                var shared = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < shared.Length; i++)
                {
                    if (shared[i] == null) { shared[i] = Fallback(); changed = true; continue; }
                    var bm = Brighten(shared[i]);
                    // V6.8.1 可选阵营/朝代染色：基于提亮材质再实例化一份按 tint 混色（不污染缓存的共享材质）
                    if (tint.HasValue)
                    {
                        var tm = new Material(bm);
                        int tpid = tm.HasProperty(BaseColor) ? BaseColor : (tm.HasProperty(Color) ? Color : 0);
                        if (tpid != 0) { UnityEngine.Color bc0 = tm.GetColor(tpid); UnityEngine.Color nc0 = UnityEngine.Color.Lerp(bc0, tint.Value, tintStrength); nc0.a = bc0.a; tm.SetColor(tpid, nc0); }
                        bm = tm;
                    }
                    if (bm != shared[i]) { shared[i] = bm; changed = true; }
                }
                if (changed) r.sharedMaterials = shared;
            }
            return wrap;
        }

        /// <summary>暗色材质保色相提亮（最亮通道 &lt;0.4 时抬到约 0.62，倍率上限 5）；亮色原样返回。结果按源材质缓存。</summary>
        static Material Brighten(Material src)
        {
            if (_bright.TryGetValue(src, out var cached)) return cached;
            var m = new Material(src); // 实例化，避免污染导入的共享材质
            int pid = m.HasProperty(BaseColor) ? BaseColor : (m.HasProperty(Color) ? Color : 0);
            if (pid != 0)
            {
                Color c = m.GetColor(pid);
                float mx = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                if (mx > 1e-4f && mx < 0.55f)
                {
                    float f = Mathf.Clamp(0.78f / mx, 1f, 7f);
                    Color nc = c * f; nc.a = c.a;
                    m.SetColor(pid, nc);
                }
            }
            _bright[src] = m;
            return m;
        }

        /// <summary>计算一组渲染器相对 target 的本地轴对齐包围盒。</summary>
        static Bounds LocalBounds(Transform target, Renderer[] rends)
        {
            bool first = true; var b = new Bounds();
            foreach (var r in rends)
            {
                var wb = r.bounds; // 世界包围盒
                for (int cx = 0; cx < 2; cx++) for (int cy = 0; cy < 2; cy++) for (int cz = 0; cz < 2; cz++)
                {
                    var p = new Vector3(cx == 0 ? wb.min.x : wb.max.x, cy == 0 ? wb.min.y : wb.max.y, cz == 0 ? wb.min.z : wb.max.z);
                    var lp = target.InverseTransformPoint(p);
                    if (first) { b = new Bounds(lp, Vector3.zero); first = false; }
                    else b.Encapsulate(lp);
                }
            }
            return b;
        }

        static Material Fallback()
        {
            if (_fallback != null) return _fallback;
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _fallback = new Material(sh) { color = new Color(0.62f, 0.55f, 0.45f) };
            return _fallback;
        }
    }
}
