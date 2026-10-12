# Judge round 1: the 3D cast, billboards and watch (37 stills, VibeJamKit-play/out/vh-people/20261012-234922, at 95c1719)

**Verdict: FAIL.** Likeness 5, model quality 3, faces 3, Rafael/representative as different people 4, materials 4,
paper billboards 5, watch 2. Nine blockers. Several stills don't show what they meant to, so some scores are provisional.
Judge crops: Temp/claude/.../scratchpad/c/ (faces_zoom.png, backs.png, lineup_heads.png).

What works: the cast reads as a coherent stylised family; Mags's hair and cardigan knit are close to her sheet; the
paper edge, scene lighting and contact shadows on the cut-outs work.

## Defects, most severe first
1. **BLOCKER: the back of every head still reads as a face** (golden-pair-Elias-Rafael(-close),
   hospital-pair-Elias-Rafael-close, turntable back cells): a smooth faceless dome above a wavy hairline with a skin
   "chin"; Rafael's glossy black hair shows eye-socket shading; the representative's nape is a pale face shape under a
   dark cap. Fix: a real sculpted hair mass over the back, matte, hairline down to the collar.
2. **BLOCKER: hair paint smeared across faces** (hospital-face-Rafael columns 2 and 4: black over his right eye and brow;
   golden-face-Elias: over the right eye). The recolour's 3D gate catches face texels. Fix: mask the face before
   recolouring hair; check decal draw order.
3. **BLOCKER: the face decal shows as a mask** (the representative's chalk-white face with a hard edge against a
   grey-violet neck: golden-pair-Mags-Representative-close, golden-face-Representative; Mags's darker muzzle/chin patch
   in the lineups). Fix: build the atlas from the recoloured body texture, feather with alpha, match skin at the border.
4. **BLOCKER: the representative's blink lids float above her glasses** (golden-face-Representative top-right); her
   lenses carry Mags's cloudy glare, hiding her eyes. Fix: her own eye landmarks; clear faintly tinted lens material.
5. **BLOCKER: EliasBed stands on the bed in an A-pose** (hospital-turn-EliasBed, hospital-bed-EliasBed-close);
   hospital-bed-EliasBed.png is a blank blown-out floor. Fix: check the avatar and that `lie` plays; place him on the
   mattress; re-aim the camera.
6. **BLOCKER: garment backs still carry the front's paint** (Elias's bib outline, neck loop, pocket; Rafael's shiny teal
   bib patch; pocket ghosts on Mags and the representative). Fix: repaint backs from clean fabric; flatten normals and
   roughness there.
7. **BLOCKER: the watch** (watch-close, watch-close-2-05): half the star case sunk in the forearm, the strap an arch with a
   gap, lugs like pipes, a grainy dial, unreadable hands; from first person a speck on an untextured grey mitten of an
   arm. Fix: seat on the skin (offset along the forearm's normal), wrap the strap at the arm's radius, short lugs, clean
   enamel dial, larger.
8. **BLOCKER: the face pass** (golden-face-Elias, hospital-face-Rafael and the rest): the closed mouth is a hole in the
   beard with fang notches; mouth-open barely changes; the blink is a pencil outline; smile/sad/angry/surprised look the
   same: a mannequin. Fix: lips as geometry, jaw and corner shapes 2-3x stronger, eyelid geometry, brow and cheek keys.
9. **BLOCKER (capture): the rig hides half the work.** Both Elias-Rafael pairs shot from behind; 5 of 8 cells in every
   hospital turntable are inside a wall; face sheets cut at mid-forehead; everyone in an A-pose (posing can't be
   judged). Fix the cameras, play `idle` and `talk`, recapture.
10. The billboards use the old chibi crayon dream art, not the 3D sheets: Walt's head ~2x Elias's, chibi proportions
    against "tall, thin"; Lena's cut-out wears a round watch, not the star; the backs (paper-back) glow like peach ghosts.
    Fix: proportions matched to the 3D cast; backlit translucency ~30%; the back a mid-grey card tone. (Main's note:
    whether to redraw the cut-outs from the 3D sheets is the creator's call; the crayon dream art was approved.)
11. Smaller: the representative's trousers speckled white, shoes melted; both glasses cords render as lumpy "worms"; a
    black dot on her hand; EliasBed's ear speckled, hair through the bandage; the representative's "blouse" is still a
    scrub top with a V-neck and pocket; Elias's apron goes lavender in golden light; Elias's hair is a clay helmet.

Rafael differs mostly by colour and still wears an apron (Elias's silhouette); the representative keeps Mags's sack
cardigan, build and smile.

## The three changes that raise the scores most
1. Fix the heads: real back-of-head hair, matte, no recolour bleed onto faces, decal colour-matched and feathered.
2. Rebuild the face rig (sculpted lips and lids, 2-3x stronger keys, per-character landmarks), then recapture with
   working cameras, idle poses and EliasBed lying down.
3. Make the alterations real (Rafael: no apron, a different silhouette; the representative: a cut blazer, a real blouse,
   her own face texture), fix the billboards' proportions and backs, rebuild the watch on the wrist.
