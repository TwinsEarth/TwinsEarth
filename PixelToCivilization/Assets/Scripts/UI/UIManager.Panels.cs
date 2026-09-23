using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using PixelToCivilization.Core;
using PixelToCivilization.Data;
using PixelToCivilization.AI;
using PixelToCivilization.Systems;

namespace PixelToCivilization.UI
{
    /// <summary>UIManager 的模态面板部分：科技树/政策/九神/海洋/太空/建筑信息</summary>
    public partial class UIManager
    {
        private GameObject _techModal,_policyModal,_godsModal,_oceanModal,_spaceModal,_buildingModal,_shipModal,_cartModal;
        private GameObject _campaignModal,_colonyModal;   // V6.1.4 群雄讨伐 / V6.1.5 殖民地
        private ShipEntity _selectedShip;
        private CartEntity _selectedCart;
        private Transform _modalLayer;

        private void BuildModals(Transform parent)
        {
            var layerGo=UITheme.Panel("ModalLayer",parent,new Color(0,0,0,0));
            // 透明容器层不拦射线；阻挡由各 modal 打开时的 55% 遮挡层负责
            layerGo.GetComponent<Image>().raycastTarget=false;
            _modalLayer=layerGo.transform;
            Stretch(_modalLayer.gameObject);
            _techModal=MakeModal("TechModal"," 科技树",out _);
            _policyModal=MakeModal("PolicyModal"," 政策法令",out _);
            _godsModal=MakeModal("GodsModal"," 九神共治 · AI智能体议会",out _);
            _oceanModal=MakeModal("OceanModal"," 海洋大开发",out _);
            _spaceModal=MakeModal("SpaceModal"," 太空探索",out _);
            _buildingModal=MakeModal("BuildingModal","建筑",out _);
            _shipModal=MakeModal("ShipModal","船只详情",out _);
            // 船浮窗为紧凑小窗（对齐 v5.9.9 186×174 船只浮窗）
            var sbox=_shipModal.transform.Find("Box").GetComponent<RectTransform>();
            sbox.sizeDelta=new Vector2(320,400);
            _cartModal=MakeModal("CartModal","车辆详情",out _);
            var cbox=_cartModal.transform.Find("Box").GetComponent<RectTransform>();
            cbox.sizeDelta=new Vector2(320,380);
            // V6.1.4 群雄讨伐 / V6.1.5 殖民地面板
            _campaignModal=MakeModal("CampaignModal","【群雄争霸 · 出师讨伐】",out var cab);
            cab.parent.GetComponent<RectTransform>().sizeDelta=new Vector2(580,620);
            _colonyModal=MakeModal("ColonyModal","【殖民时代 · 海外领地】",out var cob);
            cob.parent.GetComponent<RectTransform>().sizeDelta=new Vector2(660,620);
        }

        private GameObject MakeModal(string name,string title,out RectTransform body,bool destroyOnClose=false)
        {
            var overlay=UITheme.Panel(name,_modalLayer,UITheme.HexA(0x000000,0.55f));Stretch(overlay);
            var box=UITheme.Surface("Box",overlay.transform,new Color(0.985f,0.975f,0.935f,0.99f));
            var rt=box.GetComponent<RectTransform>();
            rt.anchorMin=rt.anchorMax=new Vector2(0.5f,0.5f);rt.sizeDelta=new Vector2(760,640);
            var vl=box.AddComponent<VerticalLayoutGroup>();vl.spacing=8;vl.padding=new RectOffset(18,18,16,16);
            vl.childControlWidth=true;vl.childForceExpandWidth=true;
            var head=UITheme.Panel("Head",box.transform,new Color(0,0,0,0));
            head.AddComponent<LayoutElement>().preferredHeight=36;
            UITheme.Label("title",head.transform,title,22,TextAnchor.MiddleLeft,UITheme.Gold)
                .SetInset(8,0);
            var close=UITheme.Btn("close",head.transform,"×",16);
            // 小正方框关闭钮（不再纵向拉成长条）
            var crt=close.GetComponent<RectTransform>();crt.anchorMin=new Vector2(1,1);crt.anchorMax=new Vector2(1,1);
            crt.pivot=new Vector2(1,1);crt.sizeDelta=new Vector2(30,30);crt.anchoredPosition=new Vector2(0,2);
            var le=close.GetComponent<LayoutElement>();le.ignoreLayout=true;le.preferredWidth=30;le.preferredHeight=30;
            close.onClick.AddListener(()=>{ if(destroyOnClose) Destroy(overlay); else overlay.SetActive(false); });
            var bodyGo=UITheme.Panel("Body",box.transform,new Color(0,0,0,0));
            body=bodyGo.GetComponent<RectTransform>();
            bodyGo.AddComponent<LayoutElement>().flexibleHeight=1;
            overlay.SetActive(false);
            return overlay;
        }

        private GameObject CreateModal(string title)
        {
            var ov=MakeModal("Tmp_"+Time.frameCount,title,out _,true);
            ov.transform.SetParent(_modalLayer,false);
            ov.SetActive(true);
            return ov;
        }
        private GameObject ModalBody(GameObject overlay)=>overlay.transform.Find("Box/Body").gameObject;
        private void Open(GameObject m){ if(m!=null){m.SetActive(true);RefreshModalContent(m);AutoBindHovers(m.transform);} }
        private void RefreshModalContent(GameObject m)
        {
            if (m==_techModal) FillTech(m);
            else if (m==_policyModal) FillPolicy(m);
            else if (m==_godsModal) FillGods(m);
            else if (m==_oceanModal) FillOcean(m);
            else if (m==_spaceModal) FillSpace(m);
            else if (m==_campaignModal) FillCampaign(m);
            else if (m==_colonyModal) FillColony(m);
        }

