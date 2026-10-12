# Larch Hill texture packs: sources and licences

Every downloaded pack below is **CC0 1.0 Universal** (public domain dedication,
https://creativecommons.org/publicdomain/zero/1.0/). Downloaded on **2026-10-12**.
Poly Haven assets: https://polyhaven.com/license. ambientCG assets: https://docs.ambientcg.com/license/.

File convention (see `Editor/Street/Kit/HillMaterials.cs`): `<pack>/<pack>_color`, `<pack>_normal` (OpenGL, +Y up:
Poly Haven "nor_gl", ambientCG "NormalGL"), optional `<pack>_ao`.

## Downloaded packs

| Pack | Source | Asset (id) | URL | Author(s) | Licence | Maps (colour / normal / AO) |
|---|---|---|---|---|---|---|
| grass | ambientCG | Grass 004 (`Grass004`) | https://ambientcg.com/a/Grass004 | ambientCG (Lennart Demes) | CC0 1.0 | 1K / 1K / 1K |
| dirt | Poly Haven | Dirt Floor (`dirt_floor`) | https://polyhaven.com/a/dirt_floor | eye-candy.xyz | CC0 1.0 | 1k / 1k / - |
| leaf_litter | Poly Haven | Forest Leaves 02 (`forest_leaves_02`) | https://polyhaven.com/a/forest_leaves_02 | Rob Tuytel | CC0 1.0 | 1k / 1k / 1k |
| gravel | Poly Haven | Gravel Floor 02 (`gravel_floor_02`) | https://polyhaven.com/a/gravel_floor_02 | Jenelle van Heerden, Dimitrios Savva | CC0 1.0 | 1k / 1k / - |
| asphalt | Poly Haven | Asphalt 01 (`asphalt_01`) | https://polyhaven.com/a/asphalt_01 | Dario Barresi, Charlotte Baglioni | CC0 1.0 | 2k / 1k / - |
| paving | Poly Haven | Concrete Pavement (`concrete_pavement`) | https://polyhaven.com/a/concrete_pavement | Charlotte Baglioni | CC0 1.0 | 2k / 1k / 1k |
| cobbles | Poly Haven | Cobblestone Floor 04 (`cobblestone_floor_04`) | https://polyhaven.com/a/cobblestone_floor_04 | Rob Tuytel | CC0 1.0 | 1k / 1k / 1k |
| concrete | Poly Haven | Concrete Floor Worn 001 (`concrete_floor_worn_001`) | https://polyhaven.com/a/concrete_floor_worn_001 | Dimitrios Savva, Rico Cilliers | CC0 1.0 | 1k / 1k / 1k |
| stone_wall | Poly Haven | Rough Block Wall (`rough_block_wall`) | https://polyhaven.com/a/rough_block_wall | Rob Tuytel | CC0 1.0 | 2k / 1k / 1k |
| brick_red | Poly Haven | Red Brick (`red_brick`) | https://polyhaven.com/a/red_brick | Rob Tuytel | CC0 1.0 | 2k / 1k / 1k |
| brick_cream | Poly Haven | Yellow Bricks (`yellow_bricks`) | https://polyhaven.com/a/yellow_bricks | Rob Tuytel | CC0 1.0 | 1k / 1k / 1k |
| stucco | Poly Haven | White Stucco (`white_stucco`) | https://polyhaven.com/a/white_stucco | Amal Kumar | CC0 1.0 | 1k / 1k / - |
| clapboard | ambientCG | Wood Siding 009 (`WoodSiding009`) | https://ambientcg.com/a/WoodSiding009 | ambientCG (Lennart Demes) | CC0 1.0 | 1K / 1K / 1K |
| shingle_wall | ambientCG | Wood Siding 013 (`WoodSiding013`) | https://ambientcg.com/a/WoodSiding013 | ambientCG (Lennart Demes) | CC0 1.0 | 1K / 1K / 1K |
| church_stone | Poly Haven | White Sandstone Blocks 02 (`white_sandstone_blocks_02`) | https://polyhaven.com/a/white_sandstone_blocks_02 | Rob Tuytel | CC0 1.0 | 1k / 1k / 1k |
| concrete_panel | Poly Haven | Concrete Slab Wall (`concrete_slab_wall`) | https://polyhaven.com/a/concrete_slab_wall | Dimitrios Savva | CC0 1.0 | 1k / 1k / - |
| roof_shingles | Poly Haven | Grey Roof 01 (`grey_roof_01`) | https://polyhaven.com/a/grey_roof_01 | Rob Tuytel | CC0 1.0 | 2k / 1k / 1k |
| roof_clay | Poly Haven | Clay Roof Tiles 02 (`clay_roof_tiles_02`) | https://polyhaven.com/a/clay_roof_tiles_02 | Amal Kumar | CC0 1.0 | 1k / 1k / 1k |
| roof_slate | Poly Haven | Roof Slates 03 (`roof_slates_03`) | https://polyhaven.com/a/roof_slates_03 | Rob Tuytel | CC0 1.0 | 1k / 1k / 1k |
| fence_planks | Poly Haven | Wood Planks Grey (`wood_planks_grey`) | https://polyhaven.com/a/wood_planks_grey | Rob Tuytel | CC0 1.0 | 1k / 1k / 1k |
| deck_planks | Poly Haven | Wooden Planks (`wooden_planks`) | https://polyhaven.com/a/wooden_planks | Dario Barresi, Charlotte Baglioni | CC0 1.0 | 1k / 1k / 1k |
| metal | ambientCG | Metal 012 (`Metal012`) | https://ambientcg.com/a/Metal012 | ambientCG (Lennart Demes) | CC0 1.0 | 1K / 1K / - |
| corrugated | Poly Haven | Corrugated Iron (`corrugated_iron`) | https://polyhaven.com/a/corrugated_iron | Jenelle van Heerden, Dimitrios Savva | CC0 1.0 | 1k / 1k / - |
| canvas | Poly Haven | Rough Linen (`rough_linen`) | https://polyhaven.com/a/rough_linen | colormass, Rico Cilliers | CC0 1.0 | 1k / 1k / - |
| bark | Poly Haven | Bark Brown 02 (`bark_brown_02`) | https://polyhaven.com/a/bark_brown_02 | Rob Tuytel | CC0 1.0 | 1k / 1k / 1k |

Poly Haven files came from the API (`https://api.polyhaven.com/files/<id>`: "Diffuse", "nor_gl", "AO", jpg);
ambientCG files from `https://ambientcg.com/get?file=<id>_1K-JPG.zip` (`_Color`, `_NormalGL`, `_AmbientOcclusion` only).

### Changes made to the downloads

Normal and AO maps are unchanged copies. Colour maps were evened out so the materials' tints (HillMat.cs) land on the
palette: a saturation scale and a power curve that sets the mean luminance (pack: target luminance, saturation):
asphalt 0.40/0.6, concrete 0.70/1.0, fence_planks 0.50/1.0, roof_shingles 0.50/0.8, shingle_wall 0.55/0.7,
clapboard 0.78/0.12, roof_clay 0.48/0.8, canvas 0.80/0 (fully desaturated; the linen is blue), grass 0.45/1.0,
deck_planks 0.48/0.9, bark 0.40/0.9, roof_slate 0.50/1.0, brick_red 0.46/0.9, dirt -/0.8, leaf_litter -/0.85,
stone_wall 0.52/0.6, cobbles 0.52/0.85, paving 0.56/0.7, church_stone 0.68/0.8, brick_cream 0.62/0.45.
Others are unchanged copies. The clapboard maps (a 2:1 strip, 1024 x 512) were stacked twice into 1024 x 1024 so the
boards keep their proportions on square UVs; the canvas maps (1024 x 1026) were cropped to 1024 x 1024.

## Generated packs

Drawn by `tools/art/hill-textures.py` (numpy + Pillow; run
`uv run --with pillow --with numpy python tools/art/hill-textures.py [pack ...]`), original work for this project:

- `gen_interiors`, `gen_interiors_shop`, `gen_interiors_ward`: rooms seen through windows (layouts in HillAtlas.cs)
- `gen_signs`: painted signboards with HillAtlas.SignTexts. Fonts: DM Serif Display and Crimson Text Bold, both under the
  SIL Open Font License 1.1, from `Assets/JamKit/Templates/VisitingHours/Argument/Resources/ArgumentFonts/`
- `gen_awning_red`, `gen_awning_green`, `gen_awning_blue`: striped awning canvas (+ normal)
- `gen_leaves`, `gen_leaves_blossom`, `gen_ivy`: leaf clumps with alpha (+ normal)
- `gen_grass_cards`, `gen_flowers`: tufts rooted at the bottom edge with alpha (+ normal)
- `gen_cracks`, `gen_stains`, `gen_leaf_decals`, `gen_manhole` (+ normal), `gen_drain` (+ normal): decals with alpha
- `gen_white`: plain white
- `hedge` (colour, normal, AO), `water` (colour, ripple normal), `glass` (grime colour, waviness normal),
  `painted_wood` (colour, normal): tileable surfaces that no CC0 library offered in a usable form (no hedge surface; a
  near-white painted wood; a mostly-white window grime map; a ripple normal), so they are generated too.
