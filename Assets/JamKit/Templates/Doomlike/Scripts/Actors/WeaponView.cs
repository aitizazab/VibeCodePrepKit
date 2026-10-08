using UnityEngine;

namespace JamKit.Doomlike
{
    /// <summary>
    /// The gun in your hands: a quad in front of the camera drawn over everything (the weapon
    /// material ignores depth), showing frame 0 of the weapon's strip at rest and the firing frames
    /// after each shot. It bobs with your stride, dips while swapping and kicks when firing. Being
    /// in the world, it gets the pixel look like everything else.
    /// </summary>
    public sealed class WeaponView : MonoBehaviour
    {
        static readonly int FrameId = Shader.PropertyToID("_Frame");
        static readonly int TexId = Shader.PropertyToID("_MainTex");

        const float Distance = 0.5f;

        public MeshRenderer quad;
        public Camera cam;
        MaterialPropertyBlock _block;
        WeaponAsset _weapon;
        float _fireT = 99f;
        float _kick;
        int _shownFrame = -1;

        public WeaponAsset Weapon => _weapon;

        public static WeaponView Create(Camera cam, Material material)
        {
            var go = new GameObject("Weapon view");
            go.transform.SetParent(cam.transform, false);
            var view = go.AddComponent<WeaponView>();
            view.cam = cam;
            var q = new GameObject("Quad");
            q.transform.SetParent(go.transform, false);
            q.AddComponent<MeshFilter>().sharedMesh = MeshKit.BillboardQuad;
            var mr = q.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            view.quad = mr;
            return view;
        }

        public void SetWeapon(WeaponAsset w)
        {
            if (w == _weapon) return;
            _weapon = w;
            _fireT = 99f;
            _shownFrame = -1;
            if (quad != null) quad.enabled = w != null && w.viewTexture != null;
        }

        public void Fired()
        {
            _fireT = 0f;
            _kick = 1f;
        }

        public void Tick(float dt, Vector2 bob, float lowered01, bool visible)
        {
            if (quad == null || cam == null) return;
            if (quad.enabled != (visible && _weapon != null && _weapon.viewTexture != null)) quad.enabled = visible && _weapon != null && _weapon.viewTexture != null;
            if (!quad.enabled) return;
            _fireT += dt;
            _kick = Mathf.MoveTowards(_kick, 0f, dt * 9f);

            float screenH = 2f * Distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var tex = _weapon.viewTexture;
            int frames = Mathf.Max(1, _weapon.viewFrames);
            float cellAspect = tex != null ? (tex.width / (float)frames) / Mathf.Max(1, tex.height) : 1f;
            float h = screenH * 0.42f * Mathf.Max(0.1f, _weapon.viewScale);
            float w = h * cellAspect;
            transform.localPosition = new Vector3(
                _weapon.viewOffset.x * screenH + bob.x,
                -screenH * 0.5f + _weapon.viewOffset.y * screenH + bob.y - lowered01 * h * 0.9f - _kick * h * 0.06f,
                Distance);
            quad.transform.localScale = new Vector3(w, h, 1f);

            int frame = 0;
            if (frames > 1 && _fireT < _weapon.fireFrameSeconds * (frames - 1))
                frame = 1 + Mathf.Min(frames - 2, (int)(_fireT / Mathf.Max(0.01f, _weapon.fireFrameSeconds)));
            if (frame == _shownFrame) return;
            _shownFrame = frame;
            if (_block == null) _block = new MaterialPropertyBlock();
            quad.GetPropertyBlock(_block);
            float fw = 1f / frames;
            _block.SetVector(FrameId, new Vector4(frame * fw, 0f, fw, 1f));
            if (tex != null) _block.SetTexture(TexId, tex);
            quad.SetPropertyBlock(_block);
        }
    }
}
