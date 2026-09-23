using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PixelToCivilization.Core;
using PixelToCivilization.Data;
using PixelToCivilization.Systems;

namespace PixelToCivilization.UI
{
    /// <summary>
    /// V6.1.9(j) 界面系统重构（对齐 7 张参考图）：
    /// 1 建筑面板 10 分类 + 卡片 + 最大/最小化；2 顶部 9 资源 + 悬浮说明；3 底部常用按钮(按使用次数)；
    /// 4 帮助界面；5 左下按钮组；6 国家状态面板最大/最小化；7 世界建筑浮标 + 建筑详情(实拍内视图/寿命/耐久/升级拆除/居住/塔)。
    /// </summary>
    public partial class UIManager
    {
        // —— 建筑面板 ——
        private GameObject _leftBody, _tabGrid;
        private bool _leftCollapsed;
        private int _lastBuildEra = -999;
        // —— 国家状态面板 ——
        private RectTransform _rightRt;
        private GameObject _rightBody;
        private bool _rightCollapsed, _rightWide;
        private Button _leftMinBtn, _rightMinBtn;
        // —— 顶部资源悬浮说明 ——
        private GameObject _resTipGo;
        private Text _resTipText;
        // —— 底部常用按钮（按实际使用次数） ——
        private GameObject _quickBar;
        private readonly Dictionary<string,int> _usage = new();
        // —— 世界建筑浮标 ——
        private RectTransform _markerLayer;
        private readonly Dictionary<BuildingEntity,GameObject> _markers = new();
        private float _markerTimer;
        private Sprite _badgeSprite;
        // —— 帮助 / 日志 ——
        private GameObject _helpModal, _logModal;

        // 用户指定的 10 个建造分类（显示顺序）
        private static readonly string[] BuildTabsV2 =
            {"居住","农业","工业","经济","文化","军事","交通","科技","能源","太空"};
        // 用户指定的顶部 9 项资源（顺序即显示顺序；后三项为“实力”）
        private static readonly (string key,string label)[] ResBarV2 =
        {
            ("food","粮食"),("wood","木材"),("stone","石头"),("gold","金币"),
            ("steel","钢铁"),("bronze","青铜"),("goods","经济实力"),
            ("culture","文化实力"),("research","科技实力"),
        };
        private static readonly Dictionary<string,string> ResDesc = new()
        {
            {"food","粮食：维持人口生存，粮食不足会爆发饥荒、人口流失"},
            {"wood","木材：最基础的建筑与造船材料，伐木场/森林产出"},
            {"stone","石料：坚固建筑、城墙与防御塔材料，矿场产出"},
            {"gold","金币：贸易与市场所得，训练、升级、殖民的硬通货"},
            {"steel","钢铁：工业时代后的核心材料，现代建筑/军队必备"},
            {"bronze","青铜：早期礼器与兵器材料，青铜坊冶炼"},
            {"goods","经济实力：作坊/工厂产出的货物，代表综合经济规模"},
            {"culture","文化实力：学派、文化建筑积累，影响民心与时代演进"},
            {"research","科技实力：研究点，用于在科技树解锁新技术、新时代"},
        };

        // ============================================================
        // 需求2：顶部资源栏（9 项 + 悬浮说明）
        // ============================================================
private void BuildTopBarV2(Transform parent)
        {
            var bar=UITheme.Surface("TopBar",parent,new Color(0.97f,0.97f,0.94f,0.96f));
            var rt=bar.GetComponent<RectTransform>();
            rt.anchorMin=new Vector2(0,1);rt.anchorMax=new Vector2(1,1);rt.pivot=new Vector2(0.5f,1);
            rt.offsetMin=new Vector2(10,-46);rt.offsetMax=new Vector2(-10,-6);
            var h=bar.AddComponent<HorizontalLayoutGroup>();
            h.padding=new RectOffset(14,14,5,5);h.spacing=7;h.childAlignment=TextAnchor.MiddleLeft;
            h.childControlWidth=true;h.childControlHeight=true;h.childForceExpandWidth=false;h.childForceExpandHeight=false;

            foreach (var (key,label) in ResBarV2)
            {
                var item=UITheme.Panel("Res_"+key,bar.transform,new Color(0,0,0,0));
                var ile=item.AddComponent<LayoutElement>();ile.preferredWidth=92;ile.preferredHeight=38;ile.minWidth=84;
                var ih=item.AddComponent<HorizontalLayoutGroup>();ih.spacing=4;ih.padding=new RectOffset(4,4,0,0);
                ih.childControlWidth=false;ih.childControlHeight=true;ih.childForceExpandWidth=false;ih.childForceExpandHeight=false;ih.childAlignment=TextAnchor.MiddleCenter;
                UITheme.Icon(item.transform,key,20);
                var v=UITheme.Label("v",item.transform,"0",14,TextAnchor.MiddleLeft,UITheme.Gold,FontStyle.Bold);
                var vle=v.gameObject.AddComponent<LayoutElement>();vle.preferredWidth=52;
                _resTexts[key]=v;
                string tip=$"{label}｜{ResDesc[key]}";
                AddHover(item,tip);
            }
            var tsp=UITheme.Panel("tsp",bar.transform,new Color(0,0,0,0));tsp.AddComponent<LayoutElement>().flexibleWidth=1;
            _eraTag=Pill(bar.transform,"",UITheme.HexA(0xf0d9a8,0.97f),118);
            _dynastyTag=Pill(bar.transform,"",UITheme.Chip,88);
            _timeText=Pill(bar.transform,"第1年",UITheme.Chip,66);
            _gregText=Pill(bar.transform,"公元前3000年",UITheme.Chip,118,UITheme.Sky);
            _popText=Pill(bar.transform,"80",UITheme.Chip,112); _popText.horizontalOverflow=HorizontalWrapMode.Overflow; // V6.3.6 加宽+不换行，避免“人口88/162”折成两行误读为8
            _envText=Pill(bar.transform,"晴天",UITheme.Chip,228,UITheme.Sky);

            _resTipGo=UITheme.Surface("ResTip",parent,new Color(0.99f,0.98f,0.95f,0.98f));
            UITheme.SetOutline(_resTipGo,UITheme.Gold,1);
            var trt=_resTipGo.GetComponent<RectTransform>();
            trt.anchorMin=new Vector2(0,1);trt.anchorMax=new Vector2(0,1);trt.pivot=new Vector2(0,1);
            trt.anchoredPosition=new Vector2(14,-52);trt.sizeDelta=new Vector2(360,34);
            _resTipText=UITheme.Label("t",_resTipGo.transform,"",13,TextAnchor.MiddleLeft,UITheme.Text);
            Stretch(_resTipText.gameObject);_resTipText.rectTransform.offsetMin=new Vector2(8,0);_resTipText.rectTransform.offsetMax=new Vector2(-8,0);
            _resTipGo.SetActive(false);
        }

