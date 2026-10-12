# Look: lighting, sky, post, the material library (the Look agent's status)

Owner: the Look agent (docs/REDESIGN.md section 5). Pass 1 was judged on 2026-10-12 (docs/redesign/judge/look-1.md:
**FAIL**, 5/3/3/4, four blockers). **Pass 2** (this commit) answers every defect in that report; it is **not yet seen in
Unity** (agents can't run it). Main runs `tools/look-capture.ps1` and a new judge scores the twelve captures.

## Pass 2: the judge's defects and what changed

| # | Defect (round 1) | Pass 2 |
|---|---|---|
| 1 | BLOCKER: hill houses were tan boxes, overlapping | `VhLookHouses`: real houses at a far LOD (windows with trim, sills, sashes and dark reflective glass, panelled doors, shutters, porches with posts, chimneys, eaves, gutters, downspouts, corner boards, siding or brick), random but plausible specs, on lots 29-31 m apart along three hill streets, foundations from 0.3 m below the lowest corner, driveways. Merged per material, culled past ~1.5 km. |
| 2 | BLOCKER: the world ends | `VhLookBackdrop`: a terrain ring to 2.9 km continuing the hill, rolling into farmland hills and wooded ridges (to ~190 m) all round, ~40 stands of real trees in its near part, a tree line of ~2200 canopy masses further out; **far haze** in the atmosphere pass (0.0042/m from 150 m, gentle height falloff, max 0.97) whose colour is the sky's own horizon band, so the ring dissolves into the sky. The lone tree in the void is gone (the verge tree moved off the sun's line, the ring behind it). |
| 3 | BLOCKER: repeating hillside ground | The ground is a Unity Terrain (`VhLookGround.BuildTerrain`): four layers (mown lawn; dry gold grass; dirt; a clover-green copy of the lawn at another scale and offset as a repeat-breaker) mixed by a ~80 m mask, a ~13 m one and a 3 m mottle; dirt along fences, beds, tree bases and a worn path. Grass, weed and wildflower tufts (~4-5k, chunked) in drifts and as overhang along every paving edge. |
| 4 | BLOCKER: lollipop trees | `VhLookTrees` rewritten: four species (maple, oak, birch, elm) plus shrubs, three variants each, ±30% height in stands; limbs from 2.3-3 m and tips spread over the crown's height (no bare pole); ~580 cards per crown at a quarter of the old area, built in clumps round branch tips and clamped inside the crown's outline; a new painted atlas (many small leaves, olive/warm greens); rim-weighted translucency (1.9, focus 3) that survives the crown's self-shadow; `Cluster`/`Scatter` place stands, not grids. LODs: see below. |
| 5 | Roof invisible from below, gutter up the gable, angled downspout, sky gap | Roof is a 0.2 m slab with every face wound to its own normal: shingles on top, painted soffit under, fascia and rake boards on its edges (pass 1's underside faced up). Gable ends rise to the soffit (no gap); the frieze rises into it. Gutters only on the two eaves; downspouts elbow back to the wall and drop straight down beside the corner boards to a splash shoe. |
| 6 | Siding courses step; stray panel | Wall pieces are unbevelled and every wall piece and both gables get UVs in the house's own frame (u along the wall, v = height): one continuous course layout per wall, corner to corner and up into the gable. The stray panel was the per-piece UVs and bevels; gone. |
| 7 | Dark purple sidewalks, flat lawns | Sidewalk and kerb albedos regraded (`tools/look/regrade-albedo.py`) to pale warm grey (sRGB mean 0.45 -> 0.67): now the brightest surface on the street. Lawns: the terrain, the edge tufts, leaf litter drifts in the gutters and under trees, oil and dirt stains, three kerb-inlet storm drains with iron grates and grime. |
| 8 | Sky and sun flat; no flare | Sky turned **and tilted** so the HDRI's own sun sits exactly on the light (pass 1 drew the disc 3 degrees below the light: the flare and shafts came from empty sky); warm lift and horizon band cut ~40% so the HDRI's gold-lit cumulus and rays show; disc 110 (was 60), tight glow 2.8 (was 1.6), wide halo halved. Bloom 1.0 / threshold 0.9 / scatter 0.68; shafts 0.8; lens flare brighter with a wide warm glow element, occlusion radius about the sun's own (a point test before, so any leaf hid it). |
| 9 | Underexposed, lilac shadows, no haze | Exposure -0.05 (+0.1); split-toning shadow colour half as lilac, balance -6, shadows lifted warm; bounce fill 0.42 warmer; far haze (row 2). HDR colour grading (URP asset) so the grade works on the HDR image. |
| 10 | Grey squiggle near the horizon | A dark stub painted into the HDRI's horizon left of the sun (found by mapping the shots' pixels back into the equirect). Painted out with `tools/look/hdri-patch.py` (rect 2206,996-2258,1046). |
| 11 | Dead-end street; orange manhole; blank windows; navy car blob | The cross street climbs the hill (kerbs, sidewalks, a cut in the terrain) to Rowan Street halfway up and the crest road at the top. Manhole regraded to dark iron; car cover to a faded pale fabric (its folds show). Full-detail houses have a dividing wall and furniture silhouettes behind the windows (sofas, lamps, armchairs, bookcases, tables, dressers). |

