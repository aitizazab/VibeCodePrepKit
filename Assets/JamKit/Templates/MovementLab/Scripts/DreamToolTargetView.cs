using JamKit.Movement;
using UnityEngine;

namespace JamKit.MovementLab
{
    /// <summary>
    /// A dream-tool target as you see it (core <c>DreamToolCourse.targets[index]</c>): a socket is a gold ring with a
    /// glowing core that spins while it waits and dims once built; a switch is a diamond in the colour a hit will make
    /// solid; a glass tile's target is its pane (this draws only the lock frame around it). Locked: it swells and its
    /// lock frame shows. Hit: a white flash. Set by <see cref="DreamTool"/> and <see cref="DreamToolRun"/>.
    /// </summary>
    public sealed class DreamToolTargetView : MonoBehaviour
    {
        public int index;
        public DreamTargetKind kind;
        /// <summary>The part that spins, swells and flashes.</summary>
        public Transform visual;
        public Renderer[] renderers;
        /// <summary>Shown while the aim is locked on this target.</summary>
        public GameObject lockFrame;
        /// <summary>A switch's look per colour it will make solid (index: <see cref="DreamToolCourse.Pink"/>, <see cref="DreamToolCourse.Blue"/>).</summary>
        public Material[] switchMaterials;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        MaterialPropertyBlock _block;
        Vector3 _scale;
        bool _home, _locked, _done;
        float _flash, _lock;
        int _colour = -1;
        Color[] _base;

        public bool Locked => _locked;
        public bool Done => _done;

        void Remember()
        {
            if (_home || visual == null) return;
            _home = true;
            _scale = visual.localScale;
            _block = new MaterialPropertyBlock();
            _base = new Color[renderers != null ? renderers.Length : 0];
            for (int i = 0; i < _base.Length; i++)
                _base[i] = renderers[i] != null && renderers[i].sharedMaterial != null && renderers[i].sharedMaterial.HasProperty(ColorId) ? renderers[i].sharedMaterial.GetColor(ColorId) : Color.white;
        }

        void Awake()
        {
            Remember();
            if (lockFrame != null) lockFrame.SetActive(false);
        }

        public void SetLocked(bool on)
        {
            if (on == _locked) return;
            _locked = on;
            if (lockFrame != null) lockFrame.SetActive(on);
        }

        public void Flash() => _flash = 1f;

        /// <summary>A tile popped or a socket built (dim, still); and for a switch, the active colour (it shows the other).</summary>
        public void Show(bool done, int activeColour)
        {
            Remember();
            _done = done;
            if (kind == DreamTargetKind.Switch && activeColour != _colour && switchMaterials != null && switchMaterials.Length == 2)
            {
                _colour = activeColour;
                var mat = switchMaterials[1 - activeColour];
                foreach (var r in renderers) if (r != null) { r.sharedMaterial = mat; r.SetPropertyBlock(null); }
                for (int i = 0; i < _base.Length; i++) _base[i] = mat.HasProperty(ColorId) ? mat.GetColor(ColorId) : Color.white;
            }
            if (kind == DreamTargetKind.Tile && visual != null) visual.gameObject.SetActive(!done);
        }

        void Update()
        {
            if (visual == null) return;
            Remember();
            float dt = Time.deltaTime;
            _lock = Mathf.MoveTowards(_lock, _locked ? 1f : 0f, dt * 8f);
            _flash = Mathf.MoveTowards(_flash, 0f, dt * 5f);
            if (kind != DreamTargetKind.Tile)
            {
                float spin = _done ? 10f : 70f;
                visual.Rotate(0f, spin * dt, 0f, Space.Self);
                float pulse = _done ? 0.75f : 1f + 0.06f * Mathf.Sin(Time.time * 4f + index);
                visual.localScale = _scale * (pulse + 0.25f * _lock + 0.3f * _flash);
            }
            if (_flash > 0f)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    var r = renderers[i];
                    if (r == null) continue;
                    r.GetPropertyBlock(_block);
                    _block.SetColor(ColorId, Color.Lerp(_base[i], Color.white, _flash));
                    r.SetPropertyBlock(_block);
                }
            }
            else if (_wasFlashing) foreach (var r in renderers) if (r != null) r.SetPropertyBlock(null);
            _wasFlashing = _flash > 0f;
        }

        bool _wasFlashing;
    }
}