        // ===== 诸子百家（策划书·学派抉择） =====
        private void OpenPhilosophyModal()
        {
            var phil = GM.Philosophy;
            if (phil == null) { Toast("百家系统未就绪", false); return; }
            var modal = CreateModal("诸子百家 · 择国之道");
            var body = ModalBody(modal);
            Clear(body);
            UITheme.Label("hint", body.transform,
                "择一家学派作为治国理念（随时可改换，立即生效）。春秋以降，思想即国运。",
                12, TextAnchor.UpperLeft).gameObject.AddComponent<LayoutElement>().preferredHeight = 34;
            foreach (var s in PixelToCivilization.Systems.PhilosophySystem.Schools)
            {
                bool current = S.Philosophy == s.id;
                var row = UITheme.Panel("ph_" + s.id, body.transform,
                    UITheme.HexA(current ? 0xFFD700 : 0xffffff, current ? 0.16f : 0.05f));
                row.AddComponent<LayoutElement>().preferredHeight = 60;
                UITheme.Label("n", row.transform, s.name, 17, TextAnchor.MiddleLeft,
                    UITheme.Hex((int)s.color)).gameObject.AddComponent<LayoutElement>().preferredWidth = 76;
                UITheme.Label("d", row.transform, s.desc, 12, TextAnchor.MiddleLeft)
                    .gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                var b = UITheme.Btn("adopt", row.transform, current ? "国策" : "择为", 12);
                b.GetComponent<LayoutElement>().preferredWidth = 64;
                b.interactable = !current;
                string id = s.id;
                b.onClick.AddListener(() => { phil.Adopt(id); Destroy(modal); });
            }
        }

        // ===== 科技树 =====
        private void OpenTechModal(){ FillTech(_techModal);Open(_techModal); }        private void FillTech(GameObject modal)
        {
            var body=ModalBody(modal);Clear(body);
            UITheme.VerticalScroll("TechScroll",body.transform,out var content,6);
            foreach (var era in GM.Eras)
            {
                UITheme.Label("era",content,$"【{era.Name}】",15,TextAnchor.MiddleLeft,era.ThemeColor);
                foreach (var t in GM.Techs.Values)
                {
                    if (t.Era!=era.Id) continue;
                    bool done=S.ResearchedTechs.Contains(t.Id);
                    bool researching=S.CurrentResearch==t.Id;
                    bool can=GM.Tech.CanResearch(t.Id,out _);
                    var row=UITheme.Panel("t_"+t.Id,content,UITheme.HexA(done?0x2e7d32:0xffffff, done?0.15f:0.05f));
                    var le=row.AddComponent<LayoutElement>();le.preferredHeight=54;
                    var info=UITheme.Label("info",row.transform,$"{t.Name}  ({t.Cost})\n{t.Desc}  前置:{(t.HasRequirement?string.Join(",",t.Requires):"无")}{(researching?"  研究中…":"")}",12,TextAnchor.MiddleLeft);
                    info.rectTransform.anchorMin=new Vector2(0,0);info.rectTransform.anchorMax=new Vector2(0.72f,1);
                    info.rectTransform.offsetMin=new Vector2(8,2);info.rectTransform.offsetMax=new Vector2(-4,-2);
                    var b=UITheme.Btn("btn",row.transform,done?"已研":"研究",12);
                    var brt=b.GetComponent<RectTransform>();brt.anchorMin=new Vector2(0.74f,0.2f);brt.anchorMax=new Vector2(0.99f,0.8f);brt.offsetMin=brt.offsetMax=Vector2.zero;
                    b.interactable=!done&&can; string id=t.Id;
                    b.onClick.AddListener(()=>{GM.Tech.StartResearch(id);FillTech(modal);});
                }
            }
        }

        // ===== 政策 =====
        private void OpenPolicyModal(){ FillPolicy(_policyModal);Open(_policyModal); }
        private void FillPolicy(GameObject modal)
        {
            var body=ModalBody(modal);Clear(body);
            UITheme.VerticalScroll("PolScroll",body.transform,out var content,6);
            foreach (var p in GM.Policies.Values)
            {
                bool on=S.Policies.Contains(p.Id);
                var row=UITheme.Panel("p_"+p.Id,content,UITheme.HexA(on?0xFFD700:0xffffff,on?0.14f:0.05f));
                row.AddComponent<LayoutElement>().preferredHeight=48;
                UITheme.Label("i",row.transform,$"{p.Name}（{GM.Eras[p.Era].Name}）\n{p.Desc}",12,TextAnchor.MiddleLeft)
                    .SetInset(8,0.28f);
                var b=UITheme.Btn("b",row.transform,on?"推行中":"推行",12);
                var brt=b.GetComponent<RectTransform>();brt.anchorMin=new Vector2(0.74f,0.2f);brt.anchorMax=new Vector2(0.99f,0.8f);brt.offsetMin=brt.offsetMax=Vector2.zero;
                string id=p.Id;b.onClick.AddListener(()=>{GM.Policy.Toggle(id);FillPolicy(modal);});
            }
        }

