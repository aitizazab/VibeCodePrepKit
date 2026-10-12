# Look: lighting, sky, post, the material library (the Look agent's status)

Owner: the Look agent (docs/REDESIGN.md section 5). Pass 1 was judged FAIL 5/3/3/4 (judge/look-1.md), pass 2 FAIL 6/5/4/6
(judge/look-2.md: autumn foliage, the near ground reading as a lake, the polka-dot backdrop, hard-rectangle decals).
**Pass 3** (this commit) answers every defect in look-2.md and the Perf agent's list for Look (perf.md section 7). It is
**not yet seen in Unity** (agents can't run it): main runs `tools/look-capture.ps1`, a new judge scores the twelve
captures (the same twelve cameras), and the Perf agent's probe measures it.

## Pass 4 (after judge round 3: FAIL 5/5/5/6; look-3.md)

| Round 3 defect | Pass 4 |
|---|---|
| Black, unhazed far hills; backdrop darker than the lawn | Two causes. (1) The haze composite rebuilt positions with the pass's own UNITY_MATRIX_I_VP / _WorldSpaceCameraPos, which weren't the camera's inside the full-screen draw: every pixel came out a few metres away, so there was no haze anywhere, in any round. The feature now passes the camera itself (position, axes, FOV, its own depth linearisation) and logs JAMKIT_LOOK atmosphere: depth bound/MISSING. (2) The project uses Adaptive Probe Volumes; the foliage and FarLand shaders read per-object SH, which came out black. They now use a sky/horizon/ground hemispherical ambient from the rig's globals (VhLookRig.ambientSky/Equator/Ground). |
| Hard orange disc (08) | The sun's lens flare is off (VhLook.Options.lensFlare = false). |
| No sun disc | Not the ridge: the whole sunward sky sat at the tonemapper's ceiling (229, 216, 198), so a disc couldn't be brighter. Sky x(0.8, 0.83, 0.88), the sun side dimmed to 0.55, glow and halo cut; the haze colours follow. |
| Warm, crushed grade | The warmth is the sun's alone (1, 0.84, 0.66): white balance +2, no colour filter, split toning near-neutral with cool shadows, SMH cool shadows, saturation -8, contrast +5, exposure +0.3; bounce neutral. |
| Topiary crowns | Generator 5: 3 branches a limb, shell holes filled with clumps on short branches from the nearest limb, a small dark leaf ball inside every clump (the gaps show leaves, not sky), clumps grow rather than move when merged (the spikes), clump cells fade to a round ragged edge inside the cell (no straight card edges), yellowed leaves 1.5% and only on outer tops, a greener rim; birches fuller (clumps 1.4 m); translucency 2.6, focus 2.5. |
| Siding seams | Every wall column is cut at every opening's heights: no T-junctions, no light through the cracks. |
| Flat lawns, empty hill lots | Stronger dry and lush patches in the terrain mix (clover layer darker); 2-4 foundation shrubs in front of every hill house. |
| Prism hedgerows | Knobbly tops (fast noise), crowns every 12-22 m, trees standing out, the line wandering, sampled every 4-16 m. |
| Window blocks | The sheer covers both sashes. |
| Kerb tuft runs | Runs 0.3-1.8 m, gaps 0.8-3.5 m. |

## Pass 3: the judge's defects and what changed

| # | Defect (round 2) | Pass 3 |
|---|---|---|
| 1 | BLOCKER: autumn foliage (the leaf atlas averaged hue 65: yellow; gold translucency; warm tints) | Leaf atlas repainted as dense summer clumps (deep, mid and olive greens, a warm yellow-green only on the clumps' sunlit rims, ~4% yellowed leaves; average hue 95). Foliage tints neutral-green; translucency yellow-green (0.72, 0.95, 0.34) instead of gold; the foliage shader's new `_LightTint` (0.88, 1, 0.9) keeps the warm key light from turning leaves gold. Tufts green with dry gold tips. Billboards repainted. All checked by `tools/look/palette-check.py` (below). |
| 2 | BLOCKER: the near ground reads as a lake; far hills sharp, near washed; a seam at the backdrop | Three causes. (a) The lawn's mask smoothness averages 0.73 and the layer multiplied it by 0.6: a sky mirror at grazing angles. Now x0.07 (about 0.05); dry grass x0.09. (b) **The atmosphere's composite most likely never saw the depth texture** (it relied on the global `_CameraDepthTexture`, which Render Graph doesn't guarantee inside a pass that doesn't declare it): every pixel read as sky, so no haze reached anything and the shaft mask used sunlit ground as sky (likely round 2's "giant orange flare disc" too). The pass now binds the depth texture itself. (c) Near haze halved (0.0016/m from 25 m, max 0.6), far haze carries the distance (0.0034/m from 120 m, max 0.95): far is always paler than near, the ring included. The ring's ground starts as the lawn's own colour at the scene's edge and blends into the fields over ~250 m (FarLand shader, vertex alpha): no seam. |
| 3 | BLOCKER: polka-dot backdrop (canopy blobs dotted over lime hills) | The blobs are gone. The ring is a patchwork of fields (pasture, hay, stubble, crops, ploughed, meadow; 90-260 m fields in 250-450 m blocks, turned 17 degrees off the street), with **continuous hedgerows and tree lines along the field edges** (3D strips 4-6 m wide, 3.5-8 m tall with taller trees in them, out to 1.9 km; beyond that the painted ones), **woods over whole fields** (a lumpy canopy 11-17 m high) and **forest along the rim's ridges**, all dark olive and hazed. One layout (`VH_FarFields.json`) drives the painted and the 3D hedgerows. |
| 4 | BLOCKER: stain decals render as hard rectangles | URP Lit's "preserve specular lighting" (default for transparents) kept the sky's reflection at full strength over the whole quad, alpha 0 or not: pale rectangles. Now off, no environment reflections, no highlights, matte; and every stain fades to zero well inside its atlas cell (the streak ran to its cell's edge). |
| 5 | No sun disc, a cream wash, a giant soft orange flare disc over the road | The wide warm-glow flare element is gone (the glow round the sun smaller and fainter). The stand of trees that hid the sun in 08 is moved off the sun's line, and nothing tall is placed within 8 degrees of it. Disc brighter and crisper (160, 0.55 deg), wide halo 0.18 -> 0.07, warm lift lighter, the sun side of the HDRI dimmed 18% (`_SunSideDim`) and bloom cut (threshold 1.0, intensity 0.7, scatter 0.62) so the gold-rimmed clouds read out of the haze. Shafts 0.8 -> 0.6 (and, with the depth fix, from the sky only). |
| 6 | Trees: twig spikes, loose floating cards, pale seam rings at limb joints, straight limbs forking from one point | Trees rebuilt (generator 4). Wood is one swept tube per trunk, limb and branch: parallel-transported rings, continuous bark UVs, no joints along it, capped thick ends; limbs bend along a curve and taper, leave the trunk at staggered heights (never one fork point), their bases sunk inside the trunk. Every leaf clump sits on a branch tip and every tip ends inside its clump; no twigs; tips kept inside the crown outline. Pass 2's tubes were wound inside-out in places and met at angles (the pale rings). |
| 7 | Tufts: identical stickers like confetti; a picket line along the kerb | `Tufts.Scatter` makes drifts (6-26 tufts thinning out from a core, each drift its own size, height and tint; ~10% lone clumps); `Tufts.Edge` runs of 0.5-3 m with gaps of 0.3-2.5 m, heights rising and falling along a run, irregular spacing and set-back. |
| 8 | Floating porch frame on the neighbour house | The porch's skirt and steps (and a stoop's) now reach the real ground in front of the door (pass 2 stopped them at the highest ground under the house: on the downhill side the porch hung in the air). Downspouts reach the ground at their corner. |
| 9 | Birch trunks bright yellow sticks with tiny crowns | Painted chalk-white birch bark (`VH_BirchBark`, dark lenticels, black knots) on the library bark's normal map; a wider, fuller crown (3 m x 3.7 m) reaching down to ~3 m. |
| 10 | Stone foundations a third of the wall high | Concrete shows at most ~0.35 m above the lowest ground; on a slope the walls carry on down in their own siding (a walk-out basement). |
| 11 | Every house faces the same way; identical rows | A third of `Spec.Random` houses are gable-front (narrower, deeper, steeper roof, the door, porch and windows on the street gable). LookLab's hill houses: set back 13.5-20 m, turned up to 6 degrees off the street, lots 25-35 m wide. |
| 12 | The road stops in a field (09) | `VhLookBackdrop.Options.roads`: roads draped on the ring's rendered surface. LookLab's main road runs on both ways and curves off between the fields; Rowan Street and the crest road continue a way each side. Hedgerows and ring trees keep clear of them. |
| 13 | Brown blocks in the windows (04) | A sheer net curtain behind the lower sash, a dimmer, less orange room, darker and more opaque glass: the room is a soft glow, not blocks. |
| 14 | Vertical siding joints line up (04) | The clapboard texture has one butt joint running up every course at the same place. Full-detail clapboard pieces are now cut into their courses (16 per 2 m repeat, the first lap line 0.115 m up) and each course's texture shifted by its own amount (the same all round the house): joints staggered course by course. |

## The palette (pass 3; numbers, checked)

Albedo targets (sRGB 0-255, average of the opaque pixels after the material tint; `tools/look/palette-check.py` measures
them and fails outside the windows). Material tints set with `Material.SetColor` are sRGB (Unity converts them to linear);
terrain-layer remaps are linear multipliers; the checker treats each correctly.

| Entry | Target window (hue deg / sat / value) | Measured (pass 3) |
|---|---|---|
| Leaves, whole atlas (summer) | hue 92-118, sat 0.40-0.70, value 0.22-0.42 | (52, 79, 33): hue 95, sat 0.59, value 0.31 |
| Leaves per species | maple/elm 92-118, oak 85-115, birch 88-118, shrub 95-125 | 95 / 90 / 95 / 102 |
| Autumn-coloured leaf pixels (hue < 60, sat > 0.35) | at most 6% | 3.6% |
| Tree billboards | hue 88-118 | (55, 82, 37): hue 96 |
| Grass tufts (green) / (dry-tipped) | hue 88-112 / 70-100 | 91 / 78 |
| Lawn (terrain) | hue 84-104, sat 0.50-0.75, value 0.35-0.50 | (83, 116, 46): hue 88 |
| Dry grass (terrain) | hue 45-62, value 0.50-0.70 | (161, 153, 82): hue 54 |
| Concrete (sidewalk) | sat <= 0.15, value 0.60-0.78 (the brightest surface on the street) | (176, 167, 154): sat 0.12, value 0.69 |
| Asphalt | sat <= 0.10, value 0.28-0.40 | (84, 85, 80): sat 0.06, value 0.33 |
| Far fields (patchwork average) | hue 70-105, sat 0.35-0.65 | (100, 111, 55): hue 72 |

Rendered targets for the judge (display sRGB in the captures; not checkable offline, sample them in the stills):

| | Target | Notes |
|---|---|---|
| Sky zenith, away from the sun | about (110, 145, 195) | clear blue |
| Sky horizon away / sunward | about (205, 205, 200) / (245, 225, 185) | the far haze dissolves into these |
| Sunlit foliage | hue 70-90 (yellow-green), e.g. (150, 165, 70) | warm light on green leaves; hue below 55 = autumn = fail |
| Shaded foliage | hue 95-130, e.g. (45, 68, 42) | stays green |
| Lawn sunlit / in shade | about (130, 150, 70) / (60, 82, 55) | matte: no pale sky sheen at grazing angles |
| Dry grass sunlit | about (190, 170, 105) | patches, not the whole hill |
| Concrete sunlit / in shade | about (225, 205, 175) / (125, 128, 138) | |
| Asphalt sunlit | about (95, 90, 85) | |
| Shadow tint | cool blue-grey, B >= R, e.g. (70, 78, 96) on a neutral surface | |
| Far hills at 1.5 km | within ~15% of the horizon's brightness, sat < 0.25 | far always paler than near |

## Trees: overdraw (round 2: "~580 alpha cards per near crown is heavy")

| | Pass 2 (LOD0) | Pass 3 (LOD0) |
|---|---|---|
| Cards per near crown | ~580 (maple, elm), 620 (oak), 520 (birch), 150 (shrub) | maple ~120, oak ~120, birch ~100, elm ~110, shrub ~24 (5-6 cards round each of ~20 clumps) |
| Card area per 10 m crown | ~415 m2 | ~310 m2 (maple, estimated; about -25%) |
| Opaque share of a card | 33% (two thirds of the pixels shaded and clipped) | ~45% |
| Draw order | random | outermost cards first, so most inner cards fail the depth test early |
| LOD1 / LOD2 | ~180 / ~40 cards | ~40 / ~10 cards |

The build logs the exact numbers per species and variant: `JAMKIT_LOOK tree <species> <variant>: LOD0 N cards (A m2 of
card at 10 m), LOD1 .., LOD2 ..`. Vertices per crown fall by ~80%. Wood: a 12-sided trunk and 8-sided limbs, ~1.5-2.5k
vertices a tree, LOD2's 5/4-sided wood a few hundred.

## The Perf agent's list for Look (perf.md section 7)

| # | Item | Done |
|---|---|---|
| 1 | SSAO: half resolution, depth source, lower blur | `Assets/Settings/PC_Renderer.asset`: Downsample on, Source Depth (no DepthNormals prepass), BlurQuality Medium; `VhLookSetup` enforces it on every build. |
| 2 | Bloom: no HQ filtering, quarter-res start | `VhLookProfiles`: all three profiles. |
| 3 | Atmosphere: quarter-res shafts, 8 taps, skip facing away | `VhAtmosphereFeature` / `VhAtmosphere.shader` (no shaft passes at all when facing away). Plus the depth-binding fix. |
| 4 | VhLookRig reads `VhPerfTiers.Current`; delete the old probe | The rig caps distance, cascades and resolution to the active tier while one is applied (`JamKit.Look` now references `JamKit.Perf`). `Look/Scripts/VhPerfProbe.cs` deleted (Perf/Probe's replaces it). |
| 5 | Audio imports | The 28 ambience metas now carry the import settings (loops and the bed compressed in memory and loaded in the background, one-shots decompressed on load; Unity's own metas from the play copy). Voice and music belong to other owners: `VhLookAudioImports` (menu "JamKit/Look/Stream Long Voice and Music Clips", or `-executeMethod JamKit.Look.EditorTools.VhLookAudioImports.ConfigureFromCommandLine`) sets clips of 8 s or more to Streaming, Vorbis, background loading; main decides when to run it (it edits Day's and Battle's metas). |
| 6 | A low-tier renderer without SSAO | `VhLookSetup.EnsureLowRenderer` makes `Assets/Settings/VH_Low_Renderer.asset` (a Unity copy of PC_Renderer, SSAO off) on every LookLab build. Perf: point VH_Low_RPAsset at it in `VhPerfSetup`. |
| 7 | DepthOnly / DepthNormals for the DreamPastel shaders | Added to `Dream/Shaders/DreamPastel.shader` and `DreamPastelMesh.shader` (same CBUFFER layout, SRP Batcher safe). Depth priming is now the Perf agent's call. |

## History: pass 2 (judged in round 2)

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
  to an open scene): LOD0 full (pass 3: ~100-120 cards in ~20 clumps, swept trunk with roots, limbs, branches; shadows) to
  0.12; LOD1 (~40 bigger cards; shadows) to 0.045; LOD2 (~10 big cards and 5-sided wood; **no shadows**) to 0.019; LOD3 a **crossed billboard**
  of the painted whole tree (`VH_TreeBillboards`, same foliage shader: lit, backlit glow, no shadows) to 0.004; culled
  beyond. A 10 m tree at 1080p/60 degrees/bias 1: LOD1 ~70 m, LOD2 ~190 m, billboard ~460 m, culled ~2.2 km. Cross-faded
  (dithered). Shrubs: 0.06 / 0.02 / cull 0.006, no billboard. Two materials (bark, foliage), SRP Batcher-compatible
  (all properties in UnityPerMaterial), instancing on.
- **Far houses**: merged into one mesh per material per house, one LODGroup culling at 0.006 (`VhLookHouses.FarCull`).
- **Backdrop** (pass 3): the FarLand shader (wrap-diffuse sun without shadows, SH ambient, fog; no specular, no normal
  map), casts and receives no shadows, no probes: 16 sectors of ground (~17k vertices), hedgerows in up to 16 sector meshes
  (sampled every 6-25 m by distance, out to 1.9 km; a few tens of thousands of vertices), one woods mesh, one rim-forest
  mesh, one mesh per far road: ~35 draws. 24 stands of LOD trees in its near part (mostly billboards).
- **Grass tufts**: no shadows, chunked per 24 m.
- **Renderer features and post** (estimates for the RTX 3050 laptop at 1080p, unmeasured; pass 3): the atmosphere pass
  (shaft mask and two 8-tap blurs at quarter resolution, skipped entirely facing away from the sun; one full-resolution
  composite reconstructing position from depth) ~0.2-0.4 ms; the sky ~0.1 ms; the lens flare ~0.1 ms; bloom without HQ
  filtering from quarter resolution ~0.15 ms; SSAO at half resolution from the depth texture ~0.5 ms; main-light shadows
  from the rig (asks 4 cascades, 160 m, 4096; capped by the Perf tier: 150 m / 2048 High, 80 m / 1024 Low) are the
  biggest variable.

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
toning half-lilac shadows, warm highlights; pass 3: bloom 0.7 / threshold 1.0 / scatter 0.62, no HQ, quarter start;
vignette 0.22; grain; VhAtmosphere: near haze 0.0016/m from 25 m with height falloff, max 0.6, **far haze 0.0034/m from
120 m**, max 0.95, shafts 0.6), `VH_HouseInterior`, `VH_HospitalDay` (no haze).

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

**Pass 3 API changes** (all additive; existing calls compile and keep working):

```csharp
// Backdrop: roads into the ring, the patchwork's turn, how far the 3D hedgerows go; the canopy blobs are gone.
VhLookBackdrop.Build(root, new VhLookBackdrop.Options { /* as before */
    roads = { new VhLookBackdrop.FarRoad { points = new[] { new Vector2(335f, 0f), new Vector2(900f, -60f) }, startY = 0f, width = 8f } },
    fieldRotation = 17f, hedgeDistance = 1900f, woods = true, rimForest = true });     // treeClusters now 24 by default
// Houses: gable-front (the street gable carries the door, porch and windows). Spec.Random makes a third of them.
var spec = VhLookHouses.Spec.Random(seed); spec.gableFront = true;                     // corner/yaw/width/depth mean the same
// Foundations show <= 0.35 m of concrete (a siding skirt below the floor on slopes); porches, stoops and downspouts reach
// the real ground; full-detail clapboard is cut into courses (VhLookHouses.SplitCourses) so the joints stagger.
// Ground: Tufts.Scatter makes drifts and Tufts.Edge runs with gaps (same signatures); Tufts.Add(..., groupTint) is optional.
// Library: BirchBark(), Sheer(), FarRoad(), FarFieldsLayoutPath, BirchBarkPath, LawnAverage; Grass_Lawn/Concrete/Asphalt tints
// and the ground's smoothness changed (look-library.md, "Pass 3 changes").
// Setup: VhLookSetup.LowRenderer ("Assets/Settings/VH_Low_Renderer.asset", SSAO off) for the Perf agent's VH Low tier.
// Rig: VhLookRig caps its shadow request by JamKit.Perf.VhPerfTiers.Current while a tier is applied.
// Audio: VhLookAudioImports.Configure() streams VO and music clips of 8 s or more (run on request).
```

Rules for builders: **one sun**; **don't set RenderSettings fog/ambient yourself**; **name far scenery "... (backdrop)"**;
**metre UVs**; a scene may place its own **local ProbeVolume** (BakeScene now keeps it instead of forcing a global one).

## Shaders (all URP-standard HLSL; checked offline with `tools/look/shader-check.py`, which compiles every pass with the
Windows SDK's dxc against the project's URP package for D3D11, base and shadow/SSAO/cross-fade variants)

Pass 3:
- `Look/Shaders/VhFarLand.shader` (**new**): the backdrop's ground, hedgerows, woods and roads. ForwardLit (wrap-diffuse
  sun, SH ambient, fog, vertex colour; vertex alpha blends from `_NearColor` to the texture), DepthOnly, DepthNormals.
- `Look/Shaders/VhFoliage.shader` (changed: `_LightTint` on the direct sun and the translucency).
- `Look/Shaders/VhSky.shader` (changed: `_SunSideDim`, the HDRI dimmed on the sun's side; material values in VhLookSky).
- `Look/Shaders/VhAtmosphere.shader` (changed: 8-tap blur); `Look/Scripts/VhAtmosphereFeature.cs` (changed: binds the depth
  texture itself, quarter-res shafts, no shaft passes facing away).
- `Dream/Shaders/DreamPastel.shader`, `DreamPastelMesh.shader` (changed: DepthOnly and DepthNormals passes; the Perf
  agent's list. The colour passes are untouched).

Pass 2: `VhSky` (`_VhSkyMatrix` frame, disc on the light, horizon band in the haze colours), `VhAtmosphere` (far haze),
`VhFoliage` (rim from UV1, translucency kept in self-shadow, LOD cross-fade in all four passes). Decals are URP Lit
(alpha-clip / transparent); the ground is URP's own terrain shader.

## Unity steps for main (one GPU job at a time)

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools/look-capture.ps1            # all: build, bake, capture (12 stills)
# or stage by stage:  -Stage build  |  -Stage bake  |  -Stage capture
```
Output `out/look-captures/<time>/01-...12-*.png` + `shots.txt`; log `out/look-<stage>.log` (grep `JAMKIT_LOOK`). Exit 2 =
the bake failed (captures still made). The twelve cameras are unchanged (same views as rounds 1 and 2).

Pass 3, after pulling into the play copy: the build regenerates the atlases' imports, the library materials (tints, matte
ground, the new FarLand materials, birch bark, sheer), the tree meshes (generator 4; it deletes generator 1-3's cached meshes
from `Look/Library/Trees/`), the profiles (haze, bloom), the renderer (SSAO settings) and `Assets/Settings/VH_Low_Renderer.asset`.
Grep the build log for `JAMKIT_LOOK tree` (card counts and areas per species) and `JAMKIT_LOOK backdrop` (hedgerow and
wood counts). Commit what Unity creates: the `.meta` files for `Look/Textures/VH_BirchBark.png`,
`Look/Textures/VH_FarFields.json`, `Look/Shaders/VhFarLand.shader`, `Look/Editor/VhLookAudioImports.cs`, and
`Assets/Settings/VH_Low_Renderer.asset` (+ its meta). The `perf` stage of this script is retired (the Perf agent's probe
replaces it).

Optional, main's call (it edits Day's and Battle's audio metas): `-executeMethod
JamKit.Look.EditorTools.VhLookAudioImports.ConfigureFromCommandLine` (streams VO and music of 8 s or more).

## Notes for other agents

- **World**: the hill should call `VhLookBackdrop.Build` (with its ground height function and extent) and can switch its
  trees to `VhLookTrees.Place/Cluster` (four species, LODs with impostors); `VhLookHouses` (far LOD) suits houses seen
  only from a distance. The hill must keep `VhLook.SetupCamera`'s 4 km far clip.
- **House**: `VhLookTrees.Place(parent, pos, seed, height)` still works; the yard trees now come out as species by seed
  (seed % 4: maple, oak, birch, elm). Pass a species for control.
- **Perf**: point `VH_Low_RPAsset`'s renderer list at `Assets/Settings/VH_Low_Renderer.asset` (`VhLookSetup.LowRenderer`)
  in `VhPerfSetup`; depth priming is yours to try now that the DreamPastel shaders have depth passes; the Look's old probe
  is deleted. The rig now caps itself by the tier, so `CapRigs` and the rig agree.
- **World**: pass `roads` to `VhLookBackdrop.Build` for the hill's streets that leave the play area (they're draped on the
  ring and the hedgerows keep clear of them); the far houses from `Spec.Random` now include gable-front ones (a third);
  your tufts (`Tufts.Scatter`) now come out as drifts at the same average density.

## Pending

- Round 3 judge (captures). Unseen in Unity: the new crowns and wood, the hedgerows and woods at distance, the haze now
  that it reads depth (strength may need a retune either way), the decals, the sun disc and clouds sunward, the far roads
  on the ring, the porch and foundation fixes, the course-split siding.
- If the haze still doesn't reach the far ring, the depth texture isn't reaching the composite: check
  the "VH Atmosphere Composite" pass's `_CameraDepthTexture` in the Frame Debugger.
- A midday HDRI for the hospital (uses the bluest golden-hour sky for now).
