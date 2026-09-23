using UnityEngine;
using PixelToCivilization.Core;
using PixelToCivilization.World;

namespace PixelToCivilization.Systems
{
    /// <summary>V6.1.9(i) 天气系统：晴/多云/雨/雪/雾/晚霞/雷电/龙卷风，按权重随机轮换；
    /// 调节主光与雾、在相机头顶下雨雪、雷电瞬闪、龙卷风漏斗；并通过 OceanCurrentSystem.WeatherMult 联动海风。</summary>
    public class WeatherSystem : GameSystemBase
    {
        public enum W { Clear=0, Cloudy=1, Rain=2, Snow=3, Fog=4, Sunset=5, Thunder=6, Tornado=7 }

        // 权重（合计 100）
        static readonly int[] Weight = { 26, 20, 16, 7, 9, 8, 11, 3 };
        static readonly string[] Icon = { "☀晴天", "⛅多云", "🌧下雨", "❄下雪", "🌫大雾", "🌇晚霞", "⛈雷电", "🌪龙卷风" };

        private Light _sun;
        private ParticleSystem _rain, _snow;
        private GameObject _tornado;
        private Transform _tornadoSpin;
        private float _flashCd, _flashT, _tornadoAng;
        private Vector3 _tornadoTarget;
        private Color _baseFog, _baseSun;
        private float _baseFogDensity, _baseSunIntensity = 1.25f;
        private bool _inited;

        public W Current { get; private set; } = W.Clear;
        public string CurrentText => Icon[(int)Current];

        public override void Init(GameManager gm)
        {
            base.Init(gm);
            BuildFx();
            Current = W.Clear;
            if (S != null) S.WeatherKind = 0;
        }

        public override void Tick(float dt)
        {
            if (S == null) return;
            dt = Mathf.Min(dt, 0.1f);
            EnsureSun();
            S.WeatherTimer -= dt;
            if (S.WeatherTimer <= 0f) RollNext();
            Current = (W)S.WeatherKind;
            ApplyLook(dt);
            FollowCamera(dt);
        }

        /// <summary>强制立即轮换到下一种天气（Debug/浏览器回归用）</summary>
        public void ForceNext(){ RollNext(); ApplyLook(0.016f); }
        private void RollNext()
        {
            int total = 0; foreach (var w in Weight) total += w;
            int roll = Random.Range(0, total), acc = 0, pick = 0;
            for (int i = 0; i < Weight.Length; i++) { acc += Weight[i]; if (roll < acc) { pick = i; break; } }
            // 龙卷风/雷电在最早时代也允许但更稀有：连续两次相同极端则改多云
            if ((W)pick == Current && (Current == W.Tornado || Current == W.Thunder)) pick = (int)W.Cloudy;
            S.WeatherKind = pick; Current = (W)pick;
            S.WeatherTimer = Random.Range(34f, 82f);
            EnterWeather(Current);
            GM?.AddEvent("info", "天气变化：" + Icon[pick]);
        }

        private void EnterWeather(W w)
        {
            if (_rain != null) { var e = _rain.emission; e.enabled = (w == W.Rain || w == W.Thunder || w == W.Tornado); }
            if (_snow != null) { var e = _snow.emission; e.enabled = (w == W.Snow); }
            if (_tornado != null) _tornado.SetActive(w == W.Tornado);
            _flashCd = Random.Range(2.5f, 6f); _flashT = 0f;
            if (w == W.Tornado && Camera.main != null)
            {
                var c = Camera.main.transform.position;
                _tornadoTarget = c + new Vector3(Random.Range(-40f, 40f), 0f, Random.Range(-40f, 40f));
            }
            var flow = OceanCurrentSystem.Instance;
            if (flow != null)
                flow.WeatherMult = w == W.Fog ? 0.5f : (w == W.Rain ? 1.25f : (w == W.Thunder ? 1.4f : (w == W.Tornado ? 2f : 1f)));
        }

        private void ApplyLook(float dt)
        {
            if (_sun == null) return;
            // 目标光照/雾
            Color sunC = new(1f, 0.975f, 0.88f), fogC = _baseFog;
            float sunI = _baseSunIntensity, fogD = _baseFogDensity;
            switch (Current)
            {
                case W.Clear: break;
                case W.Cloudy: sunI = 1.05f; sunC = new Color(0.92f, 0.94f, 1f); fogC = new Color(0.72f, 0.76f, 0.82f); fogD = 0.0011f; break;
                case W.Rain: sunI = 0.80f; sunC = new Color(0.78f, 0.82f, 0.9f); fogC = new Color(0.52f, 0.58f, 0.66f); fogD = 0.0017f; break;
                case W.Snow: sunI = 0.95f; sunC = new Color(0.95f, 0.97f, 1f); fogC = new Color(0.86f, 0.88f, 0.92f); fogD = 0.0015f; break;
                case W.Fog: sunI = 0.90f; fogC = new Color(0.80f, 0.82f, 0.84f); fogD = 0.0046f; break;
                case W.Sunset: sunI = 1.12f; sunC = new Color(1f, 0.60f, 0.32f); fogC = new Color(0.98f, 0.55f, 0.42f); fogD = 0.00115f; break;
                case W.Thunder: sunI = 0.70f; sunC = new Color(0.72f, 0.76f, 0.88f); fogC = new Color(0.34f, 0.38f, 0.48f); fogD = 0.0021f; break;
                case W.Tornado: sunI = 0.72f; sunC = new Color(0.70f, 0.72f, 0.80f); fogC = new Color(0.40f, 0.42f, 0.46f); fogD = 0.0026f; break;
            }
            // 雷电瞬闪
            if (Current == W.Thunder)
            {
                _flashCd -= dt;
                if (_flashCd <= 0f) { _flashT = 0.14f; _flashCd = Random.Range(3f, 7f); }
                if (_flashT > 0f) { _flashT -= dt; sunI = 2.2f; sunC = Color.white; }
            }
            _sun.intensity = Mathf.Lerp(_sun.intensity, sunI, dt * 2f);
            _sun.color = Color.Lerp(_sun.color, sunC, dt * 2f);
            RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, fogD, dt * 2f);
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, fogC, dt * 2f);