        // ===== V6.1.8 九智能体共治（AI 多智能体议会；底部保留 v5.9.9 神话九神赐福） =====
        private void OpenGodsModal(){ FillGods(_godsModal);Open(_godsModal); }
        private void FillGods(GameObject modal)
        {
            var body=ModalBody(modal);Clear(body);
            UITheme.VerticalScroll("GodScroll",body.transform,out var content,6);
            var cou=GM.Council;
            if(cou==null){ UITheme.Label("noai",content,"九智能体系统未装配",13); return; }
            cou.ComputeContinuity();
            // —— 顶部：文明存续健康分 ——
            float cv=cou.Continuity;
            Color cvCol = cv>=70?UITheme.Good : cv>=40?UITheme.Gold : UITheme.Bad;
            var top=UITheme.Panel("cv",content,UITheme.HexA(0xffffff,0.05f));
            top.AddComponent<LayoutElement>().preferredHeight=58;
            UITheme.Label("cvn",top.transform,
                $"文明存续健康分　{cv:F0} / 100\n人口{S.Pop}/{S.MaxPop}　粮{Mathf.FloorToInt(S.GetRes("food"))}　民心{S.Happiness:F0}　天命{S.DynastyMorale:F0}　兜底续命{cou.SafetyCount}次",
                14,TextAnchor.MiddleLeft,cvCol).SetInset(10,0.1f);
            // —— 控制行1：开关 / 模式 / 立即议政 ——
            var r1=Row(content,38);
            UITheme.Btn("en",r1.transform,cou.Enabled?"◉ 共治开启（自动）":"○ 共治已停（手动）",12).onClick.AddListener(()=>{cou.ToggleEnabled();FillGods(modal);});
            UITheme.Btn("mode",r1.transform,cou.Online?"🌐 联网·ARK大模型":"💾 离线·规则自治",12).onClick.AddListener(()=>{cou.SetOnline(!cou.Online);FillGods(modal);});
            UITheme.Btn("now",r1.transform,"⚡ 立即议政",12).onClick.AddListener(()=>{cou.CouncilNow();FillGods(modal);});
            // —— 控制行2：Token / 间隔 / 联网状态 ——
            string net = cou.Online ? (cou.NetOk?"联网正常":("联网失败→已自动离线："+cou.LastNetError)) : "离线自治（不耗Token，保证不断绝）";
            UITheme.Label("meta",content,
                $"共用Token累计 {cou.TokensUsed}　议政间隔 每{cou.IntervalYears}游戏年　上次议政 第{cou.LastCouncilYear}年　模式：{net}",
                11,TextAnchor.MiddleLeft,UITheme.Sky).gameObject.AddComponent<LayoutElement>().preferredHeight=30;
            UITheme.Label("tip",content,"九位职能AI自主决策、神庭仲裁：允许饥荒/灾难/动乱让文明倒退，但人口/粮/住房/民心/军力触红线即强制托底并注入恢复条件，保证5000~10000年兴衰而不断绝。",
                11,TextAnchor.UpperLeft,UITheme.Text).gameObject.AddComponent<LayoutElement>().preferredHeight=42;
            // —— 九智能体 ——
            foreach (var g in cou.Gods)
            {
                Color lamp = !cou.Enabled?UITheme.HexA(0x888888,0.6f) : g.Urgency>=0.7f?UITheme.Bad : g.Urgency>=0.45f?UITheme.Gold : UITheme.Good;
                var row=UITheme.Panel("ag_"+g.Id,content,UITheme.HexA((int)g.Color,0.10f));
                row.AddComponent<LayoutElement>().preferredHeight=62;
                string dot = !cou.Enabled?"●休眠":(g.Urgency>=0.7f?"●预警":g.Urgency>=0.45f?"●治理":"●平稳");
                UITheme.Label("i",row.transform,
                    $"{g.Name}　{g.Domain}\n关注：{g.Focus}　{dot} 紧迫{g.Urgency:F0%}　第{g.LastYear}年议政·累计{g.Actions}次\n最近：{(string.IsNullOrEmpty(g.Note)?"—":g.Note)}",
                    12,TextAnchor.MiddleLeft,lamp).SetInset(8,0.04f);
            }
            // —— 底部：v5.9.9 神话九神赐福（保留不回退） ——
            UITheme.Label("legacy",content,"——— 神话赐福 · v5.9.9 九神（保留） ———",12,TextAnchor.MiddleCenter,UITheme.Gold)
                .gameObject.AddComponent<LayoutElement>().preferredHeight=28;
            foreach (var g in GM.Gods.Gods)
            {
                int last=GM.Gods.LastDecision.Or(g.Id);
                var row=UITheme.Panel("lg_"+g.Id,content,UITheme.HexA((int)g.Color,0.10f));
                row.AddComponent<LayoutElement>().preferredHeight=50;
                UITheme.Label("i",row.transform,$"{g.Name} · {g.Domain}（{g.Desc}）　上次显灵：第{last}年",12,TextAnchor.MiddleLeft).SetInset(8,0.24f);
                var b=UITheme.Btn("b",row.transform,"祈求显灵",11);
                var brt=b.GetComponent<RectTransform>();brt.anchorMin=new Vector2(0.77f,0.2f);brt.anchorMax=new Vector2(0.99f,0.8f);brt.offsetMin=brt.offsetMax=Vector2.zero;
                string id=g.Id;b.onClick.AddListener(()=>{GM.Gods.MakeDecision(id);FillGods(modal);});
            }
        }
        private GameObject Row(Transform parent,float h)
        {
            var r=UITheme.Panel("r",parent,new Color(0,0,0,0));
            r.AddComponent<LayoutElement>().preferredHeight=h;
            var hl=r.AddComponent<HorizontalLayoutGroup>();hl.spacing=8;hl.childForceExpandWidth=true;hl.childControlWidth=true;
            return r;
        }

