using UnityEngine;

namespace PixelToCivilization.UI
{
    /// <summary>悬浮提示载体：挂在任何需要悬浮说明的 UI 对象上，由 UIManager 每帧射线检测统一驱动（V6.3.4：替代不稳定的 EventTrigger PointerEnter）。</summary>
    public class HoverTip : MonoBehaviour
    {
        public string Tip;
        public void Set(string t){ Tip=t; }
    }
}
