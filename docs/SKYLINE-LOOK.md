# Skyline Repo: the look (lighting, sky, post, materials, dressing)

Task 21 (spec section 10, items 1-6 and 12). The arena is a glass mall atrium on a bright, sunny near-future afternoon:
CONCIERGE's white and gold, crisp sun through the skylight, a city of glass towers hazed into a blue sky outside. Not yet
seen in Unity: the next pass (Task 22) builds it, lints it, records it and measures it.

## How it is built

| Part | Where | What |
|---|---|---|
| The afternoon rig | `Editor/Look/SkylineLook.cs` | Visiting Hours' golden-hour rig, copied with afternoon values (calling `VhLook.ApplyGoldenHour` would rebuild Visiting Hours' own profile and sky material, which are not in this tree, so it would write new files into their folders every pass). One Mixed sun with medium soft shadows, a shadowless bounce fill, Visiting Hours' runtime `VhLookRig` aiming them and the sky, a global post volume, render settings (sky ambient, sky reflections, no fog). |
| The sun | same | 41 degrees up from the west-south-west (bearing 235), intensity 2.6, warm white (1, 0.95, 0.87). It rakes in over the south-west walls, lights the north half, the mezzanine and the east shops, and the skylight's beams stripe the floor. Shadows: 80 m, four cascades, 2048 (the golden hour asks 160 m at 4096). |
| The sky | `ThirdParty/Sky/Sky_Kloofendal38d_4k.hdr` (Poly Haven `kloofendal_38d_partly_cloudy_puresky`, CC0), material `Look/SK_Sky_Kloofendal38d.mat` | Blue sky with thin high cloud round a 38-degree sun; a pure sky, so the towers stand in front of it. Prepared by Visiting Hours' `tools/look/hdri-prep.py` (sun found and clamped out, colours measured into `sky.json`). Shown on Visiting Hours' sky shader (`JamKit/Look/Sky`): turned so its sun sits on the light, a drawn disc, slow cloud drift; bluer tint (0.86, 0.9, 1), exposure normalised to Visiting Hours' brightness (x1.92). |
| Ambient | same | Three-colour (Trilight), linear: from above (0.6, 0.66, 0.78), round the horizon (0.62, 0.61, 0.59), from below (0.64, 0.58, 0.5), the sunlit floor's warm bounce; plus the bounce fill (0.42, warm cream). Fix round 1: the sky's own ambient left white walls in shade at about a tenth of the light they needed, slate blue. |
| Post | `Look/SK_Afternoon.asset` (built by `SkylineLook.Afternoon()`) | ACES; exposure +0.1, contrast +12, saturation +6; white balance -1; split toning with slightly cool shadows; **hue vs hue**: oranges pulled up to 8 degrees towards red before the tonemap, gold 2 (keeps the firm's orange orange and CONCIERGE's gold gold in the sun; `tools/skyline/look/grade-check.py` checks the rule: orange hue 20-35, gold 40-50 at sunlit levels); bloom threshold 1.6 / intensity 0.65 / scatter 0.62, no HQ filtering, quarter-resolution start; vignette 0.16; no grain, no haze pass. |
| Cameras | `SkylineLook.SetupCameras` | `VhLook.SetupCamera` on the player's and the stills' cameras: HDR, post on, SMAA high, NaN guard, dithering. The far clip is kept (600 m: the viewmodel rig sits 1 km below). The stills render into a half-float target and are converted to sRGB afterwards (`Editor/Recording/SkylineCapture.cs`): URP renders a camera with a target texture in that texture's format, so the old 8-bit target clamped the scene before the grade (no bloom, nothing above 223). |
| Reflections | `SkylineLook.BakeReflections` | Two box-projected probes (`VhLook.AddReflectionProbe`): the main hall (256 px) and the warm-up hall (128 px), HDR, baked once at build time to `Generated/SkylineArena_Reflections_*.exr` and set as custom cubemaps (the project's quality levels have realtime probes off). A failed bake leaves the sky's reflection and logs `SKYLINE_LOOK reflections NOT baked`. |
| The dressing | `Editor/SkylineArenaDress.cs` (the builder's `Dress(...)`) | See below. Visual-only geometry merged per material by `Editor/Look/SkylineKit.cs` (a few draw calls for hundreds of pieces), every piece under a `SkylineNoCollider` root. |
| Haze | `SkylineLookLibrary.Facade` | Aerial perspective baked into the towers: URP **Unlit** materials (the shader the signs already use in the build) whose textures are baked on each arena build into `Look/Textures/Hazed/` (surface x (1 - h) x the sunlit face's light + the sky's horizon colour x h), one material per haze band and per way a face meets the sun (sunlit / slanting / shaded), in three distance bands (h 0.40, 0.59, 0.76). Fix round 1: the old Lit-with-emission facades used a keyword set nothing else uses, and rendered as plain white boxes in the build. No renderer feature needed. |

**Bevels (fix round 1):** the visible edges are chamfered through `VhLookUv.BevelBox` (the kit's bevel path): the cover pieces, the boost kiosk and the spotlight plinths are drawn as bevelled boxes of their exact bounds (their renderers go; colliders untouched), and so are the cornices, the skylight and roof beams, the mezzanine's edge and handrail, the portal, the pilasters, the planters' rims, mullions, sills and the bases, bands and capitals (collars). Long flush wall cladding stays square (its joints are covered by the reveals, wainscots and skirtings).

Gameplay is untouched: every collider, its position and size, is `GalleriaArena`'s. The dressing re-skins the gameplay
boxes (library materials, metre UVs) and adds renderers only inside their volumes, at most 5 cm proud of a wall, or
out of reach overhead. No new solids anywhere.

## The material catalogue

Materials are assets in `Assets/JamKit/Templates/SkylineRepo/Look/Library/` (`SKL_<name>.mat`; glass `SK_Glass_*` and
glowing `SK_Emissive_*`), rebuilt on every arena build. Pick by name in editor code:
`SkylineLookLibrary.Get("Terrazzo_White")`, `SkylineLookLibrary.Variant("Plaster_White", "Sage", tint)`.
**UV convention (Visiting Hours'): 1 UV unit = 1 metre**; tiling is 1 / tile size. Every library material carries Skyline's
own low-frequency macro variation (`ThirdParty/Library/SK_MacroVariation.png`, the detail map) against visible repetition;
the white plaster takes a gentler one (`Look/Textures/SK_MacroSoft.png`, +-8%). **Both maps are imported linear**: URP
multiplies the albedo by twice the detail map's sampled value, so mid-grey is neutral only when it isn't sRGB-decoded.
Until fix round 1 they were sRGB, which halved every library surface's albedo and turned the noise's dark patches into
blotches (the "stained grey concrete" walls). Visiting Hours' own macro map (`VH_MacroVariation.png`) is imported the
same old way; their session may want the same fix.

| Name | For | Tile (m) | Source |
|---|---|---|---|
| Terrazzo_White | The main hall's floor, the mezzanine's deck: polished white terrazzo with dark warm chips | 2 | PH terrazzo_tiles, recoloured white (`prep.py`, "terrazzo_white") |
| Marble_Cream | The warm-up hall's floor, kiosks, wainscots, planters, the booth's base | 1.5 | PH marble_01 |
| Plaster_White | White cladding: walls, pillars, cornices, beams, the deck's body (tint with `Variant`) | 2 | PH white_plaster_02, made near-white (Visiting Hours' tint recipe) |
| Wood_Pine | Food-stall walls, shop shelving | 1.2 | PH kitchen_wood |
| Wood_Cherry | Lacquered display tables in the shops | 1 | PH lacquered_cherry_wood |
| Parquet_Herringbone | Shop floors | 3.4 | PH herringbone_parquet |
| Shutter_Painted | The warm-up hall's closed shop shutters | 2 | PH painted_metal_shutter, made near-white |
| Ceiling_Tile | The mezzanine's soffit, shop ceilings | 2.4 | Visiting Hours' set (ACG OfficeCeiling001), read by path |
| Concrete | Shop roofs (outside the hall) | 2 | Visiting Hours' Concrete_Kerb (PH concrete_floor_02), read by path |
| Soil | Planter soil | 1.5 | Visiting Hours' Dirt (PH forest_ground_04), read by path |
| Steel_Brushed | Shopfront frames, mullions, rails, kick plates, the portal | 1 | procedural (`make-textures.py`) |
| Gold_Brushed | CONCIERGE's gold: reveals, capitals, frames, the floor inlay | 1 | procedural brushed maps, gold tint |
| Steel_Dark | Graphite powder-coat: fascias, skirting, transoms, sign frames (nearly flat normal, satin: brushed streaks on a frame's underside read as a stretched texture) | 1 | procedural brushed maps, graphite tint |
| Hazard_Orange | Kestrel & Daughters' orange with hazard stripes: the practice gantry only | 1 | procedural |
| Products / EntryMarker / Banners | Shelves of goods (one kind a row: books, shoe boxes, bottles, jars, folded clothes, boxed electronics, cosmetics, toys) / the entry lane's gold chevrons / fabric banners (THE GALLERIA, AT YOUR SERVICE, FOOD COURT, SKY TERRACE) | atlas | procedural |
| Shutter_Painted__Navy / Sage / Sand | The warm-up's closed shops in four colours | 2 | as Shutter_Painted, tinted |
| Facade_<kind>_Band<0-2>_<Sun/Slant/Shade> | The far towers (URP Unlit, light and haze baked; kinds Blue, White, Green, Dark, Bronze, Silver, Stone) | 13 | procedural, baked to `Look/Textures/Hazed/` |
| Ground_Sun | The city's paving outside (Unlit, baked) | 2 | Visiting Hours' Concrete_Kerb, hazed |
| SK_Emissive_Signs / Posters / LightPanel / GoldGlow | Signs, lightbox posters, ceiling panels and downlights (URP Unlit, HDR for bloom) | atlas | procedural |
| SK_Glass_Shopfront / Curtain / Skylight / Counter / Balustrade / Vitrine / Frit | Glass (URP Lit, transparent, specular kept: it reflects the probes and the sky). The skylight: alpha 0.2, a faint blue-green, smoothness 0.97; fritted (milky) bands a metre wide along every beam and round the edge | - | - |
| SKL_Prop_Calathea / Fern / WetFloorSign | Planter and shop plants (alpha-clipped), a wet-floor sign | - | PH calathea_orbifolia_01, fern_02, WetFloorSign_01 (1K) |

Every downloaded source is Poly Haven (CC0; ambientCG was unreachable from this machine on 2026-10-14). The procedural
textures are made by `tools/skyline/look/make-textures.py`. Their lettering uses DM Serif Display (SIL OFL, in the
repo) and Bahnschrift (Windows): the images are ours; no font file is shipped. Nothing non-CC0 is in `Assets/`.

## The dressing (what is where)

- **Main hall walls** (the runnable boxes keep their colliders, lose their renderers): white cladding with marble
  wainscot and steel skirting between the shops; 23 glass shopfronts flush with the runnable face (a wall-run runs on the
  glass; the gondolas' mines sit on it), each with its sign on a graphite fascia and a lit room 4.2 m deep behind (floor,
  painted walls in one of six colours, ceiling with light panels, the brand's own lightbox advert, one warm point light
  without shadows) fitted out by what it sells: shelves of one kind of goods (books, shoe boxes, bottles, jars, folded
  clothes, cosmetics, toys; every shelf a different stretch of its row), clothes rails hung with garments, white display
  plinths with gold tops, a wall of glowing screens, or plants. 19 brands, so no wall shows a shop twice (23 shops: the
  bay behind the service lift's lobby has the lift doors). A gold reveal; a
  glass curtain wall (its pane at the runnable face, so a wall-run above 6 m runs on the glass) with steel mullions
  behind it from 6 m (10 m on the north wall, which has a second row of shops above the mezzanine; 8.2 m on the south
  wall, above the warm-up roof); a white cornice. South wall: four lit vitrines, THE GALLERIA over the doorway, the
  doorway's steel-and-gold portal.
- **The far goal** (fix round 1; the lobby itself is the level's `GalleriaArena.LiftLobby`, under the deck at x 4..8):
  its back wall, in the north wall's bay at x 7 under the deck, is lift doors instead of a shop: brushed-gold doors in a
  steel frame on a cream marble recess, two glowing light columns and an indicator, SKY TERRACE on the fascia; two SKY
  TERRACE banners hang from the beam in front of the deck, over the lobby. Visual only (inside the wall's slab).
- **Skylight**: white beams every 5 m with a gold underline, steel mullions every 2.5 m, tinted glass (alpha 0.2) with
  fritted bands along the beams and round the edge. **Banners** hang from the beams (bottom 10 m, out of reach): navy
  THE GALLERIA and white AT YOUR SERVICE over the west aisle, teal FOOD COURT over the east one, so the hall's halves
  differ at a glance.
- **Mezzanine**: terrazzo deck, white body, ceiling-tile soffit with 36 downlights, graphite and gold lines on its edge,
  a steel handrail and posts behind the glass; the spotlight plinths in graphite with a gold band.
- **Cover**: pillars in white cladding with a graphite base, a gold band and a gold capital; food stalls in pine, each
  with its own menu on both faces (NOODLE BAR, TACO SOL, BAO HAUS, JUICE LAB) and a steel ledge; the info booth in white
  with a marble base and CONCIERGE's screens; kiosks in marble with lightbox adverts; hollow marble planters with soil
  and plants.
- **Floors**: a gold inlay in the terrazzo (a border 1.2 m in, two aisle lines at x = +-10, an octagonal medallion between
  the kiosks), cream marble under the mezzanine.
- **Warm-up hall**: wainscot, cream-marble pilasters with gold bases and capitals, a graphite frieze with a gold line at
  4.6 m, fabric banners between the pilasters, a glowing cove line under the cornice, the glass roof and its beams; eight
  closed shops (shutters in four colours, signs, CONCIERGE's remodel notices); CONCIERGE's welcome screen and two
  lightboxes behind the start; on the doorway's side the GALLERIA sign over the portal, THE GALLERIA and FOOD COURT
  screens either side; the espresso kiosk (the boost ledge) with glowing ESPRESSO signs on all four sides, a gold cornice
  at the ledge, bottles and jars behind its counter glass; the practice gantry and its floor mark in the firm's hazard
  orange. (The wet-floor sign stands only inside a shop, behind glass: a walk-through prop in the open would read as
  phasing.)
- **The city** (backdrop): 34 towers 110-520 m out in seven facades (blue, white, teal, dark, bronze, silver ribbon
  windows, stone) and five shapes (a slab with a setback and a crown, a slender tower with a spire on a stone podium, a
  stepped tower with banded tiers, twin towers on a podium, a slab turned off the grid), three sky-bridges, a monorail on
  piers 160 m north, paved ground; all hazed by distance.

## Sign words (drafts for the creator)

THE GALLERIA / Halcyon Row · est. 2051; CONCIERGE / At your service, always; PLEASE DO NOT RUN / Floor care in progress;
REMODEL IN PROGRESS / Thank you for your patience; WELCOME, VALUED GUEST / Your visit is being recorded; GALLERIA;
ESPRESSO · KIOSK ·; FOOD COURT (arrow); SECURITY RE-DEPLOYMENT (the entry lane); SKY TERRACE / Lifts · Observation deck
(the lifts). Shops: VELLUM BOOKS, ORBIT TOYS, HALCYON OPTICS, NIMBUS TEA, PARCEL & PINE, LUMEN AUDIO, MERIDIAN SHOES,
KITE & CO, BLOOM FLORIST, AURA SKIN, NOVA PHONES, SOLSTICE SPORTS, GILT & GRAIN, COBALT DENIM, PIXEL ARCADE, ATLAS
TRAVEL, HEARTH HOME, VERDE MARKET, QUILL STATIONERY. Menus: NOODLE BAR (Beef pho 12.50, Dan dan noodles 11.00, Chilli
wontons 8.50, Miso ramen 13.25, Spring rolls 6.00, Jasmine tea 3.50); TACO SOL (Al pastor tacos 9.75, Fish tacos 10.50,
Loaded nachos 8.25, Street corn 5.00, Churros 4.75, Horchata 3.75); BAO HAUS (Pork belly bao 7.50, Crispy tofu bao
6.75, Chicken bao 7.25, Sesame fries 4.50, Quick pickles 3.00, Yuzu soda 3.95); JUICE LAB (Green machine 6.50, Sunrise
press 6.25, Acai bowl 9.00, Protein shake 7.00, Ginger shot 2.75, Cold brew 4.25). Posters (brand: headline / line):
VELLUM BOOKS: READ THE SUMMER / 3 for 2 on paperbacks; ORBIT TOYS: PLAY BIG / the new rocket kits; HALCYON OPTICS: SEE
CLEARLY / eye tests, free; NIMBUS TEA: SLOW DOWN / first-flush oolong; PARCEL & PINE: GIFTS, WRAPPED / free wrapping
today; LUMEN AUDIO: HEAR EVERYTHING / noise cancelling, all day; MERIDIAN SHOES: STEP OUT / the city runner; KITE & CO:
THE LINEN EDIT / light layers for the season; BLOOM FLORIST: FRESH TODAY / the peonies are in; AURA SKIN: GLOW / vitamin
C serum; NOVA PHONES: NOVA 9 / brighter. thinner. yours.; SOLSTICE SPORTS: GO FURTHER / members save 20%; THE GALLERIA:
MEMBERS' EVENING / Thursday, late opening; CONCIERGE: AT YOUR SERVICE / ask at the info desk; GILT & GRAIN: WARM FROM
THE OVEN / sourdough, all day; PIXEL ARCADE: HIGH SCORE / new cabinets in store. Banners: THE GALLERIA / Halcyon Row; AT
YOUR SERVICE / CONCIERGE; FOOD COURT / Level 0 · east; SKY TERRACE / Opening soon. Change them in `make-textures.py`
and re-run it.


## Rebuilding the textures

```
uv run python -I tools/skyline/look/fetch.py out/skyline-look-src                       # downloads (164 MB, git-ignored)
uv run --with numpy --with pillow python -I tools/skyline/look/prep.py out/skyline-look-src Assets/JamKit/Templates/SkylineRepo/ThirdParty
uv run --with numpy python -I tools/look/hdri-prep.py out/skyline-look-src/hdri/kloofendal_38d_partly_cloudy_puresky_4k.hdr Assets/JamKit/Templates/SkylineRepo/ThirdParty/Sky/Sky_Kloofendal38d_4k.hdr Assets/JamKit/Templates/SkylineRepo/ThirdParty/Sky/sky.json 12
uv run --with numpy --with pillow python -I tools/skyline/look/make-textures.py Assets/JamKit/Templates/SkylineRepo/Look/Textures
uv run --with numpy python -I tools/skyline/look/grade-check.py      # the grade's orange/gold rule, no Unity needed
```

Import settings (normal maps, linear masks, alpha for the leaves, the HDR sky) are set from code by
`SkylineLookLibrary.ConfigureImporters()` on every arena build: no hand-written `.meta` files.

## Cost (estimates for the RTX 3050 laptop at 1080p, unmeasured until the pass)

About 170 dressing draw calls (merged per material per area; about 60 of them the city's Unlit facades), ~50k dressing
vertices, 23 small point lights without
shadows (Forward+), one shadowed sun (80 m, four cascades, 2048, medium soft shadows), post (bloom quarter-res,
tonemapping, grading, vignette) ~0.3 ms, SMAA ~0.3 ms, two baked probes (no runtime cost), the towers Unlit (cheaper than the Lit
they replaced) and shadowless; the skylight's frit adds a few transparent quads when looking up. Texture memory: 7 library sets at 2K (BC1/BC5/BC3), the 4K sky (BC6H, 8 MB), atlases at 1-2K.

## Frame-time hitches after the ink look (perf round 5, 2026-10-14; from code and logs, not yet re-measured)

**Evidence** (`out/skyline/20261014-full/perf.json`, `stage-d-perf.log`): average 6.9 ms (was 4.6), 1% low 28.9 ms (was 14.7). The 1% low
is the mean of the slowest 1% of frames, here 221 frames, so it is not ten stalls: the ten worst frames (222, 145, 138, 103, 100, 77,
62, 62, 55, 50 ms) add up to about 1 s of the 6.4 s those 221 frames take; the rest is a long tail of 20-40 ms frames. In the three
worst mid-play frames the main thread is idle (cpu 3 to 13 ms, "wait" 72 to 208 ms: it waits for the render thread), and each follows
something drawn for the first time: the first Writ shot (145 ms), the food court coming into view with the first wrecks (222 ms), the
Breacher swap (62 ms). No frame had a garbage collection outside the start-up ones (`gcs` 0), the pipeline-state trace is empty
(`SkylineArena.graphicsstate` is 289 bytes in every pass, so the start cover's "warm from trace" never had anything to warm), and
`SKYLINE_PREWARM drew 38 idle drawings once`: the old warm-up drew only 38 switched-off pooled things.

**Cause (the best fit; the next pass checks it):** on Direct3D 12 the first draw of any shader variant x mesh layout x pass builds a
pipeline state on the render thread. InkLit has four passes (forward, shadow caster, DepthNormals, and the mask for any role), the ink
feature makes the camera render DepthNormals, and the look added ~150 new materials, so the number of states a fight can meet for the
first time went up several times, while the warm-up still covered only hidden pooled objects. Two gaps in particular: (a) every
material of the scene that is not in view in the warm-up hall (the food court's) was first drawn when it came into view; (b) a wreck's
shard mesh has no UVs (a layout nothing else drew) and the mask pass of a role material was never drawn on any layout but the bots'.

**Checked and cleared:** the ink tracer (pooled meshes, no allocation per frame or shot, material from Sprites/Default made once), the
ink feature (no per-frame allocation: static property block, non-capturing lambdas, struct descriptors), `SkylineInkRoles` (materials
cached per source and role, `SetShaderPassEnabled` only when a material is made), the wreck tint materials (shared per colour). Display
bots are capture only.

**Changed:**
- `SkylinePrewarm.DrawEverythingOnce` now also draws, in a player (not the editor, so the recordings are untouched), (1) every material of
  every renderer that is drawing, once, on its own mesh, and (2) every InkLit template (plain, normal, emission, both), with the mask pass
  off and on, on every mesh layout the scene, the bot models, the primitives and the four shard meshes use (up to 14 layouts). Done in chunks
  of 60 drawings, two frames each, within a 12 s budget (it logs `SKYLINE_PREWARM drew N drawings once (...)` and says if it stopped).
- `SkylineStartCover`: the cover may hold up to 20 s (was 8) because the warm-up takes longer; `SkylineStartCover.Done` says when it has lifted.
- `SkylinePerfProbe`: the measured window now starts after both the 3 s warm-up and the cover's lift (the load is behind the cover for
  the player, so it is not play time; `warmupWorstMs` still reports it). It also writes `over` (frames over 16.7, 25, 33, 50, 100 ms),
  `spikes` (the first 80 frames over 25 ms with cpu/physics/wait, the phase and the last `SKYLINE_*` line) and `inkOff`.
- `-skyline-no-ink` (the player argument; `pass.ps1 -Only D -Out <folder> -PerfArgs '-skyline-no-ink'`) switches the ink feature off: one extra
  stage D run prices the ink passes (the full-resolution colour copy, mask, composite and the DepthNormals prepass).

**Expected, not verified:** hitches at first sight of an area and at the first wreck/shot should go; the long tail of 20-40 ms frames should
shrink if it was first-use work. If the new `spikes` still follow no event and the no-ink run is much faster, the cost is the ink passes
themselves (GPU/render thread: 3 shadow cascades at 80 m, a second full-scene pass for DepthNormals, the mask lists, a colour copy at
1080p) and the next step is GPU-side (a half-resolution mask, one renderer list, fewer cascades). Also unexplained: a 93 ms physics step
at 19.9 s, right after the mantle line (`nan-double-jump-mantle`); check whether it repeats.
