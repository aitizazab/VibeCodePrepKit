using System.Collections.Generic;
using JamKit.Skyline;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>A bot's face (2.5: "every enemy has a face or display"): the texture
    /// <c>Resources/SkylineModels/bots/&lt;id&gt;_face_&lt;BotFace&gt;</c> for each <see cref="BotFace"/>, else a generated
    /// 16 x 10 pixel face (logged once per bot). A plain lit display: nothing on a live bot glows but the Writ's cracks.</summary>
    public sealed class SkylineBotFace : MonoBehaviour
    {
        const int W = 16, H = 10;
        const string Folder = "SkylineModels/bots/";

        static readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();

        Renderer _renderer;
        Material _material;
        string _botId;
        BotFace _shown = (BotFace)(-1);

        public BotFace Shown => _shown;

        void OnDestroy()
        {
            if (_material != null) Destroy(_material);     // the per-bot instance made in Init
        }

        public void Init(string botId, Renderer target)
        {
            _botId = botId;
            _renderer = target;
            if (_renderer != null)
            {
                _material = _renderer.material;     // its own instance: the texture changes per bot
                _material.color = Color.white;
                // the screen never sits in its own cap's or brim's shadow (judge round 1: the drone's face read as a black
                // band under its visor): still lit, but not shadowed
                _renderer.receiveShadows = false;
                _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            Show(BotFace.Happy);
        }

        public void Show(BotFace face)
        {
            if (face == _shown || _material == null) return;
            _shown = face;
            _material.mainTexture = Texture(_botId, face);
        }

        public static Texture2D Texture(string botId, BotFace face)
        {
            string key = botId + "_face_" + face;
            if (_cache.TryGetValue(key, out var t) && t != null) return t;
            t = Resources.Load<Texture2D>(Folder + key);
            if (t == null)
            {
                SkylineBotBodies.LogOnce("SKYLINE_BOT missing face " + botId + " (generated pixel faces)");
                t = Generate(botId, face);
            }
            _cache[key] = t;
            return t;
        }

        /// <summary>A pixel face: dark screen, features in the face's colour (cyan happy, white alert, red angry, amber hurt,
        /// grey wrecked).</summary>
        public static Texture2D Generate(string botId, BotFace face)
        {
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = botId + "_face_" + face };
            var bg = new Color(0.05f, 0.07f, 0.1f);
            var px = new Color[W * H];
            for (int i = 0; i < px.Length; i++) px[i] = bg;
            Color c;
            switch (face)
            {
                case BotFace.Alert: c = new Color(1f, 1f, 0.95f); break;
                case BotFace.Angry: c = new Color(1f, 0.22f, 0.15f); break;
                case BotFace.Hurt: c = new Color(1f, 0.7f, 0.2f); break;
                case BotFace.Wrecked: c = new Color(0.4f, 0.4f, 0.42f); break;
                default: c = new Color(0.4f, 0.95f, 1f); break;
            }
            // y = 0 is the bottom row.
            switch (face)
            {
                case BotFace.Happy:
                    Arc(px, 4, 6, c, true); Arc(px, 11, 6, c, true);          // ^ ^
                    Smile(px, c);
                    break;
                case BotFace.Alert:
                    Box(px, 3, 5, 3, 3, c); Box(px, 10, 5, 3, 3, c);          // O O
                    Box(px, 7, 1, 2, 2, c);                                   // o
                    break;
                case BotFace.Angry:
                    Line(px, 2, 8, 6, 6, c); Line(px, 13, 8, 9, 6, c);        // \ /
                    Box(px, 4, 4, 2, 2, c); Box(px, 10, 4, 2, 2, c);
                    Line(px, 4, 1, 11, 1, c);                                 // flat mouth
                    Set(px, 3, 2, c); Set(px, 12, 2, c);
                    break;
                case BotFace.Hurt:
                    Line(px, 3, 7, 5, 6, c); Line(px, 3, 5, 5, 6, c);         // > <
                    Line(px, 12, 7, 10, 6, c); Line(px, 12, 5, 10, 6, c);
                    Line(px, 5, 2, 10, 2, c); Set(px, 6, 1, c); Set(px, 9, 1, c);
                    break;
                case BotFace.Wrecked:
                    Line(px, 3, 7, 6, 4, c); Line(px, 3, 4, 6, 7, c);         // X X
                    Line(px, 9, 7, 12, 4, c); Line(px, 9, 4, 12, 7, c);
                    Line(px, 5, 1, 10, 1, c);
                    break;
            }
            tex.SetPixels(px);
            tex.Apply(false, false);
            return tex;
        }

        static void Set(Color[] px, int x, int y, Color c)
        {
            if (x < 0 || x >= W || y < 0 || y >= H) return;
            px[y * W + x] = c;
        }

        static void Box(Color[] px, int x, int y, int w, int h, Color c)
        {
            for (int j = 0; j < h; j++) for (int i = 0; i < w; i++) Set(px, x + i, y + j, c);
        }

        static void Line(Color[] px, int x0, int y0, int x1, int y1, Color c)
        {
            int n = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            for (int i = 0; i <= n; i++)
            {
                float t = n > 0 ? (float)i / n : 0f;
                Set(px, Mathf.RoundToInt(Mathf.Lerp(x0, x1, t)), Mathf.RoundToInt(Mathf.Lerp(y0, y1, t)), c);
            }
        }

        static void Arc(Color[] px, int cx, int y, Color c, bool up)
        {
            Set(px, cx - 2, y, c); Set(px, cx - 1, y + 1, c); Set(px, cx, y + 2, c); Set(px, cx + 1, y + 1, c); Set(px, cx + 2, y, c);
        }

        static void Smile(Color[] px, Color c)
        {
            Set(px, 4, 3, c); Set(px, 11, 3, c);
            Line(px, 5, 2, 10, 2, c);
            Line(px, 6, 1, 9, 1, c);
        }
    }
}
