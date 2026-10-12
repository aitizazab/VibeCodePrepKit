# Judge round 3: LookLab pass 3 (captures VibeJamKit-play/out/look-captures/20261013-002648, built at e576fe9)

**Verdict: FAIL.** Lighting and sky 5, materials 5, art direction 5, composition 6 (round 2: 6/5/4/6). The street finally
reads as summer, but the far view got worse (black sunward hills, a backdrop darker than the lawn), the flare disc is
still there with a hard edge, and the trees read as topiary/bonsai. Judge crops: scratchpad/j3/ (a08, a05, a04, a10,
a02, a06).

## Palette against look.md's rendered targets (display sRGB, sampled)
- Lawn sunlit (07): (63,70,4), hue 67, sat 0.94, value 0.28; target ~(130,150,70). Too dark, oversaturated, blue crushed
  (79% of pixels blue < 8). Lawn shade (07): (36,53,11), blue below red: not cool. Sidewalk shade (10): (72,55,44) warm.
- Concrete sunlit (10, 02, 07): ~(117,83,59), sat 0.50 vs target <= 0.22; orange-tan, darker than the siding (value
  0.57); identical to round 2, so the light or the grade, not the albedo. Asphalt (10): (39,29,25) red-brown vs (95,90,85).
- Sky zenith away (138,144,167), sat 0.17 vs (110,145,195): grey-lavender. Horizons near target.
- Foliage hue 76-86 everywhere (autumn check passes); shaded/backlit leaves (33,51,8), too saturated vs (45,68,42).
- Haze backwards: sunward far hills in 03, 08, 11, 12 are (5,12,0), value 0.04-0.06, against a 0.90 horizon (target within
  ~15% of the horizon); backdrop 0.21 vs near lawn 0.29 (05), 0.19 vs 0.25 (09). Round 2's backdrop was 0.51.

## Round 2's defects
Autumn foliage FIXED. Lake sheen and seam FIXED, haze WORSE. Polka dots FIXED (patchwork, hedgerows, roads) but unhazed,
dark olive, black sunward. Decals FIXED (soft). Flare disc UNCHANGED and now crisp-edged; no sun disc anywhere; lit
clouds better (01, 12). Trees: seam rings and loose cards FIXED, limbs bend; spikes remain (10: trunk stub at 1640,50-110;
twig at 1530-1620,0-80). Tufts better (drifts) but a kerb run in 01 (0-620, 640-800). Porch FIXED. Birch bark FIXED, crowns
still tiny pom-poms (03 460-670; 02 1420-1500). Foundations and road end FIXED; house variety and windows better.

## New defects, most severe first
1. **BLOCKER (choppy): black, unhazed hills sunward** (03 0-1000 x 605-750; 08 0-1150 x 640-810; 11 0-1150 x 610-770; 12
   0-200 x 470-580); the whole backdrop unhazed (05, 09). Likely: the ring and FarLand use vertex-alpha blending, don't
   write depth, so the depth-based haze treats them as sky; sunward they also get no sky ambient. Fix: render the ring
   opaque with dithered fades so it writes depth, or haze inside its own shader; give it ambient; verify numerically.
2. **BLOCKER (choppy): hard-edged orange disc in 08** (700-1200 x 640-900), a screen-space circle over hill and lawn.
   Remove or feather the sun-glow and shaft composite.
3. **BLOCKER (choppy): crowns read as topiary/bonsai** (02, 05, 09, 11, 12): ~110 cards make a few separate clumps with
   sky between, on long bare limbs; card edges show as straight lines (02 left clump 280-520 x 0-300); yellowed leaves as
   evenly spread spots (10, 02). Fix: a cheap leaf-textured inner volume with cards only round the silhouette; more,
   overlapping clumps with rounded irregular outlines; yellow clustered on outer sunlit edges.
4. **Grade: warm and crushed.** Neutralise white balance and split-toning, cool the shadows, cut green saturation ~1/3,
   lift the ground ~half a stop, re-check every rendered target.
5. **Siding:** vertical dashed seams down the wall (04 at x~880, 1200, 1540; y 0-900): light leaks between course pieces.
   Overlap or weld them.
6. **Lawns one flat saturated green** (06, 07, 12): no dry patches, no mowing variation; hill houses on empty golf-course
   lawns with no fences, drives or gardens (06).
7. **Hedgerows are flat-topped extruded prisms** (05 1330-1920 x 470-530): lumpy tops, broken ends.
8. **No sun disc**: in 08 the ring's rim ridge hides it. Lower the ridge on the sun line or raise the sun 1-2 degrees.
9. **Into-the-sun foliage looks front-lit** (11 main crown; 03 800-1180 x 380-720): no translucent glow or rim.
10. Upper sashes still show dark interior blocks (04 670-900 x 10-120; 1220-1560 x 450-620).

No LOD switches visible in stills; popping needs a fly-through.

## The three changes that raise the scores most
1. Haze on the backdrop and sunward hills (lift toward the horizon, never black); remove the hard disc; a visible sun.
2. Re-grade to the targets: cool shadows, pale neutral concrete, grey asphalt, blue zenith, greens less saturated and
   brighter with blue unclipped; dry-grass and mown variation in lawns.
3. Fill the crowns: an inner leaf volume, overlapping rounded clumps, no box edges; no stubs or spikes; fuller birches.
