using System.IO;
using JamKit.Look;
using UnityEditor;
using UnityEngine;

namespace JamKit.Look.EditorTools
{
    /// <summary>
    /// The hill's golden-hour Saturday soundscape, for the scene builders (World on the hill, the Look's LookLab).
    /// Clips live in <see cref="Dir"/> (CC0 field recordings, made by tools/look/ambience-build.py; SOURCES.md there).
    /// <code>
    /// VhLookAmbience.ConfigureImporters();                         // once, before loading clips (idempotent)
    /// VhLookAmbience.AddBed(root);                                 // the 2D bed
    /// VhLookAmbience.AddEmitter(root, VhLookAmbience.Kind.Sprinkler, yardPos);
    /// VhLookAmbience.AddSaturdayDefault(root, playAreaCentre, seed: 1);  // bed + a sensible set around a play area
    /// </code>
    /// Positions are world positions. Wander offsets are in the emitter's parent's space, so give it an unscaled parent.
    /// The scene's AudioListener is on the player camera; nothing here adds one.
    /// </summary>
    public static class VhLookAmbience
    {
        public enum Kind { Birds, Lawnmower, Sprinkler, Dog, TreeWind, LittleLeague, IceCreamTruck }

        public const string Dir = "Assets/JamKit/Templates/VisitingHours/Look/Audio/Ambience/";
        public const string BedClip = "VH_Amb_Bed_SaturdayEvening";

        /// <summary>Variant names per kind (index = the variant argument of <see cref="AddEmitter"/>).
        /// Birds: 0 robin, 1 sparrows (song sparrow + house sparrows), 2 mourning dove. Dog: 0 a dog a few yards over
        /// (bursts of barks), 1 a dog several streets away. TreeWind: 0 soft, 1 gusty. The rest have one.</summary>
        public static int VariantCount(Kind kind)
        {
            switch (kind)
            {
                case Kind.Birds: return 3;
                case Kind.Dog: return 2;
                case Kind.TreeWind: return 2;
                default: return 1;
            }
        }

        // ------------------------------------------------------------------ building

        /// <summary>The 2D stereo bed (soft evening air, far suburban hum, far birds, the odd far car), at -30 LUFS in the file.</summary>
        public static VhAmbienceBed AddBed(Transform parent)
        {
            var go = new GameObject("Ambience Bed (2D)");
            Undo.RegisterCreatedObjectUndo(go, "Add ambience bed");
            if (parent != null) go.transform.SetParent(parent, false);
            var bed = go.AddComponent<VhAmbienceBed>();
            bed.clip = Clip(BedClip);
            bed.volume = 0.8f;
            bed.fadeIn = 4f;
            return bed;
        }

        /// <summary>One positional source of <paramref name="kind"/> at world <paramref name="position"/>.
        /// <paramref name="variant"/> picks a variant (see <see cref="VariantCount"/>); -1 picks one from the position, so
        /// emitters scattered with the same call don't all sound alike.</summary>
        public static VhAmbienceEmitter AddEmitter(Transform parent, Kind kind, Vector3 position, float volume = 1f, int variant = -1)
        {
            int n = VariantCount(kind);
            if (variant < 0) variant = PositionHash(position) % n;
            variant = Mathf.Clamp(variant, 0, n - 1);

            var go = new GameObject("Ambience " + Label(kind, variant));
            Undo.RegisterCreatedObjectUndo(go, "Add ambience emitter");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = position;
            var e = go.AddComponent<VhAmbienceEmitter>();
            e.seed = PositionHash(position) + 1;
            Configure(e, kind, variant);
            e.volume = Mathf.Clamp01(e.volume * volume);
            return e;
        }

