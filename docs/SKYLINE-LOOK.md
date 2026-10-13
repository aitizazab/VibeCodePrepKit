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
| Post | `Look/SK_Afternoon.asset` (built by `SkylineLook.Afternoon()`) | ACES; exposure +0.1, contrast +12, saturation +6; white balance -3 (the sun is the only warm thing); split toning with sky-cool shadows; bloom threshold 1.0 / intensity 0.65 / scatter 0.62, no HQ filtering, quarter-resolution start; vignette 0.16; no grain, no haze pass. |
| Cameras | `SkylineLook.SetupCameras` | `VhLook.SetupCamera` on the player's and the stills' cameras: HDR, post on, SMAA high, NaN guard, dithering. The far clip is kept (600 m: the viewmodel rig sits 1 km below). |
| Reflections | `SkylineLook.BakeReflections` | Two box-projected probes (`VhLook.AddReflectionProbe`): the main hall and the warm-up hall, 128 px HDR, baked once at build time to `Generated/SkylineArena_Reflections_*.exr` and set as custom cubemaps (the project's quality levels have realtime probes off). A failed bake leaves the sky's reflection and logs `SKYLINE_LOOK reflections NOT baked`. |
| The dressing | `Editor/SkylineArenaDress.cs` (the builder's `Dress(...)`) | See below. Visual-only geometry merged per material by `Editor/Look/SkylineKit.cs` (a few draw calls for hundreds of pieces), every piece under a `SkylineNoCollider` root. |
| Haze | `SkylineLookLibrary.Facade` | Aerial perspective baked into the towers' materials: surface x (1 - h) + the sky's horizon colour x h as emission, in four distance bands (h 0.34, 0.49, 0.64, 0.77). No renderer feature needed (Visiting Hours' atmosphere pass is not installed on this project's renderer). |

**Bevels (fix round 1):** the visible edges are chamfered through `VhLookUv.BevelBox` (the kit's bevel path): the cover pieces, the boost kiosk and the spotlight plinths are drawn as bevelled boxes of their exact bounds (their renderers go; colliders untouched), and so are the cornices, the skylight and roof beams, the mezzanine's edge and handrail, the portal, the pilasters, the planters' rims, mullions, sills and the bases, bands and capitals (collars). Long flush wall cladding stays square (its joints are covered by the reveals, wainscots and skirtings).

Gameplay is untouched: every collider, its position and size, is `GalleriaArena`'s. The dressing re-skins the gameplay
boxes (library materials, metre UVs) and adds renderers only inside their volumes, at most 5 cm proud of a wall, or
out of reach overhead. No new solids anywhere.

## The material catalogue

Materials are assets in `Assets/JamKit/Templates/SkylineRepo/Look/Library/` (`SKL_<name>.mat`; glass `SK_Glass_*` and
glowing `SK_Emissive_*`), rebuilt on every arena build. Pick by name in editor code:
`SkylineLookLibrary.Get("Terrazzo_White")`, `SkylineLookLibrary.Variant("Plaster_White", "Sage", tint)`.
**UV convention (Visiting Hours'): 1 UV unit = 1 metre**; tiling is 1 / tile size. Every library material carries Skyline's
own low-frequency macro variation (`ThirdParty/Library/SK_MacroVariation.png`, the detail map) against visible repetition.

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
| Steel_Dark | Graphite: fascias, skirting, transoms, sign frames | 1 | procedural brushed maps, graphite tint |
| Hazard_Orange | Kestrel & Daughters' orange with hazard stripes: the practice gantry only | 1 | procedural |
| Products / EntryMarker | Shelves of goods (atlas rows) / the entry lane's gold chevrons | atlas | procedural |
| Facade_<Blue/White/Green/Dark>_Haze<0-3> | The far towers' curtain walls, hazed by band | 13 | procedural |
| Ground_Hazed | The city's paving outside | 2 | Visiting Hours' Concrete_Kerb, hazed |
| SK_Emissive_Signs / Posters / LightPanel / GoldGlow | Signs, lightbox posters, ceiling panels and downlights (URP Unlit, HDR for bloom) | atlas | procedural |
| SK_Glass_Shopfront / Curtain / Skylight / Counter / Balustrade / Vitrine | Glass (URP Lit, transparent, specular kept: it reflects the probes and the sky) | - | - |
| SKL_Prop_Calathea / Fern / WetFloorSign | Planter and shop plants (alpha-clipped), a wet-floor sign | - | PH calathea_orbifolia_01, fern_02, WetFloorSign_01 (1K) |

Every downloaded source is Poly Haven (CC0; ambientCG was unreachable from this machine on 2026-10-14). The procedural
textures are made by `tools/skyline/look/make-textures.py`. Their lettering uses DM Serif Display (SIL OFL, in the
repo) and Bahnschrift (Windows): the images are ours; no font file is shipped. Nothing non-CC0 is in `Assets/`.

## The dressing (what is where)

- **Main hall walls** (the runnable boxes keep their colliders, lose their renderers): white cladding with marble
  wainscot and steel skirting between the shops; 24 glass shopfronts flush with the runnable face (a wall-run runs on the
  glass; the gondolas' mines sit on it), each with its sign on a graphite fascia and a lit room 4.2 m deep behind (floor,
  painted walls in one of six colours, ceiling with light panels, shelves of goods, a lightbox poster, a display table or
  plants, one warm point light without shadows); a gold reveal; a glass curtain wall (its pane at the runnable face, so a wall-run above 6 m runs on the glass) with steel mullions behind it from 6 m (10 m on
  the north wall, which has a second row of shops above the mezzanine; 8.2 m on the south wall, above the warm-up roof);
  a white cornice. South wall: four lit vitrines, THE GALLERIA over the doorway, the doorway's steel-and-gold portal.
- **Skylight**: white beams every 5 m with a gold underline, steel mullions every 2.5 m.
- **Mezzanine**: terrazzo deck, white body, ceiling-tile soffit with 36 downlights, graphite and gold lines on its edge,
  a steel handrail and posts behind the glass; the spotlight plinths in graphite with a gold band.
- **Cover**: pillars in white cladding with a graphite base, a gold band and a gold capital; food stalls in pine with
  two menu boards each (NOODLE BAR, TACO SOL, BAO HAUS, JUICE LAB) and a steel ledge; the info booth in white with a marble
  base and CONCIERGE's screens; kiosks in marble with lightbox adverts; hollow marble planters with soil and plants.
- **Floors**: a gold inlay in the terrazzo (a border 1.2 m in, two aisle lines at x = +-10, an octagonal medallion between
  the kiosks), cream marble under the mezzanine.
- **Warm-up hall**: wainscot, pilasters, a cornice with a gold line under the glass roof and its beams; eight closed shops
  (shutters, signs, CONCIERGE's remodel notices); CONCIERGE's welcome screen and two lightboxes behind the start; on the
  doorway's side the GALLERIA sign over the portal, THE GALLERIA and FOOD COURT screens either side; the espresso kiosk
  (the boost ledge) with glowing ESPRESSO signs on all four sides, a gold cornice at the ledge, goods behind its counter
  glass; the practice gantry and its floor mark in the firm's hazard orange. (The wet-floor sign stands only inside a shop, behind glass: a walk-through prop in the open would read as phasing.)
- **The city** (backdrop): 30 glass towers 110-520 m out with setbacks and crowns, three sky-bridges, a monorail on
  piers 160 m north, paved ground; all hazed by distance.

## Sign words (drafts for the creator)

THE GALLERIA / Halcyon Row · est. 2051; CONCIERGE / At your service, always; PLEASE DO NOT RUN / Floor care in progress;
REMODEL IN PROGRESS / Thank you for your patience; WELCOME, VALUED GUEST / Your visit is being recorded; GALLERIA;
ESPRESSO · KIOSK ·; FOOD COURT (arrow); SECURITY RE-DEPLOYMENT (the entry lane). Shops: VELLUM BOOKS, ORBIT TOYS,
HALCYON OPTICS, NIMBUS TEA, PARCEL & PINE, LUMEN AUDIO, MERIDIAN SHOES, KITE & CO, BLOOM FLORIST, AURA SKIN, NOVA PHONES,
SOLSTICE SPORTS. Food stalls: NOODLE BAR, TACO SOL, BAO HAUS, JUICE LAB (dishes: Signature bowl, Spicy special, Combo
deal, Side of greens, Daily soup, Iced drink). Posters: NEW SEASON, SALE, SUMMER, LIMITED, NOW OPEN, FRESH, ICONS, THE
EDIT, FREE GIFT, MEMBERS, LAUNCH, STYLE, TASTE, SOUND, LIGHT, PLAY. Change them in `make-textures.py` and re-run it.

## Rebuilding the textures

```
uv run python -I tools/skyline/look/fetch.py out/skyline-look-src                       # downloads (164 MB, git-ignored)
uv run --with numpy --with pillow python -I tools/skyline/look/prep.py out/skyline-look-src Assets/JamKit/Templates/SkylineRepo/ThirdParty
uv run --with numpy python -I tools/look/hdri-prep.py out/skyline-look-src/hdri/kloofendal_38d_partly_cloudy_puresky_4k.hdr Assets/JamKit/Templates/SkylineRepo/ThirdParty/Sky/Sky_Kloofendal38d_4k.hdr Assets/JamKit/Templates/SkylineRepo/ThirdParty/Sky/sky.json 12
uv run --with numpy --with pillow python -I tools/skyline/look/make-textures.py Assets/JamKit/Templates/SkylineRepo/Look/Textures
```

Import settings (normal maps, linear masks, alpha for the leaves, the HDR sky) are set from code by
`SkylineLookLibrary.ConfigureImporters()` on every arena build: no hand-written `.meta` files.

## Cost (estimates for the RTX 3050 laptop at 1080p, unmeasured until the pass)

About 120 dressing draw calls (merged per material per area), ~40k dressing vertices, 24 small point lights without
shadows (Forward+), one shadowed sun (80 m, four cascades, 2048, medium soft shadows), post (bloom quarter-res,
tonemapping, grading, vignette) ~0.3 ms, SMAA ~0.3 ms, two baked probes (no runtime cost), the towers cheap and
shadowless. Texture memory: 7 library sets at 2K (BC1/BC5/BC3), the 4K sky (BC6H, 8 MB), atlases at 1-2K.
