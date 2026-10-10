using JamKit.Movement;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.EditorTools
{
    /// <summary>
    /// Wires the speed visuals (docs/SPEED-FEEL.md "Speed feel (built)") onto a first-person player for the scene
    /// builders: <see cref="SpeedFeel"/> (the tiers), <see cref="SpeedScreenFx"/> (ring, chromatic pulse, edge blur,
    /// grade), <see cref="SpeedStreaks"/>, <see cref="SpeedFootfalls"/>, and hooks the camera feel's boom and the
    /// speed lines to it. Writes its materials (so the shaders ship in builds) into the scene's materials folder.
    /// One call per builder; the <see cref="Look"/> carries the scene's palette.
    /// </summary>
    public static class SpeedFeelSetup
    {
        public const string ScreenShader = "JamKit/Movement/SpeedScreen";
        public const string MotesShader = "JamKit/Movement/SpeedMotes";

        /// <summary>Shape ids drawn by JamKit/Movement/SpeedMotes.</summary>
        public const int Mote = 0, Confetti = 1, Petal = 2, Flower = 3, ShoePrint = 4, Puff = 5;

        /// <summary>A scene's look for the speed particles.</summary>
        public sealed class Look
        {
            public Color[] streakColors;
            public Color streakRim;
            public int streakShape, streakShapes = 1;
            public Vector3 streakDrift;
            public float streakJitter;
            public Color[] puffColors;
            public Color puffRim;
            public float puffRimWidth;
            public int puffShape = Puff, puffShapes = 1;
            public float puffSoft = 0.35f;
            public Color footprint;

            /// <summary>The grey-box lab: pale dust motes with a dark outline, grey-beige dust puffs, cyan footprints.</summary>
            public static Look Lab() => new Look
            {
                streakColors = new[] { new Color(0.95f, 0.96f, 0.98f), new Color(0.86f, 0.88f, 0.92f), new Color(1f, 0.95f, 0.85f) },
                streakRim = new Color(0.14f, 0.15f, 0.19f),
                streakShape = Mote,
                puffColors = new[] { new Color(0.78f, 0.76f, 0.72f, 0.7f), new Color(0.88f, 0.86f, 0.82f, 0.65f) },
                puffRim = new Color(0.4f, 0.39f, 0.37f),
                puffRimWidth = 0f,
                puffShape = Puff,
                footprint = new Color(0.45f, 0.85f, 1f, 0.9f),
            };
        }

        public static Material EnsureMaterial(string dir, string name, string shaderName)
        {
            string path = dir + "/" + name + ".mat";
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError("Speed feel: shader " + shaderName + " not found.");
                return null;
            }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader) mat.shader = shader;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>A SpeedMotes material: shapes, rim, centre clearing, alpha or additive.</summary>
        public static Material Motes(string dir, string name, int firstShape, int shapes, Color rim, float rimWidth, float soft, bool clearCentre, bool additive, float glow = 1f)
        {
            var m = EnsureMaterial(dir, name, MotesShader);
            if (m == null) return null;
            m.SetVector("_Shapes", new Vector4(firstShape, Mathf.Max(1, shapes), 0f, 0f));
            m.SetColor("_RimColor", rim);
            m.SetFloat("_RimWidth", rimWidth);
            m.SetFloat("_Soft", soft);
            m.SetFloat("_Glow", glow);
            var def = new SpeedFeelDef();
            m.SetVector("_CenterClear", clearCentre ? new Vector4(def.centerClear, def.centerClearNear, 0f, 0f) : Vector4.zero);
            m.SetFloat("_SrcBlend", (float)BlendMode.One);
            m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        /// <summary>Adds the speed visuals to <paramref name="cam"/> (the player's camera) and hooks the camera feel
        /// (the boom) and the speed lines to them. Returns the hub.</summary>
        public static SpeedFeel Wire(Camera cam, MovementController body, MovementCamera feelCamera, SpeedLines lines, string materialsDir, Look look)
        {
            look = look ?? Look.Lab();
            var go = cam.gameObject;
            var feel = go.AddComponent<SpeedFeel>();
            feel.body = body;
            if (feelCamera != null) feelCamera.speedFeel = feel;
            if (lines != null) lines.feel = feel;

            var screen = go.AddComponent<SpeedScreenFx>();
            screen.feel = feel;
            screen.material = EnsureMaterial(materialsDir, "SpeedFeel_Screen", ScreenShader);

            var streaks = go.AddComponent<SpeedStreaks>();
            streaks.feel = feel;
            streaks.material = Motes(materialsDir, "SpeedFeel_Streaks", look.streakShape, look.streakShapes, look.streakRim, 0.24f, 0f, true, false);
            streaks.palette = look.streakColors;
            streaks.drift = look.streakDrift;
            streaks.driftJitter = look.streakJitter;

            var feet = go.AddComponent<SpeedFootfalls>();
            feet.feel = feel;
            feet.puffMaterial = Motes(materialsDir, "SpeedFeel_Puffs", look.puffShape, look.puffShapes, look.puffRim, look.puffRimWidth, look.puffSoft, false, false);
            feet.footprintMaterial = Motes(materialsDir, "SpeedFeel_Footprints", ShoePrint, 1, look.footprint * 0.6f, 0f, 0.25f, false, true, 1.4f);
            feet.puffColors = look.puffColors;
            feet.footprintColor = look.footprint;
            return feel;
        }
    }
}