        // ===== 海洋 =====
        private void OpenOceanModal(){ FillOcean(_oceanModal);Open(_oceanModal); }
        private void FillOcean(GameObject modal)
        {
            var body=ModalBody(modal);Clear(body);
            UITheme.VerticalScroll("OceanScroll",body.transform,out var content,6);
            var vl=content.GetComponent<VerticalLayoutGroup>();
            vl.spacing=8;vl.childControlWidth=true;vl.childForceExpandWidth=true;
            UITheme.Label("st",content,$"已开辟航线：{S.OceanDiscovered.Count} 条  {string.Join("、",S.OceanDiscovered)}   殖民地：{S.Colonies.Count} 处",13,TextAnchor.MiddleLeft);
            var sb=new StringBuilder();
            foreach (var k in OceanExpansionSystem.OceanResIds)
                sb.Append(OceanExpansionSystem.ResNames[k]).Append("：").Append(Mathf.FloorToInt(S.OceanResources.Or(k))).Append("  ");
            UITheme.Label("res",content,sb.ToString(),13,TextAnchor.MiddleLeft);
            var row=UITheme.Panel("row",content,new Color(0,0,0,0));
            var h=row.AddComponent<HorizontalLayoutGroup>();h.spacing=8;row.AddComponent<LayoutElement>().preferredHeight=40;
            UITheme.Btn("send",row.transform," 派遣宝船（木50金30）",12).onClick.AddListener(()=>{GM.Ocean.SendFleet();FillOcean(modal);});
            UITheme.Btn("build",row.transform," 建造宝船（木100铁10）",12).onClick.AddListener(()=>{GM.Naval.BuildTreasureShip();});
            UITheme.Btn("sell",row.transform," 出售特产",12).onClick.AddListener(()=>{GM.Ocean.SellOceanResources();FillOcean(modal);});
            UITheme.Label("tip",content,"——— V6.1.6 远洋探索副本（每步耗粮2金1，迷雾下探索海图）———",12,TextAnchor.MiddleLeft,UITheme.Gold);
            BuildExpedition(content,"ocean",modal);
            UITheme.Label("tip2",content,"提示：明·清/大航海时代解锁；宝船远航发现港口，踩到⚓良港可建立殖民地，海怪/风暴有风险，战力补给耗尽自动返航。",11,TextAnchor.UpperLeft,UITheme.Sky);
        }

        // ===== 太空 =====
        private void OpenSpaceModal(){ FillSpace(_spaceModal);Open(_spaceModal); }
        private void FillSpace(GameObject modal)
        {
            var body=ModalBody(modal);Clear(body);
            UITheme.VerticalScroll("SpaceScroll",body.transform,out var content,6);
            var vl=content.GetComponent<VerticalLayoutGroup>();vl.spacing=8;vl.childControlWidth=true;vl.childForceExpandWidth=true;
            UITheme.Label("st",content,
                $"太空电梯 {Mathf.RoundToInt(S.SpElevator)}%  飞船 {S.SpShips}艘  戴森云 {Mathf.RoundToInt(S.SpDyson)}%  月球 {Mathf.RoundToInt(S.SpLunar)}%  火星 {Mathf.RoundToInt(S.SpMars)}%",13,TextAnchor.MiddleLeft);
            var grid=UITheme.Panel("grid",content,new Color(0,0,0,0));
            var g=grid.AddComponent<GridLayoutGroup>();g.constraint=GridLayoutGroup.Constraint.FixedColumnCount;g.constraintCount=2;g.cellSize=new Vector2(330,40);g.spacing=new Vector2(8,8);
            ProjBtn(grid.transform," 太空电梯(钢50碳20)","elevator");
            ProjBtn(grid.transform," 宇宙飞船(钢30聚变10)","ship");
            ProjBtn(grid.transform," 戴森云(聚变50)","dyson");
            ProjBtn(grid.transform," 月球基地(钢60聚变15)","lunar");
            ProjBtn(grid.transform," 火星移民(钢100聚变30)","mars");
            UITheme.Label("stip",content,"——— V6.1.6 星际探索副本（每步耗聚变1钢1，揭开星图）———",12,TextAnchor.MiddleLeft,UITheme.Gold);
            BuildExpedition(content,"space",modal);
        }

