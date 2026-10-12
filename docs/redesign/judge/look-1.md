# Judge round 1: LookLab (captures VibeJamKit-play/out/look-captures/20261012-201824, built at 2718e85)

**Verdict: FAIL.** Lighting and sky 5/10, materials and detail 3/10, art direction 3/10, composition and scale 4/10.
Four blockers: placeholder hill houses, the world's edge, the repeating hillside ground, the trees.

## What works
Sun direction, warm key light, long soft shadows (tree shadows across the road in 10, lawn wedges in 07/01); the cool
zenith with lit cumulus (09); asphalt cracks (10), bark (02, 05), brick (02), kerb bevels; the main house, car and fences
at believable scale.

## Defects, most severe first
1. **BLOCKER: untextured placeholder houses.** The 26 hill houses (06, 10, 01 right, 09 left) are tan/grey boxes with a
   roof: no windows, doors, trim or eaves; some overlap (06 ~1130-1240,470-610; 01 ~1520-1700,440-530). Fix: the main
   house / bungalow builders at a lower LOD with windows, doors, eaves and siding; at least 4 m apart; foundations sunk
   0.3 m into the slope.
2. **BLOCKER: the world ends.** A hard ground edge at the horizon (05 right, 08 left, 09 right, 01 left); a lone tree in
   the void (05). Fix: terrain out to 2-3 km with backdrop hills or a tree line all round; far haze (~0.006/m beyond
   150 m) so the horizon dissolves.
3. **BLOCKER (choppy): repeating hillside ground.** One grass texture tiles as a stripe/checker grid (06, 10). Fix: 3-4
   blended terrain layers (green grass, dry gold grass, dirt, clover) with a 50-100 m variation mask and repeat-breaking
   tiling; grass blades and wildflowers near paths.
4. **BLOCKER (choppy): the trees.** All one lollipop shape: a long bare trunk and a ball of oversized leaf cards with
   stray clumps (02 ~1390-1460,280-340; 01 ~1620-1680,280-330; 05 ~540,270; 03 ~820,460); lime greens clash with the
   warm grade; evenly scattered like a 2004 game; backlit crowns don't glow. Fix: cards a quarter the size and 3x as
   many, kept inside the crown outline, lower branching, 3-4 shapes/species with ±30% size, olive/warm greens, light
   through leaf edges when backlit; clusters, not a grid.
5. **Roof and trim come apart from below.** In 03 roof planes are invisible from below (rake trim and gutter float with
   sky between), a gutter runs up the gable edge, a downspout angles across the wall; in 07 a sky gap between wall and
   gable trim. Fix: real roof thickness and underside; gutters only on horizontal eaves; downspouts straight down corners.
6. **Siding is wrong.** In 04 board courses step at x≈740, 1000, 1165, and a diagonal seam (1520-1650,0-330) where the
   gable's mapping doesn't match; in 03/07 a differently shaded panel on the side wall (03: 1280-1610,380-800). Fix: one
   continuous layout per wall incl. the gable, snapped to a 0.18 m course; remove/merge the overlapping panel.
7. **Sidewalks and lawns are flat.** The sidewalk is dark purple-brown, darker than the road (01, 05, 07); concrete should
   be the brightest surface at golden hour. Lawns are one flat tiled texture meeting the paving in a hard line. Fix: pale
   warm-grey concrete with joints and staining; grass overhang strips, leaf litter, a storm drain, oil stains.
8. **Sky and sun read flat.** The sunward sky (08, 03) is a flat cream wash over 60% of the frame: no gradient, no
   gold-lit cloud undersides, no rays; the sun sits behind an opaque lime crown with no glow, rim or flare. Fix: cut the
   sun-side warm lift and horizon haze band ~40% so the HDRI's clouds show; bloom intensity ~1.0, threshold ~0.9; a
   stronger sun disc and glow that spill round foliage; sun shafts ~0.8; check the lens flare renders (none visible);
   the ground-level light wedge in 08 (700-960,830-1000) should come from the sky, not the tree base.
9. **Foreground underexposed and muddy** (lilac-grey lawns and road in 01, 05, 07). Fix: exposure ~+0.1, halve the lilac
   shadow tint, warm bounce on the ground. Haze doesn't work: in 06/10 the far hill is as sharp and saturated as the near.
10. **A stray grey squiggle** near the horizon in 03 (~540,730) and 08 (~610,790): a broken flare piece or smoke sprite.
11. **Smaller:** the cross street dead-ends into grass (10, 06); the manhole reads as an orange disc (01 ~1050,707; 09
   ~1180,910), make it dark iron; windows show flat brown blocks behind glass (04), add furniture silhouettes; the covered
   car is a navy blob, give the cover folds and a lighter fabric.

## The three changes that raise the scores most
1. Real trees and real hill houses (smaller leaf cards, varied shapes, warmer greens, light through leaves; houses with
   windows, doors, eaves, siding).
2. Close the world and vary the ground (a distant terrain ring, haze that grows with distance, blended terrain layers,
   lawn edging and street detail).
3. Re-grade the sun side (the HDRI's gold clouds and rays, bloom and flare through foliage, a touch more exposure, less
   lilac in the shadows).
