#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PixelToCivilization.EditorTools
{
    /// <summary>命令行 / 菜单构建：打包 Standalone Windows64 单机版客户端</summary>
    public static class BuildScript
    {
        const string ScenePath = "Assets/Scenes/MainScene.unity";

        [MenuItem("像素到文明/③ 打包单机版客户端")]
        public static void BuildStandalone()
        {
            string[] scenes = { ScenePath };

            // 输出目录：工程根目录 / Build
            string projRoot = Path.GetDirectoryName(Application.dataPath);
            string outDir = Path.Combine(projRoot, "Build");
            string exeName = "从像素到文明.exe";
            string outPath = Path.Combine(outDir, exeName);

            if (Directory.Exists(outDir))
                Directory.Delete(outDir, true);
            Directory.CreateDirectory(outDir);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[Build] 构建成功！输出路径: {outPath}");
                Debug.Log($"[Build] 总大小: {summary.totalSize / 1024 / 1024} MB");
                Console.WriteLine($"BUILD_SUCCESS:{outPath}");
            }
            else
            {
                Debug.LogError($"[Build] 构建失败: {summary.result}");
                Console.WriteLine($"BUILD_FAILED:{summary.result}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>供命令行 -executeMethod 调用</summary>
        public static void BuildStandaloneCLI()
        {
            BuildStandalone();
            EditorApplication.Exit(0);
        }
    }
}
#endif
