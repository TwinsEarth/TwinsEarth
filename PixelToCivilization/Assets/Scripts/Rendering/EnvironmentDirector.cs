using UnityEngine;

namespace PixelToCivilization.Rendering
{
    /// <summary>
    /// V6.1.1 环境导演：运行时搭建电影级自然光照——主方向光（软阴影）、三波段环境光、
    /// 程序化天空盒、指数高度雾；昼夜系数可由游戏年份/调试面板驱动，默认黄金时刻偏正午以保证观感。
    /// </summary>
    public class EnvironmentDirector : MonoBehaviour
    {
        public static EnvironmentDirector Instance { get; private set; }

        public Light Sun { get; private set; }
        Material _sky;
        public float DayFactor = 1f;          // 0=夜 1=昼
        public float SunAzimuth = 35f;        // 水平方位角
        public float SunElevation = 52f;      // 仰角
        public bool AutoDayNight = false;     // 是否随真实时间缓慢循环（默认关，固定美观时刻）

        // 调色板
        static readonly Color ZenithDay = new(0.10f, 0.55f, 0.92f);  // V7.0.1
        static readonly Color HorizonDay = new(0.55f, 0.83f, 1.00f); // V7.0.1
        static readonly Color SunWarm = new(1.0f, 0.975f, 0.88f);
        static readonly Color FogDay = new(0.60f, 0.83f, 1.00f);    // V7.0.1

        void Awake() { Instance = this; Build(); }

        public void Build()
        {
            // 主方向光
            var lightGo = new GameObject("Sun_Directional");
            lightGo.transform.SetParent(transform);
            Sun = lightGo.AddComponent<Light>();
            Sun.type = LightType.Directional;
            Sun.shadows = LightShadows.Soft;
            Sun.shadowStrength = 0.5f;
            Sun.shadowBias = -0.0004f;
            Sun.shadowNormalBias = 0.4f;
            Sun.color = SunWarm;
            RenderSettings.sun = Sun;   // V6.7.1 显式指定 URP 主方向光，避免运行时主光丢失导致物体发黑

            // 天空盒
            var skyShader = Shader.Find("PxC/SkyDome");
            if (skyShader != null)
            {
                _sky = new Material(skyShader) { name = "RuntimeSky" };
                RenderSettings.skybox = _sky;
            }

            // 三波段环境光（天空/赤道/地面），PBR 间接光基础
            // 平坦地形法线严格朝上、会吃满天空环境光，若过亮会把草色冲淡成米白，故整体压低、保留饱和
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.72f, 0.85f, 0.96f);  // V7.0.1 提亮
            RenderSettings.ambientEquatorColor = new Color(0.80f, 0.78f, 0.70f);
            RenderSettings.ambientGroundColor = new Color(0.62f, 0.70f, 0.55f);
            RenderSettings.ambientIntensity = 1.35f;

            // 环境反射（让金属/水面有反射）
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 0.6f;

            // 高度雾：指数平方，远处柔化地平线
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.00055f;  // V7.0.1 减雾
            RenderSettings.fogColor = FogDay;

            // 相机用天空盒清屏
            var cam = Camera.main;
            if (cam != null) cam.clearFlags = CameraClearFlags.Skybox;

            ApplySun();
            // V6.7.1 一次性光照自检（发黑排查）：输出主光/环境光/URP Lit 是否被设备支持
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            Debug.Log($"[LIGHT] device={SystemInfo.graphicsDeviceType} litFound={(lit!=null)} litSupported={(lit!=null&&lit.isSupported)} sun.intensity={Sun.intensity} sunEnabled={Sun.enabled} ambientMode={RenderSettings.ambientMode} ambientIntensity={RenderSettings.ambientIntensity} equator={RenderSettings.ambientEquatorColor}");
        }

        void Update()
        {
            if (AutoDayNight)
            {
                // 一个完整昼夜约 12 分钟，便于观察；接入游戏年份时可由外部 SetPhase 覆盖
                float phase = Mathf.Repeat(Time.time / 720f, 1f);
                SetPhase(phase);
            }
        }

        /// <summary>phase 0..1：0/0.5 为夜，0.25 为正午</summary>
        public void SetPhase(float phase)
        {
            float elev = Mathf.Sin(phase * Mathf.PI * 2f - Mathf.PI * 0.5f) * 0.5f + 0.5f; // 0..1
            SunElevation = Mathf.Lerp(-8f, 80f, elev);
            DayFactor = Mathf.Clamp01((SunElevation + 4f) / 14f);
            ApplySun();
        }

        public void SetDayFactor(float d) { DayFactor = Mathf.Clamp01(d); ApplySun(); }

        void ApplySun()
        {
            if (Sun == null) return;
            // 由方位角/仰角算方向
            float az = SunAzimuth * Mathf.Deg2Rad, el = SunElevation * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Cos(el) * Mathf.Cos(az), Mathf.Sin(el), Mathf.Cos(el) * Mathf.Sin(az)).normalized;
            Sun.transform.rotation = Quaternion.LookRotation(-dir);
            Sun.intensity = Mathf.Lerp(0.30f, 1.65f, DayFactor);  // V7.0.1 明亮阳光   // V6.7.1 夜间也保留亮度下限，杜绝物体一团黑
            // 日出日落偏暖
            float warm = 1f - Mathf.Clamp01(Mathf.Abs(SunElevation - 30f) / 40f);
            Sun.color = Color.Lerp(new Color(0.5f, 0.58f, 0.8f), Color.Lerp(new Color(1f, 0.62f, 0.36f), SunWarm, DayFactor), DayFactor);
            Sun.color = Color.Lerp(Sun.color, new Color(1f, 0.6f, 0.35f), warm * 0.5f * DayFactor);
            Sun.enabled = SunElevation > -12f;

            if (_sky != null)
            {
                _sky.SetVector("_SunDir", new Vector4(dir.x, dir.y, dir.z, 0));
                _sky.SetFloat("_DayFactor", DayFactor);
            }

            // 雾与环境光随昼夜
            RenderSettings.fogColor = Color.Lerp(new Color(0.05f, 0.06f, 0.1f), FogDay, DayFactor);
            RenderSettings.ambientIntensity = Mathf.Lerp(0.62f, 1.25f, DayFactor);
        }
    }
}