        private float _resTipW=200f;
        // V6.3.4：EventTrigger 的 PointerEnter 在 WebGL/快速移动下会丢事件导致悬浮字时有时无，
        // 改为给对象挂 HoverTip 标记，由 UpdateHoverTip 每 0.05s 用 GraphicRaycaster 统一判定，稳定可靠。
        private void AddHover(GameObject go,string tip)
        {
            var ht=go.GetComponent<HoverTip>()??go.AddComponent<HoverTip>();
            ht.Tip=tip;
        }
        private GraphicRaycaster _uiRay;
        private PointerEventData _hoverPd;
        private readonly List<RaycastResult> _hoverHits=new List<RaycastResult>();
        private string _curHover;
        private float _hoverCd;
        private void UpdateHoverTip()
        {
            if(!_resTipGo||_hud==null)return;
            _hoverCd-=Time.unscaledDeltaTime;
            if(_hoverCd>0f){ if(_resTipGo.activeSelf)FollowTip(); return; }
            _hoverCd=0.05f;
            var es=EventSystem.current;
            if(es==null){ HideTip(); return; }
            if(_uiRay==null)_uiRay=_hud.GetComponentInParent<GraphicRaycaster>();
            if(_uiRay==null){ HideTip(); return; }
            if(_hoverPd==null)_hoverPd=new PointerEventData(es);
            _hoverPd.position=Input.mousePosition;
            _hoverHits.Clear();
            _uiRay.Raycast(_hoverPd,_hoverHits);
            HoverTip found=null;
            foreach(var h in _hoverHits)
            {
                var t=h.gameObject.GetComponentInParent<HoverTip>();
                if(t!=null&&t.enabled&&t.gameObject.activeInHierarchy){ found=t; break; }
            }
            if(found!=null)
            {
                if(_curHover!=found.Tip||!_resTipGo.activeSelf)
                {
                    _curHover=found.Tip;
                    _resTipGo.transform.SetParent(_hud.transform,false);
                    _resTipGo.transform.SetAsLastSibling();   // 永远置顶，不被任何面板/弹窗遮挡
                    _resTipText.text=found.Tip;
                    _resTipW=Mathf.Clamp(found.Tip.Length*15+24,120,420);
                    _resTipGo.SetActive(true);
                }
                FollowTip();
            }
            else { _curHover=null; HideTip(); }
        }
        private void HideTip(){ if(_resTipGo)_resTipGo.SetActive(false); }

        // V6.3.5：全 UI 递归悬浮——给所有还没有提示的按钮按其文字/命名自动补悬浮说明（每秒补扫一次，覆盖动态重建的子界面）
        private float _autoHoverCd;
        private static readonly Dictionary<string,string> NamedHoverTip=new()
        {
            {"pause","暂停 / 继续游戏时间"},{"minus","降低一档游戏速度"},{"plus","提高一档游戏速度"},
            {"up","向北移动（副本）"},{"down","向南移动（副本）"},{"left","向西移动（副本）"},{"right","向东移动（副本）"},
            {"auto","自动探索当前副本，自动揭开迷雾并结算节点"},{"return","返航整补，返回母港并补满战力与补给"},
            {"close","关闭当前窗口"},{"c0","副本格子：迷雾/已探索/当前位置"},
        };
        private void AutoBindHovers(Transform root)
        {
            if(root==null)return;
            foreach(var b in root.GetComponentsInChildren<Button>(true))
            {
                if(b.GetComponent<HoverTip>()!=null)continue;
                string tip=null;
                if(NamedHoverTip.TryGetValue(b.gameObject.name,out var nt))tip=nt;
                if(string.IsNullOrEmpty(tip)){var tx=b.GetComponentInChildren<Text>();if(tx!=null)tip=tx.text;}
                if(!string.IsNullOrWhiteSpace(tip)){var ht=b.gameObject.AddComponent<HoverTip>();ht.Tip=tip;}
            }
        }
        private void TickAutoHover(float dt)
        {
            _autoHoverCd-=dt;
            if(_autoHoverCd>0f)return;
            _autoHoverCd=1f;
            if(_hud!=null)AutoBindHovers(_hud.transform);
        }
        // 悬浮字跟随当前鼠标位置，并自动避开屏幕四边
        private void FollowTip()
        {
            if(!_resTipGo||!_resTipGo.activeSelf||_hud==null)return;
            var hudRt=_hud.GetComponent<RectTransform>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(hudRt,Input.mousePosition,null,out Vector2 lp);
            var trt=_resTipGo.GetComponent<RectTransform>();
            // V6.3.5 修复：ScreenPointToLocal 返回的是相对 HUD 中心的坐标，
            // 锚点/轴心必须同为中心(0.5,0.5)，旧代码用(0,1)左上角锚点导致提示整体偏到屏幕左外不可见。
            trt.anchorMin=trt.anchorMax=trt.pivot=new Vector2(0.5f,0.5f);
            var r=hudRt.rect; const float h=34f; float w=_resTipW;
            float cx=lp.x+16f+w*0.5f; if(cx+w*0.5f>r.xMax)cx=lp.x-16f-w*0.5f;
            cx=Mathf.Clamp(cx,r.xMin+w*0.5f+2f,r.xMax-w*0.5f-2f);
            float cy=lp.y+20f; if(cy+h*0.5f>r.yMax)cy=lp.y-20f;
            cy=Mathf.Clamp(cy,r.yMin+h*0.5f+2f,r.yMax-h*0.5f-2f);
            trt.anchoredPosition=new Vector2(cx,cy);
            trt.sizeDelta=new Vector2(w,h);
        }

