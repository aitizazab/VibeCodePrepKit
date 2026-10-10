using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// The opening's and the title's lighting, moment to moment (built by <see cref="VhLighting.Apply"/>; docs/OPENING-AND-TITLE.md
    /// "Lighting"). Runs after <see cref="VhStage"/> each frame and reads its state (the camera pose, the room's light level, the
    /// phone's screen, whether the title is live) to drive what VhStage doesn't: the extra practicals (the window's cool spill,
    /// the phone's light on her hand, the tablet's glow on the table), the two grades (night and morning), the depth of field's
    /// focus per shot. While the sets are up it also tightens the pipeline's shadow distance to the rooms (for sharp shadows)
    /// and gives it back when they go.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [ExecuteAlways] // so OnDestroy frees the cookies and profiles in edit mode too (EditMode tests build the sets)
    public sealed class VhLightingRig : MonoBehaviour
    {
        public VhStage stage;
        [Header("Grades")]
        public Volume night, morning;
        public DepthOfField nightDof, morningDof;
        public ColorAdjustments nightColor;
        [Header("Practicals")]
        [Tooltip("Cool moon spill just inside the window (follows the room's light level).")]
        public Light windowSpill;
        public float windowSpillIntensity = 0.15f;
        [Tooltip("The phone's screen on her hand and the blanket (a spot out of the screen; follows the screen's brightness).")]
        public Light phoneFace;
        public float phoneFaceIntensity = 0.013f;
        [Tooltip("The tablet's screen on the table and the mug; red and blue from the report's emergency lights while it plays.")]
        public Light tabletGlow;
        public float tabletGlowIntensity = 0.008f;
        [Tooltip("How much of the report's red and blue reaches the table (0..1), and how fast it alternates (Hz).")]
        public float emergencyTint = 0.45f, emergencyRate = 0.9f;
        [Header("Night grade at 'G'night'")]
        [Tooltip("Exposure (stops) the night grade loses as the room's light goes out.")]
        public float goodnightExposure = -1.2f;
        [Header("Shadows while the sets are up")]
        [Tooltip("The pipeline's shadow distance while the sets are up (m): the rooms are small, so the shadow map covers them tightly.")]
        public float shadowDistance = 7f;

        /// <summary>Textures and profiles made for the lighting (freed with the stage).</summary>
        public readonly List<Object> owned = new List<Object>();

        float _savedShadowDistance = -1f;
        UniversalRenderPipelineAsset _asset;
        float _clock;
        VhTitlePiece _tablet;
        float _nightExposure;

        /// <summary>The grade in use now: "night" or "morning" (tests, captures).</summary>
        public string Grade => stage != null && stage.InBedroom ? "night" : "morning";

        void OnEnable()
        {
            if (Application.isPlaying) Engage();
        }
        void OnDisable() => Release();

        void OnDestroy()
        {
            Release();
            foreach (var o in owned) VhUtil.Kill(o);
            owned.Clear();
        }

        /// <summary>Tightens the pipeline's shadow distance to the sets (undone by <see cref="Release"/>).</summary>
        public void Engage()
        {
            if (_savedShadowDistance >= 0f) return;
            _asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (QualitySettings.renderPipeline is UniversalRenderPipelineAsset q) _asset = q;
            if (_asset == null) return;
            _savedShadowDistance = _asset.shadowDistance;
            _asset.shadowDistance = shadowDistance;
        }

        /// <summary>Gives the pipeline its own shadow distance back.</summary>
        public void Release()
        {
            if (_savedShadowDistance < 0f || _asset == null) return;
            _asset.shadowDistance = _savedShadowDistance;
            _savedShadowDistance = -1f;
        }

        public void Init()
        {
            if (nightColor != null) _nightExposure = nightColor.postExposure.value;
        }

        void LateUpdate()
        {
            if (Application.isPlaying) Tick(Time.unscaledDeltaTime);
        }

        /// <summary>One frame of the lighting (the capture tool calls it in edit mode).</summary>
        public void Tick(float dt)
        {
            if (stage == null) return;
            _clock += dt;
            bool bed = stage.InBedroom;
            if (night != null) night.weight = bed ? 1f : 0f;
            if (morning != null) morning.weight = bed ? 0f : 1f;
            float lights = stage.LightsLevel, screen = stage.ScreenBrightness;

            if (windowSpill != null)
            {
                windowSpill.enabled = bed;
                windowSpill.intensity = windowSpillIntensity * lights;
            }
            if (phoneFace != null)
            {
                // Only in her hand (face up on the nightstand, the screen's glow does it).
                phoneFace.enabled = bed && screen > 0.01f && stage.PhoneHeld;
                phoneFace.intensity = phoneFaceIntensity * screen;
            }
            if (nightColor != null)
                nightColor.postExposure.value = _nightExposure + goodnightExposure * (1f - Mathf.Max(lights, screen * 0.6f));

            // Focus: the phone when it's the subject, the tablet at the table.
            var cam = stage.cam;
            if (cam != null && nightDof != null)
            {
                bool phoneShot = stage.Pose == "nightstand" || stage.Pose == "overhead";
                nightDof.active = phoneShot;
                if (phoneShot && stage.phone != null)
                {
                    float d = Vector3.Distance(cam.transform.position, stage.phone.position);
                    nightDof.gaussianStart.value = d + 0.25f;
                    nightDof.gaussianEnd.value = d + 2.2f;
                }
            }
            if (!bed)
            {
                if (_tablet == null && stage.table != null) _tablet = stage.table.Find("tablet");
                if (cam != null && morningDof != null && _tablet != null)
                {
                    float d = Vector3.Distance(cam.transform.position, _tablet.transform.position);
                    morningDof.gaussianStart.value = d + 0.35f;
                    morningDof.gaussianEnd.value = d + 2.5f;
                }
                if (tabletGlow != null)
                {
                    // Only once the tablet has faded in (its renderers come on with the fade).
                    tabletGlow.enabled = _tablet != null && _tablet.renderers.Length > 0 && _tablet.renderers[0] != null && _tablet.renderers[0].enabled;
                    if (_tablet != null)
                    {
                        // 16 cm in front of the screen (nearer, its 1/d^2 falloff blows out the table).
                        tabletGlow.transform.position = _tablet.transform.TransformPoint(new Vector3(0f, 0.16f, 0.09f));
                        bool report = !stage.Interactive;
                        float side = Mathf.Sin(_clock * emergencyRate * Mathf.PI * 2f);
                        Color emergency = side > 0f ? new Color(1f, 0.25f, 0.25f) : new Color(0.3f, 0.45f, 1f);
                        float tint = report ? emergencyTint * Mathf.Abs(side) : 0f;
                        tabletGlow.color = Color.Lerp(new Color(0.78f, 0.86f, 1f), emergency, tint);
                        tabletGlow.intensity = tabletGlowIntensity * (report ? 1f : 0.8f);
                    }
                }
            }
            else if (tabletGlow != null) tabletGlow.enabled = false;
        }
    }
}
