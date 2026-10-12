# Look: lighting, sky, post, the material library (the Look agent's status)

Owner: the Look agent (docs/REDESIGN.md section 5). Pass 1 committed 2026-10-12; **not yet seen in Unity** (agents can't
run it): main runs `tools/look-capture.ps1` and a judge scores the captures. Nothing here is "done" until then.

## What's built (pass 1)

**Sky.** Poly Haven HDRIs (CC0), prepared by `tools/look/hdri-prep.py` (finds the sun, clamps it out of the sky so the
directional light carries it and ambient isn't counted twice, measures the sky's colours). The pick:
**Kloppenheim 06 (pure sky), 4K**: scattered cumulus lit gold from underneath, crepuscular rays through them, a sun 7 degrees
up, a clear blue zenith: warm on the sun's side, cool overhead, which is what a late-summer evening looks like. Alternates
(2K, same API): Table Mountain 2 (big summer cumulus), Qwantani Sunset (hazier, warmer, few clouds), Evening Road 01 (an hour
earlier, sun 14 degrees, a full sky of altocumulus). Files: `ThirdParty/LookSky/`, stats in `skies.json`.
Shader `JamKit/Look/Sky`: the HDRI turned so its sun sits on the rig's bearing; a slow cloud drift (two offset copies
cross-faded in a loop, masked off near the sun and the horizon, higher clouds slower); a limb-darkened HDR sun disc plus
glow and halo (bloom and the lens flare catch it); a warm lift on the sun's side; a horizon haze band; a soft ground colour
below the horizon.

**The rig** (`VhLook.ApplyGoldenHour`): one root "VH Look (GoldenHour)" holding
- the **sun**: directional, warm (1, 0.77, 0.52) x 3.2, 10 degrees up at bearing 250 (west-south-west; the hill rises
  north, so the slope is side-lit, streets uphill have long shadows across them, the hilltop catches the last light),
  soft shadows (high quality), Mixed (its bounce is baked into the probes), the SRP lens flare (a soft glow, a faint ring,
  a few small ghosts);
- a **bounce fill**: a shadowless warm directional from low on the anti-sun side (light off the sunlit ground; halved
  after a bake, when the probes carry the real bounce);
- `VhLookRig`: aims both lights, turns the sky, feeds the sky/atmosphere globals, and while enabled (play mode / builds /
  the capture rig) sets the URP asset's **main-light shadows: 200 m, 4 cascades at 3.5% / 11% / 32%, 4096 map, depth bias
  0.6, normal bias 0.45**, restoring the asset's own values when disabled (other templates keep theirs);
- a global **Volume** with the post profile;
- RenderSettings: the sky material, ambient and reflections from the sky, Unity fog off (the atmosphere pass does haze).

**Post** (`Look/Profiles/`, built in code by `VhLookProfiles`, every value reviewable there):
- `VH_GoldenHour`: ACES; exposure -0.15; contrast +10, saturation +12, a warm colour filter; white balance +14 / tint +3;
  split toning: lilac-blue shadows, warm highlights (balance -12); shadows/midtones/highlights nudges; bloom threshold 1.05,
  intensity 0.65, scatter 0.72, warm tint, HQ; vignette 0.22 (warm-dark); grain Thin1 0.12; **VhAtmosphere**: haze
  density 0.0032/m from 14 m, thinning upward (0.022/m), cool away from the sun, gold towards it (colours from the sky's
  measured horizon), max 0.82; sun shafts 0.55 (threshold 1.1, length 0.75, radius 0.6).
- `VH_HouseInterior`: warmer (WB +20), softer contrast, more bloom on lamps (threshold 0.95, 0.8), vignette 0.28, no haze.
- `VH_HospitalDay`: cool (WB -12, tint -5), saturation -14, a faint fluorescent green-cyan in the shadows, gentle bloom.
- URP renderer (PC_Renderer): SSAO raised to intensity 0.6, radius 0.45, direct-light strength 0.15 (was 0.4/0.3/0.25);
  the **VhAtmosphere renderer feature** added (by `VhLookSetup.InstallRendererFeatures`, run in the build stage).
