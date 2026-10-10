using JamKit.Movement;
using UnityEngine;

namespace JamKit.MovementLab
{
    /// <summary>
    /// One piece the dream tool changes (core <c>DreamToolCourse.pieces[index]</c>), built by the MovementLabBuilder: a
    /// glass tile (solid until shot: it bursts and is gone), a built piece (a bridge that unfolds from its near end, a
    /// wall that rises from below), or a pink or blue platform (solid in its colour, a faint ghost in the other). The
    /// collider is on this object at full size and switches at once; only the child <see cref="visual"/> animates, so
    /// what you stand on never moves. <see cref="Set"/> is called by <see cref="DreamToolRun"/>.
    /// </summary>
    public sealed class DreamToolPieceView : MonoBehaviour
    {
        public int index;
        public DreamPieceRole role;
        /// <summary>A built piece that rises from below (a wall) rather than unfolding along +Z (a bridge).</summary>
        public bool rises;
        public Collider solidCollider;
        public Transform visual;
        public Renderer[] renderers;
        /// <summary>The coloured pieces' look when solid and when not (a ghost).</summary>
        public Material solidMaterial, ghostMaterial;

        const float PopTime = 0.2f, BuildTime = 0.4f, FlipTime = 0.18f;
        static readonly int ColorId = Shader.PropertyToID("_Color");

        bool _solid = true, _set;
        float _t = 1f;
        Vector3 _scale, _pos;
        bool _home;
        MaterialPropertyBlock _block;
        Color[] _base;

        public bool Solid => _solid;
        /// <summary>True once any appear or vanish animation has finished.</summary>
        public bool Settled => _t >= 1f;

        void Remember()
        {
            if (_home || visual == null) return;
            _home = true;
            _scale = visual.localScale;
            _pos = visual.localPosition;
            _block = new MaterialPropertyBlock();
            _base = new Color[renderers != null ? renderers.Length : 0];
            for (int i = 0; i < _base.Length; i++)
                _base[i] = renderers[i] != null && renderers[i].sharedMaterial != null && renderers[i].sharedMaterial.HasProperty(ColorId) ? renderers[i].sharedMaterial.GetColor(ColorId) : Color.white;
        }

        void Awake() => Remember();

        /// <summary>Solid or not; <paramref name="instant"/> skips the animation (respawns, chapter jumps).</summary>
        public void Set(bool solid, bool instant)
        {
            Remember();
            if (_set && solid == _solid && !instant) return;
            _set = true;
            _solid = solid;
            if (solidCollider != null) solidCollider.enabled = solid;
            _t = instant ? 1f : 0f;
            if (role == DreamPieceRole.Pink || role == DreamPieceRole.Blue)
                foreach (var r in renderers) if (r != null) r.sharedMaterial = solid ? solidMaterial : ghostMaterial;
            Pose();
        }

        void Update()
        {
            if (_t >= 1f) return;
            float len = role == DreamPieceRole.Tile ? PopTime : role == DreamPieceRole.Built ? BuildTime : FlipTime;
            _t = Mathf.Min(1f, _t + Time.deltaTime / len);
            Pose();
        }

        void Pose()
        {
            if (visual == null) return;
            float k = Mathf.SmoothStep(0f, 1f, _t);
            switch (role)
            {
                case DreamPieceRole.Tile:
                    // Solid: whole. Shot: swells, fades and is gone.
                    if (_solid) { Show(true); visual.localScale = _scale; Tint(1f); }
                    else if (_t >= 1f) Show(false);
                    else { Show(true); visual.localScale = Vector3.Scale(_scale, new Vector3(1f + 0.25f * k, 1f + 0.25f * k, 1f + 2f * k)); Tint(1f - k); }
                    break;
                case DreamPieceRole.Built:
                    if (!_solid) { Show(false); break; }
                    Show(true);
                    if (rises)
                    {
                        // Up from below: the wall slides up into place.
                        visual.localScale = _scale;
                        visual.localPosition = _pos + Vector3.down * (_scale.y * 0.35f * (1f - k));
                    }
                    else
                    {
                        // Unfolds from its near end along +Z.
                        float z = Mathf.Max(0.02f, k);
                        visual.localScale = new Vector3(_scale.x, _scale.y, _scale.z * z);
                        visual.localPosition = _pos + Vector3.back * (_scale.z * 0.5f * (1f - z));
                    }
                    break;
                default:
                    // A colour flip: a small pop as it changes.
                    Show(true);
                    visual.localScale = _scale * (1f + 0.05f * Mathf.Sin(k * Mathf.PI));
                    break;
            }
        }

        void Show(bool on)
        {
            foreach (var r in renderers) if (r != null && r.enabled != on) r.enabled = on;
        }

        void Tint(float alpha)
        {
            if (renderers == null || _block == null) return;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                if (alpha >= 1f) { r.SetPropertyBlock(null); continue; }
                var c = _base[i];
                c.a *= alpha;
                r.GetPropertyBlock(_block);
                _block.SetColor(ColorId, c);
                r.SetPropertyBlock(_block);
            }
        }
    }
}