Also from pass 2's own list: the **soundscape** (below). The frame-time probe and the RTX 3050 quality settings moved to
the Perf agent (the creator, 2026-10-12): `VhPerfProbe` / `VhPerfPath` (the `-perf-probe` flag) and the `perfbuild` /
`perf` stages of `tools/look-capture.ps1` were already written and are left for the Perf agent to own or replace.

## Levels of detail and cost (the creator: 120 fps target at 1080p, 60 floor; far things must cost far less)

- **Trees** (`VhLookTrees.Lods`, screen-relative heights, all public statics; `VhLookTrees.RetuneLods(root)` re-applies them
  to an open scene): LOD0 full (~580 cards, trunk with roots, limbs, branches, twigs; shadows) to 0.12; LOD1 (~180 bigger
  cards; shadows) to 0.045; LOD2 (~40 big cards and a 6-sided trunk; **no shadows**) to 0.019; LOD3 a **crossed billboard**
  of the painted whole tree (`VH_TreeBillboards`, same foliage shader: lit, backlit glow, no shadows) to 0.004; culled
  beyond. A 10 m tree at 1080p/60 degrees/bias 1: LOD1 ~70 m, LOD2 ~190 m, billboard ~460 m, culled ~2.2 km. Cross-faded
  (dithered). Shrubs: 0.06 / 0.02 / cull 0.006, no billboard. Two materials (bark, foliage), SRP Batcher-compatible
  (all properties in UnityPerMaterial), instancing on.
- **Far houses**: merged into one mesh per material per house, one LODGroup culling at 0.006 (`VhLookHouses.FarCull`).
- **Backdrop**: Simple Lit (no PBR, no normal map, no specular, receives no shadows), casts no shadows, 16 sectors of
  ground and 16 canopy chunks (~17k + ~92k vertices); ring trees use the tree LODs (mostly billboards).
- **Grass tufts**: no shadows, chunked per 24 m.
- **Renderer features and post** (estimates for the RTX 3050 laptop at 1080p, unmeasured): the atmosphere pass (shaft mask
  and two 12-tap blurs at half resolution, one full-resolution composite reconstructing position from depth) ~0.3-0.5 ms,
  the shaft passes skipped when facing away from the sun; the sky ~0.1 ms (two HDRI samples per pixel while drifting);
  the data-driven lens flare (32 occlusion samples) ~0.1 ms; bloom HQ ~0.3-0.4 ms; URP's SSAO at full resolution
  ~1-1.5 ms (half resolution ~0.5 ms: the Perf agent's call); main-light shadows from the rig (4 cascades, 160 m, 4096
  atlas) are the biggest variable: the rig's `shadowDistance`, `shadowResolution`, `cascadeSplits` fields are the knobs.

## What's built

