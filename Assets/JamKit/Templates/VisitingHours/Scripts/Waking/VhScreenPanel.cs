using JamKit.Shell;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// A UI Toolkit panel drawn into a texture, for a screen in the 3D world (the tablet's news video and
    /// its headline, the phone's call screens): the same UI as the menus (the shell's theme and fonts),
    /// shown on a mesh through <see cref="Texture"/>. Without a graphics device (the -nographics smoke
    /// run) nothing is drawn and <see cref="Texture"/> is null.
    /// </summary>
    public sealed class VhScreenPanel
    {
        readonly GameObject _go;
        readonly PanelSettings _settings;
        readonly RenderTexture _rt;
        public readonly VisualElement Root;

        public Texture Texture => _rt;
        public bool Drawn => _rt != null;

        public VhScreenPanel(Transform parent, string name, int width, int height, StyleSheet style)
        {
            Root = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            Root.style.position = Position.Absolute;
            Root.style.left = 0;
            Root.style.top = 0;
            Root.style.width = width;
            Root.style.height = height;
            Root.style.overflow = Overflow.Hidden;
            if (style != null) Root.styleSheets.Add(style);
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;

            var shell = Resources.Load<ShellAssets>(ShellAssets.ResourcePath);
            _settings = shell != null && shell.panelSettings != null ? Object.Instantiate(shell.panelSettings) : ScriptableObject.CreateInstance<PanelSettings>();
            _settings.name = name + " panel";
            _rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = name, useMipMap = true, autoGenerateMips = true, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            _rt.Create();
            _settings.targetTexture = _rt;
            _settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            _settings.scale = 1f;
            _settings.clearColor = true;
            _settings.colorClearValue = Color.black;
            _settings.sortingOrder = -10;

            _go = new GameObject(name);
            _go.SetActive(false);
            _go.transform.SetParent(parent, false);
            _doc = _go.AddComponent<UIDocument>();
            _doc.panelSettings = _settings;
            _shell = shell;
            _go.SetActive(true);
            Attach();
        }

        UIDocument _doc;
        ShellAssets _shell;
        bool _drawing = true;

        /// <summary>The panel is drawing into its texture (false while its screen is out of view).</summary>
        public bool Drawing => _drawing;

        /// <summary>
        /// Stops (or restarts) drawing the panel into its texture: off while nothing shows its screen (docs/redesign/perf.md,
        /// Day item 6: the title's panels redrew off camera). The texture keeps its last picture; the UI is put back as it was.
        /// </summary>
        public void SetDrawing(bool on)
        {
            if (_go == null || _drawing == on) return;
            _drawing = on;
            _go.SetActive(on);
            if (on) Attach();
        }

        void Attach()
        {
            var root = _doc != null ? _doc.rootVisualElement : null;
            if (root == null) return;
            if (_shell != null)
                foreach (var sheet in _shell.styleSheets)
                    if (sheet != null && !root.styleSheets.Contains(sheet)) root.styleSheets.Add(sheet);
            root.pickingMode = PickingMode.Ignore;
            if (Root.parent != root) root.Add(Root);
        }

        public void Dispose()
        {
            if (_go != null) VhUtil.Kill(_go);
            if (_rt != null)
            {
                _rt.Release();
                VhUtil.Kill(_rt);
            }
            if (_settings != null) VhUtil.Kill(_settings);
        }
    }
}