        // ============================================================
        // 需求1：建筑系统面板（10 分类 + 卡片 + 最大/最小化）
        // ============================================================
        private void BuildLeftPanelV2(Transform parent)
        {
            var panel=UITheme.Glass("LeftPanel",parent);
            _leftPanel=panel;
            var rt=panel.GetComponent<RectTransform>();
            rt.anchorMin=new Vector2(0,1);rt.anchorMax=new Vector2(0,1);rt.pivot=new Vector2(0,1);
            rt.offsetMin=new Vector2(10,-1010);rt.offsetMax=new Vector2(236,-58);
            var vl=panel.AddComponent<VerticalLayoutGroup>();vl.spacing=4;vl.padding=new RectOffset(6,6,6,6);
            vl.childControlWidth=true;vl.childForceExpandWidth=true;vl.childControlHeight=true;vl.childForceExpandHeight=false;vl.childAlignment=TextAnchor.UpperCenter;

            // 标题行 + 最小化按钮（手动锚点固定，避免被布局组纵向拉伸）
            var head=UITheme.Panel("Head",panel.transform,new Color(0,0,0,0));
            head.AddComponent<LayoutElement>().preferredHeight=28;
            var title=UITheme.Label("T",head.transform,"建造",16,TextAnchor.MiddleLeft,UITheme.Gold,FontStyle.Bold);
            title.rectTransform.anchorMin=Vector2.zero;title.rectTransform.anchorMax=Vector2.one;
            title.rectTransform.offsetMin=new Vector2(2,0);title.rectTransform.offsetMax=new Vector2(-34,0);
            var min=UITheme.Btn("min",head.transform,"—",14);FixedRight(min.GetComponent<RectTransform>(),0,28);
            min.GetComponent<LayoutElement>().ignoreLayout=true;
            _leftMinBtn=min;min.onClick.AddListener(ToggleLeftPanel);

            _leftBody=UITheme.Panel("Body",panel.transform,new Color(0,0,0,0));
            _leftBody.AddComponent<LayoutElement>().flexibleHeight=1;
            var bv=_leftBody.AddComponent<VerticalLayoutGroup>();bv.spacing=4;bv.childControlWidth=true;bv.childForceExpandWidth=true;bv.childControlHeight=true;bv.childForceExpandHeight=false;

            _tabGrid=UITheme.Panel("CatTabs",_leftBody.transform,new Color(0,0,0,0));
            var tg=_tabGrid.AddComponent<GridLayoutGroup>();tg.constraint=GridLayoutGroup.Constraint.FixedColumnCount;tg.constraintCount=4;
            tg.cellSize=new Vector2(49,26);tg.spacing=new Vector2(4,4);
            _tabGrid.AddComponent<LayoutElement>().preferredHeight=86;
            foreach (var cat in BuildTabsV2)
            {
                var b=UITheme.Btn("tab_"+cat,_tabGrid.transform,cat,11,UITheme.Chip);
                string c=cat;b.onClick.AddListener(()=>{_activeCat=c;RebuildBuildListV2();RefreshTabColors();});
            }
            var scroll=UITheme.VerticalScroll("BuildScroll",_leftBody.transform,out var content,3);
            _buildList=content;scroll.gameObject.AddComponent<LayoutElement>().flexibleHeight=1;
            RefreshTabColors();
        }

        public void WebToggleLeft(){ToggleLeftPanel();}
        public void WebToggleRight(){ToggleRightPanel();}
        // 雷达式最小化：整个面板收成 40px 标题条，露出大地图；再点恢复
        private void ToggleLeftPanel()
        {
            _leftCollapsed=!_leftCollapsed;
            _leftBody.SetActive(!_leftCollapsed);
            var lrt=_leftPanel.GetComponent<RectTransform>();
            lrt.offsetMin=new Vector2(10,_leftCollapsed?-98:-1010);
            if(_leftMinBtn)_leftMinBtn.GetComponentInChildren<Text>().text=_leftCollapsed?"+":"—";
        }
        private void ToggleRightPanel()
        {
            _rightCollapsed=!_rightCollapsed;
            _rightBody.SetActive(!_rightCollapsed);
            _rightRt.offsetMin=new Vector2(_rightWide?-392:-250,_rightCollapsed?-98:-1010);
            if(_rightMinBtn)_rightMinBtn.GetComponentInChildren<Text>().text=_rightCollapsed?"+":"—";
        }
        /// <summary>把按钮固定在父级右侧 x 偏移处、纵向拉伸（不依赖布局组）</summary>
        private static void FixedRight(RectTransform rt,float rightX,float w)
        {
            rt.anchorMin=new Vector2(1,0);rt.anchorMax=new Vector2(1,1);rt.pivot=new Vector2(1,0.5f);
            rt.sizeDelta=new Vector2(w,0);rt.anchoredPosition=new Vector2(-rightX,0);
        }
        private void RefreshTabColors()
        {
            if(_tabGrid==null)return;
            foreach(Transform t in _tabGrid.transform)
            {
                var btn=t.GetComponent<Button>(); if(btn==null)continue;
                bool on=t.name=="tab_"+_activeCat;
                btn.GetComponent<Image>().color=on?UITheme.ChipActive:UITheme.Chip;
                var tx=t.GetComponentInChildren<Text>(); if(tx){tx.color=on?UITheme.ChipActiveText:UITheme.Text;tx.fontStyle=on?FontStyle.Bold:FontStyle.Normal;}
            }
        }

        /// <summary>旧 13 分类 → 用户指定 10 分类的显示映射（不改自动生成的建筑数据库）</summary>
        private static string TabOf(BuildingDefinition d)
        {
            switch(d.Id)
            {
                case "well": case "granary": case "water_mill": return "农业";
                case "road": case "highway": case "canal": case "railway_pre":
                case "high_speed_rail": case "highway_modern": case "airport": case "caravanserai":
                case "shipyard_pre": case "treasure_shipyard": case "sea_port":
                case "customs_house": case "compass_shop": case "dockyard_modern":

                case "bridge_wood": case "bridge_stone": case "bridge_steel": case "bridge_concrete": return "交通";
                case "telegraph": return "科技";
            }
            return d.Cat switch
            {
                "居住"=>"居住","食物"=>"农业","资源"=>"工业","工业"=>"工业",
                "经济"=>"经济","文化"=>"文化","军事"=>"军事",
                "能源"=>"能源","科技"=>"科技","太空"=>"太空",_=>"工业",
            };
        }
        private static string TabIcon(string tab)=>tab switch
        {
            "居住"=>"house","农业"=>"farm","工业"=>"factory","经济"=>"bank","文化"=>"culture",
            "军事"=>"military","交通"=>"road","科技"=>"research","能源"=>"power","太空"=>"rocket",_=>"house",
        };

