#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEditor.Build;

namespace PixelToCivilization.EditorTools
{
    /// <summary>
    /// V6.7.1 HTML5(WebGL) 一键构建：自动切换 WebGL/IL2CPP、为浏览器本地静态服务器做兼容配置
    /// （关闭多线程与压缩，避免 COOP/COEP 与 Content-Encoding 问题）、默认横屏全屏，
    /// 构建后生成中文全屏加载页 index.html。
    /// 命令行：Tuanjie.exe -batchmode -quit -executeMethod PixelToCivilization.EditorTools.WebGLBuilder.BuildCLI
    /// </summary>
    public static class WebGLBuilder
    {
        const string ScenePath = "Assets/Scenes/MainScene.unity";

        [MenuItem("像素到文明/④ 打包 HTML5 网页版(WebGL)")]
        public static void Build()
        {
            // 1) 切换到 WebGL 平台（缺模块时这里会返回 false 并给出明确提示）
            bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            if (!switched)
                throw new Exception("切换 WebGL 平台失败：请先在 Tuanjie Hub 为本编辑器安装「WebGL Build Support」模块。");

            ConfigurePlayerSettings();

            string projRoot = Path.GetDirectoryName(Application.dataPath);
            string outDir = Path.Combine(projRoot, "BuildWebGL");
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            Directory.CreateDirectory(outDir);

            var options = new BuildPlayerOptions
            {
                scenes = new[]{ ScenePath },
                // WebGL 的 locationPathName 必须是“输出目录”（Unity 会在其中生成 index.html/Build/TemplateData），
                // 若写成 .../index.html 会被当成名为 index.html 的子目录。
                locationPathName = outDir,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Console.WriteLine("BUILD_FAILED:" + report.summary.result);
                EditorApplication.Exit(1);
                return;
            }

            // 2) 用自定义中文全屏加载页覆盖默认 index.html
            WriteCustomIndex(outDir);

            Console.WriteLine("BUILD_SUCCESS:" + outDir);
            Debug.Log("[WebGL] 构建成功，输出: " + outDir);
        }

        public static void BuildCLI()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (Exception e)
            {
                Debug.LogError("[WebGL] 构建异常: " + e);
                Console.WriteLine("BUILD_FAILED:" + e.Message);
                EditorApplication.Exit(1);
            }
        }

