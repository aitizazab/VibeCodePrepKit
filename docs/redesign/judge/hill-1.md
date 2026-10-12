# Judge round 1: Larch Hill (25 stills, VibeJamKit-play/out/hill-captures/20261013-003613, built at ae15114)

**Verdict: FAIL.** Level design 3, lighting and sky 2, materials 4, art direction 3, physical integrity 5, scale 4,
Saturday life 2. Judge crops: scratchpad/hj/.

## Blockers
1. **Magenta (missing/broken shader)** on windows, pool water, lamp heads, car glass, road decals, shop fronts, the bus:
   41.6% of 23, 6.9% of 25, 5.4% of 06, 5.0% of 22. (Main's note: the build now runs with -nographics; materials and
   shaders made or assigned in a -nographics editor may fall back to the error shader. Check that first.) Add a check that
   fails on magenta pixels in captures.
2. **No sky:** one clipped flat colour (255,213,178) in every still, no gradient, clouds or sun disc. The capture camera
   isn't getting Look's sky (or it's the error/fallback). LookLab renders the sky fine.
3. **Arrival shot 01 is black** (33,33,41): the Night1Arrival eye at (128, 4.5, -42) is inside/behind a mesh (the far-bank
   rise or the terrain ring).
4. **No cast:** Elias not visible in 18 ("elias-at-the-barbecue"); nobody at the block party (15); Walt (05) a featureless
   pale blob with no face, no paper edge, unlit; ~3 people across 25 stills; no kids, car wash, mower or yard sale.
5. **Challenges invisible or blocked:** the creek gap can't be seen from the far bank (02); the garage roofs are one walled
   tray with no gaps (11); the ditch is out of frame (12); the shutter is a closed door with no mill arch (13); the trench
   doesn't show (16); the Rewind street is long, flat and empty (15): "trivial".
6. **Vistas blocked:** 09 ("vista back down to the creek") faces a ~6 m fence; 25 ("the top looking down") is a brick wall
   at point-blank range; 24: only the hospital's sign peeks over a fence (930-1000 x 510); in 10 the hospital is a flat
   magenta slab and the Lasname house can't be picked out.

## Other defects
7. Walt's counter (04, 1540-1920 x 570-720) a flat untextured purple box; Walt's shop (02) a plain brick box, no sign or
   windows; inside (04) a warehouse with the bike lost in it.
8. Interpenetration: scaffold planks and pipe through the oak trunk, a bush through the stone wall (14, crop c0); a white
   beam out of the house's left side (17, crop c4 230-400 x 270-320); the chimney through the tower's cladding (17); a
   magenta wire through the shop face (22, 0-780 x 30-390); check the bus vs its shelter (21, crop c3).
9. Light leaks between fence boards (09); white blotches on the truck (16, crop c5).
10. The flare disc and hexagon ghosts (06 300-800 x 120-650, 11 incl. 1080-1520 x 600-930, 14), as in look-3.
11. Scale: 4 m retaining walls carry ~2.5 m fences, reading 6-7 m (05, 06, 09, 24); the Rowan hedge ~5 m (11, 12, 15, 16,
    24); laundry sheets ~2.5 m rigid boards (07); the gazebo a ~25 m awning (15); the hospital a ~200 m slab. Cap wall+fence
    near routes at ~4.5 m, hedges ~2.2 m.
12. The Lasname house doesn't read as a goal from the green (10): a landmark (the maple, porch light, barbecue smoke).
13. Yards are vast grey asphalt (05); every route reads as a walled canyon; no "subtly wrong" note beyond emptiness.
    Unhazed olive far hills (03, 09, 20), as look-3.

Strengths: terraces read in 19-21 (retaining walls, the Larch Steps, St. Anne's spire); good stone, brick, cobble, paving,
kerb and coping PBR; long warm shadows (05, 10, 17).

## Size and memory
3.71M vertices (3.0M "Drawn"; the house 0.40M), 5,433 renderers, 2,243 shadow casters, 3,644 unique meshes, 43 lights,
mostly drawing flat walls and boxes: not justified, little sharing. Every "commit" memory figure reads 0.00 GB, so the cap
fit is unproven: fix the probe.

## The three changes that raise the scores most
1. Fix the render pipeline: shaders (magenta), the sky, the arrival camera.
2. Populate Saturday: cut-outs and Elias spawning; crowds at the party, the car wash, kids, the yard sale.
3. Open the sightlines and stage the challenges: lower fences and hedges at route edges, the hospital and house in frame
   at every band, every gap visible from its approach.
