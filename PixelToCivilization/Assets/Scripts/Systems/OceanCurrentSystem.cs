using UnityEngine;
using PixelToCivilization.Core;

namespace PixelToCivilization.Systems
{
    /// <summary>
    /// V6.1.9(i) 洋流 & 海风系统：
    /// 用多层低频正弦构造一张缓慢演化的海洋矢量场（确定性、无贴图、零 GC），叠加全局海风方向。
    /// 职责：①给帆船提供顺风/逆风动力（SailFactor / 自由漂流 Drift）；②给鱼群提供洄游本能（沿洋流缓漂）。
    /// 天气系统可通过 WeatherMult 改变风场强度（雾天减弱、风暴/龙卷增强）。
    /// </summary>
    public class OceanCurrentSystem : GameSystemBase
    {
        public static OceanCurrentSystem Instance { get; private set; }

        private float _evolve;
        /// <summary>天气对风场的整体倍率（WeatherSystem 写入）</summary>
        public float WeatherMult = 1f;

        public override void Init(GameManager gm)
        {
            base.Init(gm);
            Instance = this;
            _evolve = Random.value * 100f;
        }

        public override void Tick(float dt)
        {
            _evolve += Mathf.Min(dt, 0.1f) * 0.5f;          // 缓慢演化
            if (S != null)
            {
                // 全局海风主方向缓慢旋转（约 2~3 分钟转一圈），风力在 0.45~1.05 间起伏
                S.WindDir += Mathf.Min(dt, 0.1f) * 0.04f;
                float target = 0.75f + 0.30f * Mathf.Sin(_evolve * 0.13f);
                S.WindStr = Mathf.Lerp(S.WindStr, target * WeatherMult, Mathf.Min(dt, 0.1f) * 0.5f);
            }
        }

        /// <summary>采样某世界坐标的合成流场（洋流+海风），返回平面向量；模长约 0.1~1.3</summary>
        public Vector2 SampleFlow(float x, float z)
        {
            float t = _evolve;
            float wind = S != null ? S.WindDir : 0f;
            float a = Mathf.Sin(x * 0.0035f + t * 0.05f)
                    + Mathf.Cos(z * 0.0041f - t * 0.04f)
                    + Mathf.Sin((x + z) * 0.0022f + t * 0.03f);
            float ang = a * 1.4f + wind;
            float str = 0.55f + 0.45f * Mathf.Sin(x * 0.006f - t * 0.02f) * Mathf.Cos(z * 0.005f + t * 0.015f);
            str = Mathf.Clamp(str, 0.20f, 1.20f) * (S != null ? S.WindStr : 0.7f);
            return new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * str;
        }

        /// <summary>帆船动力倍率：与流场同向最快、逆向最慢（顺风约 1.35，逆风约 0.65）；moveDir 为单位航向</summary>
        public float SailFactor(float x, float z, Vector2 moveDir)
        {
            Vector2 f = SampleFlow(x, z);
            if (f.sqrMagnitude < 1e-4f || moveDir.sqrMagnitude < 1e-4f) return 1f;
            float align = Vector2.Dot(moveDir.normalized, f.normalized) * Mathf.Clamp01(f.magnitude / 1.2f);
            return Mathf.Clamp(1f + 0.35f * align, 0.65f, 1.35f);
        }

        /// <summary>无动力自由漂流位移（鱼群洄游 / 民用船随波），返回本帧位移</summary>
        public Vector2 Drift(float x, float z, float dt, float driftSpeed = 1.0f)
            => SampleFlow(x, z) * (0.35f * driftSpeed) * Mathf.Min(dt, 0.1f);

        /// <summary>风向中文（8 方位），供 UI</summary>
        public string WindText
        {
            get
            {
                if (S == null) return "";
                string[] dir = { "北", "东北", "东", "东南", "南", "西南", "西", "西北" };
                float deg = S.WindDir * Mathf.Rad2Deg;
                int i = Mathf.RoundToInt(deg / 45f) & 7;
                int lv = S.WindStr < 0.4f ? 0 : S.WindStr < 0.75f ? 1 : S.WindStr < 1.05f ? 2 : 3;
                string[] lvName = { "无风", "微风", "和风", "劲风" };
                return "🌬" + dir[i] + lvName[lv];
            }
        }
    }
}
