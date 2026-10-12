using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace JamKit.Look
{
    /// <summary>
    /// URP renderer feature (Render Graph) for Visiting Hours' atmosphere: height/distance haze and screen-space sun shafts,
    /// driven by the <see cref="VhAtmosphere"/> volume override and the scene's sun (RenderSettings.sun). Does nothing unless
    /// that override is active, so it is safe on the shared PC renderer (other templates don't use the override).
    /// Installed by JamKit.Look.EditorTools.VhLookSetup.InstallRendererFeatures. Shader: JamKit/Look/Atmosphere.
    /// <para>Passes (before post-processing, in HDR): shaft mask at quarter resolution (bright sky near the sun; skipped when
    /// the camera faces away from the sun), two 8-tap radial blurs towards the sun, then one full-resolution composite that
    /// adds the haze and the shafts into a new colour target, which becomes the camera colour.</para>
    /// <para>Pass 3 fix: the composite now binds the camera depth texture itself (a property block override of
    /// _CameraDepthTexture). Before, it relied on the global, which Render Graph doesn't guarantee inside a raster pass that
    /// doesn't declare it: by round 2's captures the depth most likely read as cleared (0 = sky): the haze never reached the far ring (round 2: "far
    /// hills sharp") and the shaft mask treated sunlit ground as sky.</para>
    /// </summary>
    public sealed class VhAtmosphereFeature : ScriptableRendererFeature
    {
        public Shader shader;
        public RenderPassEvent passEvent = RenderPassEvent.BeforeRenderingPostProcessing;

        Material _material;
        AtmospherePass _pass;
        static readonly int HorizonAwayId = Shader.PropertyToID("_VhHorizonAway");
        static readonly int HorizonSunId = Shader.PropertyToID("_VhHorizonSun");
        static readonly int HorizonFocusId = Shader.PropertyToID("_VhHorizonFocus");

        public override void Create()
        {
            _pass = new AtmospherePass { renderPassEvent = passEvent };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var type = renderingData.cameraData.cameraType;
            if (type != CameraType.Game && type != CameraType.SceneView) return;
            if (!renderingData.cameraData.postProcessEnabled) return;
            var stack = VolumeManager.instance.stack;
            var atmo = stack?.GetComponent<VhAtmosphere>();
            if (atmo == null || !atmo.IsActive())
            {
                Shader.SetGlobalVector(HorizonAwayId, Vector4.zero);   // the sky falls back to its own horizon colour
                return;
            }
            // The sky draws its horizon band in the haze's colours, so hazed far hills dissolve into it.
            var hcol = atmo.hazeColor.value;
            var scol = atmo.hazeSunColor.value;
            Shader.SetGlobalVector(HorizonAwayId, new Vector4(hcol.r, hcol.g, hcol.b, 1f));
            Shader.SetGlobalVector(HorizonSunId, new Vector4(scol.r, scol.g, scol.b, 1f));
            Shader.SetGlobalFloat(HorizonFocusId, atmo.hazeSunFocus.value);
            if (_material == null)
            {
                if (shader == null) shader = Shader.Find("JamKit/Look/Atmosphere");
                if (shader == null) return;
                _material = CoreUtils.CreateEngineMaterial(shader);
            }
            _pass.Setup(_material, atmo);
            _pass.ConfigureInput(ScriptableRenderPassInput.Depth);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
            _material = null;
        }

        sealed class AtmospherePass : ScriptableRenderPass
        {
            static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
            static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
            static readonly int ShaftTexId = Shader.PropertyToID("_VhShaftTex");
            static readonly int SunScreenId = Shader.PropertyToID("_VhSunScreen");
            static readonly int SunWorldDirId = Shader.PropertyToID("_VhAtmoSunDir");
            static readonly int HazeParamsId = Shader.PropertyToID("_VhHazeParams");
            static readonly int HazeParams2Id = Shader.PropertyToID("_VhHazeParams2");
            static readonly int FarHazeParamsId = Shader.PropertyToID("_VhFarHazeParams");
            static readonly int HazeColorId = Shader.PropertyToID("_VhHazeColor");
            static readonly int HazeSunColorId = Shader.PropertyToID("_VhHazeSunColor");
            static readonly int ShaftParamsId = Shader.PropertyToID("_VhShaftParams");
            static readonly int ShaftTintId = Shader.PropertyToID("_VhShaftTint");
            static readonly int BlurStepId = Shader.PropertyToID("_VhBlurStep");
            static readonly int CameraDepthId = Shader.PropertyToID("_CameraDepthTexture");
            static readonly int CamPosId = Shader.PropertyToID("_VhCamPos");
            static readonly int CamRightId = Shader.PropertyToID("_VhCamRight");
            static readonly int CamUpId = Shader.PropertyToID("_VhCamUp");
            static readonly int CamFwdId = Shader.PropertyToID("_VhCamFwd");
            static readonly int ZParamsId = Shader.PropertyToID("_VhZParams");

            const int PassMask = 0, PassBlur = 1, PassComposite = 2;

            Material _material;
            VhAtmosphere _atmo;
            Vector4 _farHaze;
            Vector4 _camPos, _camRight, _camUp, _camFwd, _zParams;
            static int _logged;

            sealed class PassData
            {
                public Material material;
                public TextureHandle source;
                public TextureHandle shafts;
                public TextureHandle depth;
                public int pass;
                public float blurStep;
                public Vector4 sunScreen, sunDir, hazeParams, hazeParams2, farHazeParams, hazeColor, hazeSunColor, shaftParams, shaftTint;
                public bool hasShafts;
                public bool hasDepth;
                public Vector4 camPos, camRight, camUp, camFwd, zParams;
            }

            static readonly MaterialPropertyBlock Props = new MaterialPropertyBlock();

            public void Setup(Material material, VhAtmosphere atmo)
            {
                _material = material;
                _atmo = atmo;
            }

            public AtmospherePass()
            {
                profilingSampler = new ProfilingSampler("VH Atmosphere");
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                if (resources.isActiveTargetBackBuffer || _material == null || _atmo == null) return;
                var cam = cameraData.camera;

                // The sun: RenderSettings.sun (VhLookRig sets it), else straight up (no shafts).
                Vector3 toSun = RenderSettings.sun != null ? -RenderSettings.sun.transform.forward : Vector3.up;
                Vector3 vp = cam.WorldToViewportPoint(cam.transform.position + toSun * 1000f);
                float facing = Vector3.Dot(cam.transform.forward, toSun);
                float aspect = cam.pixelHeight > 0 ? (float)cam.pixelWidth / cam.pixelHeight : 1.777f;
                var sunScreen = new Vector4(vp.x, vp.y, facing, aspect);
                var sunDir = new Vector4(toSun.x, toSun.y, toSun.z, 0f);
                // The camera for the haze's reconstruction (not the pass's matrices: see the shader).
                var ct = cam.transform;
                float tanY = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                _camPos = ct.position;
                _camRight = new Vector4(ct.right.x, ct.right.y, ct.right.z, tanY * aspect);
                _camUp = new Vector4(ct.up.x, ct.up.y, ct.up.z, tanY);
                _camFwd = ct.forward;
                float n = cam.nearClipPlane, f = cam.farClipPlane;
                _zParams = SystemInfo.usesReversedZBuffer
                    ? new Vector4(-1f + f / n, 1f, (-1f + f / n) / f, 1f / f)
                    : new Vector4(1f - f / n, f / n, (1f - f / n) / f, (f / n) / f);

                var a = _atmo;
                var hazeParams = new Vector4(a.hazeDensity.value, a.hazeBaseHeight.value, a.hazeHeightFalloff.value, a.hazeStart.value);
                var hazeParams2 = new Vector4(a.hazeSunFocus.value, a.hazeMaxOpacity.value, 0f, 0f);
                _farHaze = new Vector4(a.farHazeDensity.value, a.farHazeStart.value, a.farHazeHeightFalloff.value, a.farHazeMaxOpacity.value);
                var hc = a.hazeColor.value;
                var hs = a.hazeSunColor.value;
                var tint = a.shaftTint.value;
                bool shafts = a.ShaftsActive && facing > 0f;   // facing away: no shaft passes at all
                var shaftParams = new Vector4(shafts ? a.shaftIntensity.value : 0f, a.shaftThreshold.value, a.shaftLength.value, a.shaftRadius.value);

                var colorDesc = renderGraph.GetTextureDesc(resources.cameraColor);
                colorDesc.name = "_VhAtmosphereColor";
                colorDesc.clearBuffer = false;
                colorDesc.msaaSamples = MSAASamples.None;
                TextureHandle source = resources.activeColorTexture;
                TextureHandle depth = resources.cameraDepthTexture;
                if (_logged < 3)
                {
                    // A trace for the captures' log (round 3 showed no haze at all): is there a depth texture to read?
                    _logged++;
                    Debug.Log("JAMKIT_LOOK atmosphere: camera " + cam.name + ", depth " + (depth.IsValid() ? "bound" : "MISSING") +
                              ", haze " + a.hazeDensity.value + "/" + a.farHazeDensity.value + ", shafts " + (shafts ? "on" : "off"));
                }
                TextureHandle shaftTex = TextureHandle.nullHandle;

                if (shafts)
                {
                    var half = new TextureDesc(Mathf.Max(1, colorDesc.width / 4), Mathf.Max(1, colorDesc.height / 4))
                    {
                        format = GraphicsFormat.B10G11R11_UFloatPack32,
                        filterMode = FilterMode.Bilinear,
                        wrapMode = TextureWrapMode.Clamp,
                        clearBuffer = false,
                        name = "_VhShaftsA",
                    };
                    var texA = renderGraph.CreateTexture(half);
                    half.name = "_VhShaftsB";
                    var texB = renderGraph.CreateTexture(half);
                    AddPass(renderGraph, "VH Shafts Mask", source, depth, TextureHandle.nullHandle, texA, PassMask, 0f,
                        sunScreen, sunDir, hazeParams, hazeParams2, hc, hs, shaftParams, tint);
                    AddPass(renderGraph, "VH Shafts Blur 1", texA, TextureHandle.nullHandle, TextureHandle.nullHandle, texB, PassBlur, 1f,
                        sunScreen, sunDir, hazeParams, hazeParams2, hc, hs, shaftParams, tint);
                    AddPass(renderGraph, "VH Shafts Blur 2", texB, TextureHandle.nullHandle, TextureHandle.nullHandle, texA, PassBlur, 0.25f,
                        sunScreen, sunDir, hazeParams, hazeParams2, hc, hs, shaftParams, tint);
                    shaftTex = texA;
                }

                var dest = renderGraph.CreateTexture(colorDesc);
                AddPass(renderGraph, "VH Atmosphere Composite", source, depth, shaftTex, dest, PassComposite, 0f,
                    sunScreen, sunDir, hazeParams, hazeParams2, hc, hs, shaftParams, tint);
                resources.cameraColor = dest;
            }

            void AddPass(RenderGraph rg, string name, TextureHandle src, TextureHandle depth, TextureHandle shafts, TextureHandle dst,
                int pass, float blurStep, Vector4 sunScreen, Vector4 sunDir, Vector4 hazeParams, Vector4 hazeParams2,
                Color hazeColor, Color hazeSunColor, Vector4 shaftParams, Color shaftTint)
            {
                using (var builder = rg.AddRasterRenderPass<PassData>(name, out var data, profilingSampler))
                {
                    data.material = _material;
                    data.source = src;
                    data.shafts = shafts;
                    data.depth = depth;
                    data.pass = pass;
                    data.blurStep = blurStep;
                    data.sunScreen = sunScreen;
                    data.sunDir = sunDir;
                    data.hazeParams = hazeParams;
                    data.hazeParams2 = hazeParams2;
                    data.farHazeParams = _farHaze;
                    data.hazeColor = hazeColor;
                    data.hazeSunColor = hazeSunColor;
                    data.shaftParams = shaftParams;
                    data.shaftTint = shaftTint;
                    data.hasShafts = shafts.IsValid();
                    data.hasDepth = depth.IsValid();
                    data.camPos = _camPos; data.camRight = _camRight; data.camUp = _camUp; data.camFwd = _camFwd; data.zParams = _zParams;
                    builder.UseTexture(src, AccessFlags.Read);
                    if (depth.IsValid()) builder.UseTexture(depth, AccessFlags.Read);
                    if (data.hasShafts) builder.UseTexture(shafts, AccessFlags.Read);
                    builder.SetRenderAttachment(dst, 0, AccessFlags.Write);
                    builder.SetRenderFunc((PassData d, RasterGraphContext ctx) => Execute(d, ctx));
                }
            }

            static void Execute(PassData d, RasterGraphContext ctx)
            {
                Props.Clear();
                Props.SetTexture(BlitTextureId, (RTHandle)d.source);
                Props.SetVector(BlitScaleBiasId, new Vector4(1f, 1f, 0f, 0f));
                if (d.hasDepth) Props.SetTexture(CameraDepthId, (RTHandle)d.depth);
                if (d.hasShafts) Props.SetTexture(ShaftTexId, (RTHandle)d.shafts);
                else Props.SetTexture(ShaftTexId, Texture2D.blackTexture);
                Props.SetVector(SunScreenId, d.sunScreen);
                Props.SetVector(SunWorldDirId, d.sunDir);
                Props.SetVector(HazeParamsId, d.hazeParams);
                Props.SetVector(HazeParams2Id, d.hazeParams2);
                Props.SetVector(FarHazeParamsId, d.farHazeParams);
                Props.SetVector(HazeColorId, d.hazeColor);
                Props.SetVector(HazeSunColorId, d.hazeSunColor);
                Props.SetVector(ShaftParamsId, d.shaftParams);
                Props.SetVector(ShaftTintId, d.shaftTint);
                Props.SetFloat(BlurStepId, d.blurStep);
                Props.SetVector(CamPosId, d.camPos);
                Props.SetVector(CamRightId, d.camRight);
                Props.SetVector(CamUpId, d.camUp);
                Props.SetVector(CamFwdId, d.camFwd);
                Props.SetVector(ZParamsId, d.zParams);
                ctx.cmd.DrawProcedural(Matrix4x4.identity, d.material, d.pass, MeshTopology.Triangles, 3, 1, Props);
            }
        }
    }
}