**Sky.** Poly Haven HDRIs (CC0), prepared by `tools/look/hdri-prep.py`. The pick: **Kloppenheim 06, 4K** (gold-lit
cumulus and rays round a low sun, clear blue zenith). Shader `JamKit/Look/Sky`: the HDRI in its own frame via
`_VhSkyMatrix` (the rig's yaw + tilt), slow cloud drift, a limb-darkened HDR disc, glow and halo, a light warm lift on the
sun's side, a narrow horizon band in the atmosphere's haze colours (`_VhHorizonAway/Sun/Focus`, set by the atmosphere
feature), a ground colour below the horizon.

**The rig** (`VhLook.ApplyGoldenHour`): the sun (warm, 3.2, 10 degrees up at bearing 250, soft shadows, Mixed, the SRP
lens flare), a warm shadowless bounce fill, `VhLookRig` (aims the lights, sets the sky matrix and globals, overrides the
URP asset's main-light shadows while enabled: 160 m, 4 cascades, 4096), a global Volume with the post profile, RenderSettings.

**Post** (`VhLookProfiles`): `VH_GoldenHour` (ACES; exposure -0.05; contrast +10, saturation +12; WB +14/+3; split
toning half-lilac shadows, warm highlights; bloom 1.0 / 0.9; vignette 0.22; grain; VhAtmosphere: near haze 0.0032/m from
14 m with height falloff, **far haze 0.0042/m from 150 m**, shafts 0.8), `VH_HouseInterior`, `VH_HospitalDay` (no haze).

**The library**: 26 2K CC0 sets (look-library.md), plus the painted atlases (`tools/look/make-foliage-atlas.py`): leaf
clusters, grass/flower cards, ground decals (litter, oil, dirt, grime), whole-tree billboards, far fields.

**Soundscape** (`Look/Audio/Ambience/`, 28 Ogg clips, 4.5 MB, every source CC0 from Freesound, listed with levels in
`SOURCES.md` there; built by `tools/look/ambience-build.py`): a 78 s stereo Saturday-evening bed (-30 LUFS); robins,
sparrows, a mourning dove; a distant mower; an impact sprinkler; two dogs (near, streets away); soft and gusty leaf wind; a
far little-league game (crowd loop, bat cracks with the cheer, umpire calls); an ice-cream truck playing "The Entertainer"
that drives past now and then. Runtime: `VhAmbienceEmitter` (3D loop and/or random one-shots, bursts, ping-pong or pass
wander, intermittent), `VhAmbienceBed` (2D, fade-in), `VhAmbienceMix` (a master level to duck the whole Saturday).
**Not listened to by any agent**: measured only (loudness, seams, spectra). Give it a listen.

**LookLab** (`VhLookLab.Build`): the street corner (two full-detail houses, fences, beds, a covered car, manhole, drains),
the terrain, the hill streets with ~60 far houses and street trees, stands of trees, the backdrop ring, the ambience, a
local probe volume round the street (150 x 22 x 70 m; the hill and ring are lit by the sky), a perf path along the shots.
**Twelve shots**: round 1's ten (same views) plus `11-into-the-sun-foliage` (under the corner elm's crown edge, the sun
through its lower leaves) and `12-street-wide`.

## The API (for World, House, Day, People)

```csharp
using JamKit.Look.EditorTools;   // asmdef: add "JamKit.Look.Editor" (editor) / "JamKit.Look" (runtime) to your references
var rig = VhLook.ApplyGoldenHour(scene, new VhLook.Options { sunAzimuth = 250f, sunElevation = 10f, hazeBaseHeight = 0f });
VhLook.ApplyHouseInterior(scene);  VhLook.ApplyHospitalDay(scene);
VhLook.SetupCamera(playerCamera);    // HDR, post on, SMAA high, NaN guard, dithering, far clip now 4000 (the ring)
VhLook.Material("Brick_Red");        // VhLookLibrary.Tinted / Paint / Glass / GetTerrainLayer / TerrainLayerVariant / Prop

// Trees (CHANGED): species, stands, LODs. The old Place(parent, pos, seed, height) still works (seed picks species/variant).
VhLookTrees.Place(parent, pos, VhLookTrees.Species.Oak, variant: 1, height: 11f);           // Maple, Oak, Birch, Elm, Shrub
VhLookTrees.Cluster(parent, center, radius: 12f, count: 6, seed: 3, meanHeight: 10f, ground: HeightAt, allowed: p => ...);
VhLookTrees.Scatter(parent, minXZ, maxXZ, clusters: 30, seed: 2, ground: HeightAt, allowed: p => !OnRoad(p));
VhLookTrees.Lods.Lod1 = 0.12f; ...  VhLookTrees.RetuneLods(sceneRoot);                       // the Perf agent's knobs

// Close the world (NEW): call once per outdoor scene (the hill needs it too).
VhLookBackdrop.Build(sceneRoot, new VhLookBackdrop.Options {
    center = new Vector3(0f, 0f, 150f), innerRadius = 400f, outerRadius = 2800f,
    nearHeight = (x, z) => MyGroundHeight(x, z),   // continued past your ground's edge, then blended into the hills
    hole = new Rect(xMin, zMin, width, depth),     // your ground's extent: the ring tucks 0.15 m under it there
    ridgeHeight = 190f });                          // above center.y: high enough to close the horizon from your top
// Ground detail (NEW)
var t = VhLookGround.BuildTerrain(parent, new VhLookGround.TerrainOptions { origin, size, height, lawn, dirt, assetPath });
var tufts = new VhLookGround.Tufts(seed, HeightAt); tufts.Edge(a, b, outward); tufts.Scatter(rect, 0.12f, allowed); tufts.Build(parent);
VhLookGround.Litter(parent, center, radius, quads, seed, HeightAt);
VhLookGround.Stain(parent, pos, size, yaw, VhLookGround.StainKind.Oil);  VhLookGround.StormDrain(parent, kerbPoint, along, intoRoad);
// Houses (NEW): full or far LOD
VhLookHouses.Build(parent, frontLeftCorner, yaw, VhLookHouses.Spec.Random(seed), VhLookHouses.Lod.Far, HeightAt);
// Ambience (NEW)
VhLookAmbience.AddSaturdayDefault(parent, playAreaCenter, seed);   // or AddBed / AddEmitter(parent, Kind.Lawnmower, pos)
VhLook.AddReflectionProbe(scene, center, size);  VhLook.MarkStaticForGI(root);
VhLookCapture.Capture(scenePath, shots, "hill-captures");
```

Rules for builders: **one sun**; **don't set RenderSettings fog/ambient yourself**; **name far scenery "... (backdrop)"**;
**metre UVs**; a scene may place its own **local ProbeVolume** (BakeScene now keeps it instead of forcing a global one).

## Shaders (all URP-standard HLSL; checked offline with `tools/look/shader-check.py`, which compiles every pass with the
Windows SDK's dxc against the project's URP package for D3D11, base and shadow/SSAO/cross-fade variants)