        static void ConfigurePlayerSettings()
        {
            // WebGL 仅支持 IL2CPP
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.WebGL, ScriptingImplementation.IL2CPP);
            try { PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup.WebGL, Il2CppCompilerConfiguration.Release); } catch {}
            // API 级别 .NET Standard 2.1（与 C#9 兼容）
            try { PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.WebGL, ApiCompatibilityLevel.NET_Standard); } catch {}

            // 浏览器兼容：关多线程（避免需要 COOP/COEP 跨域隔离头）、压缩关闭（任意静态服务器可直接跑）
            PlayerSettings.WebGL.threadsSupport = false;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Off;
            PlayerSettings.WebGL.dataCaching = false;   // V6.7.1fix 关闭 IndexedDB 数据缓存：同源多版本 .data 同名会与新 wasm 偏移错位，导致 _main 阶段 memory access out of bounds

            // 默认横屏 1080p、后台运行、产品名固定
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.productName = "从像素到文明 V7.0.2";
            PlayerSettings.companyName = "ToFuture";
        }

        /// <summary>扫描构建产物里的 loader 脚本名，生成自定义中文全屏加载页（默认横屏铺满、进度条、全屏按钮）</summary>
        static void WriteCustomIndex(string outDir)
        {
            string buildDir = Path.Combine(outDir, "Build");
            string loader = null;
            if (Directory.Exists(buildDir))
                foreach (var f in Directory.GetFiles(buildDir, "*.loader.js")) { loader = Path.GetFileName(f); break; }
            if (string.IsNullOrEmpty(loader)) loader = "build.loader.js";

            string baseName = loader.Replace(".loader.js", "");
            string html = IndexTemplate
                .Replace("__LOADER__", loader)
                .Replace("__DATA__", baseName + ".data")
                .Replace("__FRAME__", baseName + ".framework.js")
                .Replace("__CODE__", baseName + ".wasm")
                .Replace("__VER__", BuildVer);
            File.WriteAllText(Path.Combine(outDir, "index.html"), html, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(outDir, "启动网页版说明.txt"), ReadmeText, new UTF8Encoding(true));
            WriteServerScripts(outDir);
        }

        /// <summary>生成本地静态服务器脚本（Windows .bat/.ps1 与 macOS .command），解决 WebGL 不能 file:// 直开的问题</summary>
        static void WriteServerScripts(string outDir)
        {
            File.WriteAllText(Path.Combine(outDir, "serve_web.ps1"), ServePs1, new UTF8Encoding(true));
            // macOS .command 必须是 LF 行尾、无 BOM，否则 shebang 失效报“程序不可用”
            string cmd = StartCommand.Replace("\r\n", "\n");
            File.WriteAllText(Path.Combine(outDir, "start_webserver.command"), cmd, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(outDir, "Mac启动说明.txt"), ReadmeMac, new UTF8Encoding(true));
            // Windows bat 用 GBK + CRLF，避免中文乱码
            var gbk = System.Text.Encoding.GetEncoding(936);
            string bat = StartBat.Replace("\r\n", "\n").Replace("\n", "\r\n");
            File.WriteAllBytes(Path.Combine(outDir, "start_webserver.bat"), gbk.GetBytes(bat));
        }

        const string BuildVer = "7.0.2";
        const string IndexTemplate = @"<!doctype html>
