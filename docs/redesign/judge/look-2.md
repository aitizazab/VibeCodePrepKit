# Judge round 2: LookLab pass 2 (captures VibeJamKit-play/out/look-captures/20261012-230715, built at 95c1719)

**Verdict: FAIL.** Lighting and sky 6, materials 5, art direction 4, composition 6 (round 1: 5/3/3/4). A real step up:
the hill has houses, the horizon is closed, the stripes are gone, the roofs are solid. But it fixed shapes and broke
colour: the street reads as **October, not a late-summer Saturday**, and several new pieces look broken.

## Round 1's defects
1. Placeholder hill houses: FIXED as a blocker (06, 10, 12). Left: stone foundations a third of the wall high (07 at
   1470-1700 x 435-490; 10 at 560-690 x 540-575); every house faces the same way; identical rows (06).
2. The world ends: IMPROVED (05, 08, 09); the road in 09 stops in a field (~1260,820).
3. Repeating ground: IMPROVED (stripes gone) but the four layers don't show: one flat pastel green.
4. Trees: shape IMPROVED (limbs, species, backlit glow in 11), colour WORSE: still a blocker (new 1).
5. Roof/trim from below: FIXED. 6. Siding: FIXED (vertical joints line up at x~900 and 1300 in 04).
7. Sidewalks FIXED; lawn detail undermined by decals and tufts (new 4, 7).
8. Sunward sky: UNCHANGED (03, 08); the flare renders but as an artefact.
9. Exposure FIXED; haze WORSE (near ground washed, far hills sharp: backwards).
10. Squiggle FIXED. 11. Cross street, manhole, car cover FIXED; windows IMPROVED (04 still shows brown blocks).

## New defects, most severe first
1. **BLOCKER: autumn foliage.** Every leaf yellow/orange (02 crown, 03, 08, 11, 04 shrubs, 01 maple). Fix: repaint the
   leaf atlas in deep and olive summer greens, warm yellow-green only on sunlit edges, ~5% yellowed leaves; tufts
   mostly green with dry gold tips.
2. **BLOCKER (choppy): the near ground reads as a lake.** Lawns go pale cyan/cream at low and medium angles (05 right
   half 1000-1920 x 540-830; 08 left; 09 right; 11's lawn); a hard seam where terrain meets the saturated backdrop
   (05, y~530). Fix: terrain smoothness ~0.05 (grazing sky reflection is the likely cause); roughly halve near haze;
   the same haze on the backdrop so far is always paler than near.
3. **BLOCKER (choppy): polka-dot backdrop.** The ring's tree masses read as boulders/hay bales scattered evenly over lime
   hills (05, 08 at y~700-760, 11 left). Fix: continuous tree lines and hedgerows along ridges and field edges, dark
   olive, a patchwork of fields, hazed.
4. **BLOCKER (untextured look): stain decals render as hard rectangles** (01 600-700,715 and 1450-1620,745; 03 430-620,850
   and 930-1110,875; 07 0-250,785-900; 08 860-1140,1010-1080; 09 970-1180,990-1035; 12 880-950,745). The alpha isn't
   used. Fix: alpha-blended, or URP Decal Projectors with soft edges, multiplying darker.
5. **Sun and flare:** no sun disc anywhere, a cream wash, a giant soft orange flare disc over the road (08 600-1300 x
   700-1080). Fix: remove the wide warm-glow flare element; make the disc show above the ring (haze/horizon band may hide
   it); cut the sun-side lift again so the HDRI's clouds read.
6. **Tree construction:** twig spikes out of crowns (11 at 1000,20-90 and 1270,240; 02 240-350 x 0-120 and 870-900 x
   0-200; 08 ~1430-1530,540); loose floating leaf cards (01 ~1660,210 and 1345,240; 02 ~470,35; 08 ~1800,165); pale seam
   rings at limb joints (11 ~820,480 and 940,770); straight cylinder limbs forking from one point. Fix: clip twigs to
   the crown, drop cards far from branches, fix UV seams, taper and bend limbs.
7. **Grass tufts are identical stickers** dotted evenly like confetti (03 foreground, 08 field, 12 0-600 x 600-1080) and a
   picket line along the kerb (01). Fix: dense drifts with falloff, scale and tint variation, few lone clumps.
8. **Floating porch frame** on the neighbour house in 07 (1700-1790 x 300-440): posts and rail, no deck, no ground.
9. **Birch trunks read as bright yellow sticks** with tiny crowns (02 1450,570; 09 775,780; 10 1530,550).

Performance unproven: ~580 alpha cards per near crown is heavy overdraw on the RTX 3050.

## The three changes that raise the scores most
1. Colour as a whole: summer-green foliage and tufts; matte terrain; haze that grows with distance and covers the
   backdrop.
2. The broken pieces: decal rectangles, twig spikes, loose cards and seam rings, the flare disc, the porch frame.
3. The far view and sunward sky: tree lines and fields instead of polka dots, a visible sun disc, lit clouds.

Judge crops: Temp/claude/.../scratchpad/j2/.
