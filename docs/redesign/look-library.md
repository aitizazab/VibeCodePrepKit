# The Look library: catalogue

Visiting Hours' shared materials. Pick by name in editor code:

```csharp
var m = VhLook.Material("Brick_Red");                                   // or VhLookLibrary.Get("Brick_Red")
var paint = VhLookLibrary.Tinted("Clapboard_Painted", "Sage", new Color(0.72f, 0.8f, 0.66f));  // a house colour
var trim = VhLookLibrary.Paint("TrimWhite", new Color(0.95f, 0.94f, 0.9f));                     // trim, frames, doors
var glass = VhLookLibrary.Glass();                                       // windows (reflects, shows the room)
var grass = VhLookLibrary.GetTerrainLayer("Grass_Lawn");                 // Unity Terrain layer (sets marked "terrain")
VhLookTrees.Place(parent, position, seed: 3, height: 9f);               // a stylized tree (bark + leaf crown)
VhLookLibrary.Prop("CoveredCar", parent, position, yaw);                 // a library prop with collider
```

**UV convention: 1 UV unit = 1 metre.** Every material's tiling is 1 / (its tile size), so a mesh with metre UVs shows the
texture at its real size. Get metre UVs with `VhLookUv.BoxProject(go)` (any box-like mesh, world-aligned so neighbouring
pieces continue the pattern), `VhLookUv.BevelBox(size, bevel)` / `VhLookUv.Block(...)` (a bevelled box, edges catch the
low sun), or ProBuilder's world-space auto UVs at scale 1. Every material also carries a low-frequency colour variation
(the detail map, ~7 repeats wide) that breaks up visible tiling.

Sources: 2K CC0 sets from Poly Haven (PH) and ambientCG (ACG), prepared by `tools/look/prep-library.py` (albedo lightly
simplified and colour-enriched for the stylized look; mask packed as URP's metallic/occlusion/height/smoothness).
Textures: `Assets/JamKit/Templates/VisitingHours/ThirdParty/LookLibrary/<Name>/`. Materials: `.../Look/Library/VHM_<Name>.mat`.

| Name | For | Tile (m) | Texels/m | Source |
|---|---|---|---|---|
| Asphalt | Roads, driveways: worn asphalt with hairline cracks | 4 | 512 | PH asphalt_02 |
| Road_LaneLine | A two-lane road with worn white edge lines and a dashed centre (lines run along V; stretch one repeat across the road) | 4 | 512 | ACG Road007 |
| Concrete_Sidewalk | Sidewalks and paths: slabs with joints | 3 | 683 | PH concrete_pavement |
| Concrete_Kerb | Kerbs, steps, wall caps, foundations | 2 | 1024 | PH concrete_floor_02 |
| Grass_Lawn (terrain) | Mown late-summer lawns, verges | 2.5 | 819 | ACG Grass004 |
| Grass_DirtMix (terrain) | Worn grass: paths across lawns, under trees, field edges, the hillside | 3 | 683 | ACG Ground037 |
| Dirt (terrain) | Bare earth with pebbles: beds, the creek bank, the bike yard | 3 | 683 | PH forest_ground_04 |
| Gravel (terrain) | Gravel drives and paths | 2 | 1024 | ACG Gravel023 |
| Brick_Red | Red brick: shops, chimneys, the school | 2 | 1024 | PH red_brick_03 |
| Brick_Dark | Older dark brick: the church, the hospital base, garden walls | 2 | 1024 | PH brick_wall_001 |
| Siding_WoodBrown | Stained wooden lap siding | 2 | 1024 | ACG WoodSiding008 |
| Siding_WoodRed | Barn-red siding: sheds, Walt's shop | 2 | 1024 | ACG WoodSiding005 |
| Clapboard_Painted | Painted clapboard, near-white: tint it per house (`Tinted`) | 2 | 1024 | ACG WoodSiding009 |
| Roof_ShinglesAsphalt | Asphalt roof shingles (most houses) | 2.5 | 819 | ACG RoofingTiles013A |
| Roof_ShinglesSlate | Slate-grey shingles (church, older houses) | 2.5 | 819 | ACG RoofingTiles003 |
| Stucco | Stucco walls, near-white: tint it | 2 | 1024 | PH white_stucco_02 |
| Deck_Planks | Decks, porches, the bridge's boards | 2 | 1024 | ACG Planks023A |
| Fence_Planks | Board fences and gates (boards along V) | 2 | 1024 | PH weathered_planks |
| Galvanized_Corrugated | Corrugated galvanized sheet: sheds, lean-tos, culverts, flashing | 2 | 1024 | PH corrugated_iron_02 |
| Creek_Pebbles (terrain) | The creek bed and shallows | 2 | 1024 | PH ganges_river_pebbles |
| Creek_Rock (terrain) | Boulders, outcrops, creek banks | 3 | 683 | PH dry_riverbed_rock |
| Tree_Bark | Trunks and limbs (the tree generator uses it) | 1.5 | 1365 | PH bark_brown_02 |
| Hospital_Terrazzo | The hospital's floors | 2 | 1024 | ACG Terrazzo013 |
| Ceiling_Tile | Suspended ceiling, 0.6 m tiles | 2.4 | 853 | ACG OfficeCeiling001 |
| Plaster_Painted | Painted plaster, near-white: tint it (also the base of `Paint`) | 2.5 | 819 | PH painted_plaster_wall |
| Plaster_Interior | Smooth interior walls, near-white: tint it | 2.5 | 819 | PH beige_wall_001 |

Extras: `VHM_Glass` (window glass), `VHM_Foliage` / `VHM_Foliage_Dry` (leaf crowns, shader `JamKit/Look/Foliage`, a painted
2x2 leaf-cluster atlas made for the project), props `CoveredCar` (PH covered_car) and `ManholeCover` (PH water_manhole_cover).

Texel density target: ~500-1000 texels/m on walls and ground at eye height (1080p); detail beyond that is carried by the
normal maps and the macro variation.
