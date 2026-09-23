using System.Runtime.InteropServices;
using UnityEngine;

namespace PixelToCivilization.Platform
{
    /// <summary>跨端文件下载：WebGL 走浏览器 Blob 下载，编辑器/其他端回退剪贴板。</summary>
    public static class WebFile
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void PxDownloadFile(string name, string text);
#endif
        public static void DownloadJson(string fileName, string jsonText)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            PxDownloadFile(fileName, jsonText);
#else
            GUIUtility.systemCopyBuffer = jsonText;
#endif
        }
    }
}
