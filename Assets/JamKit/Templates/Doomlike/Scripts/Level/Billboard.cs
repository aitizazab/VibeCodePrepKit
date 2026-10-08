using UnityEngine;

namespace JamKit.Doomlike
{
    /// <summary>
    /// A flat sprite that turns to face the camera's direction (screen-aligned, the classic look):
    /// one quad, its sheet cell, tint and hit flash set through a MaterialPropertyBlock so every
    /// billboard shares one material per texture. DoomGame calls <see cref="Face"/> each frame.
    /// </summary>
    public sealed class Billboard : MonoBehaviour
    {
        static readonly int FrameId = Shader.PropertyToID("_Frame");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int FlashId = Shader.PropertyToID("_FlashColor");
        static readonly int GlowId = Shader.PropertyToID("_Glow");
        static readonly int TexId = Shader.PropertyToID("_MainTex");

        public MeshRenderer quad;
        MaterialPropertyBlock _block;
        Vector4 _frame = new Vector4(0f, 0f, 1f, 1f);
        Color _tint = Color.white;
        Color _flash = new Color(1f, 1f, 1f, 0f);
        float _glow;
        Texture _texture;
        bool _dirty = true;

        /// <summary>Makes a billboard under <paramref name="parent"/> at <paramref name="position"/>
        /// (its feet), <paramref name="width"/> x <paramref name="height"/> metres.</summary>
        public static Billboard Create(string name, Transform parent, Vector3 position, float width, float height,
            Material material, Texture texture, Color tint, float glow = 0f)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var q = new GameObject("Quad");
            q.transform.SetParent(root.transform, false);
            q.transform.localScale = new Vector3(width, height, 1f);
            q.AddComponent<MeshFilter>().sharedMesh = MeshKit.BillboardQuad;
            var mr = q.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var b = root.AddComponent<Billboard>();
            b.quad = mr;
            b._texture = texture;
            b._tint = tint;
            b._glow = glow;
            b.Apply();
            return b;
        }

        public void SetSize(float width, float height)
        {
            if (quad != null) quad.transform.localScale = new Vector3(width, height, 1f);
        }

        public void SetFrame(Vector4 frame)
        {
            if (frame == _frame) return;
            _frame = frame;
            _dirty = true;
        }

        public void SetTint(Color c)
        {
            if (c == _tint) return;
            _tint = c;
            _dirty = true;
        }

        /// <summary>amount 0 = normal, 1 = solid colour.</summary>
        public void SetFlash(Color c, float amount)
        {
            c.a = Mathf.Clamp01(amount);
            if (c == _flash) return;
            _flash = c;
            _dirty = true;
        }

        public void SetTexture(Texture t)
        {
            if (t == _texture) return;
            _texture = t;
            _dirty = true;
        }

        public void SetVisible(bool visible)
        {
            if (quad != null && quad.enabled != visible) quad.enabled = visible;
        }

        /// <summary>Turns to the camera's yaw (degrees) and applies any changed look.</summary>
        public void Face(float cameraYaw)
        {
            transform.rotation = Quaternion.Euler(0f, cameraYaw, 0f);
            if (_dirty) Apply();
        }

        void Apply()
        {
            _dirty = false;
            if (quad == null) return;
            if (_block == null) _block = new MaterialPropertyBlock();
            quad.GetPropertyBlock(_block);
            _block.SetVector(FrameId, _frame);
            _block.SetColor(ColorId, _tint);
            _block.SetColor(FlashId, _flash);
            _block.SetFloat(GlowId, _glow);
            if (_texture != null) _block.SetTexture(TexId, _texture);
            quad.SetPropertyBlock(_block);
        }
    }
}
