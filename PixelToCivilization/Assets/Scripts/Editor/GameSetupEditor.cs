#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using PixelToCivilization.Bootstrap;

namespace PixelToCivilization.EditorTools
{
    /// <summary>编辑器一键搭建：菜单「像素到文明 → 一键搭建场景」</summary>
    public static class GameSetupEditor
    {
        [MenuItem("像素到文明/① 一键搭建场景")]
        public static void SetupScene()
        {
            var existing=Object.FindObjectOfType<GameBootstrap>();
            if (existing!=null)
            {
                EditorUtility.DisplayDialog("已搭建","场景中已存在 GameBootstrap，直接点击 Play 即可开始。","好的");
                Selection.activeGameObject=existing.gameObject;
                return;
            }
            var go=new GameObject("=== 从像素到文明 v5.9.9 ===");
            go.AddComponent<GameBootstrap>();
            Selection.activeGameObject=go;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Setup] 已创建启动器。点击 Play 运行，或 Ctrl+S 保存场景。");
            EditorUtility.DisplayDialog("搭建完成",
                "已创建游戏启动器。\n\n• 点击 ▶ Play 即可从开始页进入游戏\n• 建议 Ctrl+S 将场景保存为 MainScene\n• 全部地形/相机/UI/系统均由代码运行时生成，无需手动拖拽",
                "开始");
        }

        [MenuItem("像素到文明/② 清空场景中的运行时对象")]
        public static void CleanRuntime()
        {
            foreach (var n in new[]{"GameManager","World","BuildingMeshFactory","MainCamera","UIRoot","=== 从像素到文明 v5.9.9 ==="})
            {
                var g=GameObject.Find(n);
                if(g!=null)Object.DestroyImmediate(g);
            }
            Debug.Log("[Setup] 已清理运行时对象");
        }
    }
}
#endif
