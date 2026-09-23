using System.Collections.Generic;
using UnityEngine;
using PixelToCivilization.Core;
using PixelToCivilization.Data;
using PixelToCivilization.World;

namespace PixelToCivilization.Systems
{
    /// <summary>
    /// V6.1.9(i) 月度潮汐系统：
    /// 以「游戏内的月」为周期（30 游戏日=1 月），1-15 号逐渐涨潮、16-30 号逐渐退潮（sin 平滑、首尾连续）。
    /// 反向规则：涨潮时【主大陆】水位上涨（淹没近岸），【次大陆&无人岛】水位下降（露出滩涂）；退潮相反。
    /// 实现：①向 WorldGenerator 注入 DynamicWaterLevel，使 IsWater/行船/鱼群的逻辑水位按所属陆地反向变化；
    /// ②为每块陆地生成一块随其自身水位升降的局部水面，全球深水底面保持基准水位。
    /// </summary>
    public class TideSystem : GameSystemBase
    {
        public const float Amp = 0.55f;          // 潮幅（世界单位）
        private const int MonthDays = 30;
        public static TideSystem Instance { get; private set; }

        private WorldGenerator _world;
        private Transform _root;
        private int _builtCount = -1;
        private readonly List<GameObject> _panes = new();
        // 每块陆地当前水位偏移（主+、其它-）
        private readonly List<float> _offsets = new();

        public override void Init(GameManager gm)
        {
            base.Init(gm);
            Instance = this;
            _world = Object.FindObjectOfType<WorldGenerator>();
            _root = EntityViewFactory.EnsureRoot("TideWater", gm.transform);
        }

        public override void Tick(float dt)
        {
            if (S == null) return;
            if (_world == null) _world = Object.FindObjectOfType<WorldGenerator>();
            if (_world == null) return;

            // 月度潮位：c∈[0,1)，p=sin(πc)，日1=0、日15=1、日30=0
            float c = (S.Day % MonthDays) / (float)MonthDays;
            float p = Mathf.Sin(Mathf.Clamp01(c) * Mathf.PI);
            S.MonthlyTide = p;
            S.MonthlyFlooding = c < 0.5f;
            S.DayOfMonth = Mathf.Clamp((int)(S.Day % MonthDays) + 1, 1, 30);

            EnsurePanes();
            UpdateOffsetsAndPanes(p);

            // 注入逻辑水位（幂等：始终指向本实例）
            _world.DynamicWaterLevel = WaterLevelAt;
        }

        private void EnsurePanes()
        {
            int k = _world.LandmassCount;
            if (k == _builtCount && _panes.Count == k) return;
            foreach (var go in _panes) if (go != null) Object.Destroy(go);
            _panes.Clear(); _offsets.Clear();
            var lands = _world.Landmasses;
            for (int i = 0; i < lands.Count; i++)
            {
                var L = lands[i];
                float half = L.Br * 1.18f;                 // 覆盖陆地+近岸
                var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
                go.name = "Tide_" + L.Id;
                Object.Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(_root);
                float sc = (half * 2f) / 10f;             // Unity 平面默认 10 单位
                go.transform.localScale = new Vector3(sc, 1f, sc);
                var r = go.GetComponent<Renderer>();
                r.material = MakeWaterMat();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _panes.Add(go); _offsets.Add(0f);
            }
            _builtCount = k;
        }

        private void UpdateOffsetsAndPanes(float p)
        {
            var lands = _world.Landmasses;
            for (int i = 0; i < _panes.Count && i < lands.Count; i++)
            {
                var L = lands[i];
                // 主大陆(Id==1) 与其它陆地反向
                float off = (L.Id == 1 ? Amp : -Amp) * p;
                _offsets[i] = off;
                float y = GameConstants.WaterLevel - 0.04f + off;
                if (_panes[i] != null)
                    _panes[i].transform.position = new Vector3(L.Cx, y, L.Cz);
            }
        }

        /// <summary>某世界坐标当前生效水位（按最近陆地归属决定反向偏移；开阔大洋=基准）</summary>
        public float WaterLevelAt(float x, float z)
        {
            float baseL = GameConstants.WaterLevel;
            if (_world == null) return baseL;
            var lands = _world.Landmasses;
            int best = -1; float bd = float.MaxValue;
            for (int i = 0; i < lands.Count; i++)
            {
                var L = lands[i];
                float dx = x - L.Cx, dz = z - L.Cz;
                float d = dx * dx + dz * dz;
                if (d < bd) { bd = d; best = i; }
            }
            if (best < 0) return baseL;
            var B = lands[best];
            if (bd > (B.Br * 1.55f) * (B.Br * 1.55f)) return baseL;   // 远离任何陆地=开阔大洋
            return baseL + (B.Id == 1 ? Amp : -Amp) * S.MonthlyTide;
        }

        /// <summary>船/鱼在该处的水面 Y（含潮位）</summary>
        public float SurfaceY(float x, float z) => WaterLevelAt(x, z);

        private static Material MakeWaterMat()
        {
            var mat = new Material(ShaderHelper.Water);
            if (mat.HasProperty("_Shallow"))
            {
                mat.SetColor("_Shallow", new Color(0.22f, 0.80f, 1.00f, 0.66f));
                mat.SetColor("_Deep", new Color(0.06f, 0.44f, 0.86f, 0.90f));
                mat.SetFloat("_WaveAmp", 0.12f); mat.SetFloat("_WaveFreq", 0.9f);
                mat.SetFloat("_Smoothness", 0.94f);
            }
            else if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.07f, 0.45f, 0.85f, 0.6f));
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", new Color(0.07f, 0.45f, 0.85f, 0.6f));
            return mat;
        }

        public string TideText =>
            S == null ? "" : (S.MonthlyFlooding ? "🌊涨潮 " : "🏜退潮 ") + Mathf.RoundToInt(S.MonthlyTide * 100f) + "%";
    }
}