            // 龙卷风旋转游走
            if (Current == W.Tornado && _tornado != null && _tornado.activeSelf)
            {
                _tornadoAng += dt * 6f;
                if (_tornadoSpin != null) _tornadoSpin.Rotate(0f, dt * 360f, 0f, Space.Self);
                _tornado.transform.position = Vector3.Lerp(_tornado.transform.position, _tornadoTarget, dt * 0.4f);
                if ((_tornado.transform.position - _tornadoTarget).sqrMagnitude < 36f && Camera.main != null)
                {
                    var c = Camera.main.transform.position;
                    _tornadoTarget = c + new Vector3(Random.Range(-60f, 60f), 0f, Random.Range(-60f, 60f));
                }
            }
        }

        private void FollowCamera(float dt)
        {
            var cam = Camera.main; if (cam == null) return;
            Vector3 cp = cam.transform.position;
            if (_rain != null && _rain.gameObject.activeSelf) _rain.transform.position = new Vector3(cp.x, cp.y + 55f, cp.z);
            if (_snow != null && _snow.gameObject.activeSelf) _snow.transform.position = new Vector3(cp.x, cp.y + 50f, cp.z);
        }

        private void EnsureSun()
        {
            if (_sun != null) return;
            var ed = Rendering.EnvironmentDirector.Instance;
            if (ed != null) _sun = ed.Sun;
            if (_sun == null) { var l = Object.FindObjectOfType<Light>(); if (l != null && l.type == LightType.Directional) _sun = l; }
            if (!_inited && _sun != null)
            {
                _baseSunIntensity = _sun.intensity; _baseSun = _sun.color;
                _baseFog = RenderSettings.fogColor; _baseFogDensity = RenderSettings.fogDensity;
                _inited = true;
            }
        }

        // ============ 程序化雨雪粒子 / 龙卷风漏斗 ============
        private void BuildFx()
        {
            _rain = MakePrecip("RainFX", new Color(0.55f, 0.72f, 0.95f, 0.8f), 0.05f, 70f, 1.3f, 1400f, new Vector3(130f, 1f, 130f));
            _snow = MakePrecip("SnowFX", new Color(1f, 1f, 1f, 0.92f), 0.42f, 6f, 4.5f, 700f, new Vector3(120f, 1f, 120f));
            if (_rain != null) { var e = _rain.emission; e.enabled = false; _rain.Play(); }
            if (_snow != null) { var e = _snow.emission; e.enabled = false; _snow.Play(); }

            _tornado = new GameObject("Tornado"); _tornado.transform.SetParent(GM.transform);
            _tornadoSpin = new GameObject("Spin").transform; _tornadoSpin.SetParent(_tornado.transform); _tornadoSpin.localPosition = Vector3.zero;
            var col = new GameObject("Funnel"); col.transform.SetParent(_tornadoSpin);
            var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.Destroy(cyl.GetComponent<Collider>());
            cyl.transform.SetParent(col.transform); cyl.transform.localPosition = new Vector3(0, 14f, 0);
            cyl.transform.localScale = new Vector3(3.2f, 14f, 3.2f);
            var r = cyl.GetComponent<Renderer>(); r.material = ShaderHelper.Mat(new Color(0.28f, 0.28f, 0.31f, 0.78f));
            var cone = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.Destroy(cone.GetComponent<Collider>());
            cone.transform.SetParent(col.transform); cone.transform.localPosition = new Vector3(0, 2.2f, 0);
            cone.transform.localScale = new Vector3(7f, 2.2f, 7f);
            var r2 = cone.GetComponent<Renderer>(); r2.material = ShaderHelper.Mat(new Color(0.36f, 0.34f, 0.30f, 0.7f));
            _tornado.SetActive(false);
        }

        private ParticleSystem MakePrecip(string name, Color c, float size, float speed, float life, float rate, Vector3 box)
        {
            var go = new GameObject(name); go.transform.SetParent(GM.transform);
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.playOnAwake = true; main.duration = 1f;
            main.startSize = size; main.startSpeed = speed; main.startLifetime = life;
            main.startColor = c; main.maxParticles = 4000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = rate; em.enabled = true;
            var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = box;
            var vel = ps.velocityOverLifetime; vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            float vy = speed > 20f ? -speed : -speed * 0.4f;
            vel.x = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);          // 三轴统一两常数模式
            vel.z = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);
            vel.y = new ParticleSystem.MinMaxCurve(vy, vy * 0.8f);
            var rr = ps.GetComponent<Renderer>(); if (rr != null) rr.material = ShaderHelper.Mat(c);
            ps.Play(); return ps;
        }
    }
}
