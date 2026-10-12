# Ward 4 West: third-party assets and their licences

Everything the waking days' ward (docs/redesign/day.md) uses from outside, with its source and licence.

## Textures (CC0 1.0, ambientCG): `Textures/vh_*_{albedo,normal,mask}`
Downloaded 2026-10-12 as the 1K-JPG sets from https://ambientcg.com (licence: https://docs.ambientcg.com/license/, CC0).
Repacked for URP/Lit (`tools` script, not shipped): the colour map as the albedo, the OpenGL normal map, and a mask
(R metallic, G ambient occlusion, A smoothness = 1 - roughness, 512 px).

| File | ambientCG set | Used for |
|---|---|---|
| `vh_floor_vinyl` | Terrazzo004 | the sheet vinyl floors |
| `vh_ceiling_tile` | OfficeCeiling001 | the suspended ceiling |
| `vh_wall_paint` | PaintedPlaster017 | the painted walls |
| `vh_plastic_white` | Plastic010 | the beds, equipment, doors' frames, units |
| `vh_plastic_dark` | Plastic011 | keyboards, phones, castors |
| `vh_fabric_weave` | Fabric030 | upholstery, blankets, scrubs, curtains |
| `vh_fabric_linen` | Fabric036 | sheets, pillows, gowns, paper |
| `vh_carpet` | Carpet012 | the quiet room's and family room's carpet, the noticeboard's cork, hair |
| `vh_metal_brushed` | Metal032 | steel: rails, stands, lifts |
| `vh_wood_oak` | Wood049 | laminate: counters, doors, tables |
| `vh_tiles_white` | Tiles107 | the staff room's splashback |
| `vh_skin` | Leather030 (its grain, softened, as pores) | skin |

## The sky through the windows (CC0 1.0, Poly Haven)
`Textures/vh_ward_sky_hotel_rooftop_balcony_2k.hdr`: "Hotel Rooftop Balcony" by Poly Haven
(https://polyhaven.com/a/hotel_rooftop_balcony, CC0), the 2K HDR, unchanged.

## Fonts (SIL Open Font License 1.1), used offline only
The printed and handwritten pictures in `Textures/Pictures/` (signs, notices, the rota, the round board, the TV's news
frame, the remembrance book, the vending machine's front, the monitor's digits) were drawn with **Inter**
(Copyright 2020 The Inter Project Authors) and **Caveat** (Copyright 2014 The Caveat Project Authors), from
https://github.com/google/fonts (ofl/inter, ofl/caveat). The fonts themselves are not shipped; the OFL allows the
pictures made with them to be used freely.
