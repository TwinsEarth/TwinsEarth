using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PixelToCivilization.EditorTools
{
    /// <summary>V6.1.2 诊断：batchmode 进入 Play 模式跑数秒，把运行时日志/异常（含中文堆栈）写入文件后退出。</summary>
    public static class PlayModeProbe
    {
        static string _logPath;
        static StringBuilder _sb = new();
        static double _endAt;
        static bool _armed;

        public static void StartProbe()
        {
            _logPath = Path.Combine(Application.dataPath, "..", "v612_probe.log");
            File.WriteAllText(_logPath, "probe start\n", new UTF8Encoding(true));
            Application.logMessageReceived += OnLog;
            EditorApplication.playModeStateChanged += OnPlayChanged;
            _armed = true;
            EditorApplication.EnterPlaymode();
        }

        static void OnPlayChanged(PlayModeStateChange st)
        {
            if (st == PlayModeStateChange.EnteredPlayMode)
            {
                _sb.AppendLine("[EnteredPlayMode] at " + EditorApplication.timeSinceStartup);
                _endAt = EditorApplication.timeSinceStartup + 8.0; // 跑 8 秒
                EditorApplication.update += Tick;
            }
        }

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup >= _endAt)
            {
                EditorApplication.update -= Tick;
                Flush();
                Application.logMessageReceived -= OnLog;
                EditorApplication.Exit(0);
            }
        }

        static void OnLog(string condition, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert || type == LogType.Warning)
            {
                _sb.AppendLine("==== " + type + " ====");
                _sb.AppendLine(condition);
                _sb.AppendLine(stack);
                if (_sb.Length > 200000) Flush();
            }
        }

        static void Flush()
        {
            try { File.AppendAllText(_logPath, _sb.ToString(), new UTF8Encoding(true)); _sb.Clear(); }
            catch { }
        }
    }
}
