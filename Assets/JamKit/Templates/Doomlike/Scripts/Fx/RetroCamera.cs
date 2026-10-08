using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.Doomlike
{
    /// <summary>
    /// The "pixel look": the world camera renders into a small point-filtered RenderTexture (about
    /// 480 x 270, the width following the window's shape) and a second camera shows it full screen
    /// through the screen shader (optional colour steps with dither). The shell's UI draws on top at
    /// full resolution. Off = the world camera renders straight to the screen. Skipped when there's no
    /// graphics device (batch mode, the smoke test).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class RetroCamera : MonoBehaviour
    {
        static readonly int LevelsId = Shader.PropertyToID("_Levels");
        static readonly int DitherId = Shader.PropertyToID("_Dither");

        public Material screenMaterial;
        public RetroLook look = new RetroLook();

        Camera _world;
        RenderTexture _rt;
        GameObject _display;
        Material _material;
        MeshRenderer _quad;
        bool _on;
        int _w, _h;

        public bool IsOn => _on;
        public RenderTexture Target => _rt;

        static bool HasGraphics => SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;

        void Awake() => _world = GetComponent<Camera>();

        /// <summary>Turns the pixel look on or off (the Settings toggle).</summary>
        public void SetOn(bool on)
        {
            if (!HasGraphics || screenMaterial == null) on = false;
            if (on == _on && (!on || _rt != null)) { if (on) Refresh(); return; }
            _on = on;
            if (on) Refresh();
            else Release();
        }

        void LateUpdate()
        {
            if (_on) Refresh();
        }

        void Refresh()
        {
            int h = Mathf.Clamp(look.pixelHeight, 90, 1080);
            int w = Mathf.Max(16, Mathf.RoundToInt(h * (Screen.height > 0 ? Screen.width / (float)Screen.height : 16f / 9f)));
            if (_rt == null || w != _w || h != _h)
            {
                ReleaseTexture();
                _w = w;
                _h = h;
                _rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32)
                {
                    name = "Doomlike pixel look",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    useMipMap = false,
                };
                _rt.Create();
            }
            EnsureDisplay();
            _material.mainTexture = _rt;
            _material.SetFloat(LevelsId, Mathf.Max(0, look.colorLevels));
            _material.SetFloat(DitherId, look.dither);
            _world.targetTexture = _rt;
            float aspect = w / (float)h;
            _quad.transform.localScale = new Vector3(aspect, 1f, 1f);
        }

        void EnsureDisplay()
        {
            if (_display != null) return;
            _material = new Material(screenMaterial) { name = "Doomlike screen (runtime)" };
            _display = new GameObject("Pixel look display");
            _display.hideFlags = HideFlags.DontSave;
            // Far below the level, so the display camera sees nothing but its own quad.
            _display.transform.position = new Vector3(0f, -5000f, 0f);
            var camGo = new GameObject("Display camera");
            camGo.transform.SetParent(_display.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 0f, -1f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 0.5f;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 1.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.depth = _world.depth + 10f;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Picture";
            var col = quad.GetComponent<Collider>();
            if (col != null) Destroy(col);
            quad.transform.SetParent(_display.transform, false);
            _quad = quad.GetComponent<MeshRenderer>();
            _quad.sharedMaterial = _material;
            _quad.shadowCastingMode = ShadowCastingMode.Off;
        }

        void Release()
        {
            if (_world != null) _world.targetTexture = null;
            ReleaseTexture();
            if (_display != null) Destroy(_display);
            if (_material != null) Destroy(_material);
            _display = null;
            _material = null;
            _quad = null;
        }

        void ReleaseTexture()
        {
            if (_rt == null) return;
            if (_world != null && _world.targetTexture == _rt) _world.targetTexture = null;
            _rt.Release();
            Destroy(_rt);
            _rt = null;
        }

        void OnDestroy() => Release();
    }
}
