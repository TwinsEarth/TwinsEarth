#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using URP = UnityEngine.Rendering.Universal;

namespace PixelToCivilization.EditorTools
{
    /// <summary>
    /// V6.1.1 美术大版本升级 —— URP 高清管线一键安装器（幂等）。
    /// 编辑器加载/批处理编译时自动执行：生成 ForwardRenderer + URP 高清资产并写入 Graphics/Quality 设置，
    /// 同时把颜色空间切到 Linear（PBR 必需）。资产持久化到 Assets/Settings，可随包构建。
    /// 菜单：像素到文明 → 美术升级 → 安装/修复 URP 高清管线
    /// </summary>
    [InitializeOnLoad]
    public static class UrpPipelineInstaller
    {
        const string SettingsDir = "Assets/Settings";
        const string RendererPath = SettingsDir + "/PxC_ForwardRenderer.asset";
        const string AssetPath = SettingsDir + "/PxC_URP_High.asset";
        const string MenuRoot = "像素到文明/美术升级/";
        const string PkgPostProcess = "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";

        static UrpPipelineInstaller()
        {
            EditorApplication.delayCall += Ensure;
        }

        [MenuItem(MenuRoot + "安装/修复 URP 高清管线", priority = 0)]
        public static void Ensure()
        {
            try
            {
                if (PlayerSettings.colorSpace != ColorSpace.Linear)
                {
                    PlayerSettings.colorSpace = ColorSpace.Linear;
                    Debug.Log("[URP安装] 颜色空间切换为 Linear");
                }

                if (!Directory.Exists(SettingsDir)) Directory.CreateDirectory(SettingsDir);

                var urp = AssetDatabase.LoadAssetAtPath<URP.UniversalRenderPipelineAsset>(AssetPath);
                if (urp == null)
                {
                    // 1) Forward Renderer + 默认后处理资源（internal API，按官方包路径直接加载）
                    var renderer = AssetDatabase.LoadAssetAtPath<URP.UniversalRendererData>(RendererPath);
                    if (renderer == null)
                    {
                        renderer = ScriptableObject.CreateInstance<URP.UniversalRendererData>();
                        var ppd = AssetDatabase.LoadAssetAtPath<URP.PostProcessData>(PkgPostProcess);
                        renderer.postProcessData = ppd;
                        AssetDatabase.CreateAsset(renderer, RendererPath);
                        Debug.Log("[URP安装] ForwardRenderer 已生成，PostProcessData=" + (ppd != null));
                    }

                    // 2) URP 资产（官方创建路径，内部自动 ResourceReloader）
                    urp = URP.UniversalRenderPipelineAsset.Create(renderer);
                    ConfigureHigh(urp);
                    AssetDatabase.CreateAsset(urp, AssetPath);
                    Debug.Log("[URP安装] 已生成 URP 高清资产: " + AssetPath);
                }
                else
                {
                    ConfigureHigh(urp);
                }

                // 3) 绑定到全局图形设置与当前质量等级
                if (GraphicsSettings.defaultRenderPipeline != urp)
                    GraphicsSettings.defaultRenderPipeline = urp;
                QualitySettings.renderPipeline = urp;

                EditorUtility.SetDirty(urp);
                AssetDatabase.SaveAssets();
                Debug.Log("[URP安装] 完成，URP 已绑定 GraphicsSettings/QualitySettings");
            }
            catch (Exception e)
            {
                Debug.LogError("[URP安装] 失败：" + e);
            }
        }

        /// <summary>准 3A 取向的高清参数（移动端可在质量设置里降档）</summary>
        static void ConfigureHigh(URP.UniversalRenderPipelineAsset a)
        {
            // —— public 可直接赋值的项 ——
            a.renderScale = 1.0f;
            a.supportsHDR = true;
            a.hdrColorBufferPrecision = URP.HDRColorBufferPrecision._64Bits; // R11G11B10，移动端友好的 HDR
            a.msaaSampleCount = 4;
            a.useSRPBatcher = true;
            a.supportsDynamicBatching = true;

            a.shadowDistance = 620f;
            a.shadowCascadeCount = 4;
            a.maxAdditionalLightsCount = 8;

            a.supportsCameraDepthTexture = true;
            a.supportsCameraOpaqueTexture = true;

            // —— internal set / 只读项：反射写序列化字段 ——
            SetPriv(a, "m_EnableLODCrossFade", true);
            SetPriv(a, "m_MainLightShadowsSupported", true);
            SetPriv(a, "m_MainLightShadowmapResolution", URP.ShadowResolution._2048);
            SetPriv(a, "m_AdditionalLightShadowsSupported", true);
            SetPriv(a, "m_AdditionalLightsShadowmapResolution", URP.ShadowResolution._1024);
            SetPriv(a, "m_SoftShadowsSupported", true);
        }

        static void SetPriv<T>(object obj, string field, T value)
        {
            var fi = obj.GetType().GetField(field,
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            if (fi != null) fi.SetValue(obj, value);
            else Debug.LogWarning("[URP安装] 找不到字段 " + field);
        }
    }
}
#endif
