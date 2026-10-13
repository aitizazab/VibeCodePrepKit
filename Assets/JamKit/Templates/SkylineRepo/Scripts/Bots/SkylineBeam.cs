using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>The spotlight's telegraph drawn (2.5): a thin red bar from the Lamp to the beam's point while the
    /// <see cref="JamKit.Skyline.TelegraphedShot"/> follows; at the lock it turns white and widens; the shot itself is a short
    /// white flash along the same line. A bar of a scaled cube (no shader lookups), lit, not glowing.</summary>
    public sealed class SkylineBeam : MonoBehaviour
    {
        const float Thin = 0.018f, Wide = 0.06f, FlashSeconds = 0.08f;
        static readonly Color Red = new Color(1f, 0.12f, 0.1f), White = new Color(1f, 1f, 0.97f);

        Transform _bar;
        Renderer _renderer;
        float _flash;

        public static SkylineBeam Create(Transform parent)
        {
            var go = new GameObject("Beam");
            go.transform.SetParent(parent, false);
            var b = go.AddComponent<SkylineBeam>();
            var bar = SkylineBotLook.Visual(PrimitiveType.Cube, "Bar", go.transform, SkylineBotLook.Lit(Red), false);
            b._bar = bar.transform;
            b._renderer = bar.GetComponent<Renderer>();
            SkylineBotLook.Lit(White);
            bar.SetActive(false);
            return b;
        }

        /// <summary>Draws the telegraph this frame (call every frame while it runs).</summary>
        public void Draw(Vector3 from, Vector3 to, bool locked)
        {
            if (_flash > 0f) return;
            Place(from, to, locked ? Wide : Thin, locked ? White : Red);
        }

        /// <summary>The shot: a white flash along the line.</summary>
        public void Fire(Vector3 from, Vector3 to)
        {
            Place(from, to, Wide * 1.5f, White);
            _flash = FlashSeconds;
        }

        public void Hide()
        {
            if (_flash > 0f) return;
            if (_bar != null) _bar.gameObject.SetActive(false);
        }

        void Place(Vector3 from, Vector3 to, float width, Color c)
        {
            if (_bar == null) return;
            Vector3 d = to - from;
            float len = d.magnitude;
            if (!(len > 0.01f)) { _bar.gameObject.SetActive(false); return; }
            _bar.SetPositionAndRotation(from + d * 0.5f, Quaternion.LookRotation(d / len));
            var parentScale = transform.lossyScale;
            _bar.localScale = new Vector3(width / Mathf.Max(1e-4f, parentScale.x), width / Mathf.Max(1e-4f, parentScale.y), len / Mathf.Max(1e-4f, parentScale.z));
            _renderer.sharedMaterial = SkylineBotLook.Lit(c);
            _bar.gameObject.SetActive(true);
        }

        void Update()
        {
            if (_flash <= 0f) return;
            _flash -= Time.deltaTime;
            if (_flash <= 0f && _bar != null) _bar.gameObject.SetActive(false);
        }
    }
}