        // ===== V6.1.6 统一副本网格（海图/星图）=====
        private void BuildExpedition(Transform content,string type,GameObject modal)
        {
            GM.Expedition.Prepare(type);   // 首次打开初始化网格（不切换 CurrentMap）
            var e=type=="space"?S.SpaceExp:S.OceanExp;
            UITheme.Label("epos",content,$"坐标({e.PosX},{e.PosY})  战力{Mathf.RoundToInt(e.Power)}/{Mathf.RoundToInt(e.MaxPower)}  补给{Mathf.RoundToInt(e.Supply)}",12,TextAnchor.MiddleLeft);
            // 9×9 网格
            var gp=UITheme.Panel("egrid",content,new Color(0,0,0,0));
            gp.AddComponent<LayoutElement>().preferredHeight=348;
            var eg=gp.AddComponent<GridLayoutGroup>();
            eg.constraint=GridLayoutGroup.Constraint.FixedColumnCount;eg.constraintCount=e.N;
            eg.cellSize=new Vector2(36,36);eg.spacing=new Vector2(2,2);
            for(int y=e.N-1;y>=0;y--)
                for(int x=0;x<e.N;x++)
                {
                    int idx=e.Idx(x,y); bool seen=e.Seen!=null&&idx<e.Seen.Length&&e.Seen[idx]==1;
                    bool cur=x==e.PosX&&y==e.PosY;
                    string node=seen?e.NodeKind[idx]:null;
                    string txt=cur?"★":(seen?(string.IsNullOrEmpty(node)?"·":ExpeditionSystem.NodeIcon(node)):"");
                    var cell=UITheme.Btn("c"+idx,eg.transform,txt,13);
                    cell.interactable=false;
                    var img=cell.GetComponent<Image>();
                    if(!seen) img.color=UITheme.HexA(0x000000,0.55f);
                    else if(cur) img.color=UITheme.HexA(0xFFD700,0.35f);
                    else if(!string.IsNullOrEmpty(node)) img.color=UITheme.HexA(type=="space"?0x2b3a67:0x1f4d5c,0.85f);
                    else img.color=UITheme.HexA(0xffffff,0.06f);
                }
            // 方向控制
            var d1=DirRow(content);
            UITheme.Btn("up",d1.transform,"⬆ 北",12).onClick.AddListener(()=>{EpMove(type,0,1,modal);});
            var d2=DirRow(content);
            UITheme.Btn("left",d2.transform,"⬅ 西",12).onClick.AddListener(()=>{EpMove(type,-1,0,modal);});
            UITheme.Btn("auto",d2.transform,"🔭 自动探索",12).onClick.AddListener(()=>{EpAuto(type,modal);});
            UITheme.Btn("right",d2.transform,"➡ 东",12).onClick.AddListener(()=>{EpMove(type,1,0,modal);});
            var d3=DirRow(content);
            UITheme.Btn("down",d3.transform,"⬇ 南",12).onClick.AddListener(()=>{EpMove(type,0,-1,modal);});
            UITheme.Btn("return",d3.transform,"🏠 返航整补",12).onClick.AddListener(()=>{GM.Expedition.ReturnHome(type);Refill(modal,type);});
            // 节点动作
            string here=GM.Expedition.CurrentNode(e);
            var d4=DirRow(content);
            if(type=="ocean")
            {
                var cb=UITheme.Btn("colonize",d4.transform,"⚓ 于此建立殖民地(金200木100)",12);
                cb.interactable=here=="port";
                cb.onClick.AddListener(()=>{GM.Expedition.ColonizeHere();Refill(modal,type);});
            }
            else
            {
                var ob=UITheme.Btn("outpost",d4.transform,here=="mars"?"🔴 建火星前哨":"🌙 建月球前哨",12);
                ob.interactable=here=="moon"||here=="mars";
                ob.onClick.AddListener(()=>{GM.Expedition.BuildOutpostHere();Refill(modal,type);});
            }
            UITheme.Label("elog",content,"探险日志："+(string.IsNullOrEmpty(e.LastEvent)?"（起航）":e.LastEvent),11,TextAnchor.UpperLeft,UITheme.Sky);
        }
        private GameObject DirRow(Transform parent)
        {
            var r=UITheme.Panel("dir",parent,new Color(0,0,0,0));
            r.AddComponent<LayoutElement>().preferredHeight=34;
            var h=r.AddComponent<HorizontalLayoutGroup>();h.spacing=6;h.childForceExpandWidth=true;h.childControlWidth=true;
            return r;
        }
        private void EpMove(string type,int dx,int dy,GameObject modal){ var msg=GM.Expedition.Move(type,dx,dy); if(!string.IsNullOrEmpty(msg))Toast(msg); Refill(modal,type); }
        private void EpAuto(string type,GameObject modal){ var msg=GM.Expedition.AutoExplore(type); if(!string.IsNullOrEmpty(msg))Toast(msg); Refill(modal,type); }
        private void Refill(GameObject modal,string type){ if(type=="space")FillSpace(modal); else FillOcean(modal); }
        private void ProjBtn(Transform grid,string label,string proj)
        { UITheme.Btn("p",grid,label,12).onClick.AddListener(()=>GM.Space.BuildProject(proj)); }

