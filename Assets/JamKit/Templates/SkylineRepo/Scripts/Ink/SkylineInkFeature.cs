using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// URP renderer feature (Render Graph) for Skyline's comic-ink look (docs/SKYLINE-STYLE.md, shader
    /// Hidden/Skyline/InkComposite), driven by the <see cref="SkylineInk"/> volume override. It does nothing unless that
    /// override is active, so it is safe on the shared PC renderer. Installed by SkylineInkSetup.InstallFeature. Written on the
    /// pattern of Visiting Hours' VhAtmosphereFeature (a volume gate, a property block that binds the textures itself).
    /// <para>Passes, before post-processing: (A) the mask: every renderer whose material has the <c>SkylineInkMask</c> pass
    /// enabled is drawn into an R8G8B8A8 mask, against the camera depth (read-only), the weak-spot queue last so yellow
    /// overwrites red-magenta; (B) a copy of the camera colour; (C) the composite, which reads the copy, the depth, the normals
    /// and the mask and writes the thin ink and the thick outlines straight back into the camera colour.</para>
    /// <para>Why (B) and (C) instead of swapping in a new camera colour as the atmosphere pass does: this look runs on the base
    /// camera of a stack too (the first-person viewmodel is an overlay camera), and a stacked camera hands its colour buffer to
    /// the next one. Writing into the active colour keeps that buffer the one the overlay continues from (the pattern of URP's
    /// own FullScreenPassRendererFeature).</para>
    /// <para>The overlay camera (the viewmodel): the feature runs there as well. Its depth holds only the guns (an overlay
    /// clears depth), so it adds the guns' own ink and their thin outline and nothing to the scenery. If a camera has no normals
    /// texture the composite uses depth edges only. MSAA is not supported (the PC asset has none): with it on, the pass skips
    /// and logs once.</para>
    /// </summary>
    public sealed class SkylineInkFeature : ScriptableRendererFeature
    {
        public Shader shader;
        public RenderPassEvent passEvent = RenderPassEvent.BeforeRenderingPostProcessing;

        Material _material;
        InkPass _pass;
        static bool _warnedMsaa;

        /// <summary>True when the override exists and is on: the whole feature's gate (public for the tests).</summary>
        public static bool ShouldRun(SkylineInk ink) => ink != null && ink.IsActive();

        /// <summary>A measurement switch, never set in play: the perf run's <c>-skyline-no-ink</c> argument turns the whole feature
        /// off, so one extra stage D run says how much of the frame time the ink passes cost (docs/SKYLINE-LOOK.md, perf round 5).</summary>
        public static bool ForceOff;

        public override void Create()
        {
            _pass = new InkPass { renderPassEvent = passEvent, requiresIntermediateTexture = true };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var camera = renderingData.cameraData.camera;
            var type = renderingData.cameraData.cameraType;
            if (type != CameraType.Game && type != CameraType.SceneView) return;
            VolumeStack stack = null;
            var add = camera != null ? camera.GetUniversalAdditionalCameraData() : null;
            if (add != null) stack = add.volumeStack;
            if (stack == null) stack = VolumeManager.instance.stack;
            var ink = stack != null ? stack.GetComponent<SkylineInk>() : null;
            if (ForceOff || !ShouldRun(ink)) return;
            if (renderingData.cameraData.cameraTargetDescriptor.msaaSamples > 1)
            {
                if (!_warnedMsaa) { _warnedMsaa = true; Debug.LogWarning("SKYLINE_INK the ink pass skips cameras with MSAA on (the mask and the depth would differ in sample count)"); }
                return;
            }
            if (_material == null)
            {
                if (shader == null) shader = Shader.Find("Hidden/Skyline/InkComposite");
                if (shader == null) return;
                _material = CoreUtils.CreateEngineMaterial(shader);
            }
            _pass.Setup(_material, ink);
            _pass.ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
            _material = null;
        }

        sealed class InkPass : ScriptableRenderPass
        {
            static readonly ShaderTagId MaskTag = new ShaderTagId(SkylineInkRoles.MaskPass);
            static readonly ShaderTagId XrayTag = new ShaderTagId(SkylineInkRoles.ObjectiveXrayPass);
            static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
            static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
            static readonly int DepthId = Shader.PropertyToID("_SkInkDepth");
            static readonly int NormalsId = Shader.PropertyToID("_SkInkNormals");
            static readonly int MaskId = Shader.PropertyToID("_SkInkMask");
            static readonly int InkColorId = Shader.PropertyToID("_SkInkColor");
            static readonly int LineId = Shader.PropertyToID("_SkInkLine");
            static readonly int FadeId = Shader.PropertyToID("_SkInkFade");
            static readonly int ScreenId = Shader.PropertyToID("_SkInkScreen");
            static readonly int ZParamsId = Shader.PropertyToID("_SkInkZParams");
            static readonly int TimeId = Shader.PropertyToID("_SkInkTime");

            static readonly MaterialPropertyBlock Props = new MaterialPropertyBlock();
            static int _logged;

            Material _material;
            SkylineInk _ink;

            sealed class MaskPassData
            {
                public RendererListHandle xray, ordinary, late, botWeak, weak;
            }

            sealed class CompositePassData
            {
                public Material material;
                public TextureHandle source, depth, normals, mask;
                public bool hasNormals;
                public Vector4 inkColor, line, fade, screen, zParams, time;
            }

            public InkPass()
            {
                profilingSampler = new ProfilingSampler("Skyline Ink");
            }

            public void Setup(Material material, SkylineInk ink)
            {
                _material = material;
                _ink = ink;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                var renderingData = frameData.Get<UniversalRenderingData>();
                var lightData = frameData.Get<UniversalLightData>();
                if (resources.isActiveTargetBackBuffer || _material == null || _ink == null) return;
                var cam = cameraData.camera;

                TextureHandle color = resources.activeColorTexture;
                TextureHandle depthAttachment = resources.activeDepthTexture;
                // The depth to sample: the copy/prepass texture the feature asked for; on a camera that has none, the depth
                // attachment itself (sampled in the composite, where it is not an attachment).
                TextureHandle depthTex = resources.cameraDepthTexture.IsValid() ? resources.cameraDepthTexture : depthAttachment;
                TextureHandle normals = resources.cameraNormalsTexture;
                bool hasNormals = normals.IsValid();
                if (!color.IsValid() || !depthTex.IsValid()) return;

                var colorDesc = renderGraph.GetTextureDesc(resources.cameraColor);
                int width = colorDesc.width, height = colorDesc.height;
                if (_logged < 3)
                {
                    _logged++;
                    Debug.Log("SKYLINE_INK camera " + cam.name + " (" + cameraData.renderType + "), depth " +
                              (resources.cameraDepthTexture.IsValid() ? "bound" : "attachment") + ", normals " +
                              (hasNormals ? "bound" : "MISSING (depth edges only)") + ", " + width + "x" + height);
                }

                // ---- A: the mask
                var maskDesc = new TextureDesc(width, height)
                {
                    format = GraphicsFormat.R8G8B8A8_UNorm,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    clearBuffer = true,
                    clearColor = Color.clear,
                    name = "_SkylineInkMask",
                };
                TextureHandle mask = renderGraph.CreateTexture(maskDesc);
                if (depthAttachment.IsValid())
                {
                    using (var builder = renderGraph.AddRasterRenderPass<MaskPassData>("Skyline Ink Mask", out var data, profilingSampler))
                    {
                        var sort = cameraData.defaultOpaqueSortFlags;
                        var drawing = RenderingUtils.CreateDrawingSettings(MaskTag, renderingData, cameraData, lightData, sort);
                        drawing.perObjectData = PerObjectData.None;
                        // The objective seen through walls: first, so the ordinary pass overwrites it wherever the objective is
                        // visible. Only renderers in the objective queue (roles 2 and 7), so scenery whose x-ray pass is still
                        // enabled on an old material never costs a second draw.
                        var xrayDrawing = RenderingUtils.CreateDrawingSettings(XrayTag, renderingData, cameraData, lightData, sort);
                        xrayDrawing.perObjectData = PerObjectData.None;
                        int objQ = SkylineInkRoles.ObjectiveQueue;
                        data.xray = renderGraph.CreateRendererList(new RendererListParams(renderingData.cullResults, xrayDrawing,
                            new FilteringSettings(new RenderQueueRange { lowerBound = objQ, upperBound = objQ }, -1)));
                        builder.UseRendererList(data.xray);
                        // Queues: ordinary roles below BotWeakQueue, then the bots' own weak points, then the Writ's weak spots, so
                        // where parts overlap in depth the later one overwrites the earlier (5 over 6 over 1). Anything above the
                        // weak-spot queue (a transparent role, none today) is drawn before them.
                        int botQ = SkylineInkRoles.BotWeakQueue, weakQ = SkylineInkRoles.WeakQueue;
                        data.ordinary = renderGraph.CreateRendererList(new RendererListParams(renderingData.cullResults, drawing,
                            new FilteringSettings(new RenderQueueRange { lowerBound = 0, upperBound = botQ - 1 }, -1)));
                        data.late = renderGraph.CreateRendererList(new RendererListParams(renderingData.cullResults, drawing,
                            new FilteringSettings(new RenderQueueRange { lowerBound = weakQ + 1, upperBound = 5000 }, -1)));
                        data.botWeak = renderGraph.CreateRendererList(new RendererListParams(renderingData.cullResults, drawing,
                            new FilteringSettings(new RenderQueueRange { lowerBound = botQ, upperBound = weakQ - 1 }, -1)));
                        data.weak = renderGraph.CreateRendererList(new RendererListParams(renderingData.cullResults, drawing,
                            new FilteringSettings(new RenderQueueRange { lowerBound = weakQ, upperBound = weakQ }, -1)));
                        builder.UseRendererList(data.ordinary);
                        builder.UseRendererList(data.late);
                        builder.UseRendererList(data.botWeak);
                        builder.UseRendererList(data.weak);
                        builder.SetRenderAttachment(mask, 0, AccessFlags.Write);
                        builder.SetRenderAttachmentDepth(depthAttachment, AccessFlags.Read);
                        builder.AllowPassCulling(false);
                        builder.SetRenderFunc((MaskPassData d, RasterGraphContext ctx) =>
                        {
                            ctx.cmd.DrawRendererList(d.xray);   // first: the visible outline of the ordinary pass overwrites it
                            ctx.cmd.DrawRendererList(d.ordinary);
                            ctx.cmd.DrawRendererList(d.late);
                            ctx.cmd.DrawRendererList(d.botWeak);
                            ctx.cmd.DrawRendererList(d.weak);   // last: where a weak spot overlaps its body in depth, yellow wins
                        });
                    }
                }

                // ---- B: a copy of the colour (the composite reads the copy and writes the camera colour)
                var copyDesc = colorDesc;
                copyDesc.name = "_SkylineInkColorCopy";
                copyDesc.clearBuffer = false;
                copyDesc.msaaSamples = MSAASamples.None;
                TextureHandle copy = renderGraph.CreateTexture(copyDesc);
                renderGraph.AddBlitPass(color, copy, Vector2.one, Vector2.zero, passName: "Skyline Ink Copy Color");

                // ---- C: the composite
                float scale = Mathf.Max(0.5f, height / 1080f);   // widths are given at 1080p
                float n = cam.nearClipPlane, f = cam.farClipPlane;
                var zParams = SystemInfo.usesReversedZBuffer
                    ? new Vector4(-1f + f / n, 1f, (-1f + f / n) / f, 1f / f)
                    : new Vector4(1f - f / n, f / n, (1f - f / n) / f, (f / n) / f);
                using (var builder = renderGraph.AddRasterRenderPass<CompositePassData>("Skyline Ink Composite", out var d, profilingSampler))
                {
                    var ink = _ink;
                    var c = ink.inkColor.value.linear;
                    d.material = _material;
                    d.source = copy;
                    d.depth = depthTex;
                    d.normals = normals;
                    d.mask = mask;
                    d.hasNormals = hasNormals;
                    d.inkColor = new Vector4(c.r, c.g, c.b, 1f);
                    d.line = new Vector4(ink.lineWidthPx.value * scale, ink.outlineWidthPx.value * scale, ink.depthThreshold.value, ink.normalThreshold.value);
                    d.fade = new Vector4(ink.fadeStart.value, ink.fadeEnd.value, ink.farInk.value, hasNormals ? 1f : 0f);
                    d.screen = new Vector4(1f / width, 1f / height, width, height);
                    d.zParams = zParams;
                    d.time = new Vector4(Time.time, 0f, 0f, 0f);
                    builder.UseTexture(copy, AccessFlags.Read);
                    builder.UseTexture(depthTex, AccessFlags.Read);
                    if (hasNormals) builder.UseTexture(normals, AccessFlags.Read);
                    builder.UseTexture(mask, AccessFlags.Read);
                    builder.SetRenderAttachment(color, 0, AccessFlags.Write);
                    builder.SetRenderFunc((CompositePassData data, RasterGraphContext ctx) => ExecuteComposite(data, ctx));
                }
            }

            static void ExecuteComposite(CompositePassData d, RasterGraphContext ctx)
            {
                Props.Clear();
                Props.SetTexture(BlitTextureId, (RTHandle)d.source);
                Props.SetVector(BlitScaleBiasId, new Vector4(1f, 1f, 0f, 0f));
                Props.SetTexture(DepthId, (RTHandle)d.depth);
                if (d.hasNormals) Props.SetTexture(NormalsId, (RTHandle)d.normals);
                else Props.SetTexture(NormalsId, Texture2D.blackTexture);
                Props.SetTexture(MaskId, (RTHandle)d.mask);
                Props.SetVector(InkColorId, d.inkColor);
                Props.SetVector(LineId, d.line);
                Props.SetVector(FadeId, d.fade);
                Props.SetVector(ScreenId, d.screen);
                Props.SetVector(ZParamsId, d.zParams);
                Props.SetVector(TimeId, d.time);
                ctx.cmd.DrawProcedural(Matrix4x4.identity, d.material, 0, MeshTopology.Triangles, 3, 1, Props);
            }
        }
    }
}
