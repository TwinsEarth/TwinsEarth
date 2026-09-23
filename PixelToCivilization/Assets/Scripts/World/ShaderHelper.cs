using System.Collections.Generic;
using UnityEngine;
using PixelToCivilization.Art;

namespace PixelToCivilization.World
{
    /// <summary>
    /// V6.1.1 统一材质工厂（URP PBR）：所有运行时材质走 Universal Render Pipeline/Lit，
    /// 叠加程序化 Albedo/Normal/Mask 贴图获得近 PBR 表面细节；保留 V5.9.9 的 Mat/Emissive/Trans 接口，
    /// 旧调用方零改动即可升级。找不到 URP Lit 时逐级回退，绝不变品红。
    /// </summary>
    public static class ShaderHelper
    {
        private static Shader _lit, _transparent, _vertexColor, _water;

        public static Shader Lit => _lit ??= Pick("Universal Render Pipeline/Lit", "Universal Render Pipeline/Simple Lit", "Standard", "Unlit/Color");
        // 兼容旧命名
        public static Shader Building => Lit;
        public static Shader Transparent => _transparent ??= Pick("Universal Render Pipeline/Transparent Unlit", "Universal Render Pipeline/Simple Lit", "Sprites/Default", "Unlit/Transparent");
        public static Shader VertexColor => _vertexColor ??= Lit; // 地形改为烘焙大贴图，仍用 URP Lit
        public static Shader Water => _water ??= Pick("PxC/WaterURP", "Universal Render Pipeline/Lit", "Sprites/Default");

        private static readonly Dictionary<int, Material> _matCache = new();

        private static Shader Pick(params string[] names)
        {
            foreach (var n in names) { var s = Shader.Find(n); if (s != null) return s; }
            return Shader.Find("Hidden/InternalErrorShader");
        }

        /// <summary>纯色不透明材质（旧接口，内部升级为带细微 PBR 纹理的 URP Lit）</summary>
        public static Material Mat(Color c) => Pbr(c, 0f, 0.35f, StableSeed(c));

        /// <summary>PBR 材质：金属度/光滑度可控，自动配程序贴图</summary>
        public static Material Pbr(Color baseColor, float metallic, float smoothness, int seed,
            float normalStrength = 1.2f, bool useDetail = true)
        {
            int key = HashKey(baseColor, metallic, smoothness, seed, useDetail ? 1 : 0, Mathf.RoundToInt(normalStrength * 10));
            if (_matCache.TryGetValue(key, out var cached)) return cached;

            var m = new Material(Lit) { name = "PBR_" + key };
            SetSurfaceOpaque(m);
            m.SetColor("_BaseColor", baseColor);
            m.color = baseColor;
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smoothness);

            // V6.7.1：仅用基础 URP/Lit 变体 + 细微 Albedo 变化；不再运行时启用
            // _NORMALMAP/_METALLICSPECGLOSSMAP 关键字（WebGL 构建会剥离这些运行时变体，导致物体发黑）
            if (useDetail)
            {
                var albedo = ProceduralTextures.Albedo(baseColor, seed);
                m.SetTexture("_BaseMap", albedo);
            }
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smoothness);
            m.renderQueue = 2000;
            _matCache[key] = m;
            return m;
        }

        /// <summary>自发光材质（霓虹/能量/未来建筑）</summary>
        public static Material Emissive(Color c, Color e)
        {
            var m = new Material(Lit);
            SetSurfaceOpaque(m);
            m.SetColor("_BaseColor", c); m.color = c;
            m.SetColor("_EmissionColor", e * 2.2f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return m;
        }

        /// <summary>半透明材质（能量罩/玻璃/水保底）</summary>
        public static Material Trans(Color c)
        {
            var m = new Material(Lit);
            ConfigureTransparent(m);
            m.SetColor("_BaseColor", c); m.color = c;
            m.SetFloat("_Smoothness", 0.75f);
            return m;
        }

        // ---------- URP Lit 渲染状态 ----------
        public static void SetSurfaceOpaque(Material m)
        {
            m.SetOverrideTag("RenderType", "Opaque");
            m.SetFloat("_Surface", 0);
            m.SetFloat("_Blend", 0);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            m.SetInt("_ZWrite", 1);
            m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 2000;
        }

        public static void ConfigureTransparent(Material m)
        {
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_Surface", 1);
            m.SetFloat("_Blend", 0); // Alpha blend
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
        }

        private static int StableSeed(Color c) =>
            Mathf.RoundToInt(c.r * 255) * 65536 + Mathf.RoundToInt(c.g * 255) * 256 + Mathf.RoundToInt(c.b * 255);
        private static int HashKey(Color c, float a, float b, int s, int d, int e)
        {
            int h = StableSeed(c) * 397 ^ Mathf.RoundToInt(a * 100) * 17 ^ Mathf.RoundToInt(b * 100) * 31 ^ s * 7 ^ d * 13 ^ e;
            return h & 0x7fffffff;
        }

        public static void ClearCache() { foreach (var m in _matCache.Values) if (m) Object.Destroy(m); _matCache.Clear(); }
    }
}
