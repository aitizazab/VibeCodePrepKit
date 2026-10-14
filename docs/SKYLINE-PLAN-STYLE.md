# Skyline comic-ink look test: implementation plan

> **For agentic workers:** implement task by task; steps use checkbox (`- [ ]`) syntax. You are a Sonnet subagent: you
> write code and run the no-Unity checks only. **Never start Unity, never take the Unity lock, never wait or poll.**
> When the tasks are done, stop and report; the main session runs the pass.

**Goal:** the current M1 arena, the enemy gallery and the firing range render in the comic-ink style of
`docs/SKYLINE-STYLE.md`, so the creator can judge stills from the game.

**Architecture:** one hand-written URP shader (`Skyline/InkLit`) that keeps URP Lit's property and keyword names (so
existing code that sets `_BaseColor`, `_EmissionColor`, `_NORMALMAP` keeps working), one renderer feature
(`SkylineInkFeature`) gated by a volume override (Visiting Hours' `VhAtmosphereFeature` pattern), and an editor
conversion step run at the end of each Skyline scene build that swaps every scene material for an InkLit twin.

**Tech stack:** Unity 6000.3, URP 17.3 (package at `Library/PackageCache/com.unity.render-pipelines.universal@a0dd9d2bd983`),
Forward+ (`PC_Renderer.asset` rendering mode 2), Render Graph.

**Spec:** `docs/SKYLINE-STYLE.md` (read it first, whole).

## Global constraints

- Only Skyline files change, plus one renderer-feature entry on `Assets/Settings/PC_Renderer.asset`. The feature must do
  nothing unless the `SkylineInk` override is active, because Visiting Hours (another session, same repo) shares the
  renderer.
- Any keyword a material turns on at run time must be saved on a build-included material (the build strips unused
  variants; this made the bots invisible once). `Resources/SkylineFx/*.mat` are build-included.
- Gameplay untouched: no collider, position or script behaviour changes.
- Outline hues: hostile `#FF2E63`, objective `#2EE6FF`, scrap `#5CFF8A`; viewmodel ink 2.5 px; scenery ink about 1.5 px;
  outlines about 4 px at 1080p, constant on screen.
- Budget: mask pass plus composite at most 0.6 ms at 1080p on an RTX 3050.
- Stage files by name (never `git add -A`); commit after each task; end commit messages with
  `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`.
- Unity-side tests: the pass runs only the assemblies in `SkylinePassBatch.TestAssemblies`
  (`Editor/SkylinePassBatch.cs`, now `{ "JamKit.Skyline.Tests" }`, in `Packages/com.vibejam.jamkit/Tests/Skyline`). The
  new EditMode tests go in a new Editor-only, test-only asmdef `JamKit.SkylineRepo.Tests` at
  `Assets/JamKit/Templates/SkylineRepo/Editor/Tests/` (referencing `JamKit.SkylineRepo`, `JamKit.SkylineRepo.Editor`,
  URP runtime, NUnit), added to that array. Write its `.asmdef` as a normal file; **never hand-write `.meta` files**
  (Unity makes them).
- The check you can run: `powershell -NoProfile -File tools/skyline/test.ps1` (pure C# tests plus a compile check of
  Skyline's Unity-side code). It does **not** compile shaders; be careful with HLSL and copy URP's own patterns.

## Review focus

1. Forward+ additional lights: with `_CLUSTER_LIGHT_LOOP`/`_FORWARD_PLUS` the light loop must use URP's
   `LIGHT_LOOP_BEGIN/END` macros, or point lights silently vanish. Copy the keyword list from the package's
   `Shaders/Lit.shader` ForwardLit pass exactly.
2. SRP Batcher: every pass includes the same `UnityPerMaterial` CBUFFER (one include file), or the batcher breaks.
3. The viewmodel's overlay camera: the feature runs there too; it must not fail when that camera has no normals texture,
   and the gun must get its 2.5 px ink.
4. The sky: pixels at the far plane get no ink except silhouette edges against geometry.
5. A material with an emission keyword, a normal map or a texture must keep them in the twin.

---

### Task 1: the InkLit shader

**Files:**
- Create: `Assets/JamKit/Templates/SkylineRepo/Look/Ink/InkLitInput.hlsl` (CBUFFER, textures, shared helpers)
- Create: `Assets/JamKit/Templates/SkylineRepo/Look/Ink/InkLit.shader` (`Shader "Skyline/InkLit"`)
- Test: `Assets/JamKit/Templates/SkylineRepo/Editor/Tests/SkylineInkShaderTests.cs` (EditMode; runs in the pass's stage A)

**Interfaces (produces):** properties `_BaseColor`, `_BaseMap`, `_BumpMap`, `_BumpScale`, `_EmissionColor`,
`_EmissionMap`, `_GradBottom` (0.78), `_GradFloorY` (0), `_GradHeight` (6), `_Desaturate` (0..1), `_HatchStrength`
(0.15), `_InkRole` (0 none, 1 hostile, 2 objective, 3 scrap, 4 viewmodel), `_RimStrength` (0.25). Keywords
`_NORMALMAP`, `_EMISSION` (shader_feature_local). Pass names/tags: `ForwardLit` (`UniversalForward`), `ShadowCaster`,
`DepthOnly`, `DepthNormals`, `SkylineInkMask` (LightMode `SkylineInkMask`).

- [ ] **Step 1:** Write the EditMode test: `Shader.Find("Skyline/InkLit")` is not null and `isSupported`; a material
  made from it has each pass name above (`Material.FindPass(name) >= 0`); it has every property listed.
- [ ] **Step 2:** Write `InkLitInput.hlsl`: the `UnityPerMaterial` CBUFFER with every property above (plus `_BaseMap_ST`),
  the texture declarations, and `half3 InkAlbedo(float2 uv, float3 posWS)` = base map x base colour, desaturated by
  `_Desaturate` (luminance lerp), times the vertical gradient
  `lerp(_GradBottom, 1.05, saturate((posWS.y - _GradFloorY) / _GradHeight))`.
- [ ] **Step 3:** Write the ForwardLit pass. Copy the `#pragma multi_compile` lines for main-light shadows, additional
  lights, shadows, soft shadows, Forward+/cluster loop, light layers and fog from the package's `Lit.shader` ForwardLit
  pass. Lighting:
  - main light: `x = NdotL * shadowAtten`; bands `lit = smoothstep(0.45 - w, 0.45 + w, x)`, `half = smoothstep(0.08 - w, 0.08 + w, x)`
    with `w = fwidth(x)`; intensity `0.28 + 0.32 * half + 0.4 * lit` times light colour;
  - shade colour: albedo x `SampleSH(normalWS)` x 0.9 (the ambient is what keeps shade cool);
  - hatching in the deepest band only: world-space lines `frac(dot(posWS, normalize(float3(1,1,0))) * 6)` thresholded
    at 0.5 with fwidth AA, darkening by `_HatchStrength` where `half < 0.5`;
  - additional lights inside `LIGHT_LOOP_BEGIN/END` (and the non-Forward+ path, as `Lit` does): `a = distanceAttenuation *
    shadowAttenuation * NdotL`; banded `smoothstep(0.03, 0.06, a) * 0.55 + smoothstep(0.25, 0.3, a) * 0.85` times
    light colour (hard-edged pools: a strong light takes the room over);
  - rim `pow(1 - NdotV, 4) * _RimStrength` times main light colour; emission added last; `MixFog`.
- [ ] **Step 4:** Write ShadowCaster (`ApplyShadowBias` + `_LightDirection`/`_LightPosition` as URP's
  `ShadowCasterPass.hlsl` does), DepthOnly, and DepthNormals (writes `NormalizeNormalPerPixel` world normal the way
  URP's `DepthNormalsPass` does) as minimal passes including `InkLitInput.hlsl`.
- [ ] **Step 5:** Write `SkylineInkMask`: ZTest LEqual, ZWrite Off, Cull Back; outputs `float4(hue, 1)` from `_InkRole`
  (1 `#FF2E63`, 2 `#2EE6FF`, 3 `#5CFF8A`, 4 the ink colour `#16141F` with alpha 0.6 meaning "thin outline"); role 0 discards.
- [ ] **Step 6:** Run `tools/skyline/test.ps1`; commit.

### Task 2: the SkylineInk override and the renderer feature

**Files:**
- Create: `Assets/JamKit/Templates/SkylineRepo/Look/Ink/SkylineInk.cs` (`VolumeComponent, IPostProcessComponent`,
  namespace `JamKit.SkylineRepo`; copy `VhAtmosphere.cs`'s shape)
- Create: `Assets/JamKit/Templates/SkylineRepo/Look/Ink/SkylineInkFeature.cs`
- Create: `Assets/JamKit/Templates/SkylineRepo/Look/Ink/InkComposite.shader` (`Hidden/Skyline/InkComposite`)
- Create: `Assets/JamKit/Templates/SkylineRepo/Editor/Look/SkylineInkSetup.cs` (`InstallFeature()` adds the feature to
  `PC_Renderer.asset` once; copy `VhLookSetup.InstallRendererFeatures`)
- Test: `Assets/JamKit/Templates/SkylineRepo/Editor/Tests/SkylineInkFeatureTests.cs`

**Interfaces:**
- `SkylineInk` parameters: `BoolParameter active` (false), `ColorParameter inkColor` (`#16141F`),
  `ClampedFloatParameter lineWidthPx` (1.5, 0.5..4), `ClampedFloatParameter outlineWidthPx` (4, 1..8),
  `FloatParameter fadeStart` (20), `FloatParameter fadeEnd` (140), `ClampedFloatParameter farInk` (0.3, 0..1),
  `ClampedFloatParameter depthThreshold` (0.03), `ClampedFloatParameter normalThreshold` (0.35).
  `IsActive() => active.value`.
- `public static bool SkylineInkFeature.ShouldRun(SkylineInk ink)` (null or inactive: false), used by the tests.

- [ ] **Step 1:** Tests: `ShouldRun(null)` false; inactive override false; active true; after
  `SkylineInkSetup.InstallFeature()` twice, `PC_Renderer.asset` holds exactly one `SkylineInkFeature`.
- [ ] **Step 2:** The feature (read `VhAtmosphereFeature.cs` first and follow its Render Graph code, including its fix
  of binding the depth texture itself). `ConfigureInput(Depth | Normal)`. Event `BeforeRenderingPostProcessing`.
  - Pass A (raster): create a mask texture (camera size, `R8G8B8A8_UNorm`, clear to 0); depth attachment = the active
    depth texture, read-only; draw a renderer list for LightMode `SkylineInkMask`, opaque queue range.
  - Pass B (raster): full-screen composite into a new colour texture, then set it as the camera colour (as
    `VhAtmosphereFeature` does). Shader inputs: camera colour, depth, normals (if the normals texture is invalid, as on an
    overlay camera without it, use depth edges only), the mask, the override's values, the screen size.
- [ ] **Step 3:** `InkComposite.shader`:
  - thin ink: compare the centre's linear eye depth and normal with 4 neighbours at `lineWidthPx` offsets (a Roberts
    cross); edge where `|dDepth| / depth > depthThreshold` or `1 - dot(nA, nB) > normalThreshold`; strength fades from
    1 at `fadeStart` to `farInk` at `fadeEnd` metres; sky pixels (raw depth at the far plane) take an edge only when a
    neighbour is geometry; colour = lerp(scene, inkColor, edge);
  - thick outline: if the centre's mask alpha is 0, take 12 taps on a circle of `outlineWidthPx` and 8 at half radius;
    the strongest tap's hue wins; a viewmodel tap (alpha 0.6) uses `inkColor` at `0.6 * outlineWidthPx` (2.5 px); blend
    by the fraction of taps hit for anti-aliasing.
- [ ] **Step 4:** Run `tools/skyline/test.ps1`; commit.

### Task 3: the conversion and the palette

**Files:**
- Create: `Assets/JamKit/Templates/SkylineRepo/Editor/Look/SkylineInkPalette.cs` (the table)
- Create: `Assets/JamKit/Templates/SkylineRepo/Editor/Look/SkylineInkConvert.cs`
- Modify: `Resources/SkylineFx/SK_Bot_Paint.mat`, `SK_Fx_Acid.mat`, `SK_Emissive_FxGlow.mat` (shader to InkLit through
  the converter at build, saved to disk so the build keeps them; `_InkRole` 1)
- Test: `Assets/JamKit/Templates/SkylineRepo/Editor/Tests/SkylineInkConvertTests.cs`

**Interfaces:**
- `SkylineInkPalette.TryGet(string materialName, out InkEntry e)`; `struct InkEntry { Color color; float desaturate;
  float hatch; bool keepTexture; }`. Lookup by exact name, then by prefix (longest prefix wins).
- `SkylineInkConvert.ConvertScene(Scene scene)`: for every renderer, every shared material on URP Lit, URP Unlit or
  `SimpleLit` gets a cached twin `Assets/JamKit/Templates/SkylineRepo/Look/Ink/Twins/INK_<name>.mat` (created or
  updated), keeping `_BaseMap` only when `keepTexture`, `_BumpMap` + `_NORMALMAP`, emission + `_EMISSION`; transparent
  materials (surface type 1, the `SK_Glass_*`) are left alone. Returns the count converted.
- `SkylineInkConvert.SetRole(Material m, int role)` sets `_InkRole` and enables the `SkylineInkMask` pass only when
  role > 0 (`SetShaderPassEnabled`).

**The palette (starting values; flat painted colour, TF2-like muted scenery, the bots stay saturated):**

| Name or prefix | Colour | Notes |
|---|---|---|
| `SKL_Plaster_White` | `#EDE5D8` | `__Blush #E8C9C0`, `__Mint #CFE3D3`, `__Sage #BCC9A8`, `__Sand #E3CFAE`, `__Sky #C4D8E6`, `__Slate #8E9BA8` |
| `SKL_Marble_Cream`, `SKL_Terrazzo_White` | `#DCD2C1` | floor; gradient off (`_GradBottom` 1) |
| `SKL_Parquet_Herringbone`, `SKL_Wood_Cherry` | `#A0603E` | `SKL_Wood_Pine #C99B62` |
| `SKL_Gold_Brushed` | `#D4A23F` | CONCIERGE's gold |
| `SKL_Steel_Brushed` | `#A7ADB3` | `SKL_Steel_Dark #4A4F57` |
| `SKL_Concrete`, `SKL_Ceiling_Tile` | `#B9B3A8` | |
| `SKL_Shutter_Painted` | `#C9C3B6` | `__Navy #3E4E6E`, `__Sage #8FA27E`, `__Sand #D2B98C`, `__Lift #E0A33A`, `__Lockers #6F7F8F` |
| `SKL_Hazard_Orange` | `#F07A2A` | the firm's orange |
| `SKL_Facade_<Family>_Band<n>_*` | family: Blue `#7FA6C9`, Bronze `#B08A63`, Dark `#4E5866`, Green `#86B3A0`, Silver `#B7C0C9`, Stone `#C9BBA5`, White `#E4E6EA` | lerp towards the sky's horizon `#CFE0EE` by 0.0 / 0.25 / 0.5 for bands 0/1/2 (layered depth); `_Haze<n>` by 0.6 + 0.1 n; no hatching |
| `SKL_Ground_*` | `#BFB49E` | |
| `SKL_Prop_*`, `SKL_Products`, `SKL_Banners`, `SKL_Soil` | keep texture | painted look comes from the shading |
| `SK_Emissive_*` | keep texture and emission | |
| the bot paint, the acid | keep their colours | `_InkRole` 1 |

- [ ] **Step 1:** Tests: the prefix lookup (longest wins; `SKL_Plaster_White__Mint` gets mint, not white); a converted
  Lit material with a normal map and emission keeps both keywords; a transparent material is skipped; `SetRole(m, 0)`
  disables the mask pass and `SetRole(m, 1)` enables it.
- [ ] **Step 2:** Implement the palette and the converter.
- [ ] **Step 3:** Roles at run time: bots (all three bodies and the practice bot; find where `SkylineBotBodies` makes
  their material instances) get role 1; their mines, acid and bolts role 1; fresh wrecks role 3 and role 0 once stripped
  (`SkylineWreck`); the viewmodel's gun materials (find them via `SkylineViewmodel` / the gun body code) role 4. Because
  instances are made at run time from the Resources materials, the Resources materials must already carry the InkLit
  shader and the mask pass enabled (the build-variant rule).
- [ ] **Step 4:** Run `tools/skyline/test.ps1`; commit.

### Task 4: the ink look rig and the scenes

**Files:**
- Create: `Assets/JamKit/Templates/SkylineRepo/Look/Ink/InkSky.shader` (`Skyline/InkSky`: zenith `#5E9BE0`, horizon
  `#CFE0EE`, below-horizon `#B9B3A8`, a drawn sun disc with a soft ring, two tones of simple cloud blobs from value
  noise, posterised; slow drift)
- Modify: `Assets/JamKit/Templates/SkylineRepo/Editor/Look/SkylineLook.cs` (add `ApplyInk(Scene)` and `Ink()` profile;
  keep `ApplyAfternoon` as the fallback)
- Modify: `Editor/SkylineArenaBuilder.cs` and the firing-range and enemy-gallery builders (call `ApplyInk`, then
  `SkylineInkConvert.ConvertScene` at the end of the build; `SkylineInkSetup.InstallFeature()` once per build)
- Modify: `Editor/SkylineArenaDress.cs` (the two test areas below)

**The rig:** sun as now (bearing 235, 41 degrees) at intensity 2.2, shadows hard (no soft), distance 80 m; ambient
Trilight darker: sky `(0.34, 0.40, 0.52)`, equator `(0.36, 0.35, 0.34)`, ground `(0.30, 0.27, 0.24)`; the bounce fill off;
post profile `SK_Ink.asset`: Neutral tonemapping, exposure 0, contrast +8, saturation +4, bloom threshold 1.1 /
intensity 0.8 / scatter 0.6, vignette 0.14, and `SkylineInk` active with defaults. Reflection probes stay.

**The two test areas (rules 3 and 4 of the style sheet):**
- **The lift lobby, taken over by one light:** one point light in the lobby, violet `#8A5CFF`, intensity 6, range 9 m,
  no shadows, plus a matching emissive strip; the lobby's own small lights off. Add a still camera `lift-lobby`
  looking into it from the hall.
- **The warm-up hall, colour-free:** its materials get `_Desaturate` 1 (a per-renderer twin variant
  `INK_<name>__Grey`); its light is one cool-white strip-light row (point lights `#DDE8FF`, intensity 3) and the boost
  kiosk's sign stays coloured. Add a still camera `warmup-grey` from the doorway looking back in, the practice bot in
  frame.

- [ ] **Step 1:** Implement the sky, the rig, the builder calls and the two areas.
- [ ] **Step 2:** Run `tools/skyline/test.ps1`; commit.
- [ ] **Step 3:** Report: what was built, every file touched, anything uncertain (especially HLSL you could not
  verify), and the command for the main session to run: `powershell -NoProfile -File tools/skyline/pass.ps1 -Only A`
  then `-Only B`.

### Task 5 (main session): the pass, the judge, the page

Run stage A (shader compile errors come back to the agent), then stage B; an adversarial judge reviews the stills and the
recording against the style sheet; fix; then a before/after review page in `FableAnimationTests/art/review/2026-10-14/`.