        /// <summary>
        /// The bed plus a Saturday around a play area centred on <paramref name="center"/>: birds in five trees (10-60 m,
        /// 5-9 m up), wind in three trees, a mower working a lawn 120-200 m off, a sprinkler in a yard 30-60 m off, a dog
        /// 60-150 m off and another far off, the little-league game 250-400 m off, and the ice-cream truck now and then
        /// driving by 200-300 m off. Same seed, same layout. Returns the root ("Ambience (golden-hour Saturday)").
        /// </summary>
        public static Transform AddSaturdayDefault(Transform parent, Vector3 center, int seed = 1)
        {
            var root = new GameObject("Ambience (golden-hour Saturday)");
            Undo.RegisterCreatedObjectUndo(root, "Add Saturday ambience");
            if (parent != null) root.transform.SetParent(parent, false);
            root.transform.position = center;
            root.transform.rotation = Quaternion.identity;
            var r = new System.Random(seed);
            float baseAngle = (float)r.NextDouble() * 360f;
            int slot = 0;

            Vector3 Around(float minR, float maxR, float up)
            {
                // spread directions: golden-angle steps from a seeded start, with a little jitter
                float a = (baseAngle + slot++ * 137.508f + ((float)r.NextDouble() - 0.5f) * 30f) * Mathf.Deg2Rad;
                float d = Mathf.Lerp(minR, maxR, (float)r.NextDouble());
                return center + new Vector3(Mathf.Sin(a) * d, up, Mathf.Cos(a) * d);
            }
            float Up(float a, float b) { return Mathf.Lerp(a, b, (float)r.NextDouble()); }

            AddBed(root.transform);

            AddEmitter(root.transform, Kind.Birds, Around(10f, 30f, Up(5f, 9f)), 1f, 0);
            AddEmitter(root.transform, Kind.Birds, Around(12f, 35f, Up(5f, 9f)), 1f, 1);
            AddEmitter(root.transform, Kind.Birds, Around(25f, 45f, Up(6f, 9f)), 0.9f, 2);
            AddEmitter(root.transform, Kind.Birds, Around(35f, 60f, Up(5f, 9f)), 0.8f, 0);
            AddEmitter(root.transform, Kind.Birds, Around(30f, 55f, Up(5f, 8f)), 0.8f, 1);

            AddEmitter(root.transform, Kind.TreeWind, Around(10f, 22f, Up(4f, 7f)), 1f, 0);
            AddEmitter(root.transform, Kind.TreeWind, Around(14f, 28f, Up(4f, 7f)), 0.9f, 0);
            AddEmitter(root.transform, Kind.TreeWind, Around(18f, 32f, Up(4f, 7f)), 0.8f, 1);

            var mower = AddEmitter(root.transform, Kind.Lawnmower, Around(120f, 200f, 1f));
            float yaw = (float)r.NextDouble() * Mathf.PI;
            var lane = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw)) * 12f;
            mower.wanderFrom = -lane;
            mower.wanderTo = lane;

            AddEmitter(root.transform, Kind.Sprinkler, Around(30f, 60f, 0.3f));
            AddEmitter(root.transform, Kind.Dog, Around(60f, 150f, 0.5f), 1f, 0);
            AddEmitter(root.transform, Kind.Dog, Around(160f, 260f, 0.5f), 1f, 1);
            AddEmitter(root.transform, Kind.LittleLeague, Around(250f, 400f, 2f));

