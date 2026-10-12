using UnityEngine;

namespace JamKit.DreamEscape
{
    /// <summary>
    /// Theo in the scene: his figure follows <see cref="TheoBrain"/> (the game sets <see cref="Place"/> each frame), with a
    /// child's bounce when he walks, his face in his arms while he counts (the figure dips), and a small meter over his
    /// head that fills as he sees her (gold, then red). The figure is the cast's cut-out until the People agent's model
    /// replaces it (it's a child of this object).
    /// </summary>
    public sealed class TheoView : MonoBehaviour
    {
        /// <summary>The figure (the cut-out, or a model).</summary>
        public Transform figure;
        /// <summary>The meter over his head: a quad that fills (its x scale), and its back.</summary>
        public Transform meterFill, meterBack;
        public Renderer meterRenderer;

        float _bounce, _dip;
        Vector3 _last;
        MaterialPropertyBlock _block;
        static readonly int ColorId = Shader.PropertyToID("_BaseColor"), ColorId2 = Shader.PropertyToID("_Color");

        public void Place(Vector3 world, float yaw, float meter, bool counting, bool moving)
        {
            float dt = Time.deltaTime;
            float speed = dt > 0f ? (world - _last).magnitude / dt : 0f;
            _last = world;
            _bounce += dt * Mathf.Lerp(0f, 11f, Mathf.Clamp01(speed / 2.5f));
            _dip = Mathf.MoveTowards(_dip, counting ? 0.35f : 0f, dt * 1.5f);
            transform.SetPositionAndRotation(world, Quaternion.Euler(0f, yaw, 0f));
            if (figure != null)
            {
                float hop = moving ? Mathf.Abs(Mathf.Sin(_bounce)) * 0.06f : 0f;
                figure.localPosition = new Vector3(0f, hop - _dip, 0f);
            }
            Meter(meter, counting);
        }

        void Meter(float v, bool counting)
        {
            bool show = v > 0.02f && !counting;
            if (meterBack != null) meterBack.gameObject.SetActive(show);
            if (meterFill == null) return;
            meterFill.gameObject.SetActive(show);
            if (!show) return;
            meterFill.localScale = new Vector3(Mathf.Max(0.001f, v) * 0.5f, 0.07f, 1f);
            meterFill.localPosition = new Vector3(-0.25f + v * 0.25f, meterFill.localPosition.y, -0.002f);
            if (meterRenderer == null) return;
            if (_block == null) _block = new MaterialPropertyBlock();
            var c = Color.Lerp(new Color(1f, 0.82f, 0.4f), new Color(0.95f, 0.25f, 0.2f), Mathf.InverseLerp(0.5f, 1f, v));
            _block.SetColor(ColorId, c);
            _block.SetColor(ColorId2, c);
            meterRenderer.SetPropertyBlock(_block);
        }

        void LateUpdate()
        {
            // The meter turns to the camera.
            var cam = Camera.main;
            if (cam == null || meterBack == null) return;
            var parent = meterBack.parent;
            if (parent == null) return;
            var d = cam.transform.position - parent.position;
            d.y = 0f;
            if (d.sqrMagnitude > 1e-4f) parent.rotation = Quaternion.LookRotation(-d, Vector3.up);
        }
    }
}
