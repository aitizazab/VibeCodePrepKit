using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using JamKit.Skyline;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>SkylinePass stage D: run the built player with <c>-skyline-perf &lt;dir&gt;</c>; after a 3 s warm-up it
    /// records 45 s of frame times in the first scene (the arena: about 45 s covers its
    /// demo, docs/SKYLINE-PLAN-M1.md amendment 9), writes perf.json and quits.</summary>
    public sealed class SkylinePerfProbe : MonoBehaviour
    {
        const float Warmup = 3f, Measure = 45f;
        readonly List<float> _frames = new List<float>(4096);
        string _outDir;
        float _start;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            string dir = SkylineRunArgs.Value("-skyline-perf");
            if (dir == null) return;
            try { File.Delete(Path.Combine(dir, "perf.json")); } catch (Exception) { /* a stale file must not pass for this run */ }
            var go = new GameObject("SkylinePerfProbe");
            DontDestroyOnLoad(go);
            var probe = go.AddComponent<SkylinePerfProbe>();
            probe._outDir = dir;
            probe._start = Time.realtimeSinceStartup;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Application.runInBackground = true;      // this run only (the player's own setting, never the shared PlayerSettings asset): a window that loses focus must keep presenting
        }

        static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        static string Esc(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

        void Update()
        {
            float t = Time.realtimeSinceStartup - _start;
            if (t < Warmup) return;
            _frames.Add(Time.unscaledDeltaTime);
            if (t < Warmup + Measure) return;
            var s = FrameStats.Summarize(_frames);
            int exitCode = 0;
            try
            {
                Directory.CreateDirectory(_outDir);
                File.WriteAllText(Path.Combine(_outDir, "perf.json"),
                    "{\"frames\":" + s.frames + ",\"avgMs\":" + F(s.avgMs) + ",\"onePercentLowMs\":" + F(s.onePercentLowMs) +
                    ",\"worstMs\":" + F(s.worstMs) + ",\"resolution\":\"" + Screen.width + "x" + Screen.height + "\",\"gpu\":\"" + Esc(SystemInfo.graphicsDeviceName) +
                    "\",\"scene\":\"" + Esc(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name) + "\"}");
            }
            catch (Exception e)
            {
                Debug.LogError("SKYLINE_PERF could not write perf.json: " + e.Message);
                exitCode = 1;
            }
            enabled = false;
            Application.Quit(exitCode);
        }
    }
}
