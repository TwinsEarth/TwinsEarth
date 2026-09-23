using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace PixelToCivilization.UI
{
    /// <summary>UI主题 V6.1.9(j)：对齐 7 张参考图的「极简海军蓝 + 金」扁平风——
    /// 圆角 Surface/Card/Chip、彩色矢量图标、金色标题、浅色正文。保留全部既有工厂签名。</summary>
    public static class UITheme
    {
        public static readonly Color Bg = new(0.07f,0.45f,0.80f,1f);             // V7.0.1 明亮海蓝
        // 深海军蓝面板（参考图取色 #161d33 一带）
        public static readonly Color PanelBg = new(0.985f,0.975f,0.935f,0.97f);  // 奶白面板
        public static readonly Color CardBg = new(1f,1f,1f,0.96f);              // 白卡片
        public static readonly Color Chip = new(0.875f,0.925f,0.935f,1f);        // 浅青灰
        public static readonly Color ChipActive = Hex(0xff8a1e);                  // 活力橙
        public static readonly Color ChipActiveText = Hex(0xffffff);
        public static readonly Color Gold = new(0.92f,0.62f,0.08f,1f);                        // 金币橙黄
        public static readonly Color Text = Hex(0x21303f);                        // 墨蓝灰正文
        public static readonly Color Sub = Hex(0x5d6b78);
        public static readonly Color Sky = Hex(0x27aef0);
        public static readonly Color Good = Hex(0x2fbf63);
        public static readonly Color Bad = Hex(0xef4a45);
        public static readonly Color Bronze = Hex(0x9a5f2c);
        public static readonly Color BronzeLight = Hex(0xd08a45);
        public static readonly Color BtnGold = new(1f,0.54f,0.12f,0.97f);         // 橙色主按钮
        public static readonly Color PanelBorder = new(0.10f,0.60f,0.64f,0.30f);  // 青描边
        private static Font _font;
        public static Font Font
        {
            get
            {
                if (_font != null) return _font;
                _font = Resources.Load<Font>("Fonts/SimHei");
                if (_font == null)
                    _font = Font.CreateDynamicFontFromOSFont(
                        new[]{"Microsoft YaHei","PingFang SC","SimHei","Segoe UI Emoji","Segoe UI Symbol","Arial Unicode MS"},14);
                return _font;
            }
        }

        public static Color Hex(int h)=>new(((h>>16)&255)/255f,((h>>8)&255)/255f,(h&255)/255f);
        public static Color HexA(int h,float a){var c=Hex(h);c.a=a;return c;}

        // —— 运行时圆角九宫 Sprite（极简风的关键：所有卡片/按钮统一圆角）——
        private static Sprite _round;
        public static Sprite RoundSprite
        {
            get
            {
                if(_round!=null)return _round;
                const int S=40,B=16,R=13;
                var tex=new Texture2D(S,S,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear};
                var px=new Color32[S*S];var w=new Color32(255,255,255,255);var z=new Color32(0,0,0,0);
                for(int y=0;y<S;y++)for(int x=0;x<S;x++)px[y*S+x]=InsideRound(x,y,S,R)?w:z;
                tex.SetPixels32(px);tex.Apply(true,false);
                _round=Sprite.Create(tex,new Rect(0,0,S,S),new Vector2(0.5f,0.5f),S,0,SpriteMeshType.FullRect,new Vector4(B,B,B,B));
                return _round;
            }
        }
        private static bool InsideRound(int x,int y,int S,int r)
        {
            int cx=Mathf.Clamp(x,r,S-1-r),cy=Mathf.Clamp(y,r,S-1-r);
            int dx=x-cx,dy=y-cy;return dx*dx+dy*dy<=r*r;
        }
        /// <summary>把一个 Image 变为圆角（九宫切片，保证任意尺寸不糊）</summary>
        public static Image Round(Image img){ if(img==null)return null; img.sprite=RoundSprite;img.type=Image.Type.Sliced;img.fillCenter=true;return img; }

        public static RectTransform EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>()==null)
            {
                var es=new GameObject("EventSystem");
                es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>();
            }
            return null;
        }

        public static Canvas CreateCanvas(string name)
        {
            EnsureEventSystem();
            var go=new GameObject(name);
            var canvas=go.AddComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var sc=go.AddComponent<CanvasScaler>();
            sc.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution=new Vector2(1920,1080);
            sc.matchWidthOrHeight=0.6f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>直角色块（用于透明布局容器、进度填充、滚动视口等不应圆角处）</summary>
        public static GameObject Panel(string name,Transform parent,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
            go.transform.SetParent(parent,false);
            go.GetComponent<Image>().color=color;
            return go;
        }

        /// <summary>圆角 Surface（主面板/卡片容器）</summary>
        public static GameObject Surface(string name,Transform parent,Color? fill=null)
        {
            var go=Panel(name,parent,fill??PanelBg);
            Round(go.GetComponent<Image>());
            return go;
        }

        public static RectTransform Anchored(RectTransform rt,Vector2 anchorMin,Vector2 anchorMax,
            Vector2 offsetMin,Vector2 offsetMax)
        {
            rt.anchorMin=anchorMin;rt.anchorMax=anchorMax;rt.offsetMin=offsetMin;rt.offsetMax=offsetMax;
            return rt;
        }

        public static Text Label(string name,Transform parent,string content,int size=14,
            TextAnchor anchor=TextAnchor.MiddleLeft,Color? color=null,FontStyle style=FontStyle.Normal)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));
            go.transform.SetParent(parent,false);
            var t=go.GetComponent<Text>();
            t.text=content;t.font=Font;t.fontSize=size+2;t.fontStyle=style;t.alignment=anchor;
            t.color=color??Text;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Overflow;
            t.raycastTarget=false;
            return t;
        }

        public static Button Btn(string name,Transform parent,string content,int size=13,Color? bg=null)
        {
            var go=Panel(name,parent,bg??Chip);
            Round(go.GetComponent<Image>());
            var btn=go.AddComponent<Button>();
            var colors=btn.colors;
            colors.highlightedColor=new Color(1,1,1,0.14f);colors.pressedColor=new Color(1f,0.54f,0.12f,0.80f);  // V7.0.1
            colors.fadeDuration=0.08f; btn.colors=colors;
            var le=go.AddComponent<LayoutElement>(); le.preferredHeight=32;le.preferredWidth=84;
            var txt=Label("Text",go.transform,content,size,TextAnchor.MiddleCenter,Text,FontStyle.Bold);
            txt.rectTransform.anchorMin=Vector2.zero;txt.rectTransform.anchorMax=Vector2.one;
            txt.rectTransform.offsetMin=Vector2.zero;txt.rectTransform.offsetMax=Vector2.zero;
            return btn;
        }

        /// <summary>矢量图标+文字按钮（圆角）</summary>
        public static Button BtnIcon(string name,Transform parent,string iconKey,string content,int size=13,Color? bg=null)
        {
            var go=Panel(name,parent,bg??Chip);
            Round(go.GetComponent<Image>());
            var btn=go.AddComponent<Button>();
            var colors=btn.colors;
            colors.highlightedColor=new Color(1,1,1,0.14f);colors.pressedColor=new Color(1f,0.54f,0.12f,0.80f);  // V7.0.1
            colors.fadeDuration=0.08f;btn.colors=colors;
            var le=go.AddComponent<LayoutElement>();le.preferredHeight=36;le.preferredWidth=Mathf.Max(96,content.Length*(size+2)+(size+6)*2+24);
            var h=go.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment=TextAnchor.MiddleCenter;h.spacing=5;h.padding=new RectOffset(6,8,2,2);
            h.childControlWidth=true;h.childControlHeight=true;h.childForceExpandWidth=false;h.childForceExpandHeight=true;
            Icon(go.transform,iconKey,size+6);
            var txt=Label("Text",go.transform,content,size,TextAnchor.MiddleCenter,Text,FontStyle.Bold);
            var tle=txt.gameObject.AddComponent<LayoutElement>();tle.preferredWidth=content.Length*size+8;tle.flexibleWidth=1;
            return btn;
        }

        public static ScrollRect VerticalScroll(string name,Transform parent,out RectTransform content,float spacing=4)
        {
            var view=Panel(name,parent,new Color(0.05f,0.12f,0.16f,0.08f));  // V7.0.1
            // V6.3.3：滚动视图始终铺满父容器（父级无布局组时裸 Panel 会塌成100x100居中，导致帮助/讨伐等弹窗内容挤成窄条）
            var vrt0=view.GetComponent<RectTransform>();
            vrt0.anchorMin=Vector2.zero;vrt0.anchorMax=Vector2.one;vrt0.pivot=new Vector2(0.5f,0.5f);
            vrt0.offsetMin=Vector2.zero;vrt0.offsetMax=Vector2.zero;
            view.AddComponent<RectMask2D>();
            var sr=view.AddComponent<ScrollRect>();sr.horizontal=false;
            var cGo=Panel("Content",view.transform,new Color(0,0,0,0));
            content=cGo.GetComponent<RectTransform>();
            content.anchorMin=new Vector2(0,1);content.anchorMax=new Vector2(1,1);content.pivot=new Vector2(0.5f,1);
            var vlg=cGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing=spacing;vlg.childAlignment=TextAnchor.UpperCenter;vlg.padding=new RectOffset(2,10,2,2);
            vlg.childControlWidth=true;vlg.childControlHeight=false;
            vlg.childForceExpandWidth=true;vlg.childForceExpandHeight=false;
            var f=cGo.AddComponent<ContentSizeFitter>();f.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            sr.content=content;sr.viewport=view.GetComponent<RectTransform>();sr.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
            var sbGo=Panel("Scrollbar",view.transform,new Color(0,0,0,0));
            var sb=sbGo.AddComponent<Scrollbar>();
            var sbrt=sbGo.GetComponent<RectTransform>();
            sbrt.anchorMin=new Vector2(1,0);sbrt.anchorMax=new Vector2(1,1);sbrt.pivot=new Vector2(1,0.5f);
            sbrt.sizeDelta=new Vector2(8,0);sbrt.anchoredPosition=Vector2.zero;
            sb.direction=Scrollbar.Direction.BottomToTop;
            var slide=Panel("Sliding Area",sbGo.transform,new Color(0,0,0,0));
            var slrt=slide.GetComponent<RectTransform>();slrt.anchorMin=Vector2.zero;slrt.anchorMax=Vector2.one;
            slrt.offsetMin=Vector2.zero;slrt.offsetMax=Vector2.zero;
            var handle=Panel("Handle",slide.transform,new Color(1f,0.55f,0.15f,0.65f));
            var hrt=handle.GetComponent<RectTransform>();hrt.anchorMin=Vector2.zero;hrt.anchorMax=Vector2.one;
            hrt.offsetMin=Vector2.zero;hrt.offsetMax=Vector2.zero;
            sb.handleRect=hrt; sb.targetGraphic=handle.GetComponent<Image>();
            sr.verticalScrollbar=sb;
            return sr;
        }

        public static void SetOutline(GameObject go,Color c,int width=1)
        {
            var o=go.AddComponent<Outline>();o.effectColor=new Color(c.r,c.g,c.b,0.30f);o.effectDistance=new Vector2(width,-width);
        }

        /// <summary>程序化彩色矢量图标（IconFactory 已按语义上品牌色）</summary>
        public static Image Icon(Transform parent,string iconKey,float size=24f)
        {
            var go=new GameObject("Icon_"+iconKey,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
            go.transform.SetParent(parent,false);
            var img=go.GetComponent<Image>();
            img.sprite=IconFactory.Get(iconKey);img.raycastTarget=false;img.preserveAspect=true;
            // V6.2.2：所有图标统一放大一倍
            float s=size*2f;
            var le=go.AddComponent<LayoutElement>();le.preferredWidth=s;le.preferredHeight=s;le.minWidth=s;le.minHeight=s;
            return img;
        }

        public static RawImage Portrait(Transform parent,Texture2D tex,float height=132f)
        {
            if(tex==null) return null;
            var card=Surface("PortraitCard",parent,new Color(0,0,0,0.06f));
            var le0=card.AddComponent<LayoutElement>();le0.preferredHeight=height;le0.flexibleWidth=1;
            var go=new GameObject("Portrait",typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage));
            go.transform.SetParent(card.transform,false);
            var ri=go.GetComponent<RawImage>();ri.texture=tex;ri.raycastTarget=false;
            var rt=ri.rectTransform;rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;
            rt.offsetMin=new Vector2(4,4);rt.offsetMax=new Vector2(-4,-4);
            var outline=card.AddComponent<Outline>();outline.effectColor=new Color(Gold.r,Gold.g,Gold.b,0.35f);outline.effectDistance=new Vector2(1,-1);
            return ri;
        }

        /// <summary>玻璃拟态圆角面板：海军蓝底 + 金色细描边 + 顶部高光</summary>
        public static GameObject Glass(string name,Transform parent,Color? fill=null)
        {
            var go=Surface(name,parent,fill??new Color(0.99f,0.98f,0.95f,0.86f));  // V7.0.1 奶白玻璃
            var outline=go.AddComponent<Outline>();
            outline.effectColor=new Color(Gold.r,Gold.g,Gold.b,0.26f);outline.effectDistance=new Vector2(1,-1);
            var hl=Panel("TopHighlight",go.transform,new Color(1f,1f,1f,0.55f));
            var rt=hl.GetComponent<RectTransform>();
            rt.anchorMin=new Vector2(0,1);rt.anchorMax=new Vector2(1,1);rt.pivot=new Vector2(0.5f,1);
            rt.offsetMin=new Vector2(4,-3f);rt.offsetMax=new Vector2(-4,-1f);
            return go;
        }

        public static RectTransform TitleBar(Transform parent,float height=4f)
        {
           var bar=Panel("TitleBar",parent,new Color(ChipActive.r,ChipActive.g,ChipActive.b,0.95f));  // V7.0.1
           var rt=bar.GetComponent<RectTransform>();
           var le=bar.AddComponent<LayoutElement>();le.preferredHeight=height;le.flexibleWidth=1;
           return rt;
        }
    }
}