        private void RebuildBuildListV2()
        {
            if(_buildList==null)return;
            var cvlg=_buildList.GetComponent<VerticalLayoutGroup>();
            if(cvlg!=null)cvlg.childControlHeight=true;
            for(int i=_buildList.childCount-1;i>=0;i--)Destroy(_buildList.GetChild(i).gameObject);
            RefreshTabColors();
            int n=0;
            // V6.3.4 交通类：车辆、船只排在最前，交通类建筑排在其后
            if(_activeCat=="交通")
            {
                UITheme.Label("ct",_buildList,"— 车辆（陆地）—",12,TextAnchor.MiddleCenter,UITheme.Gold)
                    .gameObject.AddComponent<LayoutElement>().preferredHeight=20;
                foreach(var kv in GM.Cart.Defs)
                {
                    var d=kv.Value;string id=d.Id;
                    MakeBuildCard(_buildList,"cart",d.Name,d.Cost,"陆地运输",true,()=>
                    { S.SelectedBuildType="cart:"+id;S.Tool="build";RecordUsage("c:"+id); });
                }
                UITheme.Label("st",_buildList,"— 船只（水域）—",12,TextAnchor.MiddleCenter,UITheme.Gold)
                    .gameObject.AddComponent<LayoutElement>().preferredHeight=20;
                foreach(var kv in GM.Naval.Defs)
                {
                    var d=kv.Value;bool unlocked=d.Era<=S.Era;string id=d.Id;
                    MakeBuildCard(_buildList,"ship",d.Name,d.Cost,d.Military?"军用舰船":"水上运输",unlocked,()=>
                    { S.SelectedBuildType="ship:"+id;S.Tool="build";RecordUsage("s:"+id); });
                }
            }

            foreach(var kv in GM.Buildings.OrderBy(k=>k.Value.Era).ThenBy(k=>k.Key))
            {
                var d=kv.Value;
                if(TabOf(d)!=_activeCat)continue;
                bool unlocked=d.Era<=S.Era;
                string id=d.Id;
                MakeBuildCard(_buildList,BuildingIconKey(d),d.Name,d.Cost,CapText(d),unlocked,()=>
                {
                    S.SelectedBuildType=id;S.Tool="build";RecordUsage("b:"+id);
                });
                n++;
            }
            if(n==0 && _activeCat!="交通")
                UITheme.Label("empty",_buildList,"（当前时代暂无此类建筑）",12,TextAnchor.MiddleCenter,UITheme.HexA(0x999999,1));
        }

        /// <summary>建筑 → IconFactory 矢量图标 key（复用既有分类映射，保证无 Emoji 也能显示）</summary>
        private static string BuildingIconKey(BuildingDefinition d)=>CatIconLegacy(d.Cat,d.Id);

        /// <summary>建筑能力摘要（居住/攻击/防御/产出），纯中文短字（WebGL 字体无 Emoji）</summary>
        private static string CapText(BuildingDefinition d)
        {
            var sb=new StringBuilder();
            float h=d.GetFunc("housing"); if(h>0)sb.Append("住").Append(Mathf.RoundToInt(h));
            float atk=d.GetFunc("attack"); if(atk>0)sb.Append(" 攻").Append(Mathf.RoundToInt(atk)).Append("/射").Append(Mathf.RoundToInt(d.GetFunc("range")));
            float def=d.GetFunc("defense"); if(def>0)sb.Append(" 防").Append(Mathf.RoundToInt(def));
            if(d.Production!=null)
                foreach(var p in d.Production)
                    sb.Append(' ').Append(ResShort(p.Key)).Append('+').Append(p.Value.ToString("0.#"));
            return sb.ToString();
        }
        private static readonly Dictionary<string,string> ResShortMap=new()
        {
            {"wood","木"},{"stone","石"},{"food","粮"},{"gold","金"},{"iron","铁"},{"bronze","铜"},
            {"goods","货"},{"culture","文"},{"research","研"},{"power","电"},{"steel","钢"},
            {"concrete","砼"},{"fusion","聚"},{"carbon","碳"},{"helium3","氦"},
        };
        private static string ResShort(string k)=>ResShortMap.TryGetValue(k,out var v)?v:k;

        private bool Affordable(Dictionary<string,int> cost)
        {
            if(cost==null)return true;
            foreach(var c in cost) if(S.GetRes(c.Key)<c.Value)return false;
            return true;
        }
        private static string CostSmall(Dictionary<string,int> cost)
        {
            if(cost==null||cost.Count==0)return "—";
            var sb=new StringBuilder();bool first=true;
            foreach(var kv in cost)
            {
                if(!first)sb.Append(' ');first=false;
                sb.Append(ResShort(kv.Key)).Append(kv.Value);
            }
            return sb.ToString();
        }

        /// <summary>参考图 1 风格的建筑卡：圆角图标块 + 名称 + 资源成本 + 能力行</summary>
private Button MakeBuildCard(Transform parent,string iconKey,string title,Dictionary<string,int> cost,string cap,bool unlocked,Action onClick)
        {
            var b=UITheme.Btn("card",parent,"",12);
            var defTxt=b.transform.Find("Text"); if(defTxt)Destroy(defTxt.gameObject);
            b.GetComponent<Image>().color=unlocked?UITheme.HexA(0x232e50,0.96f):UITheme.HexA(0x1a2033,0.8f);
            b.interactable=unlocked;
            b.GetComponent<LayoutElement>().preferredHeight=52;
            var h=b.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding=new RectOffset(6,6,5,5);h.spacing=8;h.childAlignment=TextAnchor.MiddleLeft;
            h.childControlWidth=true;h.childControlHeight=true;h.childForceExpandWidth=false;h.childForceExpandHeight=false;
            var ib=UITheme.Surface("ico",b.transform,UITheme.HexA(0x141d36,1f));
            var ile=ib.AddComponent<LayoutElement>();ile.preferredWidth=38;ile.preferredHeight=38;ile.minWidth=38;ile.minHeight=38;
            UITheme.SetOutline(ib,UITheme.Gold,1);
            var il=ib.AddComponent<HorizontalLayoutGroup>();il.childAlignment=TextAnchor.MiddleCenter;il.childControlWidth=true;il.childControlHeight=true;il.childForceExpandWidth=false;il.childForceExpandHeight=false;
            UITheme.Icon(ib.transform,iconKey,26);
            var vb=UITheme.Panel("txt",b.transform,new Color(0,0,0,0));
            var vle=vb.AddComponent<LayoutElement>();vle.flexibleWidth=1;vle.preferredHeight=40;
            var v=vb.AddComponent<VerticalLayoutGroup>();v.spacing=2;v.padding=new RectOffset(2,2,0,0);v.childAlignment=TextAnchor.MiddleCenter;
            v.childControlWidth=true;v.childForceExpandWidth=true;v.childControlHeight=true;v.childForceExpandHeight=false;
            UITheme.Label("n",vb.transform,title+(unlocked?"":" ·锁定"),13,TextAnchor.MiddleLeft,unlocked?UITheme.Text:UITheme.HexA(0x999999,1))
                .gameObject.AddComponent<LayoutElement>().preferredHeight=18;
            Color cc=unlocked&&Affordable(cost)?UITheme.Good:(unlocked?UITheme.Hex(0xe0b48a):UITheme.HexA(0x888888,1));
            UITheme.Label("c",vb.transform,CostSmall(cost)+(string.IsNullOrEmpty(cap)?"":"    "+cap),10,TextAnchor.MiddleLeft,cc)
                .gameObject.AddComponent<LayoutElement>().preferredHeight=14;
            b.onClick.AddListener(()=>onClick());
            return b;
        }

