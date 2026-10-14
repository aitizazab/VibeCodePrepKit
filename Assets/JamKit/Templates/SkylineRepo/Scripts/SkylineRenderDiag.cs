using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.SkylineRepo
{
    /// <summary>With <c>-skyline-render-diag</c>: 8 s after start, logs every bot renderer's state (layer, shadow mode, each
    /// material's shader, support, queue, surface, colour alpha and keywords) and the main camera's culling mask, as
    /// <c>SKYLINE_DIAG</c> lines. For build-only rendering bugs (2026-10-14: bots near-invisible in the build, fine in the
    /// editor).</summary>
    public sealed class SkylineRenderDiag : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (!SkylineRunArgs.Has("-skyline-render-diag")) return;
            var go = new GameObject("SkylineRenderDiag");
            DontDestroyOnLoad(go);
            go.AddComponent<SkylineRenderDiag>();
        }

        float _at = 8f;

        void Update()
        {
            if (_at < 0f || Time.unscaledTime < _at) return;
            _at = -1f;
            var cam = Camera.main;
            Debug.Log("SKYLINE_DIAG camera " + (cam != null ? cam.name + " mask " + cam.cullingMask : "none")
                + " api " + SystemInfo.graphicsDeviceType);
            int n = 0;
            var roots = new System.Collections.Generic.List<Transform>();
            foreach (var bot in FindObjectsByType<SkylineBot>(FindObjectsSortMode.None)) if (n++ < 2) roots.Add(bot.transform);
            var rig = GameObject.Find("Skyline Viewmodel Rig");
            if (rig != null) roots.Add(rig.transform);
            foreach (var root in roots)
            {
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    string line = "SKYLINE_DIAG " + root.name + "/" + r.name + " on " + r.enabled + " active " + r.gameObject.activeInHierarchy
                        + " layer " + r.gameObject.layer + " shadows " + r.shadowCastingMode + " visible " + r.isVisible;
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null) { line += " | null"; continue; }
                        line += " | " + m.name + " [" + (m.shader != null ? m.shader.name + (m.shader.isSupported ? "" : " UNSUPPORTED") : "no shader")
                            + "] q " + m.renderQueue
                            + (m.HasProperty("_Surface") ? " surf " + m.GetFloat("_Surface") : "")
                            + (m.HasProperty("_BaseColor") ? " a " + m.GetColor("_BaseColor").a.ToString("0.00") : "")
                            + " kw " + string.Join(",", m.shaderKeywords);
                    }
                    Debug.Log(line);
                }
            }
        }
    }
}