        // ===== V6.1.4 群雄争霸·讨伐 =====
        private void OpenCampaignModal(){ FillCampaign(_campaignModal);Open(_campaignModal); }
        private void FillCampaign(GameObject modal)
        {
            var body=ModalBody(modal);Clear(body);
            UITheme.VerticalScroll("CampScroll",body.transform,out var content,6);
            // V6.3.4 征兵/讨伐界面实拍内视图（Resources/Portraits/military/campaign_1.jpg，缺失回退矢量军营图）
            var campTex=PortraitLoader.Load("military","campaign",1);
            if(campTex!=null) UITheme.Portrait(content,campTex,140);
            else{
                var camph=UITheme.Surface("CampPortrait",content,UITheme.HexA(0x1b2440,0.92f));
                camph.AddComponent<LayoutElement>().preferredHeight=118;
                UITheme.SetOutline(camph,UITheme.Gold,1);
                var chh=camph.AddComponent<HorizontalLayoutGroup>();chh.childAlignment=TextAnchor.MiddleCenter;chh.spacing=12;chh.childControlHeight=true;chh.childForceExpandHeight=false;
                var cimg=UITheme.Icon(camph.transform,"military",64);cimg.rectTransform.sizeDelta=new Vector2(64,64);
                var ile=cimg.gameObject.AddComponent<LayoutElement>();ile.preferredWidth=64;ile.preferredHeight=64;ile.minWidth=64;ile.minHeight=64;
                UITheme.Label("cap",camph.transform,"军营内景 · 征兵 / 训练骑兵 / 出师讨伐",14,TextAnchor.MiddleLeft,UITheme.Gold);
            }
            int inf=0,cav=0; foreach(var u in S.FriendlyUnits){ if(u.IsCavalry)cav++;else inf++; }
            UITheme.Label("mine",content,
                $"我方兵力：士兵{Mathf.RoundToInt(S.MilSoldiers)} 骑兵{Mathf.RoundToInt(S.MilCavalry)}　机动部队：步兵队{inf} 骑兵队{cav}",13,TextAnchor.MiddleLeft,UITheme.Gold);
            var tr=UITheme.Panel("tr",content,new Color(0,0,0,0));tr.AddComponent<LayoutElement>().preferredHeight=36;
            var th=tr.AddComponent<HorizontalLayoutGroup>();th.spacing=8;
            UITheme.Btn("train1",tr.transform,"征兵（粮20/人5）",12).onClick.AddListener(()=>{GM.Military.TrainSoldiers();FillCampaign(modal);});
            UITheme.Btn("train2",tr.transform,"训练骑兵（需马厩·粮30金20）",12).onClick.AddListener(()=>{GM.Military.TrainCavalry();FillCampaign(modal);});
            UITheme.Label("rule",content,"克制：骑兵克步兵（×1.5），箭塔/炮塔克骑兵；先征兵/训骑组建机动部队，再出师讨伐。攻克据点即兼并其地。",11,TextAnchor.UpperLeft,UITheme.Sky);
            var mil=GM.Military;
            if(!mil.FactionsInited||mil.Factions.Count==0)
                UITheme.Label("nf",content,"当前暂无割据势力（进入春秋战国·秦汉时代后群雄并起）。",13,TextAnchor.MiddleCenter);
            foreach(var f in mil.Factions)
            {
                var row=UITheme.Panel("f_"+f.Id,content,UITheme.HexA((int)f.ColorHex,f.Destroyed?0.06f:0.14f));
                row.AddComponent<LayoutElement>().preferredHeight=54;
                UITheme.Label("i",row.transform,
                    $"{f.Name}　{(f.Destroyed?"已覆灭":"人口"+Mathf.RoundToInt(f.Population)+" 守军"+f.Army.Count+" 军力"+Mathf.RoundToInt(f.Power))}",
                    13,TextAnchor.MiddleLeft).SetInset(8,0.24f);
                var b=UITheme.Btn("atk",row.transform,f.Destroyed?"已灭":"出师讨伐",12);
                var brt=b.GetComponent<RectTransform>();brt.anchorMin=new Vector2(0.76f,0.18f);brt.anchorMax=new Vector2(0.99f,0.82f);brt.offsetMin=brt.offsetMax=Vector2.zero;
                b.interactable=!f.Destroyed;
                string fid=f.Id;
                b.onClick.AddListener(()=>{ if(GM.Military.LaunchCampaign(fid)){FillCampaign(modal);} });
            }
        }

        // ===== V6.1.5 殖民时代 =====
        private void OpenColonyModal(){ FillColony(_colonyModal);Open(_colonyModal); }
        private void FillColony(GameObject modal)
        {
            var body=ModalBody(modal);Clear(body);
            UITheme.VerticalScroll("ColScroll",body.transform,out var content,6);
            var col=GM.Colonization;
            bool open=col.EraOpen;
            UITheme.Label("st",content,
                (open?"殖民时代已开启（大航海/明·清）":"尚未进入殖民时代（公元1000年大航海、明·清时代开启）")+
                $"　海外领地 {S.Colonies.Count} 处（每处 +2% 金币产出，8处达成日不落）",12,TextAnchor.MiddleLeft,open?UITheme.Gold:UITheme.Sky);
            var fr=UITheme.Panel("fr",content,new Color(0,0,0,0));fr.AddComponent<LayoutElement>().preferredHeight=38;
            var fh=fr.AddComponent<HorizontalLayoutGroup>();fh.spacing=8;
            var found=UITheme.Btn("found",fr.transform,"建立殖民地（金200木100·需1船）",12);
            found.interactable=open;
            found.onClick.AddListener(()=>{col.FoundColony();FillColony(modal);});
            if(S.Colonies.Count==0)
                UITheme.Label("empty",content,"尚无殖民地。可直接远航建立，或在海洋探索副本踩到⚓良港时建立。",12,TextAnchor.UpperLeft);
            foreach(var c in S.Colonies)
            {
                var row=UITheme.Panel("c_"+c.Id,content,UITheme.HexA(0xFFD700,0.08f));
                row.AddComponent<LayoutElement>().preferredHeight=72;
                string resName=OceanExpansionSystem.ResNames.TryGetValue(c.ResId,out var rn)?rn:c.ResId;
                UITheme.Label("i",row.transform,
                    $"🌍 {c.Name}　Lv.{c.Level}({(c.Level==1?"贸易站":c.Level==2?"殖民地":"领地")})　人口{Mathf.RoundToInt(c.Pop)}　安定{Mathf.RoundToInt(c.Loyalty)}%　特产:{resName}",
                    13,TextAnchor.UpperLeft).SetInset(8,0.3f);
                var ub=UITheme.Btn("up",row.transform,c.Level>=3?"已领地":"升格",11);
                var urt=ub.GetComponent<RectTransform>();urt.anchorMin=new Vector2(0.70f,0.52f);urt.anchorMax=new Vector2(0.86f,0.95f);urt.offsetMin=urt.offsetMax=Vector2.zero;
                ub.interactable=c.Level<3;
                ub.onClick.AddListener(()=>{col.Upgrade(c);FillColony(modal);});
                var sb=UITheme.Btn("sup",row.transform,"派兵镇压(粮50)",11);
                var srt=sb.GetComponent<RectTransform>();srt.anchorMin=new Vector2(0.70f,0.05f);srt.anchorMax=new Vector2(0.86f,0.48f);srt.offsetMin=srt.offsetMax=Vector2.zero;
                sb.onClick.AddListener(()=>{col.Suppress(c);FillColony(modal);});
            }
        }

