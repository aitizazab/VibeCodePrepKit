# Perf: the frame-time bar, the tools and the numbers (the Perf agent)

Owner: the Perf agent (docs/REDESIGN.md sections 3 and 9). Paths: `Assets/JamKit/Templates/VisitingHours/Perf/**`,
`ProjectSettings/QualitySettings.asset`, and the quality fields of the URP assets. Status 2026-10-12: **the tools are
built and compile against Unity's DLLs, but none of it has run in Unity yet.** No number below has been measured. The
first probe run is the baseline.

**The bar.** The target is 120 fps at 1080p on the RTX 3050 laptop: 8.3 ms a frame on average. The floor is 60 fps on the
1% lows: the mean of the slowest 1% of frames under 16.7 ms, measured in a build. A scene that can't open in the editor
under the 4.6 GB cap is a blocker too.

## 1. The perf probe (a player flag)

```
VisitingHours.exe -perf-probe "C:/.../out/perf/<time>" [-perf-scene hill1,hill2,house,escape,ward,battle]
    [-perf-warmup 3] [-perf-max 150] [-vh-tier high|low] [-perf-windowed] [-perf-stay]
    -logFile "C:/.../out/perf/<time>/player.log"
```

- **What it does:** it skips the opening and goes to the title. Then, for each scene, it starts the scene (through the
  presenter's chapters; the ward goes through the slice), warms up for 3 s with the camera at the flight's start, and then
  flies the camera:
  - **hill1** (Night 1): every leg of the critical path at 7 m/s at eye height (the polylines come from
    `DreamStreetGame.LintRoutes`), then each of the 25 judge views, held 2.5 s while panning 30°.
  - **hill2** (Night 2, `night = 2`): Night2Start to the gate along Rowan Street, the middle band's escape line, then the
    views above the house.
  - **house**: the middle of each of the 31 rooms at 1.5 m, looking all the way round in 3 s.
  - **escape**: down the middle of every escape room at 9 m/s. The game's logic is paused so the fold can't throw the
    probe out.
  - **ward**: the 18 framed views of `VhWardShot` (Day 1, Day 2 and the booth), each panning 40°. It enters each area
    before its first view.
  - **battle**: Dream-Lena, "watch it play" (the autopilot), for 45 s on the battle's own camera.
- **How it runs:** 1920x1080 (full-screen window), vSync off, frame rate uncapped. Frames within 0.5 s of a cut (a
  teleport, entering a ward area) count as **hitches**, not as steady frames.
- **Output** in the folder:
  - `<scene>.json`:
    - the verdict;
    - frame stats: mean, p50, p90, p99, 1% low, worst, and the share of frames over 8.3 and 16.7 ms;
    - CPU main thread, render thread and GPU times, with the GPU p99;
    - draw calls, SetPass calls, batches, triangles, vertices and shadow casters (mean and max);
    - hitches, peak memory (total, system, gfx, GC heap), and GC collections;
    - **every leg** (what, where, mean, p99, worst, GPU, CPU, draws, triangles, casters), so a slow street corner has a
      name;
    - the machine and settings: GPU, VRAM, API, CPU, RAM, resolution, quality level, tier, URP asset, render scale,
      upscaler, MSAA, shadows, LOD bias, vSync, development or release build.
  - `summary.txt`: one line per scene plus its five worst legs.
  - `summary.json`: every scene in one file.
  - Log lines start with `JAMKIT_PERF`. The last one is `JAMKIT_PERF_DONE` (exit 0) or `JAMKIT_PERF_FAIL` (exit 1: a
    scene couldn't be measured, and the reason is in its JSON).
- **What each build reports:**
  - Frame times and memory: any build.
  - CPU and GPU times (FrameTimingManager): need Frame Timing Stats. A development build has it, and `VhPerfBuild`
    turns it on for a release build.
  - Draw calls, SetPass calls, triangles and shadow casters (ProfilerRecorder): **development build only**.
  - So main makes two builds. The **release** build is the bar. The **development** build supplies the counters.
- **The paths follow the content.** They are computed from the game's data (LarchHill, DreamHouse, HouseEscape,
  VhWardShot), so when a builder moves a street, the probe follows it.
- The Look agent's first probe (`Look/Scripts/VhPerfProbe.cs`, flag `-perf-probe` with no folder) is replaced. This probe
  destroys it if both are in a build. **Look: delete `Look/Scripts/VhPerfProbe.cs`.** Keep `VhPerfPath`: the editor
  estimate reads it on LookLab.

Code: `Perf/Probe/` (`VhPerfProbe`, `VhPerfRecorder`, `VhPerfFlight`, `VhPerfScenes`), assembly `JamKit.Perf.Probe`. It
allocates nothing per frame.

## 2. The editor estimate (no GPU)

```
Unity.exe -batchmode -nographics -projectPath . -executeMethod JamKit.Perf.EditorTools.VhPerfSceneReport.RunFromCommandLine
    -perfScene all|<scene path> [-perfOut out/perf-report/<time>] -logFile out/perf-report.log -quit
```

The menu has it too: JamKit > Perf > Report Open Scene.

It takes the probe's flights as viewpoints (up to 120), or LookLab's `VhPerfPath`, or the main camera. For each viewpoint
it counts:
- triangles in view, by LOD band (each LODGroup's choice at lodBias 1);
- draw calls (sub-meshes);
- shadow casters within the tier's shadow distance;
- realtime lights in view and shadowed point lights.

It flags views over the budget: **1.5M triangles, 2500 draws, 1500 casters, 16 realtime lights and 2 shadowed point
lights a view**. That budget is a guide until the probe calibrates it.

It also writes:
- scene totals: renderers, LOD groups, meshes, materials, triangles at full detail;
- **the editor's memory after opening the scene** (Unity's allocation and the process's committed memory, against the
  4.6 GB cap);
- the audits (section 5).

Output: `<scene>.txt` and `<scene>.json`.

## 3. Quality tiers and central settings

| | VH High (default) | VH Low (weaker machines) |
|---|---|---|
| Render scale / upscaler | 0.8 with STP (forces TAA; MSAA off) | 0.6 with STP |
| Sun shadows (cap on the Look rig) | 150 m, 4 cascades, 2048 map, soft Medium | 80 m, 2 cascades, 1024, soft Low |
| Point/spot shadows | on (atlas 2048) | off |
| LOD bias / cull scale | 1.0 / 1.0 | 0.6 / 0.7 |
| Textures | full res, mip streaming 1024 MB | half res (mip limit 1), streaming 512 MB |
| Opaque texture | off (nothing samples it) | off |
| Frame cap | 120 (vSync off) | 60 |

- **How a tier is chosen and applied** (`Perf/Scripts/VhPerfTiers.cs`, runtime, nothing per frame):
  - When a Visiting Hours scene loads, the tier's quality level becomes active.
  - The Look rig's shadow override (`VhLookRig`: 160 m, 4 cascades, 4096) and the lights' soft-shadow quality are
    capped to the tier.
  - The choice is `-vh-tier high|low` if given, else the saved choice (`VhPerfTiers.SetTier`, for a settings menu), else
    a guess (under 3.5 GB of VRAM means low).
  - Another template's scene puts its own quality level back. `-vh-uncapped` lifts the frame cap.
  - Builders who make lights at runtime (the ward, the title) call `VhPerfTiers.CapNow()` once after building.
- **What I changed, exactly:**
  - `ProjectSettings/QualitySettings.asset`:
    - added the levels "VH High" and "VH Low". Both point at PC_RPAsset until VhPerfSetup runs.
    - "Mobile" and "PC" are untouched, so other templates keep theirs.
  - **The URP assets:**
    - Nothing is edited in `Assets/Settings/PC_RPAsset.asset`. The Look agent has an uncommitted change there, and other
      templates share it.
    - `VhPerfSetup` makes `Perf/Settings/VH_High_RPAsset.asset` and `VH_Low_RPAsset.asset` as copies of PC_RPAsset,
      re-synced on every run. It then sets these fields only: `m_RenderScale`, `m_UpscalingFilter` (STP), `m_MSAA`,
      `m_RequireOpaqueTexture` (off), `m_RequireDepthTexture` (on), `m_ShadowDistance`, `m_ShadowCascadeCount`,
      `m_MainLightShadowmapResolution`, `m_SoftShadowQuality`, `m_AdditionalLightShadowsSupported`,
      `m_AdditionalLightsShadowmapResolution`, `m_AdditionalLightsCookieResolution` (1024), `m_EnableLODCrossFade`, and
      `m_Cascade2Split` on Low.
    - The renderer (PC_Renderer: SSAO, the atmosphere feature) is the Look agent's and is untouched.
  - `VhPerfSetup` also:
    - adds the layers "PerfSmall" and "PerfDetail" (for `VhPerfCullDistances`) to TagManager;
    - applies the **texture budgets** to every 3D texture under Visiting Hours: mip streaming on; normal and mask maps at
      most 1024 (albedo and characters keep 2048); compressed; normal maps imported as normal maps. Sprites, UI, 2D art,
      skies and readable textures are left alone.
    - sets **prop model imports** to no cameras, no lights, and no Animator rig.
  - An import hook turns on mip streaming for new 3D textures.
  - World's `HillMesh.Build` gets one line: `VhPerfMesh.Compact(mesh)` (see section 6). Its asmdef now references
    `JamKit.Perf.Editor`.
  - `core-tests/UnityLayer*`: the Perf folders were added to the compile checks.
- **What I didn't change, and why:**
  - **The 4096 shadow map in VhLookRig.** It's the Look agent's, and the tier caps it at runtime anyway.
  - **SSAO settings** and **depth priming**: these belong to the Look agent, and depth priming is unsafe (section 7).
  - **ProjectSettings.asset** (IL2CPP, stack traces): not my file. Handed to main.

## 4. The tools (API for builders)

Add `"JamKit.Perf.Editor"` to the editor asmdef. For the runtime pieces (`VhPerfTiers`, `VhPerfCullDistances`), add
`"JamKit.Perf"` instead.

```csharp
using JamKit.Perf.EditorTools;

// LODs. Distances in metres, converted to screen heights for a 65-degree camera at lodBias 1. Cross-fade is on.
VhPerfLod.ChunkLods(chunkRoot, VhPerfLod.ChunkSpec.Hill, detail: matName => ...);  // a merged chunk: LOD1/LOD2 per material
VhPerfLod.AddLods(go, new[] { new VhPerfLod.Level(40f, 0.15f), new VhPerfLod.Level(120f, 0.6f) }, cullAt: 400f, farRenderers: null);
VhPerfLod.CullBeyond(cupGo, 25f);                 // a one-level LODGroup: gone when it's as small as at 25 m
VhPerfLod.CullSmall(roomRoot, maxSize: 1.2f);     // every small renderer: a 0.3 m cup at 24 m, a 1 m chair at 80 m

// Meshes.
VhPerfMesh.Simplify(mesh, cell: 0.5f);            // crack-free vertex clustering (cells share one position; hard edges kept)
VhPerfMesh.ThinCards(mesh, keepOneIn: 3, grow: 1.6f);  // grass, flowers and leaf cards: fewer, bigger
VhPerfMesh.Compact(mesh);                         // half-precision normals and tangents: 36 bytes a vertex, not 48
VhPerfMesh.Save(mesh, path, container);           // compact + save (as a sub-asset when container is an asset) + not readable
VhPerfMesh.MarkNotReadable(mesh);                 // the build drops the CPU copy (not for meshes the geometry lint reads)

// Impostors: a cross-billboard captured from the real mesh under the scene's light (needs a GPU, not -nographics).
var imp = VhPerfImpostor.Capture(treeGo, "Assets/.../Generated/Impostors/oak", cellSize: 256, planes: 3);
imp.Attach(tree.transform);                       // or emit its quads into a merged mesh with imp.cells / imp.size

// Shadows.
VhPerfShadows.Apply(root, VhPerfShadows.Rules.Outdoor);   // small things, cards and "(backdrop)" cast no shadows
VhPerfShadows.LimitShadowedLights(houseRoot, 2);          // keep the 2 strongest shadowed point/spot lights

// Draw calls, culling, occlusion.
VhPerfBatching.MarkStatic(root);                          // batching, occludee, probes static; big solids become occluders
VhPerfBatching.MergeByMaterial(propsRoot, 40f, folder);   // small static props: one renderer per chunk and material
var cull = sceneRoot.gameObject.AddComponent<JamKit.Perf.VhPerfCullDistances>();  // PerfSmall 25 m, PerfDetail 60 m
// Occlusion bake (CPU, on the saved scene):
//   -executeMethod JamKit.Perf.EditorTools.VhPerfOcclusion.BakeFromCommandLine -perfScene <path>

// Audits (also JamKit > Perf > Audit Open Scene).
VhPerfAudit.Materials(renderers);  // shaders outside the SRP Batcher, MaterialPropertyBlocks, repeated pairs, duplicate materials
VhPerfAudit.Lights(lights, probes); // weak realtime lights, shadowed point lights, High soft shadows, realtime probes
VhPerfAudit.Memory(renderers);      // mesh and texture memory, readable meshes, wide vertices, unstreamed or uncompressed textures

// Runtime.
JamKit.Perf.VhPerfTiers.CapNow();                         // after building lights at runtime
JamKit.Perf.VhPerfTiers.SetTier(JamKit.Perf.VhPerfTiers.Low, save: true);
```

Notes:
- **SRP Batcher vs. instancing.** With the SRP Batcher on, URP ignores GPU instancing on SRP-compatible materials. So for
  repeated trees and props the wins are merging per chunk (fewer renderers to cull) or the GPU Resident Drawer (section
  7), not the instancing checkbox. The audit reports repeated mesh and material pairs as candidates.
- **Mesh and texture memory.** Generated LOD meshes are saved inside the LOD0 mesh's asset (no new files), compacted and
  not readable.

## 5. Memory: why the hill needed more than 4.6 GB, and the disk

**What the crash log shows.** The street capture opened DreamStreet (51,405 objects; scene integration took 79 s). It ran
out of memory in its first `ScriptableRenderContext.Cull`. Unity's own allocators held only about 0.95 GB at that point
(GFX 383 MB, cache objects 378 MB, default 178 MB). The rest of the 4.6 GB committed was:
- the scene's objects;
- Mono's heap from building the scene;
- the graphics driver's commit for uploaded resources.

**Where the weight is** (measured on the play checkout's generated hill):
- 3,876 mesh assets, 525 MB on disk (text YAML, hex-encoded).
- 5.26M vertices and 2.7M triangles, all at full detail at every distance.
- 48 bytes a vertex, all readable.
- So: 256 MB of vertex and index data, twice over (CPU and GPU copies).
- An index-to-vertex ratio of 1.45: faces don't share vertices (flat faces with hard edges).

**Fixed in shared code:**
- `VhPerfMesh.Compact` in HillMesh.Build: −25% mesh RAM, VRAM and disk (about −65 MB of vertex data each way, −130 MB
  on disk).
- The tools save LOD meshes compacted and not readable.

**World's list:**
- **LODs** (`ChunkLods`): far chunks drop 60-90% of their triangles, and the shadow passes drop with them.
- **Mark the chunk meshes not readable** once the geometry lint reads colliders instead of render meshes: −256 MB RAM in a
  build. Until then the lint needs them readable.
- **Fewer, bigger cards** on far chunks.

**The editor cap** is mostly object count (51k objects) and the full-detail scene. Run the editor estimate under the cap
first: it reports the committed memory after opening.

**Disk:**
- C: had **4.2 GB free** at 22:00, which is why the scene rebuild failed writing a mesh.
- Big items:
  - `VibeJamKit-play/Builds` 2.8 GB (old builds: prune);
  - `VibeJamKit/Library` 4.0 GB (this checkout only compiles checks: `Library/Artifacts` and `Library/Bee` could go, but
    Unity would reimport if opened here: main's call);
  - `VibeJamKit-play/Library` 3.3 GB;
  - the generated hill, 525 MB.
- Compaction takes the hill to about 400 MB.
- The rest would need binary serialization for generated assets (EditorSettings is Force Text; Mixed would halve them, but
  changes how other assets save: test on a copy) or building the hill at load time.
- No duplicated per-chunk meshes were found: only 87 identical collider wedges, 0.3 MB.

## 6. Free and cheap wins, ranked (estimates; the probe will confirm)

| # | Win | Where | Est. saving | Risk | Applied / whom |
|---|---|---|---|---|---|
| 1 | Render scale 0.8 + STP | URP tier assets | 2-4 ms GPU | softer image, TAA ghosting on leaves | **applied** (VhPerfSetup, VH High) |
| 2 | Sun shadows capped: 150 m, 2048, soft Medium | VhPerfTiers caps VhLookRig (160 m / 4096) | 1-2.5 ms GPU | softer far shadows | **applied** (runtime cap) |
| 3 | LODs for the hill's chunks (trees, houses, cards) | DreamStreetBuilder | 2-5 ms GPU + shadow passes | LOD seams if mistuned | **World** (one call per chunk) |
| 4 | Shadowed point lights: House 9 (54 cube faces), ward 5 | HouseFurnishing, VhWardFloor / VhWakingWard | 1-3 ms each scene | lamps lose contact shadows | **House, Day** (`LimitShadowedLights(root, 2)`) |
| 5 | Small props cast no shadow | ward `VhMeshKit.Show` (shadows on by default), house props, hill props | 1-2 ms | none visible | **Day, House, World** (`VhPerfShadows.Apply`) |
| 6 | SSAO: half resolution, depth source, lower blur | PC_Renderer | 0.5-1.5 ms GPU, 1-3 ms render thread | softer AO | **Look** |
| 7 | IL2CPP for Standalone, stack traces off for Log/Warning | ProjectSettings.asset | 1.3-2x C# speed; less GC on logs | longer builds; reflection stripping (keep stripping Low) | **main** |
| 8 | Ward: per-room culling or cull distances; build in steps | VhWardFloor (thousands of renderers, built in one frame) | 1-3 ms CPU; a 0.3-1.5 s load hitch | low | **Day** |
| 9 | Opaque texture off | tier URP assets | 0.1-0.2 ms | none (unused) | **applied** |
| 10 | Bloom: no HQ filtering, quarter-res start | VhLookProfiles | 0.2-0.5 ms | slightly blockier bloom | **Look** |
| 11 | Atmosphere pass: quarter-res shafts, 8 taps, skip when facing away | VhAtmosphereFeature | 0.1-0.3 ms | none | **Look** |
| 12 | Mip streaming + normal and mask maps at 1024 | texture importers | 100-150 MB VRAM; fewer cache misses | close-up normal detail | **applied** (VhPerfSetup) |
| 13 | Frame cap 120 / 60 per tier | VhPerfTiers | steadier pacing, less thermal throttling | none | **applied** |
| 14 | Per-frame garbage: `LarchHill.Spot(id)` lambda 4-14 times a frame (~120 KB/s) | DreamStreetGame | GC spikes every few seconds | none | **World** (cache the spots) |
| 15 | Per-frame strings and 3 `MarkDirtyRepaint` a frame | ArgumentStage | 0.5-1 ms CPU + garbage | none | **Battle** |
| 16 | Per-frame strings and layout from `style.width` | HouseHud, HideAndSeek, VisitingHoursGame.PromptFor, StealthHud | garbage + layout | none | **House, Day** |
| 17 | Blend shapes written every frame, a MaterialPropertyBlock per face decal (off the SRP Batcher) | VhFace | 0.3-0.5 ms for 10 people | none | **People** |
| 18 | Load hitches: Decompress On Load VO and music; no shader warm-up; the cut-outs' first-flip readback | audio metas; GraphicsStateCollection; DreamCutoutView | 20-150 ms spikes | none | **Look** (audio), **People** (cut-outs), **main** (warm-up) |
| 19 | Realtime reflection probes never render (realtime probes off in quality) | VhWardLight, HouseFurnishing | correctness, not ms | none | **Day, House** (bake them, or Look's bake) |
| 20 | Maximum Allowed Timestep 0.333 → 0.1 | TimeManager.asset | no catch-up spirals after a hitch | none | **main** |
| 21 | GPU Resident Drawer (BRG) | URP asset + Graphics settings (BRG stripping Keep All) | 2-5 ms CPU with thousands of renderers | custom shaders lack DOTS instancing; conflicts with static batching and MaterialPropertyBlocks | **later**: try it with the probe |
| 22 | Readable runtime textures (stand-ins, HUD) | VhTextures, VhHud, VhBattleView | memory | none | **Day, Battle** (`Apply(true, true)` / `Compress`) |
| 23 | Title: Nadia skinned off-screen (`updateWhenOffscreen`), lights left on at intensity 0, UI panels redrawing off camera | VhStageBuilder, VhLighting, VhScreenPanel | 0.3-1.5 ms (title) | none | **Day** (title owner) |
| 24 | Props' model imports: an Animator on every chair | Dream/House models | small CPU, import time | none | **applied** (VhPerfSetup) |
| 25 | Shutter: `GetComponent` every frame and moving a static collider | DreamStreetGame.TickShutter | 0.05-0.2 ms | none | **World** |

## 7. Lists per builder

**World** (the hill, `Dream/Editor/Street/**`):
1. **LOD every chunk.** In `DreamStreetBuilder.BuildScene`, after each `m.Build(drawn, MeshFolder)`:
   `VhPerfLod.ChunkLods(go, VhPerfLod.ChunkSpec.Hill, id => id == HillMat.Flowers || HillMat.Find(id)?.kind == HillMat.Kind.Decal)`.
   - Full detail to 60 m, 0.2 m cells and half the cards to 160 m, 0.9 m cells and one card in five beyond.
   - The material name must be the HillMat id; pass your own predicate if it isn't.
   - Saving: 2-5 ms GPU and most of the shadow cost.
   - Tune `lod1At` / `lod2At` against the captures.
   - Backdrop chunks get `new ChunkSpec { lod1At = 0.1f, ... }`, or no LOD0 at all.
2. **Far trees as impostors.** `VhPerfImpostor.Capture` once per tree kind in a lit preview (HillKitPreview), then emit a
   cross-card per tree into an impostor material sink for LOD2 instead of thinned cards. This is the creator's "tree behind
   the hospital". Saving: about 95% of a far tree's triangles.
3. **Shadows.** `VhPerfShadows.Apply(drawn, Rules.Outdoor)` after building. Better still, put props smaller than 0.5 m in
   a non-shadow material id.
4. **Occlusion.** `VhPerfBatching.MarkStatic(drawn)`, then main bakes occlusion on DreamStreet. Terraces hide most of the
   town from street level, which cuts the CPU culling and the draws.
5. **Memory.**
   - Switch the geometry lint to colliders (or keep a list of the meshes it needs), then call
     `VhPerfMesh.MarkNotReadable` on the chunk meshes: −256 MB RAM in a build.
   - `Compact` is already in.
6. **GC.** Cache `LarchHill.Spot(id)` results in `Start` (`DreamStreetGame` lines 346-467). Cache the shutter's collider
   and give it a kinematic Rigidbody.

**House** (`Dream/Editor/House/**`, `Dream/Scripts/House/**`):
1. **Lights.** `VhPerfShadows.LimitShadowedLights(houseRoot, 2)`. There are 89 point lights and 9 shadowed now, which is
   54 cube faces a frame. Bake the lamps that never change and keep an emissive bulb. Make the memories' glow lights
   emissive-only or off when far away. Saving: 1-3 ms.
2. **Small props.** `VhPerfLod.CullSmall(houseRoot)` and `VhPerfShadows.Apply(houseRoot, Rules.Indoor)`. Saving: 0.5-1.5
   ms.
3. **Occlusion.** `VhPerfBatching.MarkStatic` on the house body, then main bakes occlusion on DreamHouse: rooms hide rooms.
   The escape's rolling rooms must not be occluder-static.
4. **The six realtime box probes** (`OnAwake`) never render: realtime probes are off in every quality level. Bake them.
5. **HUD.** Build "Theo's counting N" only when N changes, and use `style.scale` for the eye bar (`HouseHud.cs` 209-211).

**Day** (the ward and the title, `Scripts/Waking/**`, `Scripts/World/**`, `Scripts/Opening/**`):
1. **Shadowed lights.** About 31 realtime point lights; 4-5 cast soft shadows. Keep 1-2 near the player (a spot instead of
   a point where possible), and call `JamKit.Perf.VhPerfTiers.CapNow()` after building an area. Saving: 2-5 ms.
2. **Small parts.** `VhMeshKit.Show` defaults to `shadows=true`. Default it to false for parts under about 15 cm and for
   decoration. Saving: 1-2 ms.
3. **The floor.** Build it room by room across frames, cache meshes by size (`VhMeshKit.Box` makes a new mesh per call),
   and free the source meshes after `StaticBatchingUtility.Combine`. Saving: a 0.3-1.5 s hitch and tens of MB.
4. **Culling.** Root the props per room and disable rooms out of view, or use `VhPerfCullDistances`. Saving: 1-3 ms CPU.
5. **Probes.** The 6 realtime probes don't render (realtime probes are off in quality). Bake them or render them from code
   with a quality override.
6. **Title.** Nadia's `updateWhenOffscreen` off with fixed bounds; disable lights at intensity 0; disable the screen
   panels' UIDocuments when off camera.
7. **Textures.** `VhTextures`: `Compress(true)` then `Apply(false, true)`.
8. **Prompt string.** Cache `PromptFor` per spot.

**Look** (`Look/**`, `Assets/Settings/PC_Renderer.asset`):
1. **SSAO.** Downsample on, Source Depth (it drops the DepthNormals prepass), BlurQuality Medium or Low. Saving: 0.5-1.5
   ms GPU and 1-3 ms render thread.
2. **Bloom.** `highQualityFiltering` off and downscale Quarter in `VhLookProfiles` (lines 92, 150, 185). Saving: 0.2-0.5
   ms.
3. **Atmosphere.** Quarter-res shafts, 8 taps, skip when `facing < 0`. Saving: 0.1-0.3 ms.
4. **Rig and probe.**
   - Let `VhLookRig` read its shadow numbers from `JamKit.Perf.VhPerfTiers.Current` (add `"JamKit.Perf"` to
     JamKit.Look's references) instead of 160/4/4096; the reflection cap works meanwhile.
   - Delete `Look/Scripts/VhPerfProbe.cs` (replaced).
5. **Audio imports.** The 29 ambience loops (no metas yet) and the VO and music clips: Compressed In Memory for loops,
   Streaming for music and VO, load in background. Saving: 20-150 ms load spikes.
6. **A low-tier renderer.** A copy of PC_Renderer without SSAO for VH Low (`VhPerfSetup` can point the Low asset at it
   once it exists).
7. **Depth priming.** Only after `DreamPastel.shader` and `DreamPastelMesh.shader` get DepthOnly and DepthNormals passes:
   right now they're missing from the depth texture (the haze and SSAO treat them as sky: a correctness bug too). Priming
   would then save 0.5-2 ms of foliage overdraw.

**People** (`Characters/**`, `Dream/Scripts/DreamCutoutView.cs`):
1. **VhFace.** Write only the blend shapes that changed. Skip faces that aren't visible. Update the decal only when its
   cell changes, and use a material per cell instead of a MaterialPropertyBlock (SRP Batcher). Saving: 0.3-0.5 ms.
2. **Cut-outs.** Prewarm every pose's paper mesh in the builder: the first flip does a synchronous readback, a 5-30 ms
   spike. Mark the paper meshes `UploadMeshData(true)`.
3. **LODs for the 3D people.** `VhPerfLod.AddLods(person, new[] { new Level(15f, 0.03f), new Level(40f, 0.1f) })`, or
   decimated LOD meshes from Tripo.

**Battle** (`Argument/**`):
1. **Per-frame strings.** `ArgumentStage.cs` makes new strings every frame (704, 807-812, 1199, 1208): rebuild them only
   when the values change.
2. **Repaints.** Three `MarkDirtyRepaint` calls a frame (634-636): repaint the backdrop only on change and the monitor at
   30 Hz; use scale for the patience fill. Saving: 0.5-1 ms CPU.
3. **Blips.** `ArgumentAudio`: cache the blip names, and prewarm the first blip.

**Main** (settings outside Perf's paths):
- `ProjectSettings.asset`:
  - Standalone scripting backend IL2CPP, managed stripping Low (plus a link.xml for Ink and reflected Look fields).
  - Stack traces None for Log and Warning.
  - `bakeCollisionMeshes` 1.
- `TimeManager.asset`: Maximum Allowed Timestep 0.1.
- Shader warm-up: record a GraphicsStateCollection on a probe run and warm it up behind the black cards.
- Disk (section 5).

## 8. Unity steps for main (one GPU job at a time)

1. **Setup** (CPU; also writes metas for the new Perf files):
   ```
   Unity.exe -batchmode -nographics -projectPath . -executeMethod JamKit.Perf.EditorTools.VhPerfSetup.ApplyFromCommandLine -logFile out/perf/setup.log -quit
   ```
   Expect `JAMKIT_PERF_SETUP_OK`. Commit:
   - `Perf/Settings/VH_High_RPAsset.asset` and `VH_Low_RPAsset.asset` (+ metas);
   - `ProjectSettings/QualitySettings.asset` and `ProjectSettings/TagManager.asset`;
   - the texture and model `.meta` changes.
   Add `-perfNoTextures` to skip the importer pass (it reimports textures: minutes).
2. **The editor estimate** (CPU, `-nographics`, under the memory cap; it also tells whether DreamStreet opens within 4.6 GB):
   ```
   Unity.exe -batchmode -nographics -projectPath . -executeMethod JamKit.Perf.EditorTools.VhPerfSceneReport.RunFromCommandLine -perfScene all -perfOut out/perf-report/<time> -logFile out/perf-report.log -quit
   ```
3. **Builds** (the scenes must be in Build Settings, as for the VH play build):
   ```
   Unity.exe -batchmode -projectPath . -executeMethod JamKit.Perf.EditorTools.VhPerfBuild.BuildFromCommandLine -jamOut Builds/Perf -logFile out/perf/build.log -quit
   Unity.exe -batchmode -projectPath . -executeMethod JamKit.Perf.EditorTools.VhPerfBuild.BuildFromCommandLine -jamOut Builds/PerfDev -perfDev -logFile out/perf/build-dev.log -quit
   ```
   - The first is a release build with Frame Timing Stats on: CPU and GPU times. It is the bar.
   - The second is a development build: draw calls, triangles, SetPass calls and shadow casters.
4. **The probe**, nothing else running (GPU; about 8-10 minutes for all six scenes):
   ```
   Builds/Perf/VisitingHours.exe -perf-probe "<abs>/out/perf/<time>-release" -logFile "<abs>/out/perf/<time>-release/player.log"
   Builds/PerfDev/VisitingHours.exe -perf-probe "<abs>/out/perf/<time>-dev" -logFile "<abs>/out/perf/<time>-dev/player.log"
   ```
   Read `summary.txt`. For the low tier: `-vh-tier low` into another folder.
5. **Occlusion bakes**, once World and House have marked static (CPU, on the saved scenes, after their rebuilds):
   ```
   Unity.exe -batchmode -nographics -projectPath . -executeMethod JamKit.Perf.EditorTools.VhPerfOcclusion.BakeFromCommandLine -perfScene Assets/JamKit/Templates/VisitingHours/Dream/Scenes/DreamStreet.unity -logFile out/perf/occlusion-street.log -quit
   ```
   Then the same with `DreamHouse.unity`. Today nothing is occluder-static, so it reports that and exits 2.

## 9. Not verified

None of this has run in Unity. The things most likely to need a fix:
- the vertex layout `Compact` uses (it restores the mesh and warns on failure);
- the impostor capture's black/white alpha recovery;
- `VhPerfSetup`'s SerializedObject edits of QualitySettings and TagManager;
- the probe's presenter jumps between scenes (it goes back to the title and retries once);
- the ward's area entry.

The ms savings are estimates from reading code, not measurements.
