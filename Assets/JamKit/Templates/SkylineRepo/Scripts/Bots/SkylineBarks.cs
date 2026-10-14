using JamKit.Skyline;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>The bots' barks (2.5), one per BotBrain bark id: "spotted", "hurt", "lost", "wrecked". DRAFTS for the creator to
    /// rewrite (one file, as the plan asks).</summary>
    public static class SkylineBarks
    {
        public const float BubbleSeconds = 1.5f;

        public static string Line(string botId, string bark)
        {
            switch (botId)
            {
                case SkylineEnemies.DroneId:
                    return Pick(bark, "Halt! Mall security!", "That's a citation!", "Suspect fled. Noted.", "Officer... down...");
                case SkylineEnemies.FloorCareId:
                    return Pick(bark, "Ooh! A stain!", "Rude! I'm cleaning!", "Spotless again!", "Out of... order...");
                case SkylineEnemies.SpotlightId:
                    return Pick(bark, "Lights on you. Smile.", "My lens! My LENS!", "Searching the dark.", "Lights... out...");
                case SkylineEnemies.GondolaId:
                    return Pick(bark, "Oi! Off my glass!", "Watch the bucket!", "Back to the panes.", "Hanging... up...");
                default:
                    return null;
            }
        }

        static string Pick(string bark, string spotted, string hurt, string lost, string wrecked)
        {
            switch (bark)
            {
                case "spotted": return spotted;
                case "hurt": return hurt;
                case "lost": return lost;
                case "wrecked": return wrecked;
                default: return null;
            }
        }

        /// <summary>The bark ids (BotBrain's).</summary>
        public static readonly string[] Ids = { "spotted", "hurt", "lost", "wrecked" };

        /// <summary>Synthesizes (and caches) the babble of one bark of the bot <paramref name="botId"/> seeded
        /// <paramref name="seed"/> (the bot's own seed, as <see cref="SkylineBot"/> barks with), so the bark itself costs
        /// nothing mid-fight. Returns false when there is no such line.</summary>
        public static bool Prepare(string botId, string bark, int seed)
        {
            string line = Line(botId, bark);
            if (string.IsNullOrEmpty(line)) return false;
            SkylineBabble.Make(botId, line, seed);
            return true;
        }

        /// <summary>Shows <paramref name="text"/> in a small world-space bubble above <paramref name="anchor"/> for
        /// <see cref="BubbleSeconds"/> and speaks it in <paramref name="voice"/>'s babble from <paramref name="source"/>.</summary>
        public static SkylineBarkBubble Say(Transform anchor, Vector3 offset, string text, string voice, int seed, AudioSource source, SkylineBarkBubble previous)
        {
            if (string.IsNullOrEmpty(text) || anchor == null) return previous;
            if (previous != null) Object.Destroy(previous.gameObject);
            var bubble = SkylineBarkBubble.Create(anchor, offset, text);
            if (source != null)
            {
                var clip = SkylineBabble.Make(voice, text, seed);
                if (clip != null) source.PlayOneShot(clip, 0.9f);
                if (clip != null) SkylineSounds.NoteSpeech(source, clip.length);   // the mix ducks the effects under the bark
            }
            return bubble;
        }
    }

    /// <summary>A bark's bubble that follows its bot for its reading time (judge round 1: it was drawn in the world, so the
    /// viewmodel covered it, and it was a flat grey card too small to read). With a HUD it is drawn on the HUD (a cream card
    /// with a navy frame and a tail, 17-24 px text by distance, hidden while a wall is between it and the camera, fading in
    /// and out); without one (or for the prewarm's tiny copy) it is a world-space card facing the camera.</summary>
    public sealed class SkylineBarkBubble : MonoBehaviour
    {
        const float CharWidth = 0.095f, Height = 0.33f, FadeIn = 0.12f, FadeOut = 0.25f, ReadLead = 0.6f, CharsPerSecond = 14f;
        static Font _font;

        Transform _anchor;
        Vector3 _offset;
        float _left, _alpha;
        SkylineHud.BarkTag _tag;

        public static SkylineBarkBubble Create(Transform anchor, Vector3 offset, string text)
        {
            var go = new GameObject("Bark");
            var b = go.AddComponent<SkylineBarkBubble>();
            b._anchor = anchor;
            b._offset = offset;
            b._left = Mathf.Max(SkylineBarks.BubbleSeconds, ReadLead + (text?.Length ?? 0) / CharsPerSecond);
            var hud = SkylineHud.Instance;
            bool tiny = anchor != null && anchor.lossyScale.x < 0.05f;   // the prewarm's copy warms the world card
            if (hud != null && !tiny) b._tag = hud.ShowBark(text);
            if (b._tag == null) b.BuildWorldCard(text);
            b.Follow();
            return b;
        }

        void BuildWorldCard(string text)
        {
            float w = Mathf.Max(0.5f, text.Length * CharWidth + 0.15f);
            var edge = new Color(0.13f, 0.15f, 0.24f);
            var paper = new Color(0.98f, 0.96f, 0.9f);
            SkylineBotLook.Part(PrimitiveType.Quad, "Frame", transform, new Vector3(0f, 0f, 0.012f), new Vector3(w + 0.04f, Height + 0.04f, 1f), edge, false);
            SkylineBotLook.Part(PrimitiveType.Quad, "Card", transform, new Vector3(0f, 0f, 0.01f), new Vector3(w, Height, 1f), paper, false);
            SkylineBotLook.Part(PrimitiveType.Quad, "Tail", transform, new Vector3(0f, -Height * 0.5f - 0.03f, 0.011f), new Vector3(0.08f, 0.08f, 1f), paper, false, new Vector3(0f, 0f, 45f));
            var t = new GameObject("Text");
            t.transform.SetParent(transform, false);
            var tm = t.AddComponent<TextMesh>();
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font != null)
            {
                tm.font = _font;
                t.GetComponent<MeshRenderer>().sharedMaterial = _font.material;
            }
            tm.text = text;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.characterSize = 0.028f;
            tm.fontSize = 64;
            tm.fontStyle = FontStyle.Bold;
            tm.color = edge;
        }

        void LateUpdate()
        {
            _left -= Time.deltaTime;
            if (_left <= 0f || _anchor == null) { Destroy(gameObject); return; }
            Follow();
        }

        void OnDestroy()
        {
            var hud = SkylineHud.Instance;
            if (hud != null && _tag != null) hud.HideBark(_tag);
            _tag = null;
        }

        void Follow()
        {
            if (_anchor == null) return;
            transform.position = _anchor.position + _offset;
            var hud = SkylineHud.Instance;
            var cam = hud != null && hud.player != null && hud.player.Camera != null ? hud.player.Camera : Camera.main;
            if (cam == null) return;
            if (_tag == null)
            {
                transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, Vector3.up);
                return;
            }
            // on the HUD: hidden while something stands between the camera and the bubble (as the world card was)
            Vector3 eye = cam.transform.position, d = transform.position - eye;
            float dist = d.magnitude;
            bool seen = dist < 1e-3f || !SkylineBot.CastClear(eye, d / dist, Mathf.Max(0f, dist - 0.3f), _anchor, out _);
            float dt = Time.unscaledDeltaTime;
            bool on = seen && _left > FadeOut;
            _alpha = on ? Mathf.Min(1f, _alpha + dt / FadeIn) : Mathf.Max(0f, _alpha - dt / FadeOut);
            if (hud != null) hud.PlaceBark(_tag, transform.position, cam, _alpha);
        }
    }
}