- `Look/Shaders/VhSky.shader` (changed: `_VhSkyMatrix` frame, disc on the light, horizon band in the haze colours, tuned values).
- `Look/Shaders/VhAtmosphere.shader` (changed: far haze term).
- `Look/Shaders/VhFoliage.shader` (changed: rim from UV1 for translucency and painted occlusion, translucency kept in
  self-shadow, LOD cross-fade in all four passes). Used by crowns, tree billboards and grass cards.
- Renderer feature `Look/Scripts/VhAtmosphereFeature.cs` (changed: far haze params, horizon colour globals); volume
  override `Look/Scripts/VhAtmosphere.cs` (changed: `farHaze*` parameters).
- No new shaders: grass and billboards reuse the foliage shader; decals are URP Lit (alpha-clip / transparent); the
  backdrop is URP Simple Lit; the ground is URP's own terrain shader.

## Unity steps for main (one GPU job at a time)

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools/look-capture.ps1            # all: build, bake, capture (12 stills)
# or stage by stage:  -Stage build  |  -Stage bake  |  -Stage capture
```
Output `out/look-captures/<time>/01-...12-*.png` + `shots.txt`; log `out/look-<stage>.log` (grep `JAMKIT_LOOK`). Exit 2 =
the bake failed (captures still made). The build stage now also makes the terrain, ~60 houses and ~1000 trees: expect it
to take several minutes. After the first run, commit what Unity created: the `.meta` files for the new textures
(`Look/Textures/VH_GrassCards.png`, `VH_GroundDecals.png`, `VH_FarFields.png`, `VH_TreeBillboards.png`), the audio
(`Look/Audio/**`), the new scripts and the library's files; `Look/Library/**` (materials, terrain layers, tree meshes
under `Trees/`), `Look/Profiles/**`, `Look/Scenes/**` (scene, `LookLab_Terrain.asset`, lighting data, probe set), and
`Assets/Settings/PC_RPAsset.asset` / `PC_Renderer.asset`.

## Notes for other agents

- **World**: the hill should call `VhLookBackdrop.Build` (with its ground height function and extent) and can switch its
  trees to `VhLookTrees.Place/Cluster` (four species, LODs with impostors); `VhLookHouses` (far LOD) suits houses seen
  only from a distance. The hill must keep `VhLook.SetupCamera`'s 4 km far clip.
- **House**: `VhLookTrees.Place(parent, pos, seed, height)` still works; the yard trees now come out as species by seed
  (seed % 4: maple, oak, birch, elm). Pass a species for control.
- **Perf**: the knobs are listed above; `VhPerfProbe` (`-perf-probe`) and `tools/look-capture.ps1 -Stage perfbuild/perf`
  are yours to keep or replace.

## Pending

- Round 2 judge (captures). Unseen: tree shapes, the terrain blend, tuft density, haze strength, flare, the sky's tilt
  (expected about 3 degrees; check the HDRI's clouds don't look tilted), audio levels in context.
- A midday HDRI for the hospital (uses the bluest golden-hour sky for now).
