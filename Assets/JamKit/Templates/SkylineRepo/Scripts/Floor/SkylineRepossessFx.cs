using System;
using UnityEngine;
using NVec3 = System.Numerics.Vector3;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// Small shared helpers of the floor's repossession and spawn-door runtime (tasks R2 and SD1): the hues, a light maker,
    /// a collider-free piece, the vector conversions, a depth-first find, and the sound hook. Nothing here is a MonoBehaviour,
    /// so no scene ever has to save it (Unity can only save a MonoBehaviour that sits in a file named after it).
    /// <para>Hues follow docs/SKYLINE-STYLE.md: the objective is violet #B36BFF (the role colour; the glow uses a bright HDR
    /// violet so bloom picks it up), a beacon is amber, and nothing here is the hostile #FF2E63.</para>
    /// </summary>
    public static class SkylineRepossessFx
    {
        /// <summary>The bright and dim objective violets (HDR glow colours; the same family as the builder's socket disc).</summary>
        public static readonly Color VioletHdr = new Color(1.1f, 0.55f, 2.4f), VioletDim = new Color(0.32f, 0.16f, 0.7f);
        /// <summary>The alarm beacon's amber, bright and dim.</summary>
        public static readonly Color AmberHdr = new Color(1.9f, 1.15f, 0.12f), AmberDim = new Color(0.32f, 0.18f, 0.03f);
        /// <summary>The ink-role violet #B36BFF as a plain colour for lights.</summary>
        public static readonly Color VioletLight = new Color32(0xB3, 0x6B, 0xFF, 255);

        /// <summary>Where a floor sound goes. Tests replace it to record the names; the game plays through <see cref="SkylineSounds"/>.</summary>
        public static Action<string, Vector3, float> SoundSink = DefaultSound;

        static void DefaultSound(string sound, Vector3 at, float volume) => SkylineSounds.PlayAt(sound, at, volume);

        /// <summary>Plays a floor sound at a point through <see cref="SoundSink"/> (a null or empty name plays nothing).</summary>
        public static void Sound(string sound, Vector3 at, float volume = 1f)
        {
            if (string.IsNullOrEmpty(sound)) return;
            SoundSink?.Invoke(sound, at, volume);
        }

        /// <summary>Puts the real sound hook back (tests call it in TearDown).</summary>
        public static void UseDefaultSound() => SoundSink = DefaultSound;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => SoundSink = DefaultSound;

        public static Vector3 U(NVec3 v) => new Vector3(v.X, v.Y, v.Z);
        public static NVec3 N(Vector3 v) => new NVec3(v.x, v.y, v.z);

        /// <summary>A primitive drawn without a collider or a shadow, at a local pose.</summary>
        public static GameObject Piece(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale, Material m)
        {
            var go = SkylineBotLook.Visual(type, name, parent, m, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            return go;
        }

        /// <summary>A realtime light under <paramref name="parent"/>, switched on. A spot points along the parent's forward.</summary>
        public static Light MakeLight(Transform parent, string name, LightType type, Color colour, float intensity, float range, float spotAngle = 60f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var l = go.AddComponent<Light>();
            l.type = type;
            l.color = colour;
            l.intensity = intensity;
            l.range = range;
            if (type == LightType.Spot) { l.spotAngle = spotAngle; l.innerSpotAngle = spotAngle * 0.85f; }
            l.shadows = LightShadows.None;
            return l;
        }

        /// <summary>The first object called <paramref name="name"/> at or under <paramref name="root"/> (depth first, inactive ones
        /// too), or null.</summary>
        public static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var f = FindDeep(root.GetChild(i), name);
                if (f != null) return f;
            }
            return null;
        }

        public static float Smooth01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