        // ============================================================
        // 需求6：国家状态面板（最大化/最小化）
        // ============================================================
        private void BuildRightPanelV2(Transform parent)
        {
            var panel=UITheme.Glass("RightPanel",parent);
            _rightRt=panel.GetComponent<RectTransform>();
            _rightRt.anchorMin=new Vector2(1,1);_rightRt.anchorMax=new Vector2(1,1);_rightRt.pivot=new Vector2(1,1);
            _rightRt.offsetMin=new Vector2(-250,-1010);_rightRt.offsetMax=new Vector2(-10,-58);
            var vl=panel.AddComponent<VerticalLayoutGroup>();vl.spacing=6;vl.padding=new RectOffset(8,8,8,8);
            vl.childControlWidth=true;vl.childForceExpandWidth=true;vl.childControlHeight=true;vl.childForceExpandHeight=false;vl.childAlignment=TextAnchor.UpperCenter;

            var head=UITheme.Panel("Head",panel.transform,new Color(0,0,0,0));
            head.AddComponent<LayoutElement>().preferredHeight=28;
            var title=UITheme.Label("T",head.transform,"国家状态",16,TextAnchor.MiddleLeft,UITheme.Gold,FontStyle.Bold);
            title.rectTransform.anchorMin=Vector2.zero;title.rectTransform.anchorMax=Vector2.one;
            title.rectTransform.offsetMin=new Vector2(2,0);title.rectTransform.offsetMax=new Vector2(-68,0);
            var min=UITheme.Btn("min",head.transform,"—",14);FixedRight(min.GetComponent<RectTransform>(),0,28);min.GetComponent<LayoutElement>().ignoreLayout=true;
            var max=UITheme.Btn("max",head.transform,"□",14);FixedRight(max.GetComponent<RectTransform>(),32,28);max.GetComponent<LayoutElement>().ignoreLayout=true;

            _rightBody=UITheme.Panel("Body",panel.transform,new Color(0,0,0,0));
            _rightBody.AddComponent<LayoutElement>().flexibleHeight=1;
            var bv=_rightBody.AddComponent<VerticalLayoutGroup>();bv.spacing=6;bv.childControlWidth=true;bv.childForceExpandWidth=true;bv.childControlHeight=true;bv.childForceExpandHeight=false;
            BuildStatsSections(_rightBody.transform);

            var actions=UITheme.Panel("Actions",_rightBody.transform,new Color(0,0,0,0));
            var ag=actions.AddComponent<GridLayoutGroup>();ag.constraint=GridLayoutGroup.Constraint.FixedColumnCount;ag.constraintCount=3;
            ag.cellSize=new Vector2(70,32);ag.spacing=new Vector2(4,5);
            actions.AddComponent<LayoutElement>().preferredHeight=136;
            UITheme.BtnIcon("tech",actions.transform,"research","科技",12).onClick.AddListener(OpenTechModal);
            UITheme.BtnIcon("policy",actions.transform,"culture","政策",12).onClick.AddListener(OpenPolicyModal);
            UITheme.BtnIcon("gods",actions.transform,"god","九神",12).onClick.AddListener(OpenGodsModal);
            UITheme.BtnIcon("philosophy",actions.transform,"culture","百家",12).onClick.AddListener(OpenPhilosophyModal);
            UITheme.BtnIcon("army",actions.transform,"military","征兵",12).onClick.AddListener(()=>GM.Military.TrainSoldiers());
            UITheme.BtnIcon("cavalry",actions.transform,"military","骑兵",12).onClick.AddListener(()=>GM.Military.TrainCavalry());
            UITheme.BtnIcon("campaign",actions.transform,"military","讨伐",12).onClick.AddListener(()=>{RecordUsage("mil");OpenCampaignModal();});
            UITheme.BtnIcon("colony",actions.transform,"ship","殖民",12).onClick.AddListener(OpenColonyModal);
            UITheme.BtnIcon("wonder",actions.transform,"temple","奇观",12).onClick.AddListener(OpenWonderModal);
            UITheme.BtnIcon("auto",actions.transform,"setting","自动",12).onClick.AddListener(()=>GM.Building.AutoBuildEnabled=!GM.Building.AutoBuildEnabled);
            _oceanBtn=UITheme.BtnIcon("ocean",actions.transform,"ship","海洋",12).gameObject;
            _oceanBtn.GetComponent<Button>().onClick.AddListener(()=>{RecordUsage("ocean");OpenOceanModal();});_oceanBtn.SetActive(false);
            _spaceBtn=UITheme.BtnIcon("space",actions.transform,"rocket","太空",12).gameObject;
            _spaceBtn.GetComponent<Button>().onClick.AddListener(()=>{RecordUsage("space");OpenSpaceModal();});_spaceBtn.SetActive(false);

            UITheme.Label("LogT",_rightBody.transform,"编年史",13,TextAnchor.MiddleLeft,UITheme.Gold,FontStyle.Bold).gameObject.AddComponent<LayoutElement>().preferredHeight=20;
            var logScroll=UITheme.VerticalScroll("LogScroll",_rightBody.transform,out var logContent,2);
            logScroll.gameObject.AddComponent<LayoutElement>().preferredHeight=240;
            _eventText=UITheme.Label("log",logContent,"",11,TextAnchor.UpperLeft);
            _eventText.gameObject.AddComponent<LayoutElement>().preferredHeight=600;

            _rightMinBtn=min;min.onClick.AddListener(ToggleRightPanel);
            max.onClick.AddListener(()=>{_rightWide=!_rightWide;_rightRt.offsetMin=new Vector2(_rightWide?-392:-250,_rightCollapsed?-98:-1010);});
        }

