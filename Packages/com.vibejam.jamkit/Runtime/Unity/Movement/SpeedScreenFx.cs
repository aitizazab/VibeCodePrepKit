using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace JamKit.Movement
{
    /// <summary>
    /// The speed visuals drawn over the whole screen (docs/SPEED-FEEL.md): the boom's shockwave ring and one-frame
    /// chromatic pulse (V1), the soft edge zoom blur at flying (V6) and the dream's brighter, more saturated grade
    /// (V7, set by the dream escape through <see cref="SetGrade"/>). One full-screen pass with one small shader
    /// (JamKit/Movement/SpeedScreen), injected into this camera's URP renderer only while something shows, so a
    /// still or slow frame costs nothing. No renderer asset is changed. Put it on the camera, after <see cref="SpeedFeel"/>.
    /// </summary>
    [DefaultExecutionOrder(60)]
    [RequireComponent(typeof(Camera))]
    public sealed class SpeedScreenFx : MonoBehaviour
    {
        public SpeedFeel feel;
        [Tooltip("A material using JamKit/Movement/SpeedScreen (the builder makes one, so the shader ships in builds).")]
        public Material material;
        [Tooltip("The edge blur's samples along the radius are fixed in the shader (8); this scales its reach on top of the tuning.")]
        [Range(0f, 2f)] public float blurReachScale = 1f;

        static readonly int ShockId = Shader.PropertyToID("_Shock");
        static readonly int ChromaId = Shader.PropertyToID("_Chroma");
        static readonly int BlurId = Shader.PropertyToID("_EdgeBlur");
        static readonly int GradeId = Shader.PropertyToID("_Grade");

        Camera _cam;
        Pass _pass;
        Material _mat;
        float _gradeSat, _gradeLift;
        bool _active;
        int _activeFrames;

        /// <summary>Whether the pass is queued this frame (tests).</summary>
        public bool Active => _active;
        /// <summary>Frames the pass has been queued (tests).</summary>
        public int ActiveFrames => _activeFrames;
        /// <summary>This frame's edge blur strength, chromatic split and ring push (tests).</summary>
        public float EdgeBlur => _blur;
        public float Chroma => _chroma;
        public float ShockStrength => _shock;
        /// <summary>The grade on top: extra saturation (0.25 = +25%) and brightness lift.</summary>
        public float GradeSaturation => _gradeSat;
        public float GradeLift => _gradeLift;
        float _blur, _chroma, _shock;

        /// <summary>V7: the dream's answer at speed (0, 0 for none). Visual only.</summary>
        public void SetGrade(float saturation, float lift)
        {
            _gradeSat = saturation;
            _gradeLift = lift;
        }

        void Awake()
        {
            _cam = GetComponent<Camera>();
            _pass = new Pass();
            // A copy, so play mode never edits the material asset.
            if (material != null) _mat = new Material(material) { name = material.name + " (live)" };
        }

        void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
        }

        void OnEnable() => RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            _active = false;
        }

        void LateUpdate()
        {
            _active = false;
            if (_mat == null || feel == null) return;
            var l = feel.Levels;
            var def = feel.Def;
            _shock = l.shockStrength;
            _chroma = l.chroma;
            _blur = l.edgeBlur;
            float aspect = _cam != null && _cam.pixelHeight > 0 ? (float)_cam.pixelWidth / _cam.pixelHeight : 16f / 9f;
            _mat.SetVector(ShockId, new Vector4(l.shockRadius, l.shockStrength, Mathf.Max(0.001f, l.shockWidth), aspect));
            _mat.SetVector(ChromaId, new Vector4(l.chroma, 0f, 0f, 0f));
            float spread = def != null ? def.edgeBlurSpread : 0.07f;
            float inner = def != null ? def.edgeBlurInner : 0.45f, outer = def != null ? def.edgeBlurOuter : 1f;
            _mat.SetVector(BlurId, new Vector4(l.edgeBlur * spread * blurReachScale, inner, Mathf.Max(inner + 0.01f, outer), 0f));
            _mat.SetVector(GradeId, new Vector4(_gradeSat, _gradeLift, 0f, 0f));
            _active = l.shockStrength > 1e-4f || l.chroma > 1e-5f || l.edgeBlur > 1e-3f || _gradeSat > 1e-3f || _gradeLift > 1e-3f;
            if (_active) _activeFrames++;
        }

        void OnBeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (!_active || camera != _cam || _mat == null) return;
            var data = camera.GetUniversalAdditionalCameraData();
            var renderer = data != null ? data.scriptableRenderer : null;
            if (renderer == null) return;
            _pass.material = _mat;
            renderer.EnqueuePass(_pass);
        }

        /// <summary>Copies the camera colour through the material into a new texture that becomes the camera colour.</summary>
        sealed class Pass : ScriptableRenderPass
        {
            public Material material;

            public Pass()
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (material == null) return;
                var res = frameData.Get<UniversalResourceData>();
                if (res.isActiveTargetBackBuffer) return;
                TextureHandle source = res.activeColorTexture;
                if (!source.IsValid()) return;
                var desc = renderGraph.GetTextureDesc(source);
                desc.name = "_SpeedScreenFx";
                desc.clearBuffer = false;
                TextureHandle dest = renderGraph.CreateTexture(desc);
                renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, dest, material, 0), "Speed screen fx");
                res.cameraColor = dest;
            }
        }
    }
}
