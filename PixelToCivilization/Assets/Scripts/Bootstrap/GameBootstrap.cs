using UnityEngine;
using PixelToCivilization.Core;
using PixelToCivilization.World;
using PixelToCivilization.Buildings;
using PixelToCivilization.UI;
using PixelToCivilization.Rendering;

namespace PixelToCivilization.Bootstrap
{
    /// <summary>
    /// 游戏启动器 —— 运行时代码搭建全部场景：管理器、地形、相机、光照、建筑工厂、输入、UI。
    /// 用法：新建空场景，创建空物体挂载本脚本，点Play即可；或菜单「像素到文明 → 一键搭建场景」。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameBootstrap : MonoBehaviour
    {
        [Header("地形种子（相同种子=相同地图）")]
        public int TerrainSeed = 20260829;
        public bool AutoBootOnStart = true;

        /// <summary>打开工程直接 Play 即自动搭建，无需在场景里手动挂脚本/拖引用</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBoot()
        {
            if (GameManager.Instance != null) return;
            if (FindObjectOfType<GameBootstrap>() != null) return; // 场景已手动挂载则交给其Start
            var go = new GameObject("=== 从像素到文明 V6.1.2 随机大陆版 (Auto) ===");
            go.AddComponent<GameBootstrap>().Boot();
        }

        private void Start()
        {
            if (AutoBootOnStart && GameManager.Instance==null) Boot();
        }

        public void Boot()
        {
            // 0. V6.1.1 画质自适应：手机/WebGL 或内存≤3.5GB 走性能档（贴图128、关景深/颗粒/色散）
            bool highEnd = Application.platform!=RuntimePlatform.WebGLPlayer && SystemInfo.systemMemorySize>3500;
            if (!highEnd) PixelToCivilization.Art.ProceduralTextures.Res=128;

            // 1. 游戏管理器（Awake加载数据库；显式再调一次以兼容 Edit 模式/冒烟测试，幂等）
            var gmGo=new GameObject("GameManager");
            var gm=gmGo.AddComponent<GameManager>();
            gm.EnsureAwake();

            // 2. 世界地形
            var worldGo=new GameObject("World");
            var terrain=worldGo.AddComponent<WorldGenerator>();
            terrain.GenerateBalanced(TerrainSeed);
            // V6.1.1 程序化植被（树/草静态合并）
            var veg=worldGo.AddComponent<VegetationSystem>();
            veg.Populate(terrain,TerrainSeed);

            // 3. 建筑网格工厂（BuildingSystem.Init 时查找）
            var factoryGo=new GameObject("BuildingMeshFactory");
            var factory=factoryGo.AddComponent<BuildingMeshFactory>();

            // 4. 装配全部游戏子系统
            gm.InstallSystems();
            // 建筑点击→弹出信息
            factory.ClickHandler = b => FindObjectOfType<UIManager>()?.ShowBuilding(b);

            // 5. 相机 + 轨道控制
            var camGo=new GameObject("MainCamera");
            camGo.tag="MainCamera";
            var cam=camGo.AddComponent<Camera>();
            cam.clearFlags=CameraClearFlags.SolidColor;
            cam.backgroundColor=new Color(0.55f,0.7f,0.85f);
            cam.nearClipPlane=0.3f;cam.farClipPlane=2000f;
            camGo.AddComponent<AudioListener>();
            var rig=camGo.AddComponent<CameraRig>();
            var target=new GameObject("CameraTarget");target.transform.position=new Vector3(terrain.SettlementCenter.x,terrain.SettlementCenter.y,0);
            rig.Target=target.transform;rig.Distance=108f;

            // 5.1 V6.1.1 环境光照（天空盒/主光/三波段环境光/雾）+ 电影级后处理
            var envGo=new GameObject("Environment");
            envGo.AddComponent<EnvironmentDirector>();
            PostProcessDirector.Ensure(cam).SetQuality(highEnd);

            // 6. 输入控制
            gmGo.AddComponent<GameInputController>();

            // 7. UI
            var uiGo=new GameObject("UIRoot");
            var ui=uiGo.AddComponent<UIManager>();
            ui.Boot(gm);

            Debug.Log("[GameBootstrap] 场景搭建完成，点击「开始」进入游戏");
        }
    }
}