            Vector3 truckAt = Around(200f, 300f, 1.5f);
            var truck = AddEmitter(root.transform, Kind.IceCreamTruck, truckAt);
            Vector3 radial = truckAt - center;
            radial.y = 0f;
            Vector3 street = Vector3.Cross(Vector3.up, radial.normalized) * 90f; // drives across the view, not at us
            truck.wanderFrom = -street;
            truck.wanderTo = street;
            return root.transform;
        }

        static void Configure(VhAmbienceEmitter e, Kind kind, int variant)
        {
            e.priority = 170;
            switch (kind)
            {
                case Kind.Birds:
                    e.loopClip = Clip(variant == 0 ? "VH_Amb_Birds_Robin_Loop" : variant == 1 ? "VH_Amb_Birds_Sparrows_Loop" : "VH_Amb_Birds_MourningDove_Loop");
                    e.volume = 0.85f;
                    e.minDistance = 5f; e.maxDistance = 80f; e.spread = 70f;
                    e.loopPitchJitter = 0.04f;
                    break;
                case Kind.Lawnmower:
                    e.loopClip = Clip("VH_Amb_Lawnmower_Loop");
                    e.volume = 0.85f;
                    e.minDistance = 30f; e.maxDistance = 350f; e.spread = 90f;
                    e.wander = VhAmbienceEmitter.WanderMode.PingPong;
                    e.wanderFrom = new Vector3(-12f, 0f, 0f); e.wanderTo = new Vector3(12f, 0f, 0f);
                    e.wanderSpeed = 1.1f;
                    break;
                case Kind.Sprinkler:
                    e.loopClip = Clip("VH_Amb_Sprinkler_Loop");
                    e.volume = 0.8f;
                    e.minDistance = 4f; e.maxDistance = 70f; e.spread = 40f;
                    break;
                case Kind.Dog:
                    e.oneShots = variant == 0
                        ? Clips("VH_Amb_Dog_Bark_01", "VH_Amb_Dog_Bark_02", "VH_Amb_Dog_Bark_03", "VH_Amb_Dog_Bark_04", "VH_Amb_Dog_Bark_05", "VH_Amb_Dog_Bark_06")
                        : Clips("VH_Amb_Dog_BarkFar_01", "VH_Amb_Dog_BarkFar_02", "VH_Amb_Dog_BarkFar_03", "VH_Amb_Dog_BarkFar_04");
                    e.volume = 0.9f;
                    e.minDistance = variant == 0 ? 18f : 30f; e.maxDistance = variant == 0 ? 260f : 400f; e.spread = 30f;
                    e.oneShotInterval = variant == 0 ? new Vector2(8f, 22f) : new Vector2(10f, 30f);
                    e.burstCount = new Vector2Int(1, 4);
                    e.burstGap = new Vector2(0.45f, 1.1f);
                    e.pitchRange = new Vector2(0.97f, 1.03f);
                    e.intermittent = true;
                    e.activeDuration = new Vector2(25f, 60f);
                    e.silentDuration = new Vector2(40f, 120f);
                    e.stretchFade = 0.5f;
                    break;
                case Kind.TreeWind:
                    e.loopClip = Clip(variant == 0 ? "VH_Amb_TreeWind_Soft_Loop" : "VH_Amb_TreeWind_Gusty_Loop");
                    e.volume = variant == 0 ? 0.7f : 0.6f;
                    e.minDistance = 6f; e.maxDistance = 50f; e.spread = 150f;
                    e.loopPitchJitter = 0.05f;
                    break;
                case Kind.LittleLeague:
                    e.loopClip = Clip("VH_Amb_LittleLeague_Crowd_Loop");
                    e.oneShots = Clips("VH_Amb_LittleLeague_Hit_01", "VH_Amb_LittleLeague_Hit_02", "VH_Amb_LittleLeague_Hit_03",
                        "VH_Amb_LittleLeague_Umpire_01", "VH_Amb_LittleLeague_Umpire_02", "VH_Amb_LittleLeague_Umpire_03",
                        "VH_Amb_LittleLeague_Umpire_04", "VH_Amb_LittleLeague_Cheer_01");
                    e.volume = 0.9f;
                    e.minDistance = 70f; e.maxDistance = 700f; e.spread = 150f;
                    e.oneShotInterval = new Vector2(12f, 35f);
                    e.pitchRange = new Vector2(0.98f, 1.02f);
                    e.volumeJitter = 0.3f;
                    break;
                case Kind.IceCreamTruck:
                    e.loopClip = Clip("VH_Amb_IceCreamTruck_Loop");
                    e.volume = 0.8f;
                    e.minDistance = 25f; e.maxDistance = 450f; e.spread = 60f;
                    e.loopPitchJitter = 0f; // a tune: keep it in tune
                    e.wander = VhAmbienceEmitter.WanderMode.Pass;
                    e.wanderFrom = new Vector3(-90f, 0f, 0f); e.wanderTo = new Vector3(90f, 0f, 0f);
                    e.wanderSpeed = 4f; // about 9 mph, crawling for customers: a 45 s pass
                    e.intermittent = true;
                    e.silentDuration = new Vector2(60f, 150f);
                    e.startSilent = true;
                    e.stretchFade = 2f;
                    break;
            }
        }

        static string Label(Kind kind, int variant)
        {
            switch (kind)
            {
                case Kind.Birds: return variant == 0 ? "Birds (robin)" : variant == 1 ? "Birds (sparrows)" : "Birds (mourning dove)";
                case Kind.Dog: return variant == 0 ? "Dog (next yards)" : "Dog (far)";
                case Kind.TreeWind: return variant == 0 ? "Tree wind (soft)" : "Tree wind (gusty)";
                case Kind.LittleLeague: return "Little league (far)";
                case Kind.IceCreamTruck: return "Ice-cream truck";
                default: return kind.ToString();
            }
        }

        static int PositionHash(Vector3 p)
        {
            unchecked
            {
                int h = Mathf.RoundToInt(p.x * 3f) * 73856093 ^ Mathf.RoundToInt(p.y * 3f) * 19349663 ^ Mathf.RoundToInt(p.z * 3f) * 83492791;
                return h & 0x7FFFFFFF;
            }
        }

        // ------------------------------------------------------------------ clips

        /// <summary>Loads <see cref="Dir"/> + name + ".ogg" (importing it first if it's on disk but not yet in the
        /// AssetDatabase); warns and returns null if missing.</summary>
        public static AudioClip Clip(string name)
        {
            string path = Dir + name + ".ogg";
            var c = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (c == null && File.Exists(path))
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                c = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
            if (c == null) Debug.LogWarning("[VhLookAmbience] missing clip " + path);
            return c;
        }

        static AudioClip[] Clips(params string[] names)
        {
            var list = new System.Collections.Generic.List<AudioClip>(names.Length);
            foreach (var n in names)
            {
                var c = Clip(n);
                if (c != null) list.Add(c);
            }
            return list.ToArray();
        }

        /// <summary>
        /// Import settings for every clip in <see cref="Dir"/>, set in code so Unity's own .meta files suffice: the bed and
        /// the loops Vorbis, compressed in memory, loaded in the background; the short one-shots Vorbis, decompressed on
        /// load (no decode cost when a bark fires). Reimports only what changed. Returns how many it changed.
        /// </summary>
        public static int ConfigureImporters()
        {
            string folder = Dir.TrimEnd('/');
            if (!AssetDatabase.IsValidFolder(folder))
            {
                if (Directory.Exists(folder)) AssetDatabase.Refresh();
                if (!AssetDatabase.IsValidFolder(folder)) { Debug.LogWarning("[VhLookAmbience] no folder " + folder); return 0; }
            }
            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = AssetImporter.GetAtPath(path) as AudioImporter;
                if (imp == null) continue;
                string file = Path.GetFileNameWithoutExtension(path);
                bool streamy = file.Contains("_Bed_") || file.EndsWith("_Loop");

                AudioImporterSampleSettings s = imp.defaultSampleSettings;
                var want = s;
                want.loadType = streamy ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                want.compressionFormat = AudioCompressionFormat.Vorbis;
                want.quality = streamy ? 0.6f : 0.7f;
                want.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                want.preloadAudioData = true;

                bool dirty = s.loadType != want.loadType || s.compressionFormat != want.compressionFormat ||
                             Mathf.Abs(s.quality - want.quality) > 0.001f || s.sampleRateSetting != want.sampleRateSetting ||
                             s.preloadAudioData != want.preloadAudioData ||
                             imp.loadInBackground != streamy || imp.forceToMono;
                if (!dirty) continue;
                imp.defaultSampleSettings = want;
                imp.loadInBackground = streamy;
                imp.forceToMono = false;
                imp.SaveAndReimport();
                changed++;
            }
            return changed;
        }
    }
}
