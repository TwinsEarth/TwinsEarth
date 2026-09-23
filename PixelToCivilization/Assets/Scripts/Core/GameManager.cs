using System;
using System.Collections.Generic;
using UnityEngine;
using PixelToCivilization.Data;
using PixelToCivilization.Systems;
using PixelToCivilization.Buildings;
using PixelToCivilization.AI;
using PixelToCivilization.World;
using PixelToCivilization.UI;

namespace PixelToCivilization.Core
{
    public enum GameStateType { Menu, Playing, Paused, Victory, Defeat }

    /// <summary>游戏主管理器：加载数据库、编排全部子系统、速度/暂停/Debug密码门</summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("状态")]
        public GameState State = new();
        public GameStateType StateType = GameStateType.Menu;

        [Header("数据库")]
        public List<EraDefinition> Eras;
        public List<DynastyDefinition> Dynasties;
        public Dictionary<string, BuildingDefinition> Buildings = new();
        public Dictionary<string, TechDefinition> Techs = new();
        public Dictionary<string, PolicyDefinition> Policies = new();

        [Header("子系统")]
        public GameTime Time;
        public EconomySystem Economy;
        public PopulationSystem Population;
        public BuildingSystem Building;
        public TechSystem Tech;
        public PolicySystem Policy;
        public MilitarySystem Military;
        public NavalSystem Naval;
        public OceanExpansionSystem Ocean;
        public SpaceExpansionSystem Space;
        public CanalSystem Canal;
        public CartSystem Cart;
        public BridgeSystem Bridge;        // V6.3.9 桥梁（材料/跨距随时代，同阵营相邻陆地自动最短建桥）
        public EmbarkSystem Embark;        // V6.3.7 Lv2 车船自动载人
        public InfrastructureSystem Infra;
        public CultureSystem Culture;
        public GodsSystem Gods;
        public EnvironmentSystem Env;
        public SaveSystem SaveSystem;
        // 策划书扩展系统
        public PhilosophySystem Philosophy;
        public DisasterSystem Disaster;
        public HistoryEventSystem HistoryEvent;
        public VictorySystem Victory;
        public NationSystem Nation;   // V6.1.3 多聚落 + 天下分合（分裂3-7国 ↔ 大一统王朝）
        public ColonizationSystem Colonization;   // V6.1.5 殖民时代
        public ExpeditionSystem Expedition;       // V6.1.6 海洋/太空网格探索副本
        public WonderSystem Wonder;               // V6.8.0 世界奇观·文明丰碑
        public AICouncilSystem Council;           // V6.1.8 九智能体共治（AI多智能体，离线硬保证+联网ARK可选增强）
        public TideSystem Tide;                   // V6.1.9(i) 月度潮汐（主涨它降反向）
        public OceanCurrentSystem OceanFlow;      // V6.1.9(i) 洋流&海风（帆船动力/鱼群洄游）
        public WeatherSystem Weather;             // V6.1.9(i) 天气（雨雪晴云雾晚霞雷电龙卷）

        // V6.1.2 底部工具栏当前工具：select 选择 / tree 种树 / npc 招民（对齐 v5.9.9 setTool）
        public string Tool = "select";

        public event Action<GameStateType> OnStateChanged;
        public event Action<LogEntry> OnEventLogged;

        private readonly List<GameSystemBase> _systems = new();

        private void Awake() => EnsureAwake();