- URP asset (PC_RPAsset): **light probe system = Adaptive Probe Volumes** (scenes with no baked probes fall back to the
  sky's ambient, as before).

**Material library**: 26 2K CC0 sets, catalogue in `docs/redesign/look-library.md` (name, use, tile size, texel density).

**Trees**: `VhLookTrees.Place`: stylized deciduous trees (bark trunk + limbs with a capsule collider; a crown of painted
leaf-cluster cards with crown-shaped normals, so it shades as one soft volume and glows gold when backlit; gentle wind).

**LookLab** (`Look/Scenes/LookLab.unity`, `VhLookLab.Build`): an intersection with kerbs, sidewalks, a painted road and a
manhole; a two-storey sage clapboard house with real window openings (trim, sills, glass, six-over-six muntins, curtains, a
room behind), a panelled red door, a porch with steps, posts and balusters, corner boards, frieze, fascia, gutters,
downspouts, a brick chimney; a brick bungalow next door; a picket fence and a board fence; a flower bed, a gravel side path;
six trees and shrubs; a covered car on the driveway; a hillside of 70 trees and 26 houses rising north into the haze.
Ten fixed shots (street corner, the sunlit side, into the sun through the maple, facade close-up, kerb-level, the hill
vista, the driveway, sky towards / away from the sun, the cross street uphill).

## The API (for World, House, Day, People)

```csharp
using JamKit.Look.EditorTools;   // asmdef: add "JamKit.Look.Editor" (editor) / "JamKit.Look" (runtime) to your references
var rig = VhLook.ApplyGoldenHour(scene, new VhLook.Options { sunAzimuth = 250f, sunElevation = 10f, hazeBaseHeight = 0f });
VhLook.ApplyHouseInterior(scene);    // the house inside (the House agent places warm lamps, 2700 K)
VhLook.ApplyHospitalDay(scene);      // the ward (the Day agent places cool fluorescent panels, 5000 K)
VhLook.SetupCamera(playerCamera);    // HDR, post on, SMAA high, NaN guard, dithering, far clip 1500
VhLook.Material("Brick_Red");        // the library; VhLookLibrary.Tinted / Paint / Glass / GetTerrainLayer / Prop
VhLookUv.BoxProject(go);             // metre UVs; VhLookUv.Block(...) for bevelled blocks
VhLookTrees.Place(parent, pos, seed, height);
VhLook.AddReflectionProbe(scene, center, size);   // one per street / room
VhLook.MarkStaticForGI(root);        // optional: BakeScene marks everything outside a "(backdrop)"-named root
// then main runs the bake (GPU) on the saved scene:
//   -executeMethod JamKit.Look.EditorTools.VhLook.BakeFromCommandLine -vhScene <scene path> [-vhProbeSpacing 3]
// and captures with the same rig:
VhLookCapture.Capture(scenePath, shots, "hill-captures");          // 1920x1080 PNGs to out/hill-captures/<time>/
```

Rules for builders: **one sun** (Apply disables other directional lights), **don't set RenderSettings fog/ambient yourself**
(Apply does), **name far scenery "... (backdrop)"** so the probe bake skips it, **metre UVs** on everything that uses the
library. For a big hill, bake with `-vhProbeSpacing 3` (probes every 3 m near geometry) to keep the bake and memory sane.

## Shaders written (all URP-standard HLSL; no other shaders)

- `Look/Shaders/VhSky.shader` (`JamKit/Look/Sky`): the skybox.
- `Look/Shaders/VhAtmosphere.shader` (`JamKit/Look/Atmosphere`): haze + sun shafts (Core.hlsl, core Blit.hlsl, DeclareDepthTexture.hlsl).
- `Look/Shaders/VhFoliage.shader` (`JamKit/Look/Foliage`): leaf crowns (forward, shadow caster, depth, depth-normals).
Renderer feature: `Look/Scripts/VhAtmosphereFeature.cs` (Render Graph), volume override `Look/Scripts/VhAtmosphere.cs`.

## Unity steps for main (one GPU job at a time)

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools/look-capture.ps1            # all: build, bake, capture
# or stage by stage:  -Stage build  |  -Stage bake  |  -Stage capture
```
Methods: `JamKit.Look.EditorTools.VhLookLab.AllFromCommandLine` (or `BuildFromCommandLine`, `BakeFromCommandLine`,
`CaptureFromCommandLine`). Unity runs with graphics (no `-nographics`). Output: `out/look-captures/<time>/01-...10-*.png`
plus `shots.txt`; log `out/look-<stage>.log` (grep `JAMKIT_LOOK`). Exit 2 = the bake failed (captures still made, unbaked).
After the first run, commit the files Unity created: the texture/model/HDR `.meta` files under
`ThirdParty/LookLibrary`, `ThirdParty/LookSky`, `ThirdParty/LookProps`, `Look/Textures`; `Look/Library/**`,
`Look/Profiles/**`, `Look/Scenes/**` (scene, lighting data, probe set); and `Assets/Settings/PC_Renderer.asset` (the
renderer feature sub-asset).

## Pending

- Pass 1 judge round (captures). Likely tuning: exposure, sun/sky balance, haze density, shaft strength, flare.
- Pass 2: the soundscape (Saturday ambience bed + positional loops), the `-perf-probe` flag (camera path, frame-time
  percentiles), quality settings for the RTX 3050 laptop at 1080p/60 (STP or TAA), a midday sky for the hospital.
- Known limits: the covered car is the only vehicle (an uncovered car model is People/World's call); the hospital uses the
  bluest golden-hour sky until a midday HDRI is added.

## Next step

Main runs `tools/look-capture.ps1`, sends the captures and the log back; fix what the judge finds.
