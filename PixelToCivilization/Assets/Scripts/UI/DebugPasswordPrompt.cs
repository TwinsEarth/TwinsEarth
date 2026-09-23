using System;
using UnityEngine;
using UnityEngine.UI;
using PixelToCivilization.Core;

namespace PixelToCivilization.UI
{
    /// <summary>Debug密码门：输入 ToFuture 解锁1000倍速与全部调试功能</summary>
    public class DebugPasswordPrompt : MonoBehaviour
    {
        private GameObject _root;
        private InputField _input;

        // V6.3.3：parent 必须传 UICanvas 下的活动节点（HUD）。UIManager 挂在独立 UIRoot、其 transform 在 Canvas 之外，
        // 旧版直接 parent 到 transform 会因没有 Canvas 而完全不渲染，表现为“点 Debug 没反应”。
        public void Show(GameManager gm, Action onSuccess, Transform parent=null)
        {
            Transform p = parent!=null ? parent : transform;
            _root=UITheme.Panel("DebugPrompt",p,UITheme.HexA(0x000000,0.6f));
            _root.transform.SetAsLastSibling();
            var rt=_root.GetComponent<RectTransform>();rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;
            var box=UITheme.Panel("Box",_root.transform,new Color(0.985f,0.975f,0.935f,1f));
            UITheme.SetOutline(box,UITheme.Gold,1);
            var brt=box.GetComponent<RectTransform>();
            brt.anchorMin=brt.anchorMax=new Vector2(0.5f,0.5f);brt.sizeDelta=new Vector2(380,230);
            var vl=box.AddComponent<VerticalLayoutGroup>();vl.spacing=12;vl.padding=new RectOffset(20,20,20,20);
            vl.childControlWidth=true;vl.childForceExpandWidth=true;vl.childControlHeight=true;vl.childForceExpandHeight=false;
            var t1=UITheme.Label("t",box.transform,"请输入 Debug 密码",20,TextAnchor.MiddleCenter,UITheme.Gold);
            t1.gameObject.AddComponent<LayoutElement>().preferredHeight=34;
            var fieldGo=UITheme.Panel("Input",box.transform,new Color(0.93f,0.96f,0.98f,1f)); // V7.0.1
            UITheme.SetOutline(fieldGo,UITheme.Gold,1);
            fieldGo.AddComponent<LayoutElement>().preferredHeight=46;
            var input=fieldGo.AddComponent<InputField>();
            var ph=UITheme.Label("ph",fieldGo.transform,"请输入密码…",15,TextAnchor.MiddleLeft,UITheme.HexA(0x9aa3c0,1));
            ph.rectTransform.anchorMin=Vector2.zero;ph.rectTransform.anchorMax=Vector2.one;
            ph.rectTransform.offsetMin=new Vector2(12,2);ph.rectTransform.offsetMax=new Vector2(-12,-2);
            var txt=UITheme.Label("txt",fieldGo.transform,"",15,TextAnchor.MiddleLeft,UITheme.Text);
            txt.rectTransform.anchorMin=Vector2.zero;txt.rectTransform.anchorMax=Vector2.one;
            txt.rectTransform.offsetMin=new Vector2(12,2);txt.rectTransform.offsetMax=new Vector2(-12,-2);
            input.targetGraphic=fieldGo.GetComponent<Image>();
            input.textComponent=txt;input.placeholder=ph;input.contentType=InputField.ContentType.Password;
            input.caretColor=Color.white;input.caretWidth=3;input.selectionColor=UITheme.HexA(0xf3d061,0.35f);
            input.interactable=true;
            _input=input;
            var row=UITheme.Panel("Row",box.transform,new Color(0,0,0,0));
            row.AddComponent<LayoutElement>().preferredHeight=42;
            var h=row.AddComponent<HorizontalLayoutGroup>();h.spacing=10;h.childControlWidth=true;h.childForceExpandWidth=true;h.childControlHeight=true;
            var ok=UITheme.Btn("ok",row.transform,"解锁",14,UITheme.HexA(0xFFD700,0.25f));
            var cancel=UITheme.Btn("cancel",row.transform,"取消",14);
            ok.onClick.AddListener(()=>{
                if (gm.TryUnlockDebug(_input.text)){Destroy(_root);Destroy(this);onSuccess?.Invoke();}
                else { _input.text=""; }
            });
            cancel.onClick.AddListener(()=>{Destroy(_root);Destroy(this);});
            input.ActivateInputField();
        }
    }
}