        // ============================================================
        // 需求3：底部快速操作栏（速度 + 工具 + 按使用次数的常用按钮）
        // ============================================================
        private void BuildBottomBarV2(Transform parent)
        {
            var bar=UITheme.Surface("BottomBar",parent,new Color(0.97f,0.97f,0.94f,0.97f));
            var rt=bar.GetComponent<RectTransform>();
            rt.anchorMin=new Vector2(0,0);rt.anchorMax=new Vector2(1,0);rt.pivot=new Vector2(0.5f,0);
            // V6.3.2：全宽贴底、高58，内高44正好容纳46宽方钮
            rt.offsetMin=new Vector2(0,0);rt.offsetMax=new Vector2(0,58);
            var h=bar.AddComponent<HorizontalLayoutGroup>();h.spacing=6;h.padding=new RectOffset(12,12,7,7);
            h.childControlHeight=true;h.childControlWidth=true;h.childForceExpandWidth=false;h.childAlignment=TextAnchor.MiddleCenter;

            QuickIconBtn(bar.transform,"pause","暂停 / 继续",GM.TogglePause);
            QuickIconBtn(bar.transform,"minus","减速",SpeedDown);
            _speedText=Pill(bar.transform,"1x",new Color(0.05f,0.12f,0.16f,0.10f),60);_speedText.gameObject.AddComponent<LayoutElement>().preferredHeight=44;
            QuickIconBtn(bar.transform,"plus","加速",SpeedUp);
            BuildSpeedSlider(bar.transform);
            BarSep(bar.transform);
            ToolBtn(bar.transform,"select","select");
            ToolBtn(bar.transform,"build","build");
            ToolBtn(bar.transform,"tree","tree");
            ToolBtn(bar.transform,"npc","person");
            SetTool("select");
            BarSep(bar.transform);
            _quickBar=UITheme.Panel("QuickBar",bar.transform,new Color(0,0,0,0));
            var qh=_quickBar.AddComponent<HorizontalLayoutGroup>();qh.spacing=6;qh.childControlHeight=true;qh.childControlWidth=true;qh.childForceExpandWidth=false;qh.childAlignment=TextAnchor.MiddleLeft;
            _quickBar.AddComponent<LayoutElement>().flexibleWidth=1;
            // V6.3.2：去掉与 quickBar 重复的弹性 spacer，消除中段空隙，全屏钮固定最右
            QuickIconBtn(bar.transform,"fullscreen","全屏（F11）",()=>GM.ToggleFullscreen());
            RefreshQuickBar();
        }

        private void RecordUsage(string key){ _usage.TryGetValue(key,out var v); _usage[key]=v+1; RefreshQuickBar(); }

        private void RefreshQuickBar()
        {
            if(_quickBar==null)return;
            for(int i=_quickBar.transform.childCount-1;i>=0;i--)Destroy(_quickBar.transform.GetChild(i).gameObject);
            var ranked=_usage.Where(kv=>kv.Key.StartsWith("b:")||kv.Key.StartsWith("s:")||kv.Key.StartsWith("c:"))
                             .OrderByDescending(kv=>kv.Value).Take(4).ToList();
            foreach(var kv in ranked) QuickEntry(kv.Key);
            QuickFixed("军事",()=>{RecordUsage("mil");OpenCampaignModal();});
            QuickFixed("副本",()=>{RecordUsage("ocean");OpenOceanModal();});
        }
        private void QuickEntry(string key)
        {
            string kind=key.Substring(0,2),id=key.Substring(2);string nm,ik;
            if(kind=="b:"){ if(!GM.Buildings.TryGetValue(id,out var d))return; nm=d.Name;ik=BuildingIconKey(d); }
            else if(kind=="s:"){ if(!GM.Naval.Defs.TryGetValue(id,out var d))return; nm=d.Name;ik="ship"; }
            else { if(!GM.Cart.Defs.TryGetValue(id,out var d))return; nm=d.Name;ik="cart"; }
            var b=QuickIconBtn(_quickBar.transform,ik,nm,null);
            b.onClick.AddListener(()=>{ S.SelectedBuildType=(kind=="b:"?id:kind=="s:"?"ship:"+id:"cart:"+id);S.Tool="build";RecordUsage(key); });
        }
        private void QuickFixed(string label,Action act)
        {
            string ik=label=="军事"?"sword":"wave";
            string tip=label=="军事"?"军事：征兵 / 骑兵 / 讨伐":"副本：海洋大开发 / 太空探索";
            QuickIconBtn(_quickBar.transform,ik,tip,act);
        }

        /// <summary>底栏纯图标方钮：只显示图标并自适应填满，文字仅在鼠标悬停时浮出。</summary>
        private Button QuickIconBtn(Transform parent,string iconKey,string tip,Action act)
        {
            var b=UITheme.Btn("qi_"+iconKey,parent,"",12,UITheme.Chip);
            var le=b.GetComponent<LayoutElement>();le.preferredWidth=46;le.preferredHeight=44;le.minWidth=46;
            var txt=b.transform.Find("Text"); if(txt)Destroy(txt.gameObject);
            var ic=UITheme.Icon(b.transform,iconKey,12);
            ic.rectTransform.anchorMin=Vector2.zero;ic.rectTransform.anchorMax=Vector2.one;
            ic.rectTransform.offsetMin=new Vector2(11,10);ic.rectTransform.offsetMax=new Vector2(-11,-10);
            AddHover(b.gameObject,tip);
            if(act!=null)b.onClick.AddListener(()=>act());
            return b;
        }

        // ============================================================
        // 需求5：左下角浮动按钮组（存档/中心/Debug/帮助/声音）
        // ============================================================
        private void BuildLeftBottomCluster(Transform parent)
        {
            var cluster=UITheme.Panel("LeftBottom",parent,new Color(0,0,0,0));
            cluster.GetComponent<Image>().raycastTarget=false;
            var rt=cluster.GetComponent<RectTransform>();
            rt.anchorMin=new Vector2(0,0);rt.anchorMax=new Vector2(0,0);rt.pivot=new Vector2(0,0);
            rt.anchoredPosition=new Vector2(12,66);rt.sizeDelta=new Vector2(254,46);
            var h=cluster.AddComponent<HorizontalLayoutGroup>();h.spacing=6;h.childControlHeight=true;h.childControlWidth=true;h.childForceExpandWidth=false;
            ClusterBtn(cluster.transform,"save","存档读档",OpenSaveModal);
            ClusterBtn(cluster.transform,"target","中心视角",()=>{
                var rig=UnityEngine.Object.FindObjectOfType<World.CameraRig>();
                if(rig!=null){rig.CenterView();Toast("已回到中心视角");}else Toast("未找到视角相机",false);
            });
            ClusterBtn(cluster.transform,"setting","Debug",OnClickDebug);
            ClusterBtn(cluster.transform,"help","帮助",OpenHelp);
            ClusterBtn(cluster.transform,"sound","声音",ToggleSound);
        }
        private void ClusterBtn(Transform parent,string iconKey,string tip,Action act)
        {
            // V6.3.2：纯图标方钮（与底栏一致），删除空文字占位以严格居中
            var b=UITheme.Btn("cb_"+iconKey,parent,"",12,UITheme.Chip);
            var le=b.GetComponent<LayoutElement>();le.preferredWidth=46;le.preferredHeight=46;le.minWidth=46;
            var tx=b.transform.Find("Text"); if(tx)Destroy(tx.gameObject);
            var ic=UITheme.Icon(b.transform,iconKey,12);
            ic.rectTransform.anchorMin=Vector2.zero;ic.rectTransform.anchorMax=Vector2.one;
            ic.rectTransform.offsetMin=new Vector2(11,11);ic.rectTransform.offsetMax=new Vector2(-11,-11);
            UITheme.SetOutline(b.gameObject,UITheme.Gold,1);
            AddHover(b.gameObject,tip);
            b.onClick.AddListener(()=>act());
        }