<html lang=""zh-CN"">
<head>
<meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width,initial-scale=1,maximum-scale=1,user-scalable=no,viewport-fit=cover"">
<meta http-equiv=""Cache-Control"" content=""no-store,no-cache,must-revalidate"">
<meta http-equiv=""Pragma"" content=""no-cache"">
<title>从像素到文明 V7.0.2 · HTML5 网页版</title>
<style>
  html,body{margin:0;padding:0;width:100%;height:100%;background:#0e72c8;overflow:hidden;font-family:'Microsoft YaHei',PingFang SC,Arial,sans-serif;}
  #game{position:fixed;inset:0;width:100%;height:100%;}
  canvas{width:100%!important;height:100%!important;display:block;touch-action:none;}
  #boot{position:fixed;inset:0;display:flex;flex-direction:column;align-items:center;justify-content:center;
        background:radial-gradient(circle at 50% 35%,#34aef0 0%,#0e72c8 72%);color:#f2f8ff;z-index:10;transition:opacity .6s;}
  #boot h1{font-size:30px;letter-spacing:8px;margin:0 0 6px;color:#ffffff;text-shadow:0 2px 12px rgba(0,40,90,.35);}
  #boot p{margin:0 0 26px;font-size:14px;color:#eaf6ff;letter-spacing:2px;}
  #bar{width:min(560px,72vw);height:10px;border:1px solid rgba(255,255,255,.55);border-radius:8px;overflow:hidden;background:rgba(0,40,90,.18);}
  #fill{height:100%;width:0%;background:linear-gradient(90deg,#ffb347,#ffd76b);box-shadow:0 0 14px rgba(255,200,90,.8);transition:width .2s;}
  #pct{margin-top:12px;font-size:13px;color:#eaf6ff;}
  #fs{position:fixed;right:12px;bottom:10px;z-index:20;width:40px;height:40px;padding:0;font-size:19px;line-height:1;cursor:pointer;
      color:#21303f;background:rgba(250,250,246,.94);border:1px solid rgba(255,138,30,.60);border-radius:9px;box-shadow:0 2px 8px rgba(0,30,70,.25);backdrop-filter:blur(4px);
      display:flex;align-items:center;justify-content:center;}
  #fs:hover{background:rgba(255,138,30,.92);color:#fff;}
  #fs:active{transform:scale(.96);}
  #err{position:fixed;inset:auto 5% 8% 5%;z-index:30;display:none;color:#ffb4b4;font-size:13px;line-height:1.7;
       background:rgba(40,10,14,.85);border:1px solid #c05050;border-radius:8px;padding:12px;white-space:pre-wrap;}
</style>
</head>
<body>
<canvas id=""game""></canvas>
<div id=""boot"">
  <h1>从 像 素 到 文 明</h1>
  <p>V7.0.2 · HTML5 网页版 · 九智能体共治 · AI自治文明永续</p>
  <div id=""bar""><div id=""fill""></div></div>
  <div id=""pct"">正在加载 0%</div>
</div>
<button id=""fs"" title=""全屏（F11）"">⛶</button>
<div id=""err""></div>
<script>
  var fill=document.getElementById('fill'),pct=document.getElementById('pct'),boot=document.getElementById('boot'),err=document.getElementById('err');
  function showErr(m){err.style.display='block';err.textContent=m;}
  // V6.7.1fix 版本变化时清掉同源旧 Unity IndexedDB 缓存，杜绝旧 .data 与新 wasm 错位导致 memory access out of bounds
  (function(){try{var K='pxc_build_ver',V='__VER__';if(localStorage.getItem(K)!==V){localStorage.setItem(K,V);if(window.indexedDB&&indexedDB.deleteDatabase){indexedDB.deleteDatabase('UnityCache');}}}catch(e){}})();
  var VER='?v=__VER__';
  var script=document.createElement('script');
  script.src='Build/__LOADER__'+VER;
  script.onload=function(){
    var cfg={dataUrl:'Build/__DATA__'+VER,frameworkUrl:'Build/__FRAME__'+VER,codeUrl:'Build/__CODE__'+VER,streamingAssetsUrl:'StreamingAssets/',companyName:'ToFuture',productName:'从像素到文明 V7.0.2',productVersion:'__VER__'};
    createUnityInstance(document.querySelector('#game'),cfg,function(progress){
      var p=Math.round(progress*100);fill.style.width=p+'%';pct.textContent='正在加载 '+p+'%';
    }).then(function(inst){window.unityInstance=inst;boot.style.opacity='0';setTimeout(function(){boot.style.display='none';},600);})
      .catch(function(e){showErr('启动失败：'+e+'\n若直接双击打不开，请用附带的本地服务器脚本（start_webserver）通过 http 方式打开。');});
  };
  script.onerror=function(){showErr('加载器脚本丢失，请确认 Build 目录完整，并通过本地 http 服务器访问。');};
  document.body.appendChild(script);
  document.getElementById('fs').addEventListener('click',function(){
    var el=document.documentElement;if(!document.fullscreenElement){(el.requestFullscreen||el.webkitRequestFullscreen||function(){}).call(el);}else{document.exitFullscreen&&document.exitFullscreen();}
  });
</script>
</body>
</html>";

        const string ReadmeText =
            "《从像素到文明》V7.0.2 HTML5 网页版 — 运行说明\r\n" +
            "==========================================\r\n\r\n" +
            "一、为什么不能直接双击 index.html？\r\n" +
            "    Unity WebGL 出于浏览器安全策略，必须通过 http(s) 访问，直接用 file:// 双击通常会被拦截。\r\n\r\n" +
            "二、最简单：使用自带本地服务器\r\n" +
            "    1) Windows：双击本目录下的 start_webserver.bat，会自动选择空闲端口（默认 8000，被占用则顺延）并打开浏览器\r\n" +
            "    2) macOS：双击 start_webserver.command\r\n" +
            "    3) 或在本目录执行：python -m http.server 8000，再访问 http://localhost:8000\r\n\r\n" +
            "三、手机 / 平板\r\n" +
            "    把整个 BuildWebGL 目录部署到任意静态网站托管（或本机服务器），手机浏览器访问对应地址，\r\n" +
            "    默认横屏全屏；iOS Safari / 安卓 Chrome 均可，右下角按钮可切换全屏。\r\n\r\n" +
            "四、性能提示\r\n" +
            "    本版本已按网页端自动降档（程序贴图 128、关闭景深/色散/颗粒）。如仍卡顿，可在较新设备上体验。\r\n";

        const string StartBat = @"@echo off
chcp 936 >nul
title PixelToCivilization V7.0.2 Web Server
cd /d ""%~dp0""
rem V6.7.1: auto pick free port so a stale old server cannot hijack 8000
set PORT=8000
:findport
netstat -ano -p tcp | findstr /R /C:"":%PORT% .*LISTENING"" >nul 2>nul && set /a PORT+=1 && goto findport
echo ================================================
echo   从像素到文明 V7.0.2 · 本地网页服务器
echo   URL: http://localhost:%PORT%/
echo   (8000 被旧版本占用时自动顺延到下一端口)
echo   关闭本窗口即停止服务
echo ================================================
where py >nul 2>nul
if %errorlevel%==0 (
  start """" ""http://localhost:%PORT%/""
  py -m http.server %PORT%
  goto :end
)
where python >nul 2>nul
if %errorlevel%==0 (
  start """" ""http://localhost:%PORT%/""
  python -m http.server %PORT%
  goto :end
)
powershell -NoProfile -ExecutionPolicy Bypass -File ""%~dp0serve_web.ps1""
:end
pause
";

        const string ServePs1 = @"$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
function Get-FreePort($start){
  for($pp=$start;$pp -lt ($start+80);$pp++){
    try{ $tl=New-Object System.Net.Sockets.TcpListener([System.Net.IPAddress]::Loopback,$pp); $tl.Start(); $tl.Stop(); return $pp }catch{ }
  }
  return $start
}
$port = Get-FreePort 8000
$listener = New-Object System.Net.HttpListener
$listener.Prefixes.Add(""http://localhost:$port/"")
$listener.Start()
$mime = @{ '.html'='text/html; charset=utf-8';'.js'='application/javascript; charset=utf-8';'.wasm'='application/wasm';
  '.data'='application/octet-stream';'.json'='application/json';'.css'='text/css';'.png'='image/png';'.ico'='image/x-icon';
  '.br'='application/brotli';'.gz'='application/gzip';'.mem'='application/octet-stream';'.bin'='application/octet-stream';'.txt'='text/plain; charset=utf-8' }
Write-Host ""服务器已启动: http://localhost:$port/  关闭窗口即停止"" -ForegroundColor Yellow
Start-Process ""http://localhost:$port/""
while ($listener.IsListening) {
  try {
    $ctx = $listener.GetContext()
    $url = [System.Uri]::UnescapeDataString($ctx.Request.Url.AbsolutePath.TrimStart('/'))
    if ([string]::IsNullOrEmpty($url)) { $url = 'index.html' }
    $path = Join-Path $root ($url -replace '/','\')
    if ((Test-Path $path -PathType Container)) { $path = Join-Path $path 'index.html' }
    if (Test-Path $path -PathType Leaf) {
      $ext = [System.IO.Path]::GetExtension($path).ToLower()
      $ctx.Response.ContentType = if ($mime.ContainsKey($ext)) { $mime[$ext] } else { 'application/octet-stream' }
      $bytes = [System.IO.File]::ReadAllBytes($path)
      $ctx.Response.ContentLength64 = $bytes.Length
      $ctx.Response.Headers.Add('Cross-Origin-Opener-Policy','same-origin')
      $ctx.Response.Headers.Add('Cross-Origin-Embedder-Policy','require-corp')
      $ctx.Response.OutputStream.Write($bytes,0,$bytes.Length); $ctx.Response.OutputStream.Close()
    } else { $ctx.Response.StatusCode=404; $ctx.Response.OutputStream.Close() }
  } catch { break }
}
";

        const string StartCommand = @"#!/bin/bash
# 从像素到文明 V7.0.2 - macOS 本地网页服务器
cd ""$(dirname ""$0"")"" || exit 1
PORT=8000
while lsof -iTCP:$PORT -sTCP:LISTEN -nP >/dev/null 2>&1; do PORT=$((PORT+1)); done
LANIP=""$(ipconfig getifaddr en0 2>/dev/null)""
echo ""================================================ ""
echo ""  从像素到文明 V7.0.2 · 本地网页服务器""
echo ""  本机浏览器: http://localhost:$PORT/""
if [ -n ""$LANIP"" ]; then echo ""  手机同网段: http://$LANIP:$PORT/  (默认横屏全屏)""; fi
echo ""  关闭本窗口即停止服务""
echo ""================================================ ""
( sleep 1; open ""http://localhost:$PORT/"" ) >/dev/null 2>&1 &
if command -v python3 >/dev/null 2>&1; then
  exec python3 -m http.server ""$PORT"" --bind 0.0.0.0
fi
echo """"
echo ""未检测到 python3。请任选其一后重试：""
echo ""  1) 终端执行一次: xcode-select --install  (安装苹果命令行工具)""
echo ""  2) 已装 Homebrew: brew install python""
echo ""  3) 或用任意静态服务器托管本文件夹""
echo """"
echo ""按回车关闭窗口...""; read -r
";

        // macOS 排错说明（Windows 压缩包可能丢可执行位，给出右键/chmod/终端三种兜底）
        const string ReadmeMac = @"《从像素到文明》V7.0.2 — macOS 启动说明
========================================

★ 如果提示“已损坏，无法打开 / 您应该将它移到废纸篓”（最常见，必看）
  原因：浏览器/聊天工具下载的文件被 macOS 打上“隔离”标记，而本程序未做苹果公证，
        系统会把未签名脚本提示成“已损坏”（文件本身没坏）。清除隔离标记即可：
  1. 打开“终端”（启动台 → 其他 → 终端）
  2. 输入  xattr -dr com.apple.quarantine   （末尾留一个空格，先别回车）
  3. 把“整个游戏文件夹”拖进终端窗口，会自动补上路径，回车
  4. 再双击 start_webserver.command 即可正常运行
  （等价做法：终端先 cd 到本文件夹，执行  xattr -dr com.apple.quarantine .  ）

【首选】双击 start_webserver.command
  会自动选择空闲端口（默认 8000，被占用则顺延）并打开浏览器开始游戏；关闭弹出的终端窗口即停止服务。

如果双击提示“程序不可用 / 无法打开 / 没有权限 / 来自身份不明开发者”，
按下面任一方法即可（压缩包在 Windows 制作，个别解压工具会丢掉可执行权限）：

方法一（最省事）：右键打开
  在 start_webserver.command 上点右键 → “打开” → 弹窗里再点一次“打开”。

方法二：补一次可执行权限（一劳永逸）
  1. 打开“终端”（启动台 → 其他 → 终端）
  2. 输入  chmod +x   （x 后面有一个空格，先别回车）
  3. 把 start_webserver.command 拖进终端窗口，会自动补上路径
  4. 回车，之后双击即可正常运行

方法三：不用脚本，终端直接起服务
  1. 终端输入  cd   （cd 后有空格），把“整个游戏文件夹”拖进终端，回车
  2. 输入  python3 -m http.server 8000  回车
  3. 浏览器打开 http://localhost:8000/

如果提示“未找到 python3”：
  在终端执行一次  xcode-select --install  按提示安装苹果命令行工具；
  或已装 Homebrew 的执行  brew install python  ，之后再启动。

手机 / iPad（同一 Wi-Fi）：
  电脑启动脚本后，终端会显示“手机同网段: http://192.168.x.x:8000/”，
  手机浏览器打开该地址即可，默认横屏全屏。
";
    }
}
#endif
