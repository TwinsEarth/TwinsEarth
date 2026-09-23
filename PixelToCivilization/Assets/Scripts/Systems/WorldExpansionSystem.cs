using UnityEngine;
using PixelToCivilization.Core;
using PixelToCivilization.World;
using PixelToCivilization.UI;

namespace PixelToCivilization.Systems
{
    /// <summary>
    /// V6.5.4 大地图【实时随机】自然延展系统：
    /// ·禁止"一次性建成再慢慢显现"：开局只生成初始960世界，外环留深海；扩展时实时随机增陆。
    /// ·扩展速度：每100游戏年 ×1.01（1%），每满1000游戏年再 ×1.30（30%）；长(X)封顶5倍、宽(Z)封顶3倍，最终面积15倍（V6.6.0）；
    /// ·增陆节奏：每100年+1座无人岛；每500年+2~3座次大陆；每1000年+1座主大陆（在当前已展开疆域内的深海随机隆起）；
    /// ·公元1000年大航海、公元2000年宇宙大开发，各只触发一次。
    /// </summary>
    public class WorldExpansionSystem : GameSystemBase
    {
        WorldGenerator _terrain;
        VegetationSystem _veg;
        bool _entitySynced;
        const float MaxExpansion = 5f;   // V6.5.8 长(X)5倍；宽(Z)由 ActiveHalfZ 在3倍处触顶
        int _lastCentury, _lastHalfMil, _lastMillennium;
        int _pendIsland, _pendSec, _pendMain;   // 欠账：疆域尚不足时累积，够大立即补建（不丢任何一次增陆）
        bool _primed;
        System.Random _growRng;

        /// <summary>每100年×1.01，每1000年×1.30；倍率封顶5（X到5倍，Z由ActiveHalfZ在3倍处触顶）</summary>
        public static float ExpandFactor(int gameYear)
        {
            int years = Mathf.Max(0, gameYear - 1);
            int centuries = years/100, millennia = years/1000;
            return Mathf.Min(MaxExpansion, Mathf.Pow(1.01f,centuries)*Mathf.Pow(1.30f,millennia));
        }

        public override void Init(GameManager gm)
        {
            base.Init(gm);
            _terrain = Object.FindObjectOfType<WorldGenerator>();
            _lastCentury=_lastHalfMil=_lastMillennium=0;
            _pendIsland=_pendSec=_pendMain=0;
            _growRng=new System.Random(_terrain!=null?_terrain.Seed:System.Environment.TickCount);
        }

        public override void OnYear(int year)
        {
            int years0=Mathf.Max(0,year-1);
            if (!_primed || year<=1)   // 首年/新游戏/读档后：以当前年份为基线，不补历史欠账
            {
                _lastCentury=years0/100;_lastHalfMil=years0/500;_lastMillennium=years0/1000;
                _pendIsland=_pendSec=_pendMain=0;_primed=true;
            }
            if (_terrain == null) _terrain = Object.FindObjectOfType<WorldGenerator>();
            float e = ExpandFactor(year);
            S.WorldExpansion = e;
            _terrain?.SetExpansion(e);                 // 先展开方形边疆

            // —— 实时随机增陆节奏（与倍率同一口径 (year-1)，避免错位一年）——
            int years=Mathf.Max(0,year-1);
            int c=years/100, h=years/500, m=years/1000;
            if (c>_lastCentury){_pendIsland+=c-_lastCentury;_lastCentury=c;}                 // 每百年欠1岛
            if (h>_lastHalfMil){_pendSec+=(h-_lastHalfMil)*(2+_growRng.Next(2));_lastHalfMil=h;} // 每500年欠2~3次大陆
            if (m>_lastMillennium){_pendMain+=m-_lastMillennium;_lastMillennium=m;}          // 每千年欠1主大陆
            // 疆域够大立即补建（失败则保留欠账，来年再试）；每年限量，避免一次性冒出
            FlushPending(2,ref _pendIsland,1,"无人岛");
            FlushPending(1,ref _pendSec,0,"次大陆");
            FlushPending(1,ref _pendMain,2,"主大陆");

            SyncEntityVisibility();
            UIManager.Instance?.InvalidateMinimapBase();

            int gy = GM.Time != null ? GM.Time.GregorianYear : -3000 + year;
            if (gy >= 1000 && !S.AgeOfSail)
            {
                S.AgeOfSail = true; S.OceanUnlocked = true;
                GM.AddEvent("good", "🧭 公元1000年·大航海时代开启：远洋航路解锁，世界向海洋延展！");
                UIManager.Instance?.Toast("🧭 大航海时代开启（公元1000年）");
            }
            if (gy >= 2000 && !S.AgeOfSpace)
            {
                S.AgeOfSpace = true; S.SpaceUnlocked = true;
                GM.AddEvent("good", "🚀 公元2000年·宇宙大开发时代开启：近地轨道与地外疆域解锁！");
                UIManager.Instance?.Toast("🚀 宇宙大开发时代开启（公元2000年）");
            }
        }

        /// <summary>每年最多放 perCall 块；shapeKind 0次大陆/1岛/2主大陆。选址失败(疆域不足)立即停，欠账保留来年再试</summary>
        void FlushPending(int perCall,ref int pending,int shapeKind,string label)
        {
            if (_terrain==null||pending<=0) return;
            for (int i=0;i<perCall && pending>0;i++)
            {
                int id=_terrain.GrowLandmass(shapeKind,_growRng);
                if (id<=0) break;
                pending--;
                if (_veg==null)_veg=Object.FindObjectOfType<VegetationSystem>();
                _veg?.ActivateLand(id);
                SyncEntityVisibility();
                GM.AddEvent("good", $"🌍 版图实时延展：海洋中隆起新{label}（第{id}块陆地）");
            }
        }

        public override void Tick(float dt)
        {
            if (_terrain == null) _terrain = Object.FindObjectOfType<WorldGenerator>();
            if (_terrain==null) return;
            _terrain.UpdateReveal(Mathf.Min(0.05f, Time.unscaledDeltaTime));
            if(!_entitySynced){ SyncEntityVisibility(); _entitySynced=true; }
            var found=_terrain.PollNewlyRevealed();
            if(found.Count>0 && _veg==null)_veg=Object.FindObjectOfType<VegetationSystem>();
            foreach(var id in found)
            {
                _veg?.ActivateLand(id);
                SyncEntityVisibility();
                GM.AddEvent("good", $"🧭 远洋探索发现新陆地（第{id}块大陆/岛屿），世界版图隔海延展！");
                UIManager.Instance?.Toast($"🧭 发现新陆地：第{id}块大陆");
            }
        }

        void SyncEntityVisibility()
        {
            if(_terrain==null) return;
            foreach(var b in S.Buildings)
            {
                if(b.View==null) continue;
                bool vis=_terrain.IsLandRevealed(_terrain.ContinentAt(b.X,b.Z));
                if(b.View.activeSelf!=vis) b.View.SetActive(vis);
            }
            foreach(var a in S.Agents)
            {
                if(a.View==null) continue;
                bool vis=_terrain.IsLandRevealed(_terrain.ContinentAt(a.X,a.Z));
                if(a.View.activeSelf!=vis) a.View.SetActive(vis);
            }
        }
    }
}