        /// <summary>幂等初始化单例与数据库：运行时由 Awake 调用；Edit 模式/冒烟测试可在 AddComponent 后显式调用</summary>
        public void EnsureAwake()
        {
            if (Instance == this) return;
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject);
            LoadDatabases();
            // 打包版恢复上次的显示偏好（编辑器下不改动 Game 视图）
            if (!Application.isEditor && PlayerPrefs.HasKey("fullscreen"))
                SetFullscreen(PlayerPrefs.GetInt("fullscreen") == 1, false);
        }

        public void LoadDatabases()
        {
            Eras = EraDatabase.CreateAll();
            Dynasties = DynastyDatabase.CreateAll();
            Buildings.Clear();
            foreach (var b in BuildingDatabase.CreateAll()) Buildings[b.Id] = b;
            Techs.Clear();
            foreach (var t in TechDatabase.CreateAll()) Techs[t.Id] = t;
            Policies.Clear();
            foreach (var p in PolicyDatabase.CreateAll()) Policies[p.Id] = p;
        }

        /// <summary>由Bootstrap在世界搭建完成后调用，装配全部子系统</summary>
        public void InstallSystems()
        {
            Time = gameObject.GetComponent<GameTime>() ?? gameObject.AddComponent<GameTime>();
            Time.Init(State, Eras, Dynasties);
            Time.OnYearAdvanced += year => { foreach (var s in _systems) s.OnYear(year); };
            Time.OnEraChanged += (n, o) => { foreach (var s in _systems) s.OnEra(n, o); };
            Time.OnDynastyChanged += (i, y) => { foreach (var s in _systems) s.OnDynasty(i, y); };

            Add(Economy = gameObject.GetComponent<EconomySystem>() ?? gameObject.AddComponent<EconomySystem>());
            Add(Population = gameObject.GetComponent<PopulationSystem>() ?? gameObject.AddComponent<PopulationSystem>());
            Add(Building = gameObject.GetComponent<BuildingSystem>() ?? gameObject.AddComponent<BuildingSystem>());
            Add(Tech = gameObject.GetComponent<TechSystem>() ?? gameObject.AddComponent<TechSystem>());
            Add(Policy = gameObject.GetComponent<PolicySystem>() ?? gameObject.AddComponent<PolicySystem>());
            Add(Military = gameObject.GetComponent<MilitarySystem>() ?? gameObject.AddComponent<MilitarySystem>());
            Add(Tide = gameObject.GetComponent<TideSystem>() ?? gameObject.AddComponent<TideSystem>());
        Add(OceanFlow = gameObject.GetComponent<OceanCurrentSystem>() ?? gameObject.AddComponent<OceanCurrentSystem>());
        Add(Naval = gameObject.GetComponent<NavalSystem>() ?? gameObject.AddComponent<NavalSystem>());
            Add(Ocean = gameObject.GetComponent<OceanExpansionSystem>() ?? gameObject.AddComponent<OceanExpansionSystem>());
            Add(Space = gameObject.GetComponent<SpaceExpansionSystem>() ?? gameObject.AddComponent<SpaceExpansionSystem>());
            Add(Canal = gameObject.GetComponent<CanalSystem>() ?? gameObject.AddComponent<CanalSystem>());
            Add(Cart = gameObject.GetComponent<CartSystem>() ?? gameObject.AddComponent<CartSystem>());
            Add(Bridge = gameObject.GetComponent<BridgeSystem>() ?? gameObject.AddComponent<BridgeSystem>());
            Add(Embark = gameObject.GetComponent<EmbarkSystem>() ?? gameObject.AddComponent<EmbarkSystem>());
            Add(Infra = gameObject.GetComponent<InfrastructureSystem>() ?? gameObject.AddComponent<InfrastructureSystem>());
            Add(Culture = gameObject.GetComponent<CultureSystem>() ?? gameObject.AddComponent<CultureSystem>());
            Add(Gods = gameObject.GetComponent<GodsSystem>() ?? gameObject.AddComponent<GodsSystem>());
            Add(Env = gameObject.GetComponent<EnvironmentSystem>() ?? gameObject.AddComponent<EnvironmentSystem>());
        Add(Weather = gameObject.GetComponent<WeatherSystem>() ?? gameObject.AddComponent<WeatherSystem>());
            Add(Philosophy = gameObject.GetComponent<PhilosophySystem>() ?? gameObject.AddComponent<PhilosophySystem>());
            Add(Disaster = gameObject.GetComponent<DisasterSystem>() ?? gameObject.AddComponent<DisasterSystem>());
            Add(HistoryEvent = gameObject.GetComponent<HistoryEventSystem>() ?? gameObject.AddComponent<HistoryEventSystem>());
            Add(Victory = gameObject.GetComponent<VictorySystem>() ?? gameObject.AddComponent<VictorySystem>());
            // V6.1.3 大地图随年代自然延展 + 大航海/宇宙大开发时代里程碑
            Add(gameObject.GetComponent<WorldExpansionSystem>() ?? gameObject.AddComponent<WorldExpansionSystem>());
            // V6.1.3 多聚落 + 天下分合（必须在 EnvironmentSystem.PopulateInitial 前就绪，后者调用 Nation.InitNations）
            Add(Nation = gameObject.GetComponent<NationSystem>() ?? gameObject.AddComponent<NationSystem>());
            // V6.1.5 殖民时代 / V6.1.6 海洋·太空网格探索副本
            Add(Colonization = gameObject.GetComponent<ColonizationSystem>() ?? gameObject.AddComponent<ColonizationSystem>());
            Add(Expedition = gameObject.GetComponent<ExpeditionSystem>() ?? gameObject.AddComponent<ExpeditionSystem>());
            // V6.8.0 世界奇观（在九神议会之前，供组织/技术神自动援建统筹）
            Add(Wonder = gameObject.GetComponent<WonderSystem>() ?? gameObject.AddComponent<WonderSystem>());
            // V6.1.8 九智能体共治（放在最后，可统筹全部既有系统；离线规则硬保证文明不灭绝）
            Add(Council = gameObject.GetComponent<AICouncilSystem>() ?? gameObject.AddComponent<AICouncilSystem>());

            foreach (var s in _systems) s.Init(this);
            SaveSystem = gameObject.GetComponent<SaveSystem>() ?? gameObject.AddComponent<SaveSystem>();
            SaveSystem.Init(this);
            Debug.Log("[GameManager] 子系统装配完成，数量=" + _systems.Count);
        }

        private void Add(GameSystemBase s) { if (!_systems.Contains(s)) _systems.Add(s); }

        public void StartNewGame()
        {
            State.Reset();
            State.Running = true; State.Paused = false; State.Speed = 1f;
            StateType = GameStateType.Playing;
            OnStateChanged?.Invoke(StateType);
            AddEvent("info", "🌱 文明起源：三皇五帝时代，第1年（公元前3000年）");
        }

        /// <summary>
        /// V6.1.2：每次开始游戏都用随机种子重新生成大地图（海≥50%/陆≥30%/山≤10%，湖/山/沙漠/河适应性生成），
        /// 清理上一局全部实体视图，相机归位新村址，再生成初始聚落/人口/飞鸟。
        /// </summary>
        public void StartNewRandomGame()
        {
            // 1) 清理上一局实体视图（数据由 State.Reset 清空，视图按固定根名回收）
            ClearWorldVisuals();

            // 2) 随机重建地形 + 植被
            var terrain = UnityEngine.Object.FindObjectOfType<World.WorldGenerator>();
            var veg = UnityEngine.Object.FindObjectOfType<World.VegetationSystem>();
            int seed = World.WorldGenerator.RandomSeed();
            Vector3 village = Vector3.zero;
            if (terrain != null)
            {
                village = terrain.Regenerate(seed);
                veg?.Regrow(terrain, seed);
                Debug.Log($"[StartNewRandomGame] 随机种子={seed} 大陆半径={terrain.LandRadius:F0} " +
                          $"海{terrain.SeaRatio:P0} 陆{terrain.LandRatio:P0} 山{terrain.MountainRatio:P0} 沙漠{terrain.DesertRatio:P0}");
            }

            // 3) 相机归位新村址
            var rig = UnityEngine.Object.FindObjectOfType<World.CameraRig>();
            rig?.Retarget(village);

            // 4) 状态重置并进入游戏
            StartNewGame();

            // 5) 小地图地形底图作废重烘焙
            UIManager.Instance?.InvalidateMinimapBase();

            // 6) 初始聚落/职业人口/飞鸟群
            Env?.PopulateInitial();
        }

        /// <summary>继续上次游戏：先搭好随机世界与各系统，再读取时间最新的有效存档（自动槽0或手动槽1..5）覆盖。无有效存档返回 false。</summary>
        public bool ContinueLastGame()
        {
            if (SaveSystem==null) { AddEvent("bad","没有可继续的存档"); return false; }
            int slot=SaveSystem.LatestSlot();
            if (slot<0) { AddEvent("bad","没有可继续的存档"); return false; }
            StartNewRandomGame();          // 世界/系统/State 就绪（地形随后由存档种子还原）
            SaveSystem.LoadFromSlot(slot); // 覆盖为最新存档快照
            return true;
        }

        /// <summary>销毁各实体根节点下的全部视图子物体（根节点本身保留，系统仍持有引用）</summary>
        private void ClearWorldVisuals()
        {
            string[] roots = { "Buildings","Canals","Carts","Military","Navy","Environment","Birds","Fish","Wonders" };
            foreach (Transform child in transform)
            {
                // V6.1.3 聚落节点命名 Settlement_0/1/...，按前缀一并清理
                if (System.Array.IndexOf(roots, child.name) < 0 && !child.name.StartsWith("Settlement")) continue;
                for (int i=child.childCount-1;i>=0;i--)
                {
                    var sub = child.GetChild(i);
                    // Environment 下还有 Trees/Agents 两层根，递归清它们的子物体；其余直接清
                    if (child.name=="Environment") { for(int j=sub.childCount-1;j>=0;j--) UnityEngine.Object.Destroy(sub.GetChild(j).gameObject); }
                    else UnityEngine.Object.Destroy(sub.gameObject);
                }
            }
        }

        private void Update()
        {
          try{
            HandleHotkeys();
            TickCryo(UnityEngine.Time.unscaledDeltaTime); // V6.1.9 冷冻冷却按现实秒走，暂停也计时
            if (StateType != GameStateType.Playing || State.Paused) return;
            float scaled = UnityEngine.Time.deltaTime * EffectiveSpeed; // 冷冻期实际倍速封顶10
            Time.Tick(scaled); // 年份推进（内部按游戏年份换算朝代/时代/公历）
            foreach (var s in _systems) s.Tick(scaled);
          }
          catch(System.Exception e){ Debug.LogError("[MARK_GM] "+e.GetType().Name+": "+e.Message+"\n"+e.StackTrace); }
        }

        // ===== V6.1.9 加速冷冻 =====
        /// <summary>冷冻冷却中实际生效的倍速（封顶 CryoMaxSpeed=10），非冷冻期等于设定倍速</summary>
        public float EffectiveSpeed => State.CryoActive ? Mathf.Min(State.Speed, GameConstants.CryoMaxSpeed) : State.Speed;
        private bool _cryoEntered;
        /// <summary>冷冻冷却按现实秒倒计时（不受暂停/倍速影响）；归零即自动解冻并重置累计年数</summary>
        private void TickCryo(float realDt)
        {
            if (!State.CryoActive) { _cryoEntered=false; return; }
            if (!_cryoEntered)
            {
                _cryoEntered=true;
                AddEvent("bad","❄️ 加速累计已满100年，进入冷冻冷却：300秒内最高10倍速");
                UIManager.Instance?.Toast("❄️ 冷冻冷却：300秒内最高10倍",false);
            }
            State.CryoRemainSec -= realDt;
            if (State.CryoRemainSec <= 0f) ThawCryo(true);
        }
        /// <summary>解冻：关闭冷冻、清零累计年数重新计算（倒计时归零自动调用，也供Web/调试手动解冻）</summary>
        public void ThawCryo(bool notify=true)
        {
            State.CryoActive=false; State.CryoRemainSec=0f; State.CryoAccumYears=0f; _cryoEntered=false;
            if(notify){ AddEvent("good","☀️ 冷冻结束，加速累计已重置，可继续加速"); UIManager.Instance?.Toast("☀️ 冷冻结束，已解冻",true); }
        }

        private void HandleHotkeys()
        {
            if (Input.GetKeyDown(KeyCode.Space)) TogglePause();
            if (Input.GetKeyDown(KeyCode.F11) ||
                (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.Return))) ToggleFullscreen();
        }

        public void TogglePause()
        {
            State.Paused = !State.Paused;
            StateType = State.Paused ? GameStateType.Paused : GameStateType.Playing;
            OnStateChanged?.Invoke(StateType);
        }

        // ===== 全屏/窗口切换（F11、Alt+Enter 或底部栏按钮），偏好持久化 =====
        public void ToggleFullscreen() => SetFullscreen(Screen.fullScreenMode == FullScreenMode.Windowed || !Screen.fullScreen);

        public void SetFullscreen(bool full, bool notify = true)
        {
            if (full)
            {
                Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
                Screen.fullScreen = true;
            }
            else
            {
                var res = Screen.currentResolution;
                int w = Mathf.Clamp((int)(res.width * 0.85f), 1024, 1920);
                int h = Mathf.Clamp((int)(res.height * 0.85f), 576, 1080);
                Screen.SetResolution(w, h, FullScreenMode.Windowed);
            }
            PlayerPrefs.SetInt("fullscreen", full ? 1 : 0);
            PlayerPrefs.Save();
            if (!notify) return;
            AddEvent("info", full ? "⛶ 已切换全屏（F11 切回窗口）" : "⛶ 已切换窗口模式（F11 切回全屏）");
            UIManager.Instance?.Toast(full ? "⛶ 全屏模式" : "⛶ 窗口模式");
        }

        // ===== 速度（1:1 对齐 v5.9.9：连续整数倍速，分段加减，分级上限）=====
        /// <summary>当前 Debug 等级允许的最高倍速：普通100 / Debug1=300 / 密码解锁=1000</summary>
        public float MaxSpeed => State.DebugLevel >= 2 ? GameConstants.SpeedMaxDebug2
                              : State.DebugLevel >= 1 ? GameConstants.SpeedMaxDebug1
                              : GameConstants.SpeedMaxNormal;
        public float[] SpeedTiers => State.DebugLevel switch
        {
            2 => GameConstants.SpeedTiersDebug2,
            1 => GameConstants.SpeedTiersDebug1,
            _ => GameConstants.SpeedTiersNormal
        };
        public void CycleSpeed()
        {
            var tiers = SpeedTiers;
            int idx = Array.IndexOf(tiers, State.Speed);
            State.Speed = tiers[(idx + 1) % tiers.Length];
        }

        // ===== WebGL / SendMessage 友好入口（无参，供浏览器深链、外部页面与自动化回归调用；UI 按钮逻辑不受影响）=====
        public void WebQuickSave(){ SaveSystem?.SaveToSlot(1); }

        // ===== V6.8.0 世界奇观：浏览器回归入口（SendMessage 可绑 string） =====
        public void WebBuildWonder(string id){
            if(Wonder==null){Debug.Log("[WEB] Wonder system null");return;}
            // 回归便利：自动补足资源，避免被成本卡住
            foreach(var kv in Wonder.Def(id)?.Cost??new System.Collections.Generic.Dictionary<string,int>()) State.AddRes(kv.Key, kv.Value+50);
            var r=Wonder.TryBuild(id);
            Debug.Log($"[WEB] BuildWonder {id} ok={r.ok} msg={r.msg} count={State.Wonders.Count} resMul={Wonder.ResearchMul} goldMul={Wonder.GoldMul} housing+={Wonder.HousingAdd} ach={State.Achievements.Count}");
        }
        public void WebWonderInfo(){
            if(Wonder==null){Debug.Log("[WEB] Wonder null");return;}
            Debug.Log($"[WEB] WonderInfo count={State.Wonders.Count} era={State.Era} resMul={Wonder.ResearchMul} goldMul={Wonder.GoldMul} culMul={Wonder.CultureMul} fireMul={Wonder.FireCapMul} housing+={Wonder.HousingAdd} forcePower={Wonder.ForcePower} ach={State.Achievements.Count} auto={State.WonderAuto}");
        }
        public void WebWonderAuto(){ State.WonderAuto=!State.WonderAuto; Debug.Log("[WEB] WonderAuto="+State.WonderAuto); }

        // ===== V6.8.1 回归探针：强制生成中/大马车；回报平民迈腿动画状态 =====
        public void WebSpawnCarts(){
            if(Cart==null){Debug.Log("[WEB] Cart null");return;}
            float cx=State.VillageX!=null&&State.VillageX.Count>0?State.VillageX[0]:0f;
            float cz=State.VillageZ!=null&&State.VillageZ.Count>0?State.VillageZ[0]:0f;
            var m=Cart.SpawnCart("medium_cart",cx+6f,cz+4f,2);
            var l=Cart.SpawnCart("large_cart",cx-7f,cz+7f,3);
            Debug.Log($"[WEB] SpawnCarts medium={(m!=null)} large={(l!=null)} total={State.Carts.Count}");
        }
        public void WebAgentAnim(){
            int total=State.Agents!=null?State.Agents.Count:0, withAnim=0, moving=0, stopped=0, noView=0;
            foreach(var a in State.Agents){
                if(a.Boarded){continue;}
                if(a.View==null){noView++;continue;}
                if(a.Anim==null) a.Anim=a.View.GetComponentInChildren<PixelToCivilization.Actors.HumanoidAnimator>();
                if(a.Anim==null) continue;
                withAnim++;
                float sp=new Vector2(a.Anim.Velocity.x,a.Anim.Velocity.z).magnitude;
                if(sp>0.02f && a.Anim.Rig!=null && a.Anim.Rig.Moving) moving++; else stopped++;
            }
            Debug.Log($"[WEB] AgentAnim total={total} noView={noView} withAnim={withAnim} moving={moving} stopped={stopped}");
        }

        // ===== V6.8.2 回归探针：强制退潮/涨潮，回读每艘船是否被拖进淡水湖或卡在陆地 =====
        public void WebTideEbb()
        {
            var terr=UnityEngine.Object.FindObjectOfType<PixelToCivilization.World.WorldGenerator>();
            if(terr==null||Naval==null||Tide==null){Debug.Log("[WEB] TideEbb missing refs");return;}
            // V6.8.3 自愈网确定性测试：把一艘真船分别放进大湖/内河，几帧内必须被 KeepAtSea 拉回外海
            if(State.Ships.Count>0){
                var vic=State.Ships[0]; float ox=vic.X,oz=vic.Z;
                float lakeX=0,lakeZ=0,rivX=0,rivZ=0; bool lk=false,rv=false;
                float TT=PixelToCivilization.Data.GameConstants.Tile;
                for(float gx=-380f; gx<=380f && (!lk||!rv); gx+=TT*3f)
                  for(float gz=-380f; gz<=380f && (!lk||!rv); gz+=TT*3f){
                    var bm=terr.BiomeAt(gx,gz);
                    if(!lk && bm==PixelToCivilization.World.BiomeKind.FreshWater && terr.IsWater(gx,gz)){lakeX=gx;lakeZ=gz;lk=true;}
                    if(!rv && bm==PixelToCivilization.World.BiomeKind.River && terr.IsWater(gx,gz)){rivX=gx;rivZ=gz;rv=true;}
                  }
                if(lk){ vic.X=lakeX;vic.Z=lakeZ; for(int k=0;k<12;k++) Naval.Tick(0.4f);
                        bool heal=terr.IsOceanWater(vic.X,vic.Z);
                        Debug.Log($"[WEB] Tide LAKE_HEAL={(heal?"PASS":"FAIL")} now=({vic.X:F0},{vic.Z:F0})"); }
                else Debug.Log("[WEB] Tide LAKE_HEAL=notile");
                if(rv){ vic.X=rivX;vic.Z=rivZ; for(int k=0;k<12;k++) Naval.Tick(0.4f);
                        bool heal=terr.IsOceanWater(vic.X,vic.Z);
                        Debug.Log($"[WEB] Tide RIVER_HEAL={(heal?"PASS":"FAIL")} now=({vic.X:F0},{vic.Z:F0})"); }
                else Debug.Log("[WEB] Tide RIVER_HEAL=notile");
                vic.X=ox;vic.Z=oz;
            }
            System.Action<string,int> report=(tag,day)=>{
                int ocean=0,beach=0,inland=0; string sample="";
                foreach(var sh in State.Ships){
                    bool sea=terr.IsOceanWater(sh.X,sh.Z);
                    bool dry=!terr.IsWater(sh.X,sh.Z);
                    bool tideFlat=dry && terr.BiomeAt(sh.X,sh.Z)==PixelToCivilization.World.BiomeKind.Default
                                      && terr.HeightAt(sh.X,sh.Z)<PixelToCivilization.Data.GameConstants.WaterLevel;
                    if(sea)ocean++; else if(tideFlat)beach++; else inland++;
                    if(sample.Length<170) sample+=($"({sh.X:F0},{sh.Z:F0}:{(sea?"sea":tideFlat?"flat":"INLAND")}) ");
                }
                Debug.Log($"[WEB] Tide {tag} day={day} ships={State.Ships.Count} ocean={ocean} beached={beach} IN_INLAND={inland} tide={State.MonthlyTide:F2} :: {sample}");
            };
            State.Day=22f;                                   // 退潮：主大陆近岸露出
            for(int i=0;i<1500;i++){ Tide.Tick(0.016f); Naval.Tick(0.4f); }
            report("EBB",22);
            State.Day=8f;                                    // 涨潮：近岸重新没水，坐滩船应复浮
            for(int i=0;i<900;i++){ Tide.Tick(0.016f); Naval.Tick(0.4f); }
            report("FLOOD",8);
        }
        public void WebQuickLoad(){ bool ok=SaveSystem!=null && SaveSystem.LoadFromSlot(1); Debug.Log("[Web] QuickLoad "+(ok?"OK":"FAIL")); }
        public void WebAdvanceEra(){ bool ok=Time!=null && Time.DebugAdvanceEra(); Debug.Log("[Web] AdvanceEra "+(ok?"OK":"FAIL")); }
        public void WebNextDynasty(){ bool ok=Time!=null && Time.DebugNextDynasty(); Debug.Log("[Web] NextDynasty "+(ok?"OK":"FAIL")); }
        public void WebProbeBridges(){ Bridge?.DebugProbe(); }
        public void WebForceBridge(){ bool ok=Bridge!=null&&Bridge.ForceNearest(); Debug.Log("[Web] ForceBridge "+(ok?"OK":"FAIL")); }
        public void WebToggleLeftPanel(){ UIManager.Instance?.WebToggleLeft(); }
        public void WebToggleRightPanel(){ UIManager.Instance?.WebToggleRight(); }

        // ===== V6.1.9(i) 天气/军事 Web 回归入口（无参）=====
        public void WebNextWeather(){ Weather?.ForceNext(); Debug.Log("[Web] NextWeather kind="+(Weather!=null?(int)Weather.Current:-1)); }
        public void WebTrainSquad(){ State.AddRes("food",800); State.Pop=Mathf.Max(State.Pop,40);
            bool a=Military.TrainSoldiers(), b=Military.TrainSoldiers();
            Debug.Log("[Web] TrainSquad a="+a+" b="+b+" pop="+State.Pop+" food="+State.GetRes("food")+" fu="+State.FriendlyUnits.Count); }

        // V6.3.7(真扩展) 浏览器自证探针：打印当前游戏年、扩张倍率、真实活动疆域边长/半幅/活动网格、全量上限
        public void WebProbeExpand(){
            var t=UnityEngine.Object.FindObjectOfType<World.WorldGenerator>();
            int yr=State!=null?State.Year:0;
            float fac=WorldExpansionSystem.ExpandFactor(yr);
            if(t==null){Debug.Log("[ExpandProbe] terrain null");return;}
            Debug.Log($"[ExpandProbe] year={yr} factor={fac:F3} activeHalf={t.ActiveHalf:F1} activeWorld={t.ActiveWorld:F1} activeN={t.ActiveN} fullGW={t.GW:F0} fullG={t.G} lands={t.LandmassCount} bridges={State.BridgeRuns.Count/8} pop={State.Pop}");
        }
        /// <summary>V6.5.4 实时增陆回归：记录初始陆块数→逐年跳到第2001年（触发10次百年岛/2次五百年次陆/1次千年主陆）→再探针</summary>
        public void WebGrowTest()
        {
            var t=UnityEngine.Object.FindObjectOfType<World.WorldGenerator>();
            int before=t!=null?t.LandmassCount:-1;
            Time?.DebugJumpTo(2001);
            int after=t!=null?t.LandmassCount:-1;
            float fac=WorldExpansionSystem.ExpandFactor(State.Year);
            Debug.Log($"[GROWTEST] lands {before}->{after} (新增{after-before}) year={State.Year} factor={fac:F3} activeHalf={t.ActiveHalf:F1} bridges={State.BridgeRuns.Count/8}");
        }

        // ===== V6.1.8 九智能体共治 Web 入口（无参，供 UI/浏览器/自动化回归）=====
        public void WebCouncilNow(){ Council?.CouncilNow(); Debug.Log("[Web] CouncilNow continuity="+ (Council!=null?Council.ComputeContinuity():-1)); }
        public void WebCouncilToggle(){ Council?.ToggleEnabled(); Debug.Log("[Web] Council enabled="+(Council!=null&&Council.Enabled)); }
        public void WebCouncilOnline(){ Council?.SetOnline(true); Debug.Log("[Web] Council online"); }
        public void WebCouncilOffline(){ Council?.SetOnline(false); Debug.Log("[Web] Council offline"); }
        /// <summary>V6.1.9 立即触发一次九神议政（含联网请求），用于浏览器验证 ARK 连通/CORS</summary>
        public void WebCouncilOnce(){ if(Council==null){Debug.Log("[Web] Council null");return;} Council.SetOnline(true); Council.CouncilNow(); Debug.Log("[Web] CouncilOnce online model="+Council.Model+" requesting, 请观察后续联网结果"); }
        // ---- V6.1.9 加速冷冻 Web 回归入口 ----
        /// <summary>立即进入冷冻冷却（300现实秒），验证限倍与倒计时</summary>
        public void WebCryoFreeze(){ State.CryoActive=true; State.CryoRemainSec=GameConstants.CryoCooldownSec; State.CryoAccumYears=GameConstants.CryoYearThreshold; Debug.Log("[Web] CryoFreeze active, speed will cap at "+EffectiveSpeed); }
        /// <summary>立即解冻并重置累计年数</summary>
        public void WebCryoThaw(){ ThawCryo(false); Debug.Log("[Web] CryoThaw active="+State.CryoActive+" accum="+State.CryoAccumYears+" eff="+EffectiveSpeed); }
        /// <summary>把加速累计年数设到阈值前1年，随后加速推进1年即应触发冷冻（验证自动触发）</summary>
        public void WebCryoArm(){ State.CryoAccumYears=GameConstants.CryoYearThreshold-1f; State.CryoActive=false; Debug.Log("[Web] CryoArm accum="+State.CryoAccumYears); }
        /// <summary>万年存续压测：从当前逐年补算到第10000游戏年，输出人口/存续分/兜底次数，验证文明不断绝</summary>
        public void WebMillenniumTest()
        {
            int pop0=State.Pop;
            Time?.DebugJumpTo(10000);
            Council?.SafetyNet(); float c=Council!=null?Council.ComputeContinuity():-1;
            int floor=Council!=null?Council.PopFloor:12;
            bool survive=State.Pop>=floor;
            Debug.Log($"[MILLENNIUM] 到第{State.Year}年 公历{Time?.GregorianYear} 人口{pop0}->{State.Pop}(硬底{floor}) 存续{c:F1} 兜底{Council?.SafetyCount} 存活={survive}");
        }

        /// <summary>V6.1.4→6.1.6 运行时自检（WebGL 自动化回归钩子；逐步 try，结果以 [SELFTEST] 打到控制台，不影响正常玩法）</summary>
        public void WebSelfTestV616()
        {
            int pass=0, fail=0; var log=new System.Text.StringBuilder();
            void Step(string name, System.Action act)
            {
                try{ act(); pass++; Debug.Log("[SELFTEST] OK "+name); }
                catch(System.Exception e){ fail++; Debug.Log("[SELFTEST] FAIL "+name+" => "+e.Message); }
            }
            var S=State;
            Step("给资源",()=>{ foreach(var k in new[]{"wood","stone","gold","food","iron","steel","fusion","carbon"}) S.AddRes(k,99999); });
            Step("进入航海时代",()=>{ S.Era=4; S.AgeOfSail=true; });
            Step("组建步骑机动部队",()=>{
                float hx=S.VillageX.Count>0?S.VillageX[0]:0f, hz=S.VillageZ.Count>0?S.VillageZ[0]:0f;
                S.FriendlyUnits.Add(new FriendlyUnit{Kind=0,X=hx,Z=hz,HomeX=hx,HomeZ=hz,Hp=50,MaxHp=50,Attack=6,Speed=1.6f});
                S.FriendlyUnits.Add(new FriendlyUnit{Kind=1,X=hx,Z=hz,HomeX=hx,HomeZ=hz,Hp=60,MaxHp=60,Attack=9,Speed=3.2f});
                if(S.FriendlyUnits.Count<2) throw new System.Exception("部队未入列");
            });
            Step("训练骑兵接口",()=>{ Debug.Log("[SELFTEST] TrainCavalry(无马厩可false)="+Military.TrainCavalry()); });
            Step("讨伐首个割据势力",()=>{
                Military.InitFactions();
                var f=Military.Factions.Find(x=>!x.Destroyed);
                if(f==null) throw new System.Exception("无割据势力");
                if(!Military.LaunchCampaign(f.Id)) throw new System.Exception("LaunchCampaign=false");
            });
            Step("海战生成我方战船",()=>{ if(Naval.DebugSpawnOwnShip()==null) throw new System.Exception("造舰失败"); });
            Step("建立殖民地",()=>{
                if(!Colonization.EraOpen) throw new System.Exception("殖民时代未开");
                if(!Colonization.FoundColony()){ string why; Colonization.CanFound(out why); throw new System.Exception("FoundColony=false:"+why); }
            });
            Step("殖民地升格与镇压",()=>{
                var c=S.Colonies[S.Colonies.Count-1];
                if(!Colonization.Upgrade(c)) throw new System.Exception("Upgrade=false");
                Colonization.Suppress(c);
            });
            Step("海洋副本探索",()=>{
                Expedition.Prepare("ocean");
                for(int i=0;i<6;i++) Expedition.Move("ocean", i%2==0?1:0, i%2==0?0:1);
                Expedition.AutoExplore("ocean"); Expedition.ColonizeHere(); Expedition.ReturnHome("ocean");
                if(!S.OceanExp.Inited) throw new System.Exception("海洋网格未初始化");
            });
            Step("太空副本探索",()=>{
                Expedition.Prepare("space");
                for(int i=0;i<6;i++) Expedition.AutoExplore("space");
                Expedition.BuildOutpostHere(); Expedition.ReturnHome("space");
                if(!S.SpaceExp.Inited) throw new System.Exception("太空网格未初始化");
            });
            Step("存档读档往返",()=>{
                int col=S.Colonies.Count, fu=S.FriendlyUnits.Count; bool oe=S.OceanExp.Inited;
                SaveSystem.SaveToSlot(3);
                if(!SaveSystem.LoadFromSlot(3)) throw new System.Exception("读档失败");
                if(S.Colonies.Count!=col) throw new System.Exception("殖民地未保留 "+S.Colonies.Count+"/"+col);
                if(S.FriendlyUnits.Count!=fu) throw new System.Exception("机动部队未保留 "+S.FriendlyUnits.Count+"/"+fu);
                if(oe && !S.OceanExp.Inited) throw new System.Exception("海洋副本网格未保留");
            });
            Debug.Log("[SELFTEST] ==== V616 RESULT pass="+pass+" fail="+fail+" :: "+log);
        }
        /// <summary>加速：0-9 一次+1；10-99 一次+10；≥100 一次+100（对齐 v5.9.9 speedUp），并解除暂停</summary>
        public void SpeedUp()
        {
            float s = State.Speed;
            if (s < 10f) s = Mathf.Min(10f, s + 1f);
            else if (s < 100f) s = Mathf.Min(100f, s + 10f);
            else s = Mathf.Min(MaxSpeed, s + 100f);
            State.Speed = s; State.Paused = false; StateType = GameStateType.Playing;
        }
        /// <summary>减速：>100 一次-100；>10 一次-10；>1 一次-1，最低1倍速（对齐 v5.9.9 speedDown）</summary>
        public void SpeedDown()
        {
            float s = State.Speed;
            if (s > 100f) s = Mathf.Max(100f, s - 100f);
            else if (s > 10f) s = Mathf.Max(10f, s - 10f);
            else if (s > 1f) s = Mathf.Max(1f, s - 1f);
            State.Speed = s;
        }
        /// <summary>滑条设速：取整并限制在 [1, MaxSpeed]</summary>
        public void SetSpeedClamped(float v) => State.Speed = Mathf.Clamp(Mathf.Round(v), 1f, MaxSpeed);
        public void SetSpeed(float v) => State.Speed = v;

        /// <summary>Debug密码门：ToFuture解锁1000倍速与全部修改功能</summary>
        public bool TryUnlockDebug(string password)
        {
            if (password == GameConstants.DebugPassword)
            {
                State.DebugLevel = 2;
                AddEvent("good", "🔓 Debug面板已解锁（1000倍速/全资源/时代跳转）");
                return true;
            }
            AddEvent("bad", "🔒 密码错误");
            return false;
        }

        // ===== 事件日志 =====
        public void AddEvent(string kind, string text)
        {
            var e = new LogEntry(State.Year, kind, text);
            State.EventLog.Insert(0, e);
            if (State.EventLog.Count > 200) State.EventLog.RemoveAt(State.EventLog.Count - 1);
            OnEventLogged?.Invoke(e);
        }

        public BuildingDefinition Def(string id) => Buildings.TryGetValue(id, out var d) ? d : null;
        public EraDefinition Era => Eras != null && State.Era < Eras.Count ? Eras[State.Era] : null;
    }
}
