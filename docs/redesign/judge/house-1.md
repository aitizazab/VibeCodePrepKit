# Judge round 1: the Night 2 house (43 stills, VibeJamKit-play/out/house-captures/20261012-232609, built at 95c1719)

**Verdict: FAIL.** Lighting 4, materials 3, art direction 3, level design 4, physical integrity 2, scale 6.
The rebuild lint's "0 overlaps in 522 parts" checks only the plan's boxes; the rendered meshes interpenetrate, lie on
their sides and float. The log lists 23 parts "built in place of a model" (line ~523), most reading as primitives.

## Scores, briefly
1. Lighting 4: warm interiors and window sun patches work (04, 15, seek-01). Blown ceiling hotspots; murky observatory
   (19), workshop (21), basement (seek-04); flat peach exterior sky with no clouds/sun/haze (01, 23, out-01);
   stair-stepped shadow edges (seek-04 top right); crushed escape blacks (04, 04b, 05); "attic open to the stars"
   (escape-07) has a daytime peach sky.
2. Materials 3: siding, brick, wood floor, tile, wainscot good. Cloud-noise textures on the fridge (07, 08), bathtub
   (16), guest bed (17); slab curtains (09, 16); coats as coloured planks (10); books as flat bricks (12); flat lavender
   umbrella (08b); black-cube porch light (02, 25); the escape is near-flat plaster boxes.
3. Art direction 3: cut-outs, photoscanned PBR and grey-box primitives side by side; oxblood in nearly every room so they
   blur; the escape has no "warm, nostalgic, subtly wrong" layer.
4. Level design 4: real room count and 17 hiding spots, but big empty rooms with no line-of-sight breakers (attic 18,
   rec room 20, their room 15, the upper hall's 10 m straight run 11): "see the whole room from the door". The escape
   rooms are one recoloured corridor box (01-03, 05, 06); nothing reads as tipped/rolled/turning/flattening. The
   stretching corridor has no lure: its door is a dark rectangle with no frame, light or handle (04b), no door at all in
   04, no per-bay landmarks, so the stretch can't be read.
5. Physical integrity 2 (below). 6. Scale 6: the parents' bed is child-sized in a 5.6 x 6.4 m room (15); the attic is a
   vast empty hall (18); the treehouse has an adult staircase, not a ladder (23, 24).

## Defects, most severe first
1. **BLOCKER (interpenetration): imported models lie on their sides** (a model axis/fit bug): all four dining chairs
   (05, seek-01; one through Theo's cut-out in 05); the living-room coffee table on its edge and the armchair on its back
   (04); Theo's desk chair and stool tipped (12); the den TV screen-up on a white box floating above its stand (09); the
   guest-room jar (17); the back-yard grill and plastic chair (23). Fix: per-model up/forward axes in house-assets.json;
   a mesh-level lint (renderer bounds upright, resting on floor or surface, no overlaps between meshes).
2. **BLOCKER: memory glows are hard-edged translucent spheres cutting through solids:** half-sunk in the workbench (21),
   through the bookcase (13), halved by Theo's door frame (14), clipped by the hall wall leaving a dark hole (08b),
   clipped by the fridge (08). Fix: a soft billboard or volumetric glow with depth fade around a real lit object.
3. **BLOCKER (interpenetration):** sconces behind Theo's bookcase (12, seek-03); the treehouse stair's top steps pass
   into the platform and rail (24); the front-hall stair's tread ends stick out past the stringer (03).
4. **BLOCKER (placeholder):** the telescope is white cubes on a yellow box, the "round window" is square and looks onto
   the house's own roof planks (19); the fridge is a blank slab, no handles, no visible calendar (07, 08); door-less box
   counters and island (07); a flat-coloured sink with no tap (16); the washing line is a 10 m wall of tan slabs with no
   line (23); the black-cube porch light (02); the flat umbrella (08b). Fix: replace the 23 built-in-place parts.
5. **BLOCKER:** Chapel Row (out-01) is an empty paved plane with four posts in a peach void (no street, hill, houses);
   "Lena at the window" (out-02) shows a lone door frame, a square tower window and no Lena.
6. **World edge:** from the opening street (01) the lot ends in a cut dirt edge over peach void; the attic causeway shows
   in the void through the escape's last door (08b). Fix: the hill and backdrop around the lot; occlude the escape's
   outside.
7. **Seams (choppy):** a bright gap down the middle of floor and ceiling in escape 01, 02, 06; the corridor's dado rail
   steps at every bay joint (04, 04b).
8. **Floating:** the den TV box (09); the treehouse oak's canopy has no branches joining its pole trunk (23); spike
   geometry in tree crowns (01, 25).
9. Sconces and pendants are white "eyeball" spheres on rods (05, 07), likely rotated models.
10. Theo's room shows no bed in shot; the bad-dream drawing lies flat and unreadable on top of the bookcase (13).

Captions that don't match: seek-02 shows Lena not Theo; seek-04 shows no glow; 13 ("on the desk") shows no desk;
seek-01 Theo grins instead of counting with his face in his arms.

Left to recorded runs: phasing and collision (does glass block?), runnable walls, the corridor stretch and dolly zoom,
the fold, the rolls and slow motion, Theo's search and the peek, frame time (35 lamps, 175 model parts).

## The three changes that raise the scores most
1. Fix model orientation and fit, and lint the rendered meshes, not just the boxes.
2. Replace every primitive and cloud-noise surface with real models/textures; each memory a readable lit object with a
   soft glow.
3. Art-direct the escape and the outdoors: distinct furnished rooms that visibly roll; a real starry sky over the attic;
   a warm lit door at the corridor's end and a repeated landmark every bay so the stretch reads; fix seams; a clouded
   hazy sky, the hill around the lot, a real Chapel Row.
