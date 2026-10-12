using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace JamKit.Perf
{
    /// <summary>One quality tier of Visiting Hours (docs/redesign/perf.md "Quality tiers").</summary>
    public sealed class VhPerfTier
    {
        /// <summary>"high" or "low" (the -vh-tier flag and the saved choice).</summary>
        public string id;
        /// <summary>The QualitySettings level it runs on (ProjectSettings/QualitySettings.asset; its URP asset is made by
        /// VhPerfSetup: Perf/Settings/VH_High_RPAsset.asset, VH_Low_RPAsset.asset).</summary>
        public string qualityLevel;
        /// <summary>URP render scale (STP upscales to the screen; STP forces TAA, so MSAA stays off).</summary>
        public float renderScale;
        /// <summary>The sun's shadows: a cap on what a scene's light rig asks for (VhLookRig asks 160 m, 4 cascades, 4096).</summary>
        public float shadowDistance;
        public int shadowCascades;
        public int shadowResolution;
        /// <summary>A cap on a light's own soft-shadow quality (1 low, 2 medium, 3 high; URP's SoftShadowQuality).</summary>
        public int softShadowQuality;
        public bool additionalLightShadows;
        /// <summary>QualitySettings.lodBias (1: LOD thresholds mean what the builders set).</summary>
        public float lodBias;
        /// <summary>Multiplies every per-layer cull distance (VhPerfCullDistances).</summary>
        public float cullDistanceScale;
        /// <summary>QualitySettings.globalTextureMipmapLimit (0 full, 1 half).</summary>
        public int textureMipLimit;
        /// <summary>The mipmap streaming budget (MB).</summary>
        public int streamingBudgetMB;
        /// <summary>Application.targetFrameRate with vSync off: steady pacing and a cooler laptop (the perf probe uncaps).</summary>
        public int frameCap;
    }

    /// <summary>
    /// Visiting Hours' quality tiers: <see cref="High"/> (the default: 120 fps at 1080p on the RTX 3050 laptop) and
    /// <see cref="Low"/> (weaker machines). When a Visiting Hours scene loads, the chosen tier's QualitySettings level is made
    /// active (its URP asset carries render scale, STP, shadow map and soft-shadow settings) and the scene's light rigs and
    /// lights are capped to the tier (the rig asks for more than the tier allows). Other templates' scenes put the original
    /// level back. The choice: <c>-vh-tier high|low</c> on the command line, else the saved choice (<see cref="SetTier"/>),
    /// else a guess from the GPU (under 3.5 GB of VRAM: low).
    /// <para>No per-frame work: everything happens on scene load.</para>
    /// </summary>
    public static class VhPerfTiers
    {
        public const string PrefKey = "vh_perf_tier";
        public const string TierFlag = "-vh-tier";

        public static readonly VhPerfTier High = new VhPerfTier
        {
            id = "high", qualityLevel = "VH High", renderScale = 0.8f,
            shadowDistance = 150f, shadowCascades = 4, shadowResolution = 2048, softShadowQuality = 2, additionalLightShadows = true,
            lodBias = 1f, cullDistanceScale = 1f, textureMipLimit = 0, streamingBudgetMB = 1024, frameCap = 120,
        };

        public static readonly VhPerfTier Low = new VhPerfTier
        {
            id = "low", qualityLevel = "VH Low", renderScale = 0.6f,
            shadowDistance = 80f, shadowCascades = 2, shadowResolution = 1024, softShadowQuality = 1, additionalLightShadows = false,
            lodBias = 0.6f, cullDistanceScale = 0.7f, textureMipLimit = 1, streamingBudgetMB = 512, frameCap = 60,
        };

        /// <summary>The scenes that are Visiting Hours (the tier applies while one is loaded).</summary>
        public static readonly string[] Scenes = { "VisitingHours", "DreamStreet", "DreamHouse", "DreamEscape", "ArgumentPrototype", "LookLab" };

        static VhPerfTier _current;
        static int _previousLevel = -1;
        static bool _hooked, _active;
        static readonly object[] NoArgs = new object[0];

        public static VhPerfTier Current => _current ?? (_current = Choose());
        /// <summary>True while a Visiting Hours scene holds the tier's quality level.</summary>
        public static bool Active => _active;

        /// <summary>Fires after a tier is applied (a scene's rig was capped, a setting changed).</summary>
        public static event Action<VhPerfTier> Applied
        {
            add => _applied += value;
            remove => _applied -= value;
        }
        static Action<VhPerfTier> _applied;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _current = null;
            _previousLevel = -1;
            _hooked = false;
            _active = false;
            _applied = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Hook()
        {
            if (_hooked) return;
            _hooked = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        public static bool IsVhScene(string sceneName) => Array.IndexOf(Scenes, sceneName) >= 0;

        /// <summary>A tier by id ("high" / "low"); null for anything else.</summary>
        public static VhPerfTier ById(string id)
        {
            if (string.Equals(id, High.id, StringComparison.OrdinalIgnoreCase)) return High;
            if (string.Equals(id, Low.id, StringComparison.OrdinalIgnoreCase)) return Low;
            return null;
        }

        static VhPerfTier Choose()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == TierFlag)
                {
                    var t = ById(args[i + 1]);
                    if (t != null) return t;
                }
            try
            {
                var saved = ById(PlayerPrefs.GetString(PrefKey, ""));
                if (saved != null) return saved;
            }
            catch (Exception) { }
            // A guess for a first launch: little VRAM or an old GPU feature level is a weaker machine.
            if (SystemInfo.graphicsMemorySize > 0 && SystemInfo.graphicsMemorySize < 3500) return Low;
            if (SystemInfo.graphicsShaderLevel < 45) return Low;
            return High;
        }

        /// <summary>Choose a tier (a settings menu); <paramref name="save"/> remembers it. Applies at once in a VH scene.</summary>
        public static void SetTier(VhPerfTier tier, bool save = true)
        {
            if (tier == null) return;
            _current = tier;
            if (save)
            {
                try { PlayerPrefs.SetString(PrefKey, tier.id); PlayerPrefs.Save(); } catch (Exception) { }
            }
            if (_active) Apply(SceneManager.GetActiveScene());
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (IsVhScene(scene.name)) Apply(scene);
            else if (_active && mode == LoadSceneMode.Single && !IsShellScene(scene.name)) Restore();
        }

        /// <summary>Boot and title scenes are shared by every template: they keep whatever level is on.</summary>
        static bool IsShellScene(string name) => name == "Boot" || name == "Title";

        /// <summary>Makes the tier's quality level active and caps the scene's rigs and lights to it.</summary>
        public static void Apply(Scene scene)
        {
            var tier = Current;
            int level = LevelIndex(tier.qualityLevel);
            if (level >= 0)
            {
                if (!_active) _previousLevel = QualitySettings.GetQualityLevel();
                if (QualitySettings.GetQualityLevel() != level) QualitySettings.SetQualityLevel(level, true);
            }
            else if (!_warnedMissing)
            {
                _warnedMissing = true;
                Debug.LogWarning("JAMKIT_PERF no quality level \"" + tier.qualityLevel + "\": run JamKit > Perf > Set Up Quality Tiers (VhPerfSetup) once");
            }
            _active = true;
            QualitySettings.lodBias = tier.lodBias;
            QualitySettings.globalTextureMipmapLimit = tier.textureMipLimit;
            QualitySettings.streamingMipmapsActive = true;
            QualitySettings.streamingMipmapsMemoryBudget = tier.streamingBudgetMB;
            if (QualitySettings.vSyncCount == 0 && Array.IndexOf(Environment.GetCommandLineArgs(), "-vh-uncapped") < 0) Application.targetFrameRate = tier.frameCap;
            CapRigs(tier);
            CapLights(tier);
            _applied?.Invoke(tier);
        }

        static bool _warnedMissing;

        /// <summary>Caps rigs and lights made after the scene loaded (the ward and the title build theirs at runtime: call
        /// this once after building).</summary>
        public static void CapNow()
        {
            if (!_active) return;
            CapRigs(Current);
            CapLights(Current);
        }

        /// <summary>Back to the quality level that was on before Visiting Hours (another template's scene).</summary>
        public static void Restore()
        {
            if (_previousLevel >= 0 && _previousLevel < QualitySettings.names.Length) QualitySettings.SetQualityLevel(_previousLevel, true);
            Application.targetFrameRate = -1;
            _active = false;
        }

        public static int LevelIndex(string name)
        {
            var names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++) if (names[i] == name) return i;
            return -1;
        }

        // ---------- Caps ----------

        /// <summary>
        /// The Look agent's VhLookRig overrides the URP asset's main-light shadows while enabled (distance, cascades,
        /// resolution). Its fields are capped to the tier and the override re-applied. Found by name, so this assembly
        /// doesn't depend on the Look assembly (and keeps working if the rig changes).
        /// </summary>
        static void CapRigs(VhPerfTier tier)
        {
            var type = Type.GetType("JamKit.Look.VhLookRig, JamKit.Look");
            if (type == null) return;
            var rigs = UnityEngine.Object.FindObjectsByType(type, FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (rigs.Length == 0) return;
            var distance = type.GetField("shadowDistance", BindingFlags.Public | BindingFlags.Instance);
            var cascades = type.GetField("shadowCascades", BindingFlags.Public | BindingFlags.Instance);
            var resolution = type.GetField("shadowResolution", BindingFlags.Public | BindingFlags.Instance);
            var refresh = type.GetMethod("RefreshShadows", BindingFlags.Public | BindingFlags.Instance);
            foreach (var rig in rigs)
            {
                if (distance != null && distance.FieldType == typeof(float))
                    distance.SetValue(rig, Mathf.Min((float)distance.GetValue(rig), tier.shadowDistance));
                if (cascades != null && cascades.FieldType == typeof(int))
                    cascades.SetValue(rig, Mathf.Min((int)cascades.GetValue(rig), tier.shadowCascades));
                if (resolution != null && resolution.FieldType == typeof(int))
                    resolution.SetValue(rig, Mathf.Min((int)resolution.GetValue(rig), tier.shadowResolution));
                if (refresh != null && refresh.GetParameters().Length == 0) refresh.Invoke(rig, NoArgs);
            }
            // Without a rig the asset's own values (the tier's URP asset) stand.
        }

        /// <summary>Lights asking for a higher soft-shadow quality than the tier allows are lowered to it; on a tier
        /// without additional-light shadows, point and spot lights stop casting (the URP asset turns them off anyway).</summary>
        static void CapLights(VhPerfTier tier)
        {
            var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var l in lights)
            {
                if (l == null || l.shadows == LightShadows.None) continue;
                if (l.TryGetComponent<UniversalAdditionalLightData>(out var data))
                {
                    int q = (int)data.softShadowQuality;
                    if (q > tier.softShadowQuality) data.softShadowQuality = (SoftShadowQuality)tier.softShadowQuality;
                }
                if (!tier.additionalLightShadows && l.type != LightType.Directional) l.shadows = LightShadows.None;
            }
        }

        /// <summary>The active URP asset (the tier's, once VhPerfSetup has run).</summary>
        public static UniversalRenderPipelineAsset ActiveAsset => GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
    }
}
