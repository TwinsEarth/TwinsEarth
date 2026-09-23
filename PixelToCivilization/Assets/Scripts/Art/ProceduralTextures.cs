using System.Collections.Generic;
using UnityEngine;

namespace PixelToCivilization.Art
{
    /// <summary>
    /// V6.1.1 程序化 PBR 贴图工厂：用可平铺(tileable)的 Value/FBM 噪声在运行时生成
    /// Albedo / Normal / Metallic-Smoothness-AO Mask，替代 V5.9.9 的纯色材质，达成近 PBR 细节。
    /// 全部确定性（按种子），带缓存；分辨率可按机型降档。
    /// </summary>
    public static class ProceduralTextures
    {
        public static int Res = 256;                 // 手机端可降到 128
        static Dictionary<int, Texture2D> _cache = new();
        static Dictionary<int, byte[]> _heightBytes = new();

        // ---------- 底层可平铺 Value Noise + FBM ----------
        static float Hash(int x, int y, int seed)
        {
            int h = x * 374761393 + y * 668265263 + seed * 144269504;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0x7fffffff) / (float)0x7fffffff;
        }
        static float Smooth(float t) => t * t * (3f - 2f);
        static float ValueNoise(int x, int y, int seed)
        {
            return Hash(x, y, seed);
        }
        /// <summary>双线性插值的可平铺噪声（period=n）</summary>
        static float BilerpNoise(float x, float y, int period, int seed)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            int x0 = Mod(xi, period), x1 = Mod(xi + 1, period);
            int y0 = Mod(yi, period), y1 = Mod(yi + 1, period);
            float a = ValueNoise(x0, y0, seed), b = ValueNoise(x1, y0, seed);
            float c = ValueNoise(x0, y1, seed), d = ValueNoise(x1, y1, seed);
            float u = Smooth(xf), v = Smooth(yf);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }
        static int Mod(int a, int m) { int r = a % m; return r < 0 ? r + m : r; }

        /// <summary>分形布朗运动，输出约 0~1；octaves 控制层次</summary>
        public static float Fbm(float x, float y, int period, int seed, int octaves = 4)
        {
            float amp = 0.5f, freq = 1f, sum = 0f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                float px = x / period * freq * period;
                float py = y / period * freq * period;
                sum += BilerpNoise(px, py, Mathf.Max(2, Mathf.RoundToInt(period * freq)), seed + o * 101) * amp;
                norm += amp; amp *= 0.5f; freq *= 2f;
            }
            return Mathf.Clamp01(sum / Mathf.Max(0.0001f, norm));
        }

        /// <summary>生成一张灰度 FBM 高度/噪声图（用于法线、遮罩）</summary>
        public static Texture2D HeightNoise(int seed, int res, float scale, int octaves = 4)
        {
            int key = HashKey(seed, res, Mathf.RoundToInt(scale * 100), octaves, 900);
            if (_cache.TryGetValue(key, out var t)) return t;
            var data = GetHeightData(seed, res, scale, octaves, key);
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear };
            var col = new Color32[res * res];
            for (int i = 0; i < col.Length; i++)
            {
                byte g = data[i];
                col[i] = new Color32(g, g, g, 255);
            }
            tex.SetPixels32(col); tex.Apply(true, false);
            tex.anisoLevel = 4;
            _cache[key] = tex; return tex;
        }

        /// <summary>高度图逐像素字节数据（与 HeightNoise 同源同缓存），供 PBR 通道按像素采样；
        /// 不走 GetRawTextureData，避免引擎对单通道格式步长差异导致的越界</summary>
        public static byte[] HeightBytes(int seed, int res, float scale, int octaves = 4)
            => GetHeightData(seed, res, scale, octaves,
                HashKey(seed, res, Mathf.RoundToInt(scale * 100), octaves, 900));

        static byte[] GetHeightData(int seed, int res, float scale, int octaves, int key)
        {
            if (_heightBytes.TryGetValue(key, out var b)) return b;
            var bytes = new byte[res * res];
            int period = Mathf.Max(2, Mathf.RoundToInt(scale));
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    float n = Fbm(x * period / (float)res, y * period / (float)res, period, seed, octaves);
                    bytes[y * res + x] = (byte)Mathf.Clamp(Mathf.RoundToInt(n * 255f), 0, 255);
                }
            _heightBytes[key] = bytes; return bytes;
        }

        // ---------- Albedo：基色 + 细微明暗/色斑 ----------
        public static Texture2D Albedo(Color baseColor, int seed, int res = -1, float variation = 0.045f)
        {
            res = res < 0 ? Res : res;
            int key = HashKey(seed, res, Mathf.RoundToInt(variation * 1000), ColorKey(baseColor), 100);
            if (_cache.TryGetValue(key, out var t)) return t;
            var npx = HeightBytes(seed + 7, res, 6, 4);
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear };
            var col = new Color32[res * res];
            for (int i = 0; i < col.Length; i++)
            {
                float v = 1f + (npx[i] / 255f - 0.5f) * 2f * variation;
                // V6.7.1 关键修复：贴图只给中性明度变化，基色由材质 _BaseColor 提供。
                // 旧实现把基色烘进贴图，URP 又乘一次 _BaseColor，等于基色平方，深色材质被压成纯黑。
                col[i] = new Color(v, v, v, 1f);
            }
            tex.SetPixels32(col); tex.Apply(true, false); tex.anisoLevel = 4;
            _cache[key] = tex; return tex;
        }

        // ---------- Normal：从 FBM 高度图 Sobel 求法线 ----------
        public static Texture2D Normal(int seed, int res = -1, float strength = 1.6f, float scale = 5f)
        {
            res = res < 0 ? Res : res;
            int key = HashKey(seed, res, Mathf.RoundToInt(strength * 100), Mathf.RoundToInt(scale * 100), 200);
            if (_cache.TryGetValue(key, out var t)) return t;
            var hpx = HeightBytes(seed + 21, res, Mathf.RoundToInt(scale), 4);
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear };
            var col = new Color32[res * res];
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    float hl = hpx[y * res + Mod(x - 1, res)] / 255f;
                    float hr = hpx[y * res + Mod(x + 1, res)] / 255f;
                    float hd = hpx[Mod(y - 1, res) * res + x] / 255f;
                    float hu = hpx[Mod(y + 1, res) * res + x] / 255f;
                    Vector3 n = new Vector3((hl - hr) * strength, (hd - hu) * strength, 1f).normalized;
                    col[y * res + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            tex.SetPixels32(col); tex.Apply(true, false); tex.anisoLevel = 4;
            _cache[key] = tex; return tex;
        }

        // ---------- Mask：R=Metallic G=AO B=0 A=Smoothness（URP metallic 工作流）----------
        public static Texture2D Mask(float metallic, float smoothness, int seed, int res = -1, float aoVar = 0.12f)
        {
            res = res < 0 ? Res : res;
            int key = HashKey(seed, res, Mathf.RoundToInt(metallic * 100), Mathf.RoundToInt(smoothness * 100), 300);
            if (_cache.TryGetValue(key, out var t)) return t;
            var npx = HeightBytes(seed + 33, res, 8, 3);
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear };
            var col = new Color32[res * res];
            for (int i = 0; i < col.Length; i++)
            {
                float nv = npx[i] / 255f;
                byte ao = (byte)Mathf.Clamp(Mathf.RoundToInt((1f - (nv - 0.5f) * 2f * aoVar) * 255f), 0, 255);
                byte sm = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(smoothness + (nv - 0.5f) * 0.12f) * 255f), 0, 255);
                col[i] = new Color32((byte)Mathf.RoundToInt(metallic * 255f), ao, 0, sm);
            }
            tex.SetPixels32(col); tex.Apply(true, false);
            _cache[key] = tex; return tex;
        }

        static int ColorKey(Color c) =>
            Mathf.RoundToInt(c.r * 255) * 65536 + Mathf.RoundToInt(c.g * 255) * 256 + Mathf.RoundToInt(c.b * 255);
        static int HashKey(int a, int b, int c, int d, int salt)
        {
            int h = a * 73856093 ^ b * 19349663 ^ c * 83492791 ^ d * 2654435761u.GetHashCode() ^ salt;
            return h & 0x7fffffff;
        }

        /// <summary>释放全部缓存贴图（切换画质档位/低内存时）</summary>
        public static void Clear()
        {
            foreach (var t in _cache.Values) if (t) Object.Destroy(t);
            _cache.Clear();
            _heightBytes.Clear();
        }
    }
}
