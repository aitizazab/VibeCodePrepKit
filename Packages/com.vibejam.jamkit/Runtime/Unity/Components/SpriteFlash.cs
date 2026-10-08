using UnityEngine;

namespace JamKit.Shell
{
    /// <summary>
    /// Flashes a sprite (or any renderer) toward a colour for a moment, through a
    /// MaterialPropertyBlock so materials stay shared. Needs a material with the JamKit/SpriteFlash
    /// shader (Assets/JamKit/Art/SpriteFlash.mat); with any other material it tints the SpriteRenderer
    /// colour instead. Runs on real time, so it shows during hit stop.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpriteFlash : MonoBehaviour
    {
        static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
        static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");

        public Renderer target;

        MaterialPropertyBlock _block;
        bool _useShader;
        SpriteRenderer _sprite;
        Color _baseColor = Color.white;
        Color _color = Color.white;
        float _left, _duration;

        public bool IsFlashing => _left > 0f;

        void Awake()
        {
            if (target == null) target = GetComponent<Renderer>();
            _block = new MaterialPropertyBlock();
            _sprite = target as SpriteRenderer;
            _useShader = target != null && target.sharedMaterial != null && target.sharedMaterial.HasProperty(FlashAmountId);
        }

        public void Flash(Color color, float seconds = 0.12f)
        {
            if (target == null) return;
            if (!_useShader && _sprite != null && _left <= 0f) _baseColor = _sprite.color;
            _color = color;
            _duration = Mathf.Max(0.01f, seconds);
            _left = _duration;
            Apply(1f);
        }

        public void Flash() => Flash(Color.white);

        void Update()
        {
            if (_left <= 0f) return;
            _left -= Time.unscaledDeltaTime;
            Apply(_left > 0f ? _left / _duration : 0f);
        }

        void Apply(float k)
        {
            if (_useShader)
            {
                target.GetPropertyBlock(_block);
                _block.SetFloat(FlashAmountId, k);
                _block.SetColor(FlashColorId, _color);
                target.SetPropertyBlock(_block);
            }
            else if (_sprite != null)
            {
                var c = Color.Lerp(_baseColor, _color, k);
                c.a = _sprite.color.a;
                _sprite.color = c;
            }
        }

        void OnDisable()
        {
            if (_left > 0f) { _left = 0f; Apply(0f); }
        }
    }
}
