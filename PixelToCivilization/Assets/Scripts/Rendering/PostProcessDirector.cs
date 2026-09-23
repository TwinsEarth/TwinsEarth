using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PixelToCivilization.Rendering
{
    /// <summary>
    /// V6.1.1 后处理导演：全局 Volume 电影级调色——ACES 色调映射、Bloom、对比/饱和、
    /// 白平衡、暗角、轻微色散与胶片颗粒；同时为相机开启 URP 后处理与 FXAA。
    /// 手机端可调用 SetQuality(false) 关闭昂贵项。
    /// </summary>
    public class PostProcessDirector : MonoBehaviour
    {
        public static PostProcessDirector Instance { get; private set; }
        VolumeProfile _profile;
        Bloom _bloom; ChromaticAberration _ca; FilmGrain _grain; DepthOfField _dof;
        public bool Cinematic = true;

        public static PostProcessDirector Ensure(Camera cam)
        {
            if (Instance != null) return Instance;
            var go = new GameObject("GlobalPostProcess");
            var dir = go.AddComponent<PostProcessDirector>();
            dir.Build(cam);
            return dir;
        }

        public void Build(Camera cam)
        {
            Instance = this;
            var vol = gameObject.AddComponent<Volume>();
            vol.isGlobal = true; vol.weight = 1f; vol.priority = 1f;
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            vol.sharedProfile = _profile;

            // 色调映射 Neutral：比 ACES 更保色相/饱和，贴合参考图鲜亮平涂的地图配色
            var tone = _profile.Add<Tonemapping>(true);
            tone.mode.value = TonemappingMode.Neutral;

            // Bloom：高光泛光
            _bloom = _profile.Add<Bloom>(true);
            _bloom.threshold.value = 0.88f;
            _bloom.intensity.value = 0.42f;
            _bloom.scatter.value = 0.72f;
            _bloom.tint.value = new Color(1f, 0.96f, 0.9f);
            _bloom.highQualityFiltering.value = true;

            // 颜色调整：轻微提对比、饱和、暖白滤色
            var grade = _profile.Add<ColorAdjustments>(true);
            grade.postExposure.value = 0.05f;
            grade.contrast.value = 8f;
            grade.saturation.value = 22f;
            grade.colorFilter.value = new Color(1f, 1f, 0.98f, 1f);

            // 白平衡：中性（去暖偏，保证草地为正绿、海水为正蓝）
            var wb = _profile.Add<WhiteBalance>(true);
            wb.temperature.value = 0f;

            // 暗角
            var vig = _profile.Add<Vignette>(true);
            vig.intensity.value = 0.30f;
            vig.smoothness.value = 0.62f;
            vig.color.value = new Color(0.04f, 0.03f, 0.05f, 1f);

            // 轻微色散 / 胶片颗粒（电影感，低强度）
            _ca = _profile.Add<ChromaticAberration>(true);
            _ca.intensity.value = 0.12f;
            _grain = _profile.Add<FilmGrain>(true);
            _grain.type.value = FilmGrainLookup.Medium1;
            _grain.intensity.value = 0.10f; _grain.response.value = 0.8f;

            // 景深（远景轻微，增强层次；手机端关）
            _dof = _profile.Add<DepthOfField>(true);
            _dof.mode.value = DepthOfFieldMode.Bokeh;
            _dof.focusDistance.value = 220f;
            _dof.focalLength.value = 28f;
            _dof.aperture.value = 5.6f;

            // 相机开关
            if (cam != null)
            {
                var data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
                data.antialiasingQuality = AntialiasingQuality.High;
                cam.allowHDR = true;
                cam.allowMSAA = true;
            }
        }

        /// <summary>画质档位：true=电影级(PC/主机)，false=性能档(手机/WebGL)</summary>
        public void SetQuality(bool cinematic)
        {
            Cinematic = cinematic;
            if (_bloom != null) { _bloom.highQualityFiltering.value = cinematic; _bloom.intensity.value = cinematic ? 0.42f : 0.25f; }
            if (_ca != null) _ca.intensity.value = cinematic ? 0.12f : 0f;
            if (_grain != null) _grain.intensity.value = cinematic ? 0.10f : 0f;
            if (_dof != null) _dof.active = cinematic;
        }
    }
}