        // ===== 建筑信息 =====
public void ShowBuilding(BuildingEntity b)
        {
            _buildingModal.SetActive(true);
            var box=_buildingModal.transform.Find("Box").GetComponent<RectTransform>();
            box.sizeDelta=new Vector2(460,620);
            var ht=_buildingModal.transform.Find("Box/Head/title");
            if(ht!=null) ht.GetComponent<Text>().text=$"{b.Def.Icon} {b.Def.Name}  Lv.{b.Level}";
            var body=ModalBody(_buildingModal);Clear(body);
            var vl=body.AddComponent<VerticalLayoutGroup>();vl.spacing=7;vl.padding=new RectOffset(6,6,4,4);
            vl.childControlWidth=true;vl.childForceExpandWidth=true;vl.childControlHeight=false;
            // V6.1.9(j) 实拍内视图（随等级切换，缺失回退大 Emoji 占位）
            var btex=PortraitLoader.Load("building",b.Type,b.Level);
            if(btex!=null) UITheme.Portrait(body.transform,btex,176);
            else { var ph=UITheme.Panel("ph",body.transform,UITheme.HexA(0x1b2440,0.9f));ph.AddComponent<LayoutElement>().preferredHeight=150;UITheme.SetOutline(ph,UITheme.Gold,1);var pe=UITheme.Label("e",ph.transform,b.Def.Icon,56,TextAnchor.MiddleCenter);pe.rectTransform.anchorMin=Vector2.zero;pe.rectTransform.anchorMax=Vector2.one;pe.rectTransform.offsetMin=pe.rectTransform.offsetMax=Vector2.zero; }
            int designLife=50+b.Level*30;
            float house=b.Def.GetFunc("housing");
            var sb=new StringBuilder();
            sb.Append("分类：").Append(b.Def.Cat).Append("　时代：").Append(GM.Eras[b.Def.Era].Name).Append('\n');
            sb.Append("寿命：设计约 ").Append(designLife).Append(" 年起　已使用 ").Append(b.Age).Append(" 年\n");
            sb.Append("耐久：").Append(Mathf.RoundToInt(b.Hp)).Append("%\n");
            if(house>0) sb.Append("可居住 ").Append(Mathf.RoundToInt(house*b.LevelMult)).Append(" 人（随等级提升）\n");
            float atk=b.Def.GetFunc("attack");
            if(atk>0) sb.Append("攻击 ").Append(Mathf.RoundToInt(atk*(1+(b.Level-1)*0.3f)))
                      .Append("　射程 ").Append(Mathf.RoundToInt(b.Def.GetFunc("range")))
                      .Append("　防御 ").Append(Mathf.RoundToInt(b.Def.GetFunc("defense"))).Append('\n');
            if(!string.IsNullOrEmpty(b.Def.Desc)) sb.Append(b.Def.Desc);
            UITheme.Label("meta",body.transform,sb.ToString(),13,TextAnchor.UpperLeft)
                .gameObject.AddComponent<LayoutElement>().preferredHeight=118;
            var row=UITheme.Panel("row",body.transform,new Color(0,0,0,0));
            var h=row.AddComponent<HorizontalLayoutGroup>();h.spacing=8;row.AddComponent<LayoutElement>().preferredHeight=40;
            var upCost=GM.Building.UpgradeCost(b);
            var up=UITheme.Btn("up",row.transform,upCost==null?" 已达满级":(" 升级（"+CostText(upCost)+"）"),13);
            up.interactable=GM.Building.CanUpgrade(b);
            BuildingEntity cap=b;
            up.onClick.AddListener(()=>{GM.Building.Upgrade(cap);ShowBuilding(cap);});
            UITheme.Btn("del",row.transform," 拆除(返50%)",13).onClick.AddListener(()=>{GM.Building.Demolish(cap);_buildingModal.SetActive(false);});
        }

