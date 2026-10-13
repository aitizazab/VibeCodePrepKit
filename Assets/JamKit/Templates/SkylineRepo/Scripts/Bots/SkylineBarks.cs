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
                    return Pick(bark, "Halt! Mall policy!", "Assault on an officer!", "Suspect has left the premises.", "Code... red...");
                case SkylineEnemies.FloorCareId:
                    return Pick(bark, "Oh! A stain!", "Rude!", "All clean!", "Out of... order...");
                case SkylineEnemies.SpotlightId:
                    return Pick(bark, "Target acquired. Smile.", "Lens damage reported.", "Scanning.", "Lights... out.");
                case SkylineEnemies.GondolaId:
                    return Pick(bark, "Mind the squeegee!", "Window's dirty!", "Shift's over.", "Hanging... up...");
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
            }
            return bubble;
        }
    }

    /// <summary>A bark's bubble: a pale card with dark text that follows its bot and faces the camera, gone after
    /// <see cref="SkylineBarks.BubbleSeconds"/>.</summary>
    public sealed class SkylineBarkBubble : MonoBehaviour
    {
        const float CharWidth = 0.075f, Height = 0.26f;
        static Font _font;

        Transform _anchor;
        Vector3 _offset;
        float _left;

        public static SkylineBarkBubble Create(Transform anchor, Vector3 offset, string text)
        {
            var go = new GameObject("Bark");
            var b = go.AddComponent<SkylineBarkBubble>();
            b._anchor = anchor;
            b._offset = offset;
            b._left = SkylineBarks.BubbleSeconds;
            float w = Mathf.Max(0.5f, text.Length * CharWidth + 0.15f);
            SkylineBotLook.Part(PrimitiveType.Quad, "Card", go.transform, new Vector3(0f, 0f, 0.01f), new Vector3(w, Height, 1f), new Color(0.97f, 0.96f, 0.9f), false);
            SkylineBotLook.Part(PrimitiveType.Quad, "Tail", go.transform, new Vector3(0f, -Height * 0.5f - 0.03f, 0.011f), new Vector3(0.08f, 0.08f, 1f), new Color(0.97f, 0.96f, 0.9f), false, new Vector3(0f, 0f, 45f));
            var t = new GameObject("Text");
            t.transform.SetParent(go.transform, false);
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
            tm.characterSize = 0.022f;
            tm.fontSize = 64;
            tm.color = new Color(0.1f, 0.1f, 0.14f);
            b.Follow();
            return b;
        }

        void LateUpdate()
        {
            _left -= Time.deltaTime;
            if (_left <= 0f || _anchor == null) { Destroy(gameObject); return; }
            Follow();
        }

        void Follow()
        {
            if (_anchor == null) return;
            transform.position = _anchor.position + _offset;
            var cam = Camera.main;
            if (cam != null) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, Vector3.up);
        }
    }
}
