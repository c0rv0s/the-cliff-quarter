using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TinyWorld
{
    public sealed class FrameTelemetry : MonoBehaviour
    {
        public WandererShell shell;
        readonly List<float> frames = new List<float>(10000);
        float elapsed, nextReport = 30;
        int cursor;
        void Update()
        {
            if (!shell.Playing || shell.Paused || !Application.isFocused) return;
            elapsed += Time.unscaledDeltaTime;
            if (elapsed > 3)
            {
                float ms = Time.unscaledDeltaTime * 1000;
                if (frames.Count < 18000) frames.Add(ms);
                else frames[cursor++ % 18000] = ms;
            }
            if (elapsed >= nextReport) { WriteReport(); nextReport += 30; }
        }
        public string WriteReport()
        {
            if (frames.Count < 10) return "Not enough samples";
            var sorted = frames.ToArray(); Array.Sort(sorted);
            double total = 0; foreach (float ms in frames) total += ms;
            var report = new Report { frames = frames.Count, averageFps = (float)(1000 * frames.Count / total), p95FrameMs = sorted[(int)(sorted.Length * .95f)],
                width = Screen.width, height = Screen.height, developmentBuild = Debug.isDebugBuild, device = SystemInfo.graphicsDeviceName, editor = Application.isEditor };
            var path = Path.Combine(Application.persistentDataPath, "performance.json");
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            Debug.Log("[Performance] " + JsonUtility.ToJson(report));
            return path;
        }
        void OnApplicationQuit() { WriteReport(); }
        [Serializable] sealed class Report
        {
            public int frames, width, height;
            public float averageFps, p95FrameMs;
            public bool developmentBuild, editor;
            public string device;
        }
    }
}