        // ============================================================
        // 需求4：帮助界面（游戏说明 / Debug / 日志）
        // ============================================================
        private void BuildHelpModals(Transform parent)
        {
            _helpModal=MakeModal("HelpModal","游戏帮助 · 从像素到文明",out _,false);
            var hb=_helpModal.transform.Find("Box").GetComponent<RectTransform>();hb.sizeDelta=new Vector2(720,620);
            _logModal=MakeModal("LogModal","编年史 / 事件日志",out _,false);
            var lb=_logModal.transform.Find("Box").GetComponent<RectTransform>();lb.sizeDelta=new Vector2(680,620);
        }
        private void OpenHelp(){ FillHelp();_helpModal.SetActive(true); }
        private void FillHelp()
        {
            var hb=_helpModal.transform.Find("Box").GetComponent<RectTransform>();hb.sizeDelta=new Vector2(760,660);
            var body=ModalBody(_helpModal);Clear(body);
            var helpSr=UITheme.VerticalScroll("HelpScroll",body.transform,out var c,6);
            c.GetComponent<VerticalLayoutGroup>().childControlHeight=true;
            // 自适应高度的分组卡片（标题金色 + 正文）
            void Section(string e,string t)
            {
                // 固定行高（按字数预算行数），避免嵌套 ContentSizeFitter/动态 preferred 高度形成每帧布局反馈环导致卡帧
                int cpl=54; // 每行约容纳字符数（正文 12 号、内容宽约 700）
                int lines=0; foreach(var seg in t.Split('\n')){ lines+=Math.Max(1,Mathf.CeilToInt(seg.Length/(float)cpl)); }
                int bodyH=lines*18;
                int secH=18+22+4+bodyH;
                var row=UITheme.Surface("hs",c,UITheme.HexA(0x232e50,0.9f));
                var hrle=row.AddComponent<LayoutElement>();hrle.flexibleWidth=1;hrle.preferredHeight=secH;
                var vg=row.AddComponent<VerticalLayoutGroup>();vg.spacing=4;vg.padding=new RectOffset(12,12,9,9);
                vg.childControlWidth=true;vg.childForceExpandWidth=true;vg.childControlHeight=true;vg.childForceExpandHeight=false;vg.childAlignment=TextAnchor.UpperCenter;
                UITheme.Label("e",row.transform,e,15,TextAnchor.UpperLeft,UITheme.Gold,FontStyle.Bold).gameObject.AddComponent<LayoutElement>().preferredHeight=22;
                var tx=UITheme.Label("t",row.transform,t,12,TextAnchor.UpperLeft);
                tx.horizontalOverflow=HorizontalWrapMode.Wrap;
                tx.gameObject.AddComponent<LayoutElement>().preferredHeight=bodyH;
            }
            Section("游戏目标","从三皇五帝起步，历经 16 朝代、8 大时代发展到地球联盟；建设、科研、军事、航海、太空多线并进。九位 AI 神灵共治，允许饥荒、灾难、动乱与倒退，但保证文明 5000~10000 年不断绝。");
            Section("基本操作","左键：选择 / 放置建筑、种树、招民（先用底栏切换工具）；右键或 Esc：取消工具、关闭浮窗；拖拽：旋转 / 平移视角；滚轮：缩放。底栏从左到右为 暂停·减速·倍速·加速·速度滑条·选择·建造·种树·招民·常用·军事·副本·全屏，鼠标悬停图标显示说明。左下角为 存档·中心视角·Debug·帮助·声音。");
            Section("资源与建造","顶部为 粮食·木材·石头·金币·钢铁·青铜 与 经济/文化/科技 实力，悬停查看说明。左侧建造面板分 居住/农业/工业/经济/文化/军事/交通/科技/能源/太空 十类，按四级锚点定价（T1 15木 → T4 3000木500石200铁1000金），住房随等级提升。点世界中的建筑可看实拍内视图、寿命、耐久、居住人数，并可升级或拆除（拆除返还 50%）。");
            Section("军事与群雄争霸","可征兵、训练骑兵（需马厩）；箭塔/火塔/炮塔/碉堡自动索敌防御，骑兵克步兵、防御塔克骑兵。地图上随机 2~5 股割据势力，每 20 游戏年相互攻伐兼并，进入分裂期（3~7 国）或大一统王朝；可出师讨伐、兼并势力获得金粮与人口。");
            Section("海洋 · 殖民 · 太空","公元 1000 年大航海时代开启，此前各大陆被海洋隔绝、无法跨洋作战；之后可造风帆战舰、建立 贸易站→殖民地→领地 三级海外领地并获得上贡。海洋 / 太空为 9×9 迷雾探索副本，逐格探索、获取资源、建港口与月球/火星前哨，补给耗尽自动返航。");
            Section("自然系统","月度潮汐（1-15 涨潮、16-30 退潮）；雨/雪/晴/多云/雾/晚霞/雷电/龙卷风天气；洋流与海风为帆船提供动力、引导鱼群洄游。地图每 100 年自然延展 10%、每 1000 年翻倍；植被分乔木/灌木/草本/地被四层，村落大榕树随年代生长，鸟群鱼群按 LOD 按需渲染。");
            Section("加速冷冻","加速累计推进满 100 游戏年后，强制进入 300 现实秒冷冻冷却，期间倍速封顶 10；收到解冻指令后重新累计。画面中顶显示倒计时。");
            Section("快捷键 / 存档","空格 暂停，+/- 调整倍速，F11 或 Alt+Enter 全屏；游戏每 5 分钟自动存档，也可在左下角手动存/读 5 个手动槽、导出导入 JSON 跨设备迁移。");
            var row=UITheme.Panel("hb",c,new Color(0,0,0,0));row.AddComponent<LayoutElement>().preferredHeight=40;
            var hg=row.AddComponent<HorizontalLayoutGroup>();hg.spacing=8;hg.childForceExpandWidth=true;
            UITheme.Btn("debug",row.transform,"Debug 高级解锁",12).onClick.AddListener(()=>{_helpModal.SetActive(false);OnClickDebug();});
            UITheme.Btn("save",row.transform,"存档管理",12).onClick.AddListener(()=>{_helpModal.SetActive(false);OpenSaveModal();});
            UITheme.Btn("log",row.transform,"Log 日志显示",12).onClick.AddListener(()=>{_helpModal.SetActive(false);OpenLog();});
            UITheme.Btn("close",row.transform,"关闭",12).onClick.AddListener(()=>_helpModal.SetActive(false));
        }
        private void OpenLog(){ FillLog();_logModal.SetActive(true); }
        private void FillLog()
        {
            var body=ModalBody(_logModal);Clear(body);
            var scroll=UITheme.VerticalScroll("LogScroll",body.transform,out var c,2);
            var sb=new StringBuilder();int n=0;
            for(int i=S.EventLog.Count-1;i>=0;i--){var e=S.EventLog[i];sb.Append('[').Append(e.Year).Append("年] ").Append(e.Text).Append('\n');if(++n>=200)break;}
            UITheme.Label("all",c,sb.Length==0?"（暂无事件）":sb.ToString(),12,TextAnchor.UpperLeft);
        }

