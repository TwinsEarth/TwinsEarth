using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using PixelToCivilization.Core;
using PixelToCivilization.Data;

namespace PixelToCivilization.UI
{
    /// <summary>
    /// UI总管理器 —— 运行时UGUI 1:1 复刻 v5.9.9 界面：
    /// 开始页/顶部资源时间栏/左侧建造面板/右侧国家状态与操作/底部速度栏/事件日志/通知/时代过场。模态面板见 UIManager.Panels.cs
    /// </summary>
    public partial class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }
        private GameManager GM;
        private GameState S =>GM.State;

        private GameObject _splash, _hud;
        private Text _eraTag,_dynastyTag,_timeText,_gregText,_popText,_envText;
        private readonly Dictionary<string,Text>_resTexts=new();
        private Transform _buildList;
        private GameObject _leftPanel;
        private readonly Dictionary<string,Button> _toolBtns=new();
        private bool _muted;
        // V6.1.2 存档面板
        private GameObject _saveModal;
        private RectTransform _saveSlotBox;
        private Text _saveCountdown;
        private InputField _importField;
        private Text _eventText;
        // V6.1.1 国家状态四组可折叠（📊基础/⚔️军事/👥社会/🏛️时代），对齐 v5.9.9
        private Text _statBasic,_statMil,_statSoc,_statEra;
        private readonly bool[] _statOpen={true,true,true,true};
        private Text _speedText;
        private Slider _speedSlider;
        private GameObject _oceanBtn,_spaceBtn;
        private Text _toast,_eraTitle,_eraDesc; private GameObject _eraTransition;
        // V6.1.9 画面中顶冷冻冷却倒计时条
        private GameObject _cryoBar; private Text _cryoText;
        private float _refreshCd;
        private string _activeCat="居住";
        // V6.1.2 开始页：继续上次游戏 + 10 秒无操作自动开新局
        private Text _startLabel;
        private Button _continueBtn;
        private const float AutoStartSeconds=10f;
        private float _autoCd=AutoStartSeconds;
        private bool _splashArmed;
        private Vector3 _lastMousePos;

        public void Boot(GameManager gm)
        {
            Instance=this; GM=gm;
            Build();
            GM.OnStateChanged += OnStateChanged;
            GM.OnEventLogged += _=>RefreshEventLog();
            GM.Time.OnEraChanged += (n,o)=>ShowEraTransition(GM.Eras[n].Name,GM.Eras[n].Feature);
        }

        // ============ 搭建 ============
        private void Build()
        {
            var canvas=UITheme.CreateCanvas("UICanvas");
            BuildSplash(canvas.transform);
            _hud=UITheme.Panel("HUD",canvas.transform,new Color(0,0,0,0));
            Stretch(_hud);
            // 关键修复：HUD 为全屏透明底板，Image 默认 raycastTarget=true 会拦截全部指针射线，
            // 导致 IsPointerOverGameObject() 恒为 true —— 相机旋转/平移与地面建造点击全部失效
            _hud.GetComponent<Image>().raycastTarget=false;
            BuildTopBarV2(_hud.transform);
            BuildCryoBar(_hud.transform);   // V6.1.9 中顶冷冻倒计时
            BuildLeftPanelV2(_hud.transform);
            BuildRightPanelV2(_hud.transform);
            BuildBottomBarV2(_hud.transform);
            BuildLeftBottomCluster(_hud.transform);
            BuildMarkerLayer(_hud.transform);
            BuildModals(_hud.transform);          // 先建 _modalLayer，帮助/日志弹窗才能正确挂到 Canvas 下
            BuildHelpModals(_hud.transform);
            BuildToastAndEra(_hud.transform);
            BuildMinimap(_hud.transform);
            _hud.SetActive(false);
        }

        private void Stretch(GameObject go)
        {
            var rt=go.GetComponent<RectTransform>();
            rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=Vector2.zero;rt.offsetMax=Vector2.zero;
        }

        // ---- 开始页 ----
        private void BuildSplash(Transform parent)
        {
            _splash=UITheme.Panel("Splash",parent,new Color(0.07f,0.45f,0.80f,0.98f));Stretch(_splash); // V7.0.2 海蓝
            // 全屏底板不得拦截射线：只让按钮自身的 Image 参与射线（否则整屏挡住场景点击）
            _splash.GetComponent<Image>().raycastTarget=false;
            var title=UITheme.Label("Title",_splash.transform,"从 像 素 到 文 明",64,TextAnchor.MiddleCenter,UITheme.HexA(0xffffff,1));
            Place(title.rectTransform,new Vector2(0.5f,0.68f),new Vector2(0.5f,0.68f),new Vector2(-400,-40),new Vector2(400,40));
            var sub=UITheme.Label("Sub",_splash.transform,"V7.0.2 · 明亮卡通 · 世界奇观 · 船只永留外海",24,TextAnchor.MiddleCenter,UITheme.HexA(0xf2f8ff,1));
            Place(sub.rectTransform,new Vector2(0.5f,0.56f),new Vector2(0.5f,0.56f),new Vector2(-400,-18),new Vector2(400,18));
            // 主按钮：开始新游戏（带 10 秒无操作自动开局倒计时）
            var start=UITheme.Btn("Start",_splash.transform,"",26,UITheme.BtnGold); // V7.0.2 橙色主按钮
            Place(start.GetComponent<RectTransform>(),new Vector2(0.5f,0.44f),new Vector2(0.5f,0.44f),new Vector2(-130,-42),new Vector2(130,42));
            _startLabel=start.GetComponentInChildren<Text>();
            start.onClick.AddListener(OnClickStart);
            // 次按钮：继续上次游戏（无 0 号存档时置灰）
            _continueBtn=UITheme.Btn("Continue",_splash.transform,"继续上次游戏",20,UITheme.HexA(0xf6f4ee,0.97f));
            Place(_continueBtn.GetComponent<RectTransform>(),new Vector2(0.5f,0.36f),new Vector2(0.5f,0.36f),new Vector2(-130,-34),new Vector2(130,34));
            bool hasSave = GM.SaveSystem!=null && GM.SaveSystem.LatestSlot()>=0;
            _continueBtn.interactable=hasSave;
            _continueBtn.GetComponent<Image>().color = hasSave ? UITheme.HexA(0xf6f4ee,0.97f) : UITheme.HexA(0xc2c8d0,0.85f); // V7.0.2
            _continueBtn.onClick.AddListener(OnClickContinue);
            // 自动开局倒计时武装
            ArmAutoStart();
            var ver=UITheme.Label("Ver",_splash.transform,"v7.0.2 · Unity / Tuanjie 1.6.12 · URP 高清 · 明亮卡通+世界奇观",16,TextAnchor.LowerCenter,UITheme.HexA(0xdceeff,1));
            Place(ver.rectTransform,new Vector2(0.5f,0.22f),new Vector2(0.5f,0.22f),new Vector2(-300,-15),new Vector2(300,15));
            var hint=UITheme.Label("FullHint",_splash.transform,"提示：界面太小时，按 F11 或点底部「全屏」按钮 · 10 秒无操作将自动开新局",14,TextAnchor.MiddleCenter,UITheme.HexA(0xd0e6ff,1));
            Place(hint.rectTransform,new Vector2(0.5f,0.28f),new Vector2(0.5f,0.28f),new Vector2(-360,-12),new Vector2(360,12));
        }
        private void OnClickStart()
        {
            // V6.1.2：每次开始都随机重新生成大地图，并生成初始聚落/人口/飞鸟（方法内部完成重置与PopulateInitial）
            _splashArmed=false;
            GM.StartNewRandomGame();
        }
        private void OnClickContinue()
        {
            _splashArmed=false;
            if (!GM.ContinueLastGame())
            {   // 存档失效则回退为新游戏
                GM.StartNewRandomGame();
            }
        }

        // —— 10 秒无操作自动开新局 ——
        private void ArmAutoStart()
        {
            _autoCd=AutoStartSeconds; _splashArmed=true;
            _lastMousePos=Input.mousePosition;
            RefreshAutoStartLabel();
        }
        private void RefreshAutoStartLabel()
        {
            if (_startLabel!=null)
                _startLabel.text = _splashArmed ? $"开始新游戏  ({Mathf.CeilToInt(_autoCd)})" : "开始新游戏";
        }
        /// <summary>开始页每帧推进自动开局倒计时；任意键鼠/触摸操作都会重置 10 秒。返回是否处于开始页。</summary>
        private void TickSplashAutoStart()
        {
            if (_splash==null || !_splash.activeSelf) { _splashArmed=false; return; }
            if (!_splashArmed) ArmAutoStart();
            // 任意操作即重置：按键、鼠标按下、鼠标移动、触摸
            Vector3 mp=Input.mousePosition;
            bool interacted = Input.anyKeyDown || Input.GetMouseButtonDown(0)||Input.GetMouseButtonDown(1)||Input.GetMouseButtonDown(2)
                              || (mp-_lastMousePos).sqrMagnitude>4f || Input.touchCount>0;
            _lastMousePos=mp;
            if (interacted) _autoCd=AutoStartSeconds;
            _autoCd-=Time.unscaledDeltaTime;
            RefreshAutoStartLabel();
            if (_autoCd<=0f)
            {
                _splashArmed=false;
                GM.StartNewRandomGame();
            }
        }
        private void OnStateChanged(GameStateType t)
        {
            bool playing = t!=GameStateType.Menu;
            _splash.SetActive(!playing); _hud.SetActive(playing);
            if (playing) { _splashArmed=false; RebuildBuildListV2(); }
            else ArmAutoStart();
        }

        // ---- 顶部栏 ----
        private void BuildTopBarLegacy(Transform parent)
        {
            var bar=UITheme.Panel("TopBar",parent,UITheme.PanelBg);
            var rt=bar.GetComponent<RectTransform>();
            rt.anchorMin=new Vector2(0,1);rt.anchorMax=new Vector2(1,1);rt.pivot=new Vector2(0.5f,1);
            rt.sizeDelta=new Vector2(0,48);rt.anchoredPosition=Vector2.zero; // v5.9.9 topBar 高48
            var h=bar.AddComponent<HorizontalLayoutGroup>();
            h.padding=new RectOffset(20,20,4,4);h.spacing=18;h.childAlignment=TextAnchor.MiddleLeft;
            h.childControlWidth=true;h.childControlHeight=true;h.childForceExpandHeight=true;h.childForceExpandWidth=false;

            // 资源
            var resBox=UITheme.Panel("ResBar",bar.transform,new Color(0,0,0,0));
            var rl=resBox.AddComponent<HorizontalLayoutGroup>();rl.spacing=9;rl.childControlWidth=true;rl.childControlHeight=true;
            var rle=resBox.AddComponent<LayoutElement>();rle.flexibleWidth=1;
            foreach (var id in ResourceDatabase.Order)
            {
                var item=UITheme.Panel("Res_"+id,resBox.transform,new Color(0,0,0,0));
                var le=item.AddComponent<LayoutElement>();le.preferredWidth=78;
                var icon=UITheme.Icon(item.transform,id,18);
                Place(icon.rectTransform,new Vector2(0,0),new Vector2(0,1),new Vector2(2,-2),new Vector2(24,2));
                var v=UITheme.Label("v",item.transform,"0",13,TextAnchor.MiddleRight);
                Place(v.rectTransform,new Vector2(0,0),new Vector2(1,1),new Vector2(24,0),new Vector2(0,0));
                _resTexts[id]=v;
            }
            // 时代/朝代/年/公历/人口
            // v5.9.9：era-tag 棕色渐变块、dynasty-tag 金色描边块
            _eraTag=Pill(bar.transform,"",UITheme.HexA(0x8B4513,0.92f),120);
            _dynastyTag=Pill(bar.transform,"",UITheme.HexA(0xFFD700,0.15f),90);
            _timeText=Pill(bar.transform,"第1年",new Color(0,0,0,0.3f),70);
            _gregText=Pill(bar.transform,"公元前3000年",new Color(0,0,0,0.3f),120,UITheme.Sky);
            _popText=Pill(bar.transform,"80",new Color(0,0,0,0.3f),112); _popText.horizontalOverflow=HorizontalWrapMode.Overflow;
            _envText=Pill(bar.transform,"☀晴天",new Color(0,0,0,0.3f),248,UITheme.Sky); // V6.1.9(i) 天气/潮汐/海风
        }
        private Text Pill(Transform p,string s,Color bg,float w,Color? tc=null)
        {
            var box=UITheme.Surface("Pill",p,bg);box.AddComponent<LayoutElement>().preferredWidth=w;
            var t=UITheme.Label("t",box.transform,s,13,TextAnchor.MiddleCenter,tc??UITheme.Text,FontStyle.Bold);
            t.rectTransform.anchorMin=Vector2.zero;t.rectTransform.anchorMax=Vector2.one;
            t.rectTransform.offsetMin=Vector2.zero;t.rectTransform.offsetMax=Vector2.zero;
            return t;
        }

        // ---- V6.1.9 画面中间顶部：加速冷冻冷却倒计时（仅冷冻时显示，不拦截指针）----
        private void BuildCryoBar(Transform parent)
        {
            _cryoBar=UITheme.Surface("CryoBar",parent,new Color(0.18f,0.60f,0.92f,0.95f));
            var img=_cryoBar.GetComponent<Image>(); if(img!=null) img.raycastTarget=false;
            var rt=_cryoBar.GetComponent<RectTransform>();
            rt.anchorMin=new Vector2(0.5f,1);rt.anchorMax=new Vector2(0.5f,1);rt.pivot=new Vector2(0.5f,1);
            rt.anchoredPosition=new Vector2(0,-54); rt.sizeDelta=new Vector2(440,34);
            _cryoText=UITheme.Label("CryoText",_cryoBar.transform,"",14,TextAnchor.MiddleCenter,UITheme.HexA(0xbfe9ff,1));
            Stretch(_cryoText.gameObject);
            _cryoBar.SetActive(false);
        }

        // ---- 左侧建造面板 ----
        private void BuildLeftPanelLegacy(Transform parent)
        {
            var panel=UITheme.Panel("LeftPanel",parent,UITheme.PanelBg);
            _leftPanel=panel;
            var rt=panel.GetComponent<RectTransform>();
            // v5.9.9：left:10 top:58 宽220，距底70
            rt.anchorMin=new Vector2(0,1);rt.anchorMax=new Vector2(0,1);rt.pivot=new Vector2(0,1);
            rt.offsetMin=new Vector2(10,-1010);rt.offsetMax=new Vector2(230,-58);
            var vl=panel.AddComponent<VerticalLayoutGroup>();vl.spacing=4;vl.padding=new RectOffset(6,6,6,6);
            vl.childControlWidth=true;vl.childForceExpandWidth=true;
            UITheme.Label("Title",panel.transform,"建  造",16,TextAnchor.MiddleCenter,UITheme.Gold);
            var tabs=UITheme.Panel("CatTabs",panel.transform,new Color(0,0,0,0));
            var tg=tabs.AddComponent<GridLayoutGroup>();tg.constraint=GridLayoutGroup.Constraint.FixedColumnCount;tg.constraintCount=4;
            tg.cellSize=new Vector2(50,24);tg.spacing=new Vector2(3,3);
            tabs.AddComponent<LayoutElement>().preferredHeight=108;
            foreach (var cat in new[]{"居住","基础","食物","资源","经济","文化","工业","军事","海洋","能源","科技","太空","运输"})
            {
                var b=UITheme.Btn("tab_"+cat,tabs.transform,cat,11,new Color(1,1,1,0.06f));
                string c=cat;b.onClick.AddListener(()=>{_activeCat=c;RebuildBuildListLegacy();});
            }
            var buildScroll=UITheme.VerticalScroll("BuildScroll",panel.transform,out var content,3);
            _buildList=content;
            var le=buildScroll.gameObject.AddComponent<LayoutElement>();le.flexibleHeight=1;
        }

        private void RebuildBuildListLegacy()
        {
            if (_buildList==null) return;
            for (int i=_buildList.childCount-1;i>=0;i--) Destroy(_buildList.GetChild(i).gameObject);
            if (_activeCat=="运输"){ BuildTransportListLegacy(); return; }
            foreach (var kv in GM.Buildings)
            {
                var b=kv.Value;
                if (b.Cat!=_activeCat) continue;
                bool unlocked=b.Era<=S.Era;
                string label=b.Name+(unlocked?"":"·锁定");
                var btn=UITheme.BtnIcon("b_"+b.Id,_buildList,CatIconLegacy(b.Cat,b.Id),label,12,
                    unlocked?UITheme.BtnGold:new Color(0.3f,0.3f,0.3f,0.3f));
                btn.interactable=unlocked;
                string id=b.Id;
                btn.onClick.AddListener(()=>{ S.SelectedBuildType=id; S.Tool="build"; });
            }
        }

        /// <summary>V6.1.1 运输分类：四级锚点车辆 + 九型船只，选中后在地图点击放置（前缀 cart:/ship: 由输入控制器分流）</summary>
        private void BuildTransportListLegacy()
        {
            UITheme.Label("ct",_buildList,"— 车辆（陆地）—",12,TextAnchor.MiddleCenter,UITheme.Gold);
            foreach (var kv in GM.Cart.Defs)
            {
                var d=kv.Value;
                var btn=UITheme.Btn("cart_"+d.Id,_buildList,$"{d.Icon} {d.Name}  {CostText(d.Cost)}",12,UITheme.BtnGold);
                string id=d.Id;
                btn.onClick.AddListener(()=>{S.SelectedBuildType="cart:"+id;S.Tool="build";});
            }
            UITheme.Label("st",_buildList,"— 船只（水域）—",12,TextAnchor.MiddleCenter,UITheme.Gold);
            foreach (var kv in GM.Naval.Defs)
            {
                var d=kv.Value;
                bool unlocked=d.Era<=S.Era;
                var btn=UITheme.Btn("ship_"+d.Id,_buildList,$"{d.Icon} {d.Name}{(unlocked?"":"·锁定")}  {CostText(d.Cost)}",12,
                    unlocked?UITheme.BtnGold:new Color(0.3f,0.3f,0.3f,0.3f));
                btn.interactable=unlocked;
                string id=d.Id;
                btn.onClick.AddListener(()=>{S.SelectedBuildType="ship:"+id;S.Tool="build";});
            }
        }

        /// <summary>建筑分类→矢量图标 key（个别特殊建筑按 id 精确匹配）</summary>
        private static string CatIconLegacy(string cat,string id)
        {
            switch (id)
            {
                case "great_wall": case "watchtower": case "tower": return "tower";
                case "canal": return "canal";
                case "highway": case "road": case "railway_pre": case "high_speed_rail": return "road";
                case "airport": return "airplane";
                case "space_elevator": case "rocket": return "rocket";
                case "temple": case "pagoda": return "temple";
                case "market": case "bank": return "market";
                case "farm": return "farm";
            }
            return cat switch
            {
                "居住"=>"house","基础"=>"house","食物"=>"food","资源"=>"iron","经济"=>"bank",
                "文化"=>"culture","工业"=>"factory","军事"=>"military","海洋"=>"ship",
                "能源"=>"power","科技"=>"research","太空"=>"rocket",_=>"house",
            };
        }

        // ---- 右侧面板 ----
        private void BuildRightPanelLegacy(Transform parent)
        {
            var panel=UITheme.Panel("RightPanel",parent,UITheme.PanelBg);
            var rt=panel.GetComponent<RectTransform>();
            // v5.9.9：right:10 top:58 宽240，距底70
            rt.anchorMin=new Vector2(1,1);rt.anchorMax=new Vector2(1,1);rt.pivot=new Vector2(1,1);
            rt.offsetMin=new Vector2(-250,-1010);rt.offsetMax=new Vector2(-10,-58);
            var vl=panel.AddComponent<VerticalLayoutGroup>();vl.spacing=6;vl.padding=new RectOffset(8,8,8,8);
            vl.childControlWidth=true;vl.childForceExpandWidth=true;
            UITheme.Label("T",panel.transform,"国 家 状 态",16,TextAnchor.MiddleCenter,UITheme.Gold);
            BuildStatsSections(panel.transform);
            // 操作按钮（V6.1.1 矢量图标）
            var actions=UITheme.Panel("Actions",panel.transform,new Color(0,0,0,0));
            var ag=actions.AddComponent<GridLayoutGroup>();ag.constraint=GridLayoutGroup.Constraint.FixedColumnCount;ag.constraintCount=3;
            ag.cellSize=new Vector2(72,30);ag.spacing=new Vector2(4,4);
            actions.AddComponent<LayoutElement>().preferredHeight=136;
            UITheme.BtnIcon("tech",actions.transform,"research","科技",12).onClick.AddListener(OpenTechModal);
            UITheme.BtnIcon("policy",actions.transform,"culture","政策",12).onClick.AddListener(OpenPolicyModal);
            UITheme.BtnIcon("gods",actions.transform,"god","九神",12).onClick.AddListener(OpenGodsModal);
            UITheme.BtnIcon("philosophy",actions.transform,"culture","百家",12).onClick.AddListener(OpenPhilosophyModal);
            UITheme.BtnIcon("army",actions.transform,"military","征兵",12).onClick.AddListener(()=>GM.Military.TrainSoldiers());
            UITheme.BtnIcon("cavalry",actions.transform,"military","骑兵",12).onClick.AddListener(()=>GM.Military.TrainCavalry());
            UITheme.BtnIcon("campaign",actions.transform,"military","讨伐",12).onClick.AddListener(OpenCampaignModal);
            UITheme.BtnIcon("colony",actions.transform,"ship","殖民",12).onClick.AddListener(OpenColonyModal);
            UITheme.BtnIcon("auto",actions.transform,"setting","自动",12).onClick.AddListener(()=>GM.Building.AutoBuildEnabled=!GM.Building.AutoBuildEnabled);
            _oceanBtn=UITheme.BtnIcon("ocean",actions.transform,"ship","海洋",12).gameObject;
            _oceanBtn.GetComponent<Button>().onClick.AddListener(OpenOceanModal);_oceanBtn.SetActive(false);
            _spaceBtn=UITheme.BtnIcon("space",actions.transform,"rocket","太空",12).gameObject;
            _spaceBtn.GetComponent<Button>().onClick.AddListener(OpenSpaceModal);_spaceBtn.SetActive(false);
            // 事件日志
            UITheme.Label("LogT",panel.transform,"编 年 史",13,TextAnchor.MiddleLeft,UITheme.Gold);
            var logScroll=UITheme.VerticalScroll("LogScroll",panel.transform,out var logContent,2);
            logScroll.gameObject.AddComponent<LayoutElement>().preferredHeight=240;
            _eventText=UITheme.Label("log",logContent,"",11,TextAnchor.UpperLeft);
            _eventText.gameObject.AddComponent<LayoutElement>().preferredHeight=600;
        }

        // ---- 底部操作栏（V6.1.2 对齐 v5.9.9：全宽贴底、高56、38×38 方按钮、分段）----
        private void BuildBottomBarLegacy(Transform parent)
        {
            var bar=UITheme.Panel("BottomBar",parent,UITheme.PanelBg);
            var rt=bar.GetComponent<RectTransform>();
            rt.anchorMin=new Vector2(0,0);rt.anchorMax=new Vector2(1,0);rt.pivot=new Vector2(0.5f,0);
            rt.offsetMin=Vector2.zero;rt.offsetMax=new Vector2(0,56); // v5.9.9 bottomBar 全宽高56贴底
            var h=bar.AddComponent<HorizontalLayoutGroup>();h.spacing=8;h.padding=new RectOffset(16,16,9,9);
            h.childControlHeight=true;h.childAlignment=TextAnchor.MiddleCenter;

            // 左段：速度控制（对齐 v5.9.9：暂停/继续 − 滑条 + xN）
            var bPause=SquareBtn(bar.transform,"pause","暂停",GM.TogglePause);
            SquareBtn(bar.transform,"down","−",SpeedDown);
            _speedText=Pill(bar.transform,"1x",new Color(0,0,0,0.3f),56);
            _speedText.gameObject.AddComponent<LayoutElement>().preferredHeight=38;
            SquareBtn(bar.transform,"up","+",SpeedUp);
            BuildSpeedSlider(bar.transform);   // v5.9.9 连续倍速滑条
            BarSep(bar.transform);
            // 工具组（对齐 v5.9.9 bbTools：选择 / 建造 / 种树 / 招民）
            ToolBtn(bar.transform,"select","select");
            ToolBtn(bar.transform,"build","build");
            ToolBtn(bar.transform,"tree","tree");
            ToolBtn(bar.transform,"npc","person");
            SetTool("select");
            BarSep(bar.transform);
            // 中段弹性占位
            var spacer=UITheme.Panel("spacer",bar.transform,new Color(0,0,0,0));
            spacer.AddComponent<LayoutElement>().flexibleWidth=1;
            // 右段：存档 / 回中心 / Debug / 声音 / 全屏
            SquareBtn(bar.transform,"save","存档",OpenSaveModal);
            SquareBtn(bar.transform,"center","回中心",()=>{var rig=UnityEngine.Object.FindObjectOfType<World.CameraRig>();rig?.CenterView();});
            SquareBtn(bar.transform,"debug","Debug",OnClickDebug);
            SquareBtn(bar.transform,"sound","声音",ToggleSound);
            SquareBtn(bar.transform,"fullscreen","全屏",()=>GM.ToggleFullscreen());
        }

        // V6.1.2 工具按钮（选中态金色高亮，对齐 v5.9.9 bb-btn.active）
        private Button ToolBtn(Transform parent,string tool,string iconKey)
        {
            var b=UITheme.Btn("tool_"+tool,parent,"",12,UITheme.Chip);
            var le=b.GetComponent<LayoutElement>();le.preferredWidth=46;le.preferredHeight=44;le.minWidth=46;
            var txt=b.transform.Find("Text"); if(txt)Destroy(txt.gameObject);
            var ic=UITheme.Icon(b.transform,iconKey,12);
            ic.rectTransform.anchorMin=Vector2.zero;ic.rectTransform.anchorMax=Vector2.one;
            ic.rectTransform.offsetMin=new Vector2(11,10);ic.rectTransform.offsetMax=new Vector2(-11,-10);
            ic.gameObject.SetActive(true);
            var tip=tool switch{"select"=>"选择：点选建筑/单位查看详情","build"=>"建造：展开左侧建造面板",
                "tree"=>"种树：在点击的空地种一棵树","npc"=>"招民：在点击处生成一名村民",_=>tool};
            AddHover(b.gameObject,tip);
            b.onClick.AddListener(()=>SetTool(tool));
            _toolBtns[tool]=b;
            return b;
        }
        /// <summary>纯矢量图标方钮（WebGL 不渲染 Emoji，统一走 IconFactory）</summary>
        private Button IconSquare(Transform parent,string iconKey,System.Action onClick)
        {
            var b=UITheme.Btn("is_"+iconKey,parent,"",12,UITheme.Chip);
            var le=b.GetComponent<LayoutElement>();le.preferredWidth=40;le.preferredHeight=40;le.minWidth=40;
            var txt=b.transform.Find("Text"); if(txt)Destroy(txt.gameObject);
            var ic=UITheme.Icon(b.transform,iconKey,20);
            ic.rectTransform.anchorMin=Vector2.zero;ic.rectTransform.anchorMax=Vector2.one;
            ic.rectTransform.offsetMin=new Vector2(10,10);ic.rectTransform.offsetMax=new Vector2(-10,-10);
            b.onClick.AddListener(()=>onClick());
            return b;
        }
        private void SetTool(string tool)
        {
            GM.Tool=tool=="build"?"select":tool;   // 建造按钮只负责展开/收起左面板
            GM.State.SelectedBuildType=null;
            if (tool=="build" && _leftPanel) _leftPanel.SetActive(!_leftPanel.activeSelf);
            if (_leftPanel && tool!="build" && !_leftPanel.activeSelf) _leftPanel.SetActive(true);
            foreach(var kv in _toolBtns)
            {
                bool on=kv.Key==tool && tool!="build";
                var timg=kv.Value.GetComponent<Image>();timg.color = on?UITheme.ChipActive:UITheme.Chip;
                var ttx=kv.Value.GetComponentInChildren<Text>();if(ttx)ttx.color=on?UITheme.ChipActiveText:UITheme.Text;
            }
        }
        private void ToggleSound()
        {
            _muted=!_muted;
            AudioListener.volume=_muted?0f:1f;
            GM.AddEvent("info",_muted?"🔇 已静音":"🔊 声音开启");
        }
        // V6.1.2 简易存档面板（快速存/读 0 号槽 + 导出 JSON）
        // ============ 存档管理（V6.1.2 对齐 v5.9.9：自动槽+5手动槽/覆盖/读档/删除/导出/导入） ============
        private void OpenSaveModal()
        {
            _saveModal=CreateModal("存档管理");
            _saveModal.transform.Find("Box").GetComponent<RectTransform>().sizeDelta=new Vector2(740,640);
            var body=ModalBody(_saveModal);Clear(body);
            var root=body.AddComponent<VerticalLayoutGroup>();root.spacing=8;root.padding=new RectOffset(4,4,4,4);
            root.childControlWidth=true;root.childForceExpandWidth=true;root.childControlHeight=false;

            var quick=UITheme.Panel("Quick",body.transform,new Color(0,0,0,0));quick.AddComponent<LayoutElement>().preferredHeight=38;
            var qh=quick.AddComponent<HorizontalLayoutGroup>();qh.spacing=8;qh.childForceExpandWidth=true;
            UITheme.Btn("qs",quick.transform,"快速存档",12).onClick.AddListener(()=>{GM.SaveSystem.SaveToSlot(1);RenderSaveSlots();Toast("已存入手动槽 1");});
            UITheme.Btn("ql",quick.transform,"快速读档",12).onClick.AddListener(()=>{if(GM.SaveSystem.LoadFromSlot(1)){RenderSaveSlots();Refresh();Toast("已读取手动槽 1");}});
            UITheme.Btn("refresh",quick.transform,"刷新列表",12).onClick.AddListener(RenderSaveSlots);
            UITheme.Btn("export",quick.transform,"导出 JSON",12).onClick.AddListener(()=>{
                var json=GM.SaveSystem.Export();
                PixelToCivilization.Platform.WebFile.DownloadJson("文明_第"+S.Year+"年.json",json);
                GUIUtility.systemCopyBuffer=json;Toast("已导出并复制到剪贴板");});

            var autoBar=UITheme.Surface("AutoBar",body.transform,UITheme.HexA(0x0a3d1a,0.85f));autoBar.AddComponent<LayoutElement>().preferredHeight=34;
            var ah=autoBar.AddComponent<HorizontalLayoutGroup>();ah.padding=new RectOffset(12,12,4,4);ah.childControlWidth=true;ah.childForceExpandWidth=true;ah.childAlignment=TextAnchor.MiddleCenter;
            UITheme.Label("al",autoBar.transform,"自动存档已开启（每 5 分钟覆盖自动槽 0，读档不中断）",12,TextAnchor.MiddleLeft,UITheme.HexA(0x7dff9a,1));
            _saveCountdown=UITheme.Label("ar",autoBar.transform,"",12,TextAnchor.MiddleRight,UITheme.HexA(0x7dff9a,1));

            var slotSr=UITheme.VerticalScroll("SlotScroll",body.transform,out _saveSlotBox,6);slotSr.gameObject.AddComponent<LayoutElement>().flexibleHeight=1;
            _saveSlotBox.GetComponent<VerticalLayoutGroup>().childControlHeight=true;

            var imp=UITheme.Surface("Import",body.transform,new Color(0.05f,0.12f,0.16f,0.08f));imp.AddComponent<LayoutElement>().preferredHeight=96;
            var il=imp.AddComponent<VerticalLayoutGroup>();il.spacing=4;il.padding=new RectOffset(8,8,6,6);il.childControlWidth=true;il.childForceExpandWidth=true;
            UITheme.Label("impt",imp.transform,"粘贴存档 JSON 后导入（跨设备迁移用）：",11,TextAnchor.MiddleLeft,UITheme.Sub);
            var inputGo=UITheme.Panel("ImportInput",imp.transform,new Color(0.93f,0.96f,0.98f,1f));inputGo.AddComponent<LayoutElement>().preferredHeight=30;
            _importField=inputGo.AddComponent<InputField>();
            var itxt=UITheme.Label("itxt",inputGo.transform,"",11,TextAnchor.UpperLeft);itxt.rectTransform.offsetMin=new Vector2(6,2);itxt.rectTransform.offsetMax=new Vector2(-6,-2);
            _importField.textComponent=itxt;_importField.lineType=InputField.LineType.MultiLineNewline;
            UITheme.Btn("importBtn",imp.transform,"导入粘贴的存档",12).onClick.AddListener(()=>{
                if(string.IsNullOrWhiteSpace(_importField.text)){Toast("请先粘贴存档 JSON",false);return;}
                if(GM.SaveSystem.Import(_importField.text)){RenderSaveSlots();Refresh();Toast("导入成功");}else Toast("导入失败：格式错误",false);});
            RenderSaveSlots();
        }
        private void RenderSaveSlots()
        {
            if(_saveSlotBox==null)return;
            // 只销毁子槽位行，绝不能用 Clear()：Clear 会连同 content 自身的 VerticalLayoutGroup 一起销毁，导致整列塌缩
            for(int i=_saveSlotBox.childCount-1;i>=0;i--) Destroy(_saveSlotBox.GetChild(i).gameObject);
            for(int slot=0;slot<=SaveSystem.ManualSlots;slot++) SaveSlotRow(_saveSlotBox,GM.SaveSystem.Summarize(slot));
            LayoutRebuilder.ForceRebuildLayoutImmediate(_saveSlotBox);
        }
        private void SaveSlotRow(Transform parent,SlotSummary sum)
        {
            var row=UITheme.Surface("Slot"+sum.Slot,parent,UITheme.HexA(0xffffff,0.06f));
            var rle=row.AddComponent<LayoutElement>();rle.preferredHeight=58;rle.layoutPriority=1;   // 优先级高于同行的 HLG 自动高度，防止塌缩为0
            var h=row.AddComponent<HorizontalLayoutGroup>();h.padding=new RectOffset(10,8,6,6);h.spacing=8;h.childControlWidth=true;h.childControlHeight=true;h.childForceExpandWidth=true;h.childForceExpandHeight=true;
            var info=UITheme.Panel("info",row.transform,new Color(0,0,0,0));var ile=info.AddComponent<LayoutElement>();ile.flexibleWidth=1;ile.minHeight=46;
            var iv=info.AddComponent<VerticalLayoutGroup>();iv.spacing=2;iv.childControlWidth=true;iv.childForceExpandWidth=true;iv.childControlHeight=true;iv.childForceExpandHeight=false;iv.childAlignment=TextAnchor.MiddleLeft;
            string head=sum.Slot==0?"自动存档槽":"手动存档槽 "+sum.Slot;
            var nameL=UITheme.Label("name",info.transform,sum.Damaged?"【损坏】存档"+sum.Slot:(sum.Exists?head+" · "+sum.Name:"【空】"+head),13,TextAnchor.MiddleLeft,sum.Exists?UITheme.Gold:UITheme.Sub);
            nameL.gameObject.AddComponent<LayoutElement>().preferredHeight=22;
            string detail;
            if(sum.Damaged)detail="数据损坏，建议删除";
            else if(sum.Exists){var dt=DateTimeOffset.FromUnixTimeSeconds(sum.Time).LocalDateTime;detail="第"+sum.Year+"年 · "+sum.Dynasty+" · 建筑"+sum.BuildingCount+" · "+dt.ToString("MM-dd HH:mm");}
            else detail="尚未保存";
            var detL=UITheme.Label("detail",info.transform,detail,11,TextAnchor.MiddleLeft,UITheme.Sub);
            detL.gameObject.AddComponent<LayoutElement>().preferredHeight=18;
            var btns=UITheme.Panel("btns",row.transform,new Color(0,0,0,0));var ble=btns.AddComponent<LayoutElement>();ble.preferredWidth=sum.Slot==0?96:258;ble.minHeight=46;
            btns.GetComponent<RectTransform>().localScale=Vector3.one;
            var bh=btns.AddComponent<HorizontalLayoutGroup>();bh.spacing=6;bh.childControlWidth=true;bh.childForceExpandWidth=true;
            if(sum.Damaged)
            {
                if(sum.Slot!=0)UITheme.Btn("del",btns.transform,"删除",11).onClick.AddListener(()=>{GM.SaveSystem.DeleteSlot(sum.Slot);RenderSaveSlots();});
                return;
            }
            if(sum.Slot==0)
            {
                UITheme.Btn("load",btns.transform,"读档",11).onClick.AddListener(()=>{if(GM.SaveSystem.LoadFromSlot(0))Refresh();});
            }
            else
            {
                UITheme.Btn("save",btns.transform,sum.Exists?"覆盖":"存档",11).onClick.AddListener(()=>{GM.SaveSystem.SaveToSlot(sum.Slot);RenderSaveSlots();});
                UITheme.Btn("load",btns.transform,"读档",11).onClick.AddListener(()=>{if(GM.SaveSystem.LoadFromSlot(sum.Slot))Refresh();});
                UITheme.Btn("del",btns.transform,"删除",11).onClick.AddListener(()=>{GM.SaveSystem.DeleteSlot(sum.Slot);RenderSaveSlots();});
            }
        }
        /// <summary>v5.9.9 底部 38×38 方形按钮（UITheme.Btn 已自带 LayoutElement，复用而非重复添加，避免 UGUI 每帧重建异常）</summary>
        private Button SquareBtn(Transform parent,string key,string label,System.Action onClick)
        {
            var b=UITheme.Btn(key,parent,label,12,UITheme.Chip);
            var le=b.GetComponent<LayoutElement>();
            le.preferredWidth=label.Length>=3?72:38;le.preferredHeight=38;le.minWidth=38;
            b.onClick.AddListener(()=>onClick());
            return b;
        }
        private void BarSep(Transform parent)
        {
            var sep=UITheme.Panel("sep",parent,new Color(0.10f,0.20f,0.25f,0.18f));
            var le=sep.AddComponent<LayoutElement>();le.preferredWidth=1;le.preferredHeight=32;
        }
        /// <summary>速度滑条（1→当前等级上限 100/300/1000），对齐 v5.9.9 speedSlider</summary>
        private void BuildSpeedSlider(Transform parent)
        {
            var go=UITheme.Panel("speedSlider",parent,new Color(0,0,0,0.25f));
            var le=go.AddComponent<LayoutElement>();le.preferredWidth=140;le.preferredHeight=26;le.minWidth=110;
            var slider=go.AddComponent<Slider>();
            slider.minValue=1;slider.maxValue=GM.MaxSpeed;slider.wholeNumbers=true;slider.value=1;
            // 背景
            var bg=UITheme.Panel("Background",go.transform,UITheme.HexA(0x000000,0.4f));
            Stretch(bg);
            // Fill
            var fillArea=UITheme.Panel("Fill Area",go.transform,new Color(0,0,0,0));
            var fa=fillArea.GetComponent<RectTransform>();fa.anchorMin=Vector2.zero;fa.anchorMax=Vector2.one;fa.offsetMin=new Vector2(6,4);fa.offsetMax=new Vector2(-6,-4);
            var fill=UITheme.Panel("Fill",fillArea.transform,UITheme.Gold);
            var fr=fill.GetComponent<RectTransform>();fr.anchorMin=Vector2.zero;fr.anchorMax=Vector2.one;fr.offsetMin=Vector2.zero;fr.offsetMax=Vector2.zero;
            // Handle
            var harea=UITheme.Panel("Handle Slide Area",go.transform,new Color(0,0,0,0));
            var ha=harea.GetComponent<RectTransform>();ha.anchorMin=Vector2.zero;ha.anchorMax=Vector2.one;ha.offsetMin=new Vector2(8,0);ha.offsetMax=new Vector2(-8,0);
            var handle=UITheme.Panel("Handle",harea.transform,UITheme.Hex(0xffe9b0));
            var hr=handle.GetComponent<RectTransform>();hr.sizeDelta=new Vector2(14,0);
            slider.targetGraphic=handle.GetComponent<Image>();
            slider.fillRect=fr;slider.handleRect=hr;
            slider.direction=Slider.Direction.LeftToRight;
            slider.onValueChanged.AddListener(v=>{ if(!_suppressSlider) GM.SetSpeedClamped(v); });
            _speedSlider=slider;
        }
        private bool _suppressSlider;
        private void SpeedUp(){GM.SpeedUp();SyncSlider();}
        private void SpeedDown(){GM.SpeedDown();SyncSlider();}
        private void SyncSlider(){ if(_speedSlider){_suppressSlider=true;_speedSlider.maxValue=GM.MaxSpeed;_speedSlider.value=S.Speed;_suppressSlider=false;} }
        private void OnClickDebug()
        {
            if (S.DebugLevel>=2) { OpenDebugModal(); return; }
            var prompt=gameObject.AddComponent<DebugPasswordPrompt>();
            prompt.Show(GM, OpenDebugModal, _hud!=null?_hud.transform:null);
        }

        /// <summary>V6.3.5 Debug 控制台（密码 ToFuture 解锁后 Lv2 全权限）：状态总览 + 全部15资源 + 时间进度 + 海洋/太空副本 + 社会 + 世界军事AI + 结局存档。</summary>
        private void OpenDebugModal()
        {
            var modal=CreateModal("Debug 控制台（Lv"+S.DebugLevel+" 全权限 · 最高 "+GM.MaxSpeed+" 倍速）");
            modal.transform.Find("Box").GetComponent<RectTransform>().sizeDelta=new Vector2(780,680);
            var outer=ModalBody(modal);Clear(outer);
            var ol=outer.AddComponent<VerticalLayoutGroup>();ol.spacing=6;ol.padding=new RectOffset(2,2,2,2);
            ol.childControlWidth=true;ol.childForceExpandWidth=true;ol.childControlHeight=true;ol.childForceExpandHeight=false;

            var st=UITheme.Surface("DbgStat",outer.transform,UITheme.HexA(0x232e50,0.9f));st.AddComponent<LayoutElement>().preferredHeight=58;
            var sv=st.AddComponent<VerticalLayoutGroup>();sv.spacing=2;sv.padding=new RectOffset(12,12,6,6);
            sv.childControlWidth=true;sv.childForceExpandWidth=true;sv.childControlHeight=false;sv.childForceExpandHeight=false;
            UITheme.Label("l1",st.transform,"第"+S.Year+"年 · "+GM.Time.DynastyName+" · "+GM.Time.EraName+"　地图："+(S.CurrentMap=="home"?"母大陆":S.CurrentMap),13,TextAnchor.MiddleLeft,UITheme.Gold,FontStyle.Bold);
            UITheme.Label("l2",st.transform,"人口 "+S.Pop+"/"+Mathf.RoundToInt(S.Housing)+"　建筑 "+S.Buildings.Count+"　状态 "+(S.WarActive?"战争中":"和平")+"　倍速 x"+(int)S.Speed+"　航海 "+(S.AgeOfSail?"已开":"未开"),12,TextAnchor.MiddleLeft,UITheme.Sub);

            var dbgSr=UITheme.VerticalScroll("DbgScroll",outer.transform,out var content,6);dbgSr.gameObject.AddComponent<LayoutElement>().flexibleHeight=1;
            var cvlg=content.GetComponent<VerticalLayoutGroup>();
            cvlg.childControlWidth=true;cvlg.childForceExpandWidth=true;cvlg.childControlHeight=true;cvlg.childForceExpandHeight=false;
            cvlg.padding=new RectOffset(2,12,12,6);
            dbgSr.movementType=ScrollRect.MovementType.Clamped;dbgSr.scrollSensitivity=30f;
            Transform grid=null;
            void Section(string t){ var p=UITheme.Panel("sec",content,new Color(0,0,0,0));p.AddComponent<LayoutElement>().preferredHeight=30; UITheme.Label("s",p.transform,t,14,TextAnchor.MiddleLeft,UITheme.Gold,FontStyle.Bold); }
            void BeginGrid(int count){ var g=UITheme.Panel("g",content,new Color(0,0,0,0));int rows=Mathf.CeilToInt(count/3f);var gle=g.AddComponent<LayoutElement>();gle.preferredHeight=rows*34+(rows-1)*6+6;var gl=g.AddComponent<GridLayoutGroup>();gl.constraint=GridLayoutGroup.Constraint.FixedColumnCount;gl.constraintCount=3;gl.cellSize=new Vector2(236,34);gl.spacing=new Vector2(6,6);grid=g.transform; }
            void DBtn(string t,System.Action act){ var b=UITheme.Btn("d",grid,t,12);b.onClick.AddListener(()=>{try{act();Toast("已执行："+t);Refresh();}catch(System.Exception e){Toast("执行失败："+t+"："+e.Message,false);Debug.LogError("[DEBUG-BTN] "+e);}}); AddHover(b.gameObject,t); }
            void SpeedV(int v){ DBtn("x"+v+" 倍速",()=>{S.Speed=Mathf.Min(v,GM.MaxSpeed);SyncSlider();}); }

            // —— 资源修改：覆盖全部 15 种资源 ——
            Section("资源修改（全部 "+ResourceDatabase.Order.Length+" 种，各 +1000）");
            BeginGrid(ResourceDatabase.Order.Length+3);
            foreach(var k in ResourceDatabase.Order)
            {
                string key=k; string rn=ResourceDatabase.Names.TryGetValue(key,out var nm)?nm:key;
                DBtn("+1000 "+rn,()=>S.AddRes(key,1000));
            }
            DBtn("全部资源 +10000",()=>{foreach(var k in ResourceDatabase.Order)S.AddRes(k,10000);});
            DBtn("全部资源清零",()=>{foreach(var k in ResourceDatabase.Order){float gv=S.GetRes(k);if(gv>0)S.AddRes(k,-gv);}});
            DBtn("研究点 +5000",()=>S.AddRes("research",5000));

            // —— 时间与进度 ——
            Section("时间与进度"); BeginGrid(11);
            SpeedV(1);SpeedV(10);SpeedV(100);SpeedV(300);SpeedV(1000);
            DBtn("+10 年",()=>GM.Time.DebugJumpTo(S.Year+10));
            DBtn("+100 年",()=>GM.Time.DebugJumpTo(S.Year+100));
            DBtn("+500 年",()=>GM.Time.DebugJumpTo(S.Year+500));
            DBtn("推进时代",()=>{if(!GM.Time.DebugAdvanceEra())Toast("已是最终时代",false);});
            DBtn("跳过朝代",()=>{if(!GM.Time.DebugNextDynasty())Toast("已是最后朝代",false);});
            DBtn("触发冷冻(限x10)",()=>{S.CryoActive=true;S.CryoRemainSec=GameConstants.CryoCooldownSec;S.CryoAccumYears=GameConstants.CryoYearThreshold;});
            DBtn("立即解冻",()=>GM.ThawCryo(false));

            // —— 海洋 / 太空 副本（V6.3.5 补齐） ——
            Section("海洋大开发 · 太空探索 副本"); BeginGrid(10);
            DBtn("解锁海洋大开发",()=>GM.Ocean.UnlockExpansion());
            DBtn("解锁太空探索",()=>GM.Space.UnlockExploration());
            DBtn("开启大航海时代",()=>{S.AgeOfSail=true;if(S.Era<4)S.Era=4;GM.AddEvent("good","Debug：开启大航海时代");});
            DBtn("打开海图副本",()=>OpenOceanModal());
            DBtn("打开星图副本",()=>OpenSpaceModal());
            DBtn("海图自动探索×5",()=>{GM.Expedition.Prepare("ocean");for(int i=0;i<5;i++)GM.Expedition.AutoExplore("ocean");});
            DBtn("星图自动探索×5",()=>{GM.Expedition.Prepare("space");for(int i=0;i<5;i++)GM.Expedition.AutoExplore("space");});
            DBtn("当前格建立殖民地",()=>{if(!GM.Expedition.ColonizeHere())Toast("此处不可殖民",false);});
            DBtn("当前格建前哨基地",()=>{if(!GM.Expedition.BuildOutpostHere())Toast("此处不可建前哨",false);});
            DBtn("两支队伍全部返航",()=>{GM.Expedition.ReturnHome("ocean");GM.Expedition.ReturnHome("space");});

            // —— 社会 ——
            Section("社会"); BeginGrid(6);
            DBtn("人口补满",()=>S.Pop=GameConstants.MaxPop);
            DBtn("人口 +50",()=>S.Pop=Mathf.Min(GameConstants.MaxPop,S.Pop+50));
            DBtn("民心/天命回满",()=>{S.DynastyMorale=100;S.Happiness=Mathf.Max(S.Happiness,90);});
            DBtn("完成全部科技",()=>{foreach(var t in GM.Techs.Keys)S.ResearchedTechs.Add(t);});
            DBtn("住房 +200",()=>S.Housing+=200);
            DBtn("九神强制议事一次",()=>GM.Council.CouncilNow());

            // —— 世界 / 军事 / AI ——
            Section("世界 · 军事 · 天气 · AI"); BeginGrid(9);
            DBtn("清除树木",()=>GM.Env.ClearTrees());
            DBtn("填 20 座当前时代建筑",()=>{var pool=GM.Buildings.Values.Where(d=>d.Era<=S.Era).ToList();int made=0;for(int i=0;i<20&&pool.Count>0;i++){var d=pool[UnityEngine.Random.Range(0,pool.Count)];if(GM.Building.FindAutoPosition(d.Id,out float x,out float z)&&GM.Building.PlaceInitial(d.Id,x,z)!=null)made++;}Toast("已放置 "+made+" 座");});
            DBtn("触发战争/群雄",()=>{GM.Military.InitFactions();S.WarActive=true;GM.AddEvent("bad","⚔️ Debug：战争爆发！");});
            DBtn("敌方舰队 ×3",()=>{for(int i=0;i<3;i++)GM.Naval.DebugSpawnEnemy();});
            DBtn("我方战船",()=>GM.Naval.DebugSpawnOwnShip());
            DBtn("切换下一天气",()=>GM.Weather.ForceNext());
            DBtn("九智能体 开/关",()=>GM.Council.ToggleEnabled());
            DBtn("九智能体联网",()=>GM.Council.SetOnline(true));
            DBtn("九智能体离线",()=>GM.Council.SetOnline(false));

            // —— 结局 / 存档 ——
            Section("结局 / 存档"); BeginGrid(3);
            DBtn("直接胜利",()=>{S.Victory=true;S.VictoryType="debug";GM.AddEvent("good","🏆 Debug：达成文明胜利！");});
            DBtn("快速存档(槽1)",()=>GM.SaveSystem.SaveToSlot(1));
            DBtn("快速读档(槽1)",()=>{if(GM.SaveSystem.LoadFromSlot(1))Refresh();});

            var foot=UITheme.Panel("DbgFoot",content,new Color(0,0,0,0));foot.AddComponent<LayoutElement>().preferredHeight=40;
            var fh=foot.AddComponent<HorizontalLayoutGroup>();fh.spacing=8;fh.childForceExpandWidth=true;fh.childControlHeight=false;fh.childForceExpandHeight=false;
            var cb=UITheme.Btn("close",foot.transform,"关闭控制台",13);cb.gameObject.AddComponent<LayoutElement>().preferredHeight=34;
            cb.onClick.AddListener(()=>modal.SetActive(false));
            Canvas.ForceUpdateCanvases();dbgSr.verticalNormalizedPosition=1f;
            AutoBindHovers(modal.transform);
        }

        // ---- 通知 / 时代过场 ----
        private void BuildToastAndEra(Transform parent)
        {
            _eraTransition=UITheme.Panel("EraTransition",parent,new Color(0.08f,0.42f,0.74f,0.85f));Stretch(_eraTransition);
            _eraTitle=UITheme.Label("t",_eraTransition.transform,"",48,TextAnchor.MiddleCenter,UITheme.HexA(0xffffff,1));
            Place(_eraTitle.rectTransform,new Vector2(0.5f,0.6f),new Vector2(0.5f,0.6f),new Vector2(-400,-30),new Vector2(400,30));
            _eraDesc=UITheme.Label("d",_eraTransition.transform,"",22,TextAnchor.MiddleCenter,UITheme.HexA(0xeaf4ff,1));
            Place(_eraDesc.rectTransform,new Vector2(0.5f,0.45f),new Vector2(0.5f,0.45f),new Vector2(-400,-16),new Vector2(400,16));
            _eraTransition.SetActive(false);
            _toast=UITheme.Label("Toast",parent,"",16,TextAnchor.MiddleCenter,UITheme.Good);
            Place(_toast.rectTransform,new Vector2(0.5f,0.82f),new Vector2(0.5f,0.82f),new Vector2(-200,-30),new Vector2(200,30));
            _toast.gameObject.SetActive(false);
        }
        private float _eraHide;
        public void ShowEraTransition(string title,string desc)
        {
            _eraTitle.text=title;_eraDesc.text=desc;_eraTransition.SetActive(true);_eraHide=Time.unscaledTime+3f;
        }
        private float _toastHide;
        public void Toast(string msg,bool good=true)
        {
            _toast.text=msg;_toast.color=good?UITheme.Good:UITheme.Bad;_toast.gameObject.SetActive(true);_toastHide=Time.unscaledTime+2.5f;
        }

        private void Place(RectTransform rt,Vector2 aMin,Vector2 aMax,Vector2 oMin,Vector2 oMax)
        { rt.anchorMin=aMin;rt.anchorMax=aMax;rt.offsetMin=oMin;rt.offsetMax=oMax; }

        // ============ 每帧刷新 ============
        private void Update()
        {
          try{
            if (GM==null||_hud==null||!_hud.activeSelf) { TickSplashAutoStart(); HideTimed();return; }
            _splashArmed=false;
            HandleFloatingClose();
            UpdateHoverTip();
            TickAutoHover(Time.unscaledDeltaTime);
            RefreshMinimap(Time.unscaledDeltaTime);
            RefreshMarkers(Time.unscaledDeltaTime);
            _refreshCd-=Time.unscaledDeltaTime;
            if (_refreshCd<=0){_refreshCd=0.25f;Refresh();}
            HideTimed();
          }
          catch(System.Exception e){ Debug.LogError("[MARK_UI] "+e.GetType().Name+": "+e.Message+"\n"+e.StackTrace); }
        }

        // V6.1.1：ESC / 右键 / 点击地图空白处关闭船只·建筑浮窗（对齐 v5.9.9 closeShipTooltip）
        private void HandleFloatingClose()
        {
            if (S==null) return;
            var es=UnityEngine.EventSystems.EventSystem.current;
            bool overUI=es!=null&&es.IsPointerOverGameObject();
            bool esc=Input.GetKeyDown(KeyCode.Escape);
            bool rmb=Input.GetMouseButtonDown(1)&&!overUI;
            if (esc||rmb){ CloseShipCard(); CloseCart(); if(_buildingModal)_buildingModal.SetActive(false); return; }
            // 建造放置模式下左键用于放建筑，不做关闭
            if (!string.IsNullOrEmpty(S.SelectedBuildType)||S.Tool=="build") return;
            if (Input.GetMouseButtonDown(0)&&!overUI&&!PointerHitsEntity())
            { CloseShipCard(); CloseCart(); if(_buildingModal)_buildingModal.SetActive(false); }
        }
        private bool PointerHitsEntity()
        {
            var cam=Camera.main; if(cam==null) return false;
            var hits=Physics.RaycastAll(cam.ScreenPointToRay(Input.mousePosition),1000f);
            foreach(var h in hits) if(h.collider!=null && (h.collider.GetComponentInParent<Systems.ShipClick>()!=null||h.collider.GetComponentInParent<Buildings.BuildingClick>()!=null)) return true;
            return false;
        }
        private void HideTimed()
        {
            if (_eraTransition&&_eraTransition.activeSelf&&_eraHide>0&&Time.unscaledTime>_eraHide)_eraTransition.SetActive(false);
            if (_toast&&_toast.gameObject.activeSelf&&_toastHide>0&&Time.unscaledTime>_toastHide)_toast.gameObject.SetActive(false);
        }

        private void Refresh()
        {
            foreach (var id in ResourceDatabase.Order)
                if (_resTexts.TryGetValue(id,out var t)) t.text=Mathf.FloorToInt(S.GetRes(id)).ToString();
            _eraTag.text=GM.Time.EraName;
            if(_lastBuildEra!=S.Era){_lastBuildEra=S.Era;RebuildBuildListV2();}
            _dynastyTag.text=GM.Time.DynastyName;
            _timeText.text="第"+S.Year+"年";
            _gregText.text=GM.Time.GregorianText;
            _popText.text="人口"+S.Pop+"/"+Mathf.RoundToInt(S.Housing);
            // V6.1.9(i) 天气 / 月度潮汐 / 海风
            if(_envText!=null)
            {
                string w=GM.Weather!=null?GM.Weather.CurrentText:"☀晴天";
                string t=GM.Tide!=null?GM.Tide.TideText:"";
                string wind=GM.OceanFlow!=null?GM.OceanFlow.WindText:"";
                _envText.text=$"{w}｜{t}｜{wind}";
            }
            _speedText.text=S.Paused?"暂停":"x"+(int)S.Speed;
            if (_speedSlider && !_suppressSlider) SyncSlider();
            if (_oceanBtn) _oceanBtn.SetActive(S.OceanUnlocked);
            if (_spaceBtn) _spaceBtn.SetActive(S.SpaceUnlocked);
            if (_saveCountdown!=null && _saveModal!=null && _saveModal.activeSelf)
            {
                float cd=GM.SaveSystem.AutoCountdown;
                _saveCountdown.text=$"下次存档 {Mathf.FloorToInt(cd/60f)}:{(Mathf.FloorToInt(cd%60f)).ToString("00")}";
            }
            RefreshStats();
            // V6.1.9 中顶冷冻冷却倒计时（仅冷冻时显示）
            if (_cryoBar!=null)
            {
                bool on=S.CryoActive;
                if (_cryoBar.activeSelf!=on) _cryoBar.SetActive(on);
                if (on && _cryoText!=null)
                {
                    int sec=Mathf.Max(0,Mathf.CeilToInt(S.CryoRemainSec));
                    _cryoText.text=$"❄️ 加速冷冻冷却 {sec/60}:{sec%60:00}｜累计已满100年，期间最高10倍（当前生效 x{(int)GM.EffectiveSpeed}）";
                }
            }
        }

        // V6.1.1 国家状态四组折叠：📊基础 / ⚔️军事 / 👥社会 / 🏛️时代，点击标题展开/收起
        private void BuildStatsSections(Transform parent)
        {
            var box=UITheme.Panel("StatsBox",parent,new Color(0,0,0,0));
            box.AddComponent<LayoutElement>().preferredHeight=236;
            var g=box.AddComponent<VerticalLayoutGroup>();g.spacing=3;g.padding=new RectOffset(0,0,0,0);
            g.childControlWidth=true;g.childForceExpandWidth=true;g.childControlHeight=true;g.childForceExpandHeight=false;
            _statBasic=MakeStatSection(box.transform,0,"📊 基础");
            _statMil=MakeStatSection(box.transform,1,"⚔️ 军事");
            _statSoc=MakeStatSection(box.transform,2,"👥 社会");
            _statEra=MakeStatSection(box.transform,3,"🏛️ 时代");
        }
        private Text MakeStatSection(Transform parent,int idx,string title)
        {
            var head=UITheme.Btn("sth"+idx,parent,title,12,UITheme.HexA(0x232e50,0.98f));
            head.GetComponentInChildren<Text>().color=UITheme.Gold;
            head.gameObject.AddComponent<LayoutElement>().preferredHeight=22;
            var body=UITheme.Label("stb"+idx,parent,"",11,TextAnchor.UpperLeft);
            var csf=body.gameObject.AddComponent<ContentSizeFitter>();csf.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            body.gameObject.AddComponent<LayoutElement>().flexibleHeight=1;
            head.onClick.AddListener(()=>{_statOpen[idx]=!_statOpen[idx];body.gameObject.SetActive(_statOpen[idx]);});
            return body;
        }

        private void RefreshStats()
        {
            // 📊 基础：民心/住房/人口结构/建设/科研/现代指标
            var basic=new StringBuilder();
            basic.Append("民心：").Append(Mathf.RoundToInt(S.Happiness)).Append("%\n");
            basic.Append("住房：").Append(Mathf.RoundToInt(S.Housing)).Append("\n");
            basic.Append("青/中/老：").Append(Mathf.RoundToInt(S.Children)).Append("/")
              .Append(Mathf.RoundToInt(S.Young)).Append("/").Append(Mathf.RoundToInt(S.Middle)).Append("/").Append(Mathf.RoundToInt(S.Old)).Append("\n");
            basic.Append("已研究：").Append(S.ResearchedTechs.Count).Append("项\n");
            basic.Append("建筑：").Append(S.Buildings.Count).Append("/").Append(GameConstants.MaxBuildings).Append("\n");
            if (S.Era>=6) basic.Append("电力覆盖：").Append(Mathf.RoundToInt(S.PowerCoverage)).Append("%\n");
            if (S.AiBonus>0) basic.Append("AI加成：+").Append(Mathf.RoundToInt(S.AiBonus*100)).Append("%\n");
            if (_statBasic) _statBasic.text=basic.ToString();

            // ⚔️ 军事：兵力/防御火力/战争状态
            var mil=new StringBuilder();
            mil.Append("士兵：").Append(Mathf.RoundToInt(S.MilSoldiers)).Append("骑兵：").Append(Mathf.RoundToInt(S.MilCavalry)).Append("\n");
            mil.Append("防御：").Append(Mathf.RoundToInt(S.MilDefense)).Append("火力：").Append(Mathf.RoundToInt(S.MilFirepower)).Append("\n");
            // V6.1.4 机动部队 / 割据势力；V6.1.5 殖民地
            int aliveFac=0; foreach(var f in GM.Military.Factions) if(!f.Destroyed)aliveFac++;
            if (S.FriendlyUnits.Count>0 || aliveFac>0)
                mil.Append("机动部队：").Append(S.FriendlyUnits.Count).Append("队 割据势力：").Append(aliveFac).Append("\n");
            if (S.Colonies.Count>0) mil.Append("海外殖民地：").Append(S.Colonies.Count).Append("处\n");
            if (S.WarActive) mil.Append("<color=#e74c3c>战争进行中！</color>\n");
            if (S.NavyBattleActive) mil.Append("<color=#e67e22>海战进行中！</color>\n");
            if (_statMil) _statMil.text=mil.ToString();

            // 👥 社会：君主/吏治/学派（策划书·朝代生命周期与诸子百家）
            var soc=new StringBuilder();
            soc.Append("君主：").Append(S.MonarchName).Append(S.MonarchWise ? "（明）\n" : "（昏）\n");
            soc.Append("吏治腐败：").Append(Mathf.RoundToInt(S.Corruption)).Append("%\n");
            soc.Append("学派：").Append(PhilosophyName(S.Philosophy)).Append("\n");
            if (_statSoc) _statSoc.text=soc.ToString();

            // 🏛️ 时代：朝代气数/运河/潮汐
            var era=new StringBuilder();
            era.Append("朝代气数：").Append(Mathf.RoundToInt(S.DynastyMorale)).Append("%\n");
            era.Append("运河：").Append(S.CanalSegments).Append("段(+").Append(Mathf.RoundToInt(S.CanalBonus)).Append("%)\n");
            if (S.CanalAutoBuild)
                era.Append(S.TideHigh?"🌊涨潮":"🏜️退潮").Append(Mathf.RoundToInt(S.TideLevel*100)).Append("%\n");
            // V6.1.3 天下分合：大一统 / 列国并立 + 各国人口 + 变局倒计时
            if (S.Nations!=null && S.Nations.Count>0)
            {
                bool unify=S.WorldPhase=="unify";
                era.Append(unify?"<color=#ffd700>🏛️ 大一统王朝</color>\n":"<color=#e08a2e>⚔️ 列国并立</color>\n");
                int shown=0;
                foreach (var n in S.Nations)
                {
                    if (!n.Alive) continue;
                    if (shown++>=7) break;
                    era.Append("·").Append(n.Name).Append(n.IsPlayer?"(我)":"").Append(' ').Append(n.Pop).Append('\n');
                }
                era.Append("变局倒计时：").Append(S.PhaseYearsLeft).Append("年\n");
            }
            if (_statEra) _statEra.text=era.ToString();
        }

        private void RefreshEventLog()
        {
            if (_eventText==null) return;
            var sb=new StringBuilder();
            int n=0;
            foreach (var e in S.EventLog){ if(n++>=40)break; sb.Append("[").Append(e.Year).Append("年] ").Append(e.Text).Append("\n"); }
            _eventText.text=sb.ToString();
        }

        /// <summary>学派 id → 中文名（空=未择）</summary>
        internal static string PhilosophyName(string id) => id switch
        {
            "ru"=>"儒家","fa"=>"法家","dao"=>"道家","mo"=>"墨家",
            "bing"=>"兵家","zongheng"=>"纵横家",_=>"未择（百家争鸣后可选）",
        };
    }
}
