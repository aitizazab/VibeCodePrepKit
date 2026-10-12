using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JamKit.Argument
{
    public enum BoilStyle { Off, Boil, Breathe }

    /// <summary>
    /// One setting of the opponent sprite's hand-drawn distortion, the same four variables as Lily's SVG filter
    /// (FableAnimationTests/demos/lily-warp.html: feTurbulence fractal noise driving feDisplacementMap):
    /// <list type="bullet">
    /// <item><c>strength</c>: the furthest a pixel is pushed, in pixels on a 1080p screen (the filter's scale is twice it).</item>
    /// <item><c>freq</c>: the noise's frequency, in cycles per screen pixel (lower = broader waves; Lily's 0.02 is a 50 px wave).</item>
    /// <item><c>rate</c>: boil re-rolls per second (the noise is re-seeded in steps, not moved smoothly: that's the boil).</item>
    /// <item><c>speed</c>: how fast "breathe" drifts the noise (unused by boil, kept so the four match Lily's).</item>
    /// </list>
    /// </summary>
    [Serializable]
    public struct BoilSettings
    {
        public string name;
        public BoilStyle style;
        public float strength;
        public float freq;
        public float rate;
        public float speed;

        public BoilSettings(string name, BoilStyle style, float strength, float freq, float rate, float speed)
        {
            this.name = name;
            this.style = style;
            this.strength = strength;
            this.freq = freq;
            this.rate = rate;
            this.speed = speed;
        }

        public bool Visible => style != BoilStyle.Off && strength > 0f && freq > 0f;

        /// <summary>"Gentle: 4 px, 0.011/px, 8/s" (the debug label, captures).</summary>
        public string Describe() => style == BoilStyle.Off || !Visible
            ? name + ": off"
            : name + ": " + style.ToString().ToLowerInvariant() + " " + strength.ToString("0.#") + " px, " + freq.ToString("0.####") + "/px, "
              + (style == BoilStyle.Boil ? rate.ToString("0.#") + "/s" : "speed " + speed.ToString("0.##"));

        public bool Same(in BoilSettings o) => style == o.style && strength == o.strength && freq == o.freq && rate == o.rate && speed == o.speed;
    }

    /// <summary>
    /// The battle sprite's boil (docs/ARGUMENT-PROTOTYPE.md, "The boil"): Lily's distortion filter from the scene 2
    /// prototype, ported to a shader (Resources/ArgumentShaders/ArgumentBoil) and tuned for Lena's 1024 x 1536 sprite,
    /// which stands about 860 px tall on a 1080p screen (Lily stood about 360 px tall on the 1280 x 720 stage).
    /// It distorts whatever <see cref="ArgumentArt.Sprite"/> returns, so it stacks with <see cref="ArgumentArt.SetTreatment"/>.
    ///
    /// Presets (F10 in the battle cycles them, the name and values shown under her): Gentle (the default), Subtle,
    /// Lily-equivalent (Lily's 4 px / 0.02 scaled by the two figures' heights), Strong, Slow and heavy, Off.
    /// Shift+F10 turns on <see cref="Reacting"/>: a little stronger and quicker when she's angry or rattled, calmer when softened.
    /// (F10 because the other function keys are taken: F1 the debug menu, F3/F4/F6/F7 movement, F5/F9 the Story
    /// template, F7/F8 the dream house's argument skips, F9 the opening editor; F11 is a browser's fullscreen.)
    /// </summary>
    public static class ArgumentBoil
    {
        /// <summary>The battle's boil key: cycles the presets; with Shift, toggles <see cref="Reacting"/>.</summary>
        public const Key CycleKey = Key.F10;
        public const string CycleKeyName = "F10", ReactKeyName = "Shift+F10";

        /// <summary>Lily's defaults in the scene 2 prototype (stage pixels, 1280 x 720).</summary>
        public static readonly BoilSettings Lily = new BoilSettings("Lily (scene 2)", BoilStyle.Boil, 4f, 0.02f, 8f, 0.6f);
        /// <summary>Lily's figure on her stage and Lena's in the battle (pixels tall): the scale between them.</summary>
        public const float LilyFigurePx = 362f, LenaFigurePx = 860f;

        public static readonly BoilSettings[] Presets =
        {
            new BoilSettings("Gentle", BoilStyle.Boil, 4f, 0.011f, 8f, 0.6f),
            new BoilSettings("Subtle", BoilStyle.Boil, 2.5f, 0.012f, 6f, 0.6f),
            new BoilSettings("Lily-equivalent", BoilStyle.Boil, Lily.strength * LenaFigurePx / LilyFigurePx, Lily.freq * LilyFigurePx / LenaFigurePx, Lily.rate, Lily.speed),
            new BoilSettings("Strong", BoilStyle.Boil, 8f, 0.016f, 12f, 0.6f),
            new BoilSettings("Slow and heavy", BoilStyle.Boil, 12f, 0.007f, 3f, 0.6f),
            new BoilSettings("Off", BoilStyle.Off, 0f, 0.011f, 8f, 0.6f),
        };

        static int _preset;
        static bool _reacting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _preset = 0;
            _reacting = false;
        }

        public static int PresetIndex => _preset;
        public static BoilSettings Preset => Presets[_preset];
        /// <summary>Whether her mood changes the boil (off by default: see <see cref="For"/>).</summary>
        public static bool Reacting => _reacting;

        public static void SetReacting(bool on) => _reacting = on;

        /// <summary>The next preset (wraps); returns it.</summary>
        public static BoilSettings Next()
        {
            _preset = (_preset + 1) % Presets.Length;
            return Preset;
        }

        /// <summary>Chooses a preset by name, case and spaces and dashes ignored ("lily", "slow-heavy"); false if none.</summary>
        public static bool Use(string name)
        {
            int i = Find(name);
            if (i < 0) return false;
            _preset = i;
            return true;
        }

        public static int Find(string name)
        {
            string key = NameKey(name);
            if (key.Length == 0) return -1;
            for (int i = 0; i < Presets.Length; i++)
            {
                string k = NameKey(Presets[i].name);
                if (k == key || k.StartsWith(key, StringComparison.Ordinal)) return i;
            }
            return -1;
        }

        static string NameKey(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var b = new System.Text.StringBuilder(s.Length);
            foreach (char c in s) if (char.IsLetterOrDigit(c)) b.Append(char.ToLowerInvariant(c));
            string k = b.ToString();
            return k.StartsWith("slowheavy", StringComparison.Ordinal) ? "slowandheavy" + k.Substring(9) : k;
        }

        /// <summary>The settings for an expression: the preset, changed by her mood only when <see cref="Reacting"/>.</summary>
        public static BoilSettings For(string expression) => _reacting ? React(Preset, expression) : Preset;

        /// <summary>The mood's change: angry or rattled boils harder and quicker, antagonistic a little; softened
        /// calmer and slower; hurt and pleading a touch quieter.</summary>
        public static BoilSettings React(BoilSettings s, string expression)
        {
            float k = 1f, r = 1f;
            switch (expression)
            {
                case "angry": k = 1.4f; r = 1.25f; break;
                case "rattled": k = 1.3f; r = 1.5f; break;
                case "antagonistic": k = 1.15f; break;
                case "softened": k = 0.6f; r = 0.75f; break;
                case "hurt":
                case "pleading": k = 0.85f; break;
            }
            s.strength *= k;
            s.rate *= r;
            return s;
        }

        /// <summary>
        /// What the shader needs, for a sprite of <paramref name="texture"/> pixels shown at <paramref name="shownPx"/>
        /// (1080p pixels): xy, the largest push as a share of the sprite's width and height; zw, the noise's cycles
        /// across its width and height.
        /// </summary>
        public static Vector4 ShaderScale(in BoilSettings s, Vector2 texture, Vector2 shownPx)
        {
            if (shownPx.x <= 0f || shownPx.y <= 0f) shownPx = texture;
            return new Vector4(s.strength / shownPx.x, s.strength / shownPx.y, s.freq * shownPx.x, s.freq * shownPx.y);
        }

        /// <summary>How big a <paramref name="texture"/>-pixel sprite is shown in a <paramref name="slot"/> (fit inside, as the view does).</summary>
        public static Vector2 Shown(Vector2 texture, Vector2 slot)
        {
            if (texture.x <= 0f || texture.y <= 0f || slot.x <= 0f || slot.y <= 0f || float.IsNaN(slot.x) || float.IsNaN(slot.y)) return texture;
            float k = Mathf.Min(slot.x / texture.x, slot.y / texture.y);
            return texture * k;
        }

        /// <summary>The boil's step at <paramref name="time"/>: a new seed <c>rate</c> times a second.</summary>
        public static int Step(float time, float rate) => Mathf.FloorToInt(time * Mathf.Max(0.1f, rate));

        /// <summary>Breathe's frequencies at <paramref name="time"/> (two slow waves, as in Lily's page).</summary>
        public static Vector2 BreatheFreq(in BoilSettings s, float time, float phase) =>
            new Vector2(s.freq * (1f + 0.35f * Mathf.Sin(time * s.speed + phase)), s.freq * (1f + 0.35f * Mathf.Cos(time * s.speed * 0.8f + phase)));
    }

    /// <summary>
    /// Draws the boiled sprite into a render texture the view shows instead of the sprite. Boil re-draws only when its
    /// step changes (<c>rate</c> times a second) or something else does; breathe every frame. Without a graphics device
    /// (batch test runs) or the shader, it hands back the plain sprite.
    /// </summary>
    public sealed class ArgumentBoilLayer : IDisposable
    {
        public const string ShaderPath = "ArgumentShaders/ArgumentBoil";
        static readonly int MainTex = Shader.PropertyToID("_MainTex"), Scale = Shader.PropertyToID("_BoilScale"), Seed = Shader.PropertyToID("_BoilSeed");

        Material _material;
        RenderTexture _target;
        Texture _source;
        BoilSettings _last;
        Vector2 _lastShown;
        int _step = int.MinValue;
        float _time;
        bool _tried;

        /// <summary>The shader is there and there's a graphics device to run it on.</summary>
        public static bool Available => SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;

        Texture _shown;

        /// <summary>The last texture handed back (the render texture while boiling).</summary>
        public Texture Shown => _shown;

        /// <summary>Advances the boil and returns what to show: the boiled render texture, or the sprite itself.</summary>
        public Texture Tick(float dt, Texture2D sprite, Vector2 shownPx, in BoilSettings s)
        {
            _time += dt;
            _shown = Draw(sprite, shownPx, s) ? _target : sprite;
            return _shown;
        }

        bool Draw(Texture2D sprite, Vector2 shownPx, in BoilSettings s)
        {
            if (sprite == null || !s.Visible || !Available || !EnsureMaterial()) return false;
            if (_target == null || _target.width != sprite.width || _target.height != sprite.height)
            {
                Release();
                _target = new RenderTexture(sprite.width, sprite.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                {
                    name = "Argument boil", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                };
                _target.Create();
                _step = int.MinValue;
            }
            var texSize = new Vector2(sprite.width, sprite.height);
            int step = s.style == BoilStyle.Boil ? ArgumentBoil.Step(_time, s.rate) : _step + 1;
            if (step == _step && ReferenceEquals(sprite, _source) && s.Same(_last) && shownPx == _lastShown) return true;
            _step = step;
            _source = sprite;
            _last = s;
            _lastShown = shownPx;
            var scale = ArgumentBoil.ShaderScale(s, texSize, shownPx);
            float seed = step;
            if (s.style == BoilStyle.Breathe)
            {
                var f = ArgumentBoil.BreatheFreq(s, _time, 0f);
                var shown = shownPx.x > 0f ? shownPx : texSize;
                scale.z = f.x * shown.x;
                scale.w = f.y * shown.y;
                seed = 1f;
            }
            _material.SetTexture(MainTex, sprite);
            _material.SetVector(Scale, scale);
            _material.SetFloat(Seed, seed);
            Graphics.Blit(sprite, _target, _material);
            return true;
        }

        bool EnsureMaterial()
        {
            if (_material != null) return true;
            if (_tried) return false;
            _tried = true;
            var shader = Resources.Load<Shader>(ShaderPath);
            if (shader == null || !shader.isSupported) return false;
            _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return true;
        }

        void Release()
        {
            if (_target == null) return;
            _target.Release();
            Destroy(_target);
            _target = null;
        }

        public void Dispose()
        {
            Release();
            if (_material != null) Destroy(_material);
            _material = null;
        }

        static void Destroy(UnityEngine.Object o)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }
    }
}