        // ============================================================
        // 需求7：世界建筑头顶浮标（圆形金边 emoji 徽标）
        // ============================================================
        private void BuildMarkerLayer(Transform parent)
        {
            var go=UITheme.Panel("BuildingMarkers",parent,new Color(0,0,0,0));
            go.GetComponent<Image>().raycastTarget=false;
            _markerLayer=go.GetComponent<RectTransform>();Stretch(go);
        }
        private Sprite BadgeSprite()
        {
            if(_badgeSprite!=null)return _badgeSprite;
            const int R=48;var tex=new Texture2D(R,R,TextureFormat.RGBA32,false);
            var px=new Color32[R*R];for(int i=0;i<px.Length;i++)px[i]=new Color32(0,0,0,0);
            var dark=new Color32(250,250,246,236);var gold=new Color32(255,138,30,255); // V7.0.1 奶白徽标+橙环
            for(int y=0;y<R;y++)for(int x=0;x<R;x++){int dx=x-24,dy=y-24;int q=dx*dx+dy*dy;
                if(q<=21*21)px[y*R+x]=dark; else if(q<=23*23)px[y*R+x]=gold;}
            tex.SetPixels32(px);tex.Apply(true,false);
            _badgeSprite=Sprite.Create(tex,new Rect(0,0,R,R),new Vector2(0.5f,0.5f),R);
            return _badgeSprite;
        }
        private GameObject MakeMarker(BuildingEntity b)
        {
            var m=UITheme.Panel("mk_"+b.Type,_markerLayer,new Color(1,1,1,1));
            var img=m.GetComponent<Image>();img.sprite=BadgeSprite();img.raycastTarget=false;img.SetNativeSize();
            var rt=m.GetComponent<RectTransform>();rt.sizeDelta=new Vector2(34,34);
            var ic=UITheme.Icon(m.transform,BuildingIconKey(b.Def),22);ic.raycastTarget=false;
            var irt=ic.rectTransform;irt.anchorMin=Vector2.zero;irt.anchorMax=Vector2.one;irt.offsetMin=new Vector2(6,6);irt.offsetMax=new Vector2(-6,-6);
            _markers[b]=m;return m;
        }
        private void RefreshMarkers(float dt)
        {
            if(_markerLayer==null||S==null)return;
            _markerTimer-=dt;if(_markerTimer>0)return;_markerTimer=0.08f;
            bool home=string.IsNullOrEmpty(S.CurrentMap)||S.CurrentMap=="home";
            // 清理已销毁建筑的浮标
            var dead=_markers.Keys.Where(b=>b.View==null||!S.Buildings.Contains(b)).ToList();
            foreach(var b in dead){if(_markers[b])Destroy(_markers[b]);_markers.Remove(b);}
            if(!home||Camera.main==null){_markerLayer.gameObject.SetActive(false);return;}
            _markerLayer.gameObject.SetActive(true);
            foreach(var b in S.Buildings)
            {
                if(b.View==null)continue;
                try
                {
                    if(!_markers.TryGetValue(b,out var m))m=MakeMarker(b);
                    Vector3 basePos=b.View!=null?b.View.transform.position:new Vector3(b.X,0,b.Z);
                    // V6.2.2 浮标贴近建筑顶部（旧 +6.5 过高、离建筑太远）
                    Vector3 wp=new(basePos.x,basePos.y+3.0f,basePos.z);
                    // 仅在一屏视野内、且相机距离足够近时显示，远距/屏外隐藏
                    float dc=Vector3.Distance(Camera.main.transform.position,basePos);
                    Vector3 vp=Camera.main.WorldToViewportPoint(wp);
                    bool onScreen=vp.z>0f&&vp.x>-0.03f&&vp.x<1.03f&&vp.y>-0.03f&&vp.y<1.03f;
                    if(dc>120f||!onScreen){m.SetActive(false);continue;}
                    Vector2 sp=RectTransformUtility.WorldToScreenPoint(Camera.main,wp);
                    bool behind=Vector3.Dot((wp-Camera.main.transform.position).normalized,Camera.main.transform.forward)<=0;
                    if(behind){m.SetActive(false);continue;}
                    if(RectTransformUtility.ScreenPointToLocalPointInRectangle(_markerLayer,sp,null,out var lp))
                    {m.SetActive(true);m.GetComponent<RectTransform>().anchoredPosition=lp;}
                    else m.SetActive(false);
                }
                catch(System.Exception me){ Debug.LogWarning("[MARKER] "+b.Type+" "+me.Message); }
            }
        }
    }

    internal static class UiLabelExtV2
    {
        public static Text SetInsetV(this Text t,float left,float top)
        {
            t.rectTransform.offsetMin=new Vector2(left,2);t.rectTransform.offsetMax=new Vector2(-10,-top);return t;
        }
    }
}