        // ===== 船只浮窗（对齐 v5.9.9 船只小窗：等级/船员/耐久/攻击/居住，可升级/解散，ESC·右键·点空白关闭）=====
        public void ShowShip(ShipEntity s)
        {
            _selectedShip=s; FillShip(s); _shipModal.SetActive(true);
        }
        public void CloseShipCard()
        {
            _selectedShip=null;
            if (_shipModal!=null) _shipModal.SetActive(false);
        }
        private void FillShip(ShipEntity s)
        {
            var body=ModalBody(_shipModal);Clear(body);
            var vl=body.AddComponent<VerticalLayoutGroup>();vl.spacing=7;vl.padding=new RectOffset(10,10,8,8);
            vl.childControlWidth=true;vl.childForceExpandWidth=true;
            var naval=GM.Naval;
            bool hasDef=naval.Defs.TryGetValue(s.ShipTypeId,out var d);
            string icon=hasDef?d.Icon:"🚢";
            UITheme.Label("name",body.transform,$"{icon} {s.Name} · {naval.LevelName(s)}",19,TextAnchor.MiddleCenter,UITheme.Gold);
            // V6.1.3 船只实拍图（随普通/精良/传奇切换）
            var stex=PortraitLoader.Load("ship",s.ShipTypeId,s.Level);
            if(stex!=null) UITheme.Portrait(body.transform,stex);
            var sb=new StringBuilder();
            sb.Append("类型：").Append(s.Military?"军用舰船":"民用船只").Append('\n');
            sb.Append("船员：").Append(s.Crew).Append("/").Append(naval.Capacity(s)).Append("人\n");
            if(s.Level>=2){ if(s.Military) sb.Append("<color=#8be9fd>⚔ 作战载人：").Append(s.Crew).Append("/").Append(naval.Capacity(s)).Append("（Lv2已激活）</color>\n");
                else sb.Append("<color=#8be9fd>👥 载客：").Append(s.Passengers).Append("/").Append(s.EffectiveHousing).Append("（Lv2已激活，附近居民自动登船）</color>\n"); }
            sb.Append("耐久：").Append(Mathf.Max(0,Mathf.RoundToInt(s.Hp))).Append("/").Append(Mathf.RoundToInt(s.MaxHp)).Append('\n');
            if (s.Military) sb.Append("攻击：").Append(naval.AttackOf(s)).Append("  射程：").Append(Mathf.RoundToInt(s.Range)).Append('\n');
            sb.Append("速度：").Append(naval.SpeedOf(s).ToString("0.00")).Append('\n');
            sb.Append("<color=#8be9fd>🏠 居住 ").Append(s.EffectiveHousing).Append(" 人</color>");
            UITheme.Label("meta",body.transform,sb.ToString(),13,TextAnchor.UpperLeft);
            var row=UITheme.Panel("row",body.transform,new Color(0,0,0,0));
            var h=row.AddComponent<HorizontalLayoutGroup>();h.spacing=8;row.AddComponent<LayoutElement>().preferredHeight=40;
            var cost=naval.UpgradeCost(s);
            string upLabel = cost==null ? " 已达传奇" : (" 升级("+CostText(cost)+")");
            var up=UITheme.Btn("up",row.transform,upLabel,13);
            up.interactable=cost!=null;
            up.onClick.AddListener(()=>{ if(naval.UpgradeShip(s)) FillShip(s); });
            UITheme.Btn("scuttle",row.transform," 解散",13).onClick.AddListener(()=>{naval.ScuttleShip(s);CloseShipCard();});
        }
        // ===== 车辆浮窗（V6.1.3，对齐船只小窗：等级/运力/耐久，可升级，实拍图随等级切换）=====
        public void ShowCart(CartEntity c)
        {
            _selectedCart=c; FillCart(c); _cartModal.SetActive(true);
        }
        public void CloseCart()
        {
            _selectedCart=null;
            if(_cartModal!=null) _cartModal.SetActive(false);
        }
        private void FillCart(CartEntity c)
        {
            var body=ModalBody(_cartModal);Clear(body);
            var vl=body.AddComponent<VerticalLayoutGroup>();vl.spacing=7;vl.padding=new RectOffset(10,10,8,8);
            vl.childControlWidth=true;vl.childForceExpandWidth=true;
            var cart=GM.Cart;
            bool hasDef=cart.Defs.TryGetValue(c.CartTypeId,out var cd);
            string icon=hasDef?cd.Icon:"🛒";
            UITheme.Label("name",body.transform,$"{icon} {c.Name} · {cart.CartLevelName(c)}",19,TextAnchor.MiddleCenter,UITheme.Gold);
            var ctex=PortraitLoader.Load("cart",c.CartTypeId,c.Level);
            if(ctex!=null) UITheme.Portrait(body.transform,ctex);
            var sb=new StringBuilder();
            sb.Append("类型：陆地车辆\n");
            sb.Append("运力：").Append(c.Capacity).Append(" 单位\n");
            sb.Append("耐久：").Append(Mathf.Max(0,c.Durability)).Append("/").Append(c.MaxDurability).Append('\n');
            sb.Append("驾驶员：").Append(c.HasDriver?"有":"无");
            if(c.Level>=2) sb.Append("\n<color=#8be9fd>👥 载客：").Append(c.Passengers).Append("/").Append(c.Capacity).Append("（Lv2已激活，附近居民自动登车）</color>");
            UITheme.Label("meta",body.transform,sb.ToString(),13,TextAnchor.UpperLeft);
            var row=UITheme.Panel("row",body.transform,new Color(0,0,0,0));
            var h=row.AddComponent<HorizontalLayoutGroup>();h.spacing=8;row.AddComponent<LayoutElement>().preferredHeight=40;
            var cost=cart.CartUpgradeCost(c);
            string upLabel=cost==null?" 已达传奇":(" 升级("+CostText(cost)+")");
            var up=UITheme.Btn("up",row.transform,upLabel,13); up.interactable=cost!=null;
            up.onClick.AddListener(()=>{ if(cart.UpgradeCart(c)) FillCart(c); });
            UITheme.Btn("close",row.transform," 关闭",13).onClick.AddListener(CloseCart);
        }

        private static string CostText(Dictionary<string,int> cost)
        {
            var sb=new StringBuilder();bool first=true;
            foreach(var kv in cost){ if(!first)sb.Append(' ');first=false;sb.Append(ResourceDatabase.Icons.TryGetValue(kv.Key,out var ic)?ic:"").Append(kv.Value); }
            return sb.ToString();
        }

        private void Clear(GameObject body)
        {
            for(int i=body.transform.childCount-1;i>=0;i--)Destroy(body.transform.GetChild(i).gameObject);
            // 移除上一次填充挂载的布局组件，避免重复叠加
            foreach(var lg in body.GetComponents<HorizontalOrVerticalLayoutGroup>())DestroyImmediate(lg);
            foreach(var g in body.GetComponents<GridLayoutGroup>())DestroyImmediate(g);
        }
    }

    internal static class RectExt
    {
        public static Text SetInset(this Text t,float left,float rightRatio)
        {
            t.rectTransform.offsetMin=new Vector2(left,2);
            t.rectTransform.offsetMax=new Vector2(-8,-2);
            return t;
        }
    }
}
