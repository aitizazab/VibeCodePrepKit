# People: the 3D cast, the paper cut-outs, Lena's watch (the People agent)

The redesign's people (REDESIGN.md, section 5: People; section 7: who is 3D). The bar is REDESIGN.md section 3; the
look is stylized AAA (It Takes Two, Psychonauts 2, Life is Strange: True Colors), keeping what the creator valued in the
2D designs: individuality, unique posing, a strong defining colour per character.

- **3D (Tripo models, rigged, animated, talking faces):** Elias (the dream, at the barbecue), EliasBed (the waking Elias,
  asleep), Mags, Rafael (his own Tripo model, the creator 2026-10-13), Lena (a plain 3D Lena, kept as a base), and
  LeBaget's representative (altered from the 3D Lena). Nadia's existing model stays the World agent's first-person body.
- **Paper cut-outs (billboards):** Lena, Theo, Walt, the neighbours, the dog: `DreamCutoutView`. The drawings stay as
  drawn (the creator: "I like most of it"); only technical paper fixes.
- **Lena's keepsake:** her star-shaped watch on Nadia's right wrist: `VhKeepsakeWatch`.

## Status (round 3, 2026-10-13)

Round 1 was judged FAIL (docs/redesign/judge/people-1.md, nine blockers). Round 3's answer to each:

| Blocker | What changed | State |
|---|---|---|
| 1 Heads: the back read as a face | `back_of_head` now builds a real hair mass: the rounded skull is wrapped in hair 3-7 mm thick (thicker at the crown, in clumps that run with the fall), down to a lip just above the collar; painted hair to the collar; matte (roughness 0.86 there, 0.8 on all hair); strand grooves in the normal map; curls as round tufts (cellular noise) for Mags | Elias, EliasBed, Mags rebuilt; seen in numpy renders |
| 2 Hair paint on faces | the model's own eyes, brows and lips are painted out (below), which also clears any hair lock over an eye; `vh_alter` classes take `not_in` (ellipsoids, e.g. the face) and `zskip` (a height band, e.g. hair under a bandage) | done |
| 3 The face decal as a mask | the decal carries no skin at all any more: only the drawn features (the hybrid cartoon face) over clean skin | done |
| 4 The representative's lids over her glasses | she is rebuilt from the 3D Lena (the creator) with her own drawn eyes and separate gold glasses meshes (no lens, so nothing clouds her eyes); the face step also has `lenses` (cleaned to skin, a faint glare) and `frame_protos` (frame vertices kept off the decal), used on Mags | done (numpy) |
| 5 EliasBed standing on the bed | the capture's posing was the cause (everyone was in the bind pose): it now samples with AnimationMode (and logs which method worked), places the bed under the posed body (baked mesh, lowest vertex 1 cm into the sheet, pillow under the head) and logs a warning if he is not lying | needs the Unity capture |
| 6 Garment backs | `back_paint` rules take `smooth` (the front's relief on the back is smoothed out of the mesh, e.g. Elias's bib edge and pocket) and the repainted fabric gets a plain roughness | Elias, Mags done |
| 7 The watch | rebuilt: the wrist is measured from the posed skinned mesh (a 2.4 cm slab of skin across the forearm), the strap is an ellipse at that size, the case sits on the strap on the back of the wrist; 42 mm star, short low lugs, clean enamel dial at 1024 px, bold matte hands; the capture textures Nadia's arm | needs the Unity capture |
| 8 The face pass | the hybrid cartoon face (below): nine expressions drawn clearly differently, talking and blinking cells per expression, and the shape keys about twice as strong (jaw 0.3, cheeks 0.34, brows 0.24, frown 0.28 of the eye distance) | Elias, EliasBed, Mags done |
| 9 The captures | rewritten (see "Captures") | needs the Unity capture |
| 10 Billboards | card-grey back (0.5), 30% light through the paper, forced through the property block so saved materials follow; Lena's star watch as an overlay (her drawing untouched); paper meshes prewarmed after every scene load | done |
| 11 Smaller | EliasBed: hair kept off the bandage band; Mags: lens paint cleaned; the representative's items go with her rebuild | partly |
| Perf | `VhFace` writes only changed blendshape weights and only a changed decal cell; `DreamCutoutView.Prewarm`; LOD1 per person (the body decimated to 30% in Blender, no face) in a LODGroup | done |

Rafael and the representative (the creator, 2026-10-13): Rafael is generated from his own sheet; the representative is
altered from a 3D Lena, and the plain Lena stays a prefab. The Elias-based Rafael and the Mags-based representative are
retired (their old models stay in `Models/Rafael`, `Models/Representative` until the new ones replace them).

Tripo credits: 310 at the start of round 3; spent 160 (Lena's model 60 + rig 20, Rafael's model 60 + rig 20): **150 left**.
Gemini (Flow) for Rafael's sheet: 0 credits.

## The faces: the hybrid cartoon face (from BasilDownfall's comic style)

The creator's approved answer to faces on Tripo models (BasilDownfall `art/comic-style/faces/PIPELINE.md`, route 1, used
for Yoru, Chamber and Phoenix) is to paint the model's own painted features out and draw cartoon ones on top, per
expression. What was taken from it, for a real-time decal:
- **Paint out, then draw.** `vh_face.py` inpaints the eyes, brows and lips out of the colour map (a push-pull fill in a
  front projection, written back to every visible front texel, the normal map flattened there), so the base is clean
  skin and the closed eye is the skin itself. Extra ellipses: `clean`; one-material fills (a beard over the painted
  gap round the lips): `fill_regions`.
- **The drawn parts** (as `decal.py` draws them): brows as tapered strokes with hair marks; eyes with the sclera, a
  graded iris, the pupil, two highlights, a lash line thickening to an outer flick, the crease and the lower lid line,
  lids that close by the expression (inner-down for anger, outer-down for sadness, the lower lid rising for a smile);
  the mouth as a lip line with a lower lip (or lipstick), open with teeth and tongue, per expression (grin, grimace,
  "o"). Per-character design (`face.draw` in `people/<Name>.json`): iris and lash colours, brow colour and thickness,
  lips, eye size and resting lid; EliasBed's eyes are drawn shut in every cell.
- **The acting profiles' idea** (who acts with what) is in the designs; the expression table is `EXPR` in `vh_face.py`.
- Not carried over: real eyeballs and the comic restyle of the body (route 1's ink and flat paint): the cast keeps the
  stylized-AAA paint.

Atlas: rows = expressions (neutral, warm, happy, sad, worried, hurt, angry, surprised, doubtful), 6 columns = mouth
closed / half / open, then the same with the eyes shut; 448 px cells (2688 x 4032). `VhFace` picks the cell; the decal is
a skinned copy of the face's front carrying the same shape keys (`MouthOpen`, `Smile`, `Frown`, `BrowsUp`, `BrowsDown`,
`BrowsSad`), so the drawing moves with the jaw, cheeks and brow.

## The models

| Prefab | Height | Defining colour | Made from | Round 3 |
|---|---|---|---|---|
| Elias | 1.83 m | cornflower-blue apron on white | Tripo (sheet `vh3d-elias-dream-take1`) | rebuilt |
| EliasBed | 1.83 m | pale hospital blue | Tripo (`vh3d-elias-bed2-take2`), idle `lie` | rebuilt |
| Mags | 1.62 m | oatmeal cardigan over pale-blue scrubs, brick-red cord | Tripo (`vh3d-mags-take2`) | rebuilt |
| Rafael | 1.76 m | surgical teal (scrubs over charcoal sleeves) | Tripo, his own sheet `vh3d-rafael-take1` (curly black hair with volume, clean-shaven, broad shoulders, a pen behind each ear, white trainers); back of head and scrub back fixed | built |
| Lena | 1.88 m | wine-red trousers, cream sweater | Tripo, her approved sheet `vh3d-lena2-take1` (`tools/tripo/sheets/lena/`); her long hair hides the back of the head (no second face to fix) | built (new prefab "Lena") |
| Representative | 1.74 m | an aubergine blazer with darker lapels over a cream high-neck blouse; gold | altered from the 3D Lena: espresso hair, her own drawn face (brown eyes, arched brows, berry lipstick, freckles painted out), blazer, blouse, charcoal trousers, black shoes, gold glasses (rims, bridge, arms as accessories: clear, no lens), a red-striped visitor badge, a gold pin | built |

## The pipeline (to make or change a person)

Sources: Tripo's FBX exports and their mesh dumps in `FableAnimationTests/.work/people-src/<src>/` (not in git).
Per character, `tools/tripo/people/<Name>.json`:
```
uv run --with pillow --with numpy python tools/tripo/vh_make.py prep <Name>      # recolour, back fixes, face, build spec
uv run --with pillow --with numpy python tools/tripo/vh_make.py face <Name>      # only the face step again (after prep)
node C:/Users/User/.claude/hooks/gpu-short-run.mjs -- <blender> -b --factory-startup -P tools/tripo/vh_blender.py -- build <spec>
uv run --with pillow --with numpy python tools/tripo/vh_make.py finish <Name>    # into Models/<Name>/, preview renders
```
(Blender: `FableAnimationTests/BasilDownfall/tools/blender/blender.exe`, a build takes about 5 s. Use forward slashes in
the spec path.) `prep` keeps the alter step's maps as `<Name>_Color_alter.jpg` / `_Normal_alter.png` so `face` can re-run
alone. Checks: `out/<Name>/face-check.png` (every atlas cell over the cleaned face), `faces.png` (the build with its shape
keys, numpy), `body.png`.

A new Tripo model: a sheet with Gemini (`FableAnimationTests/art-tools/flow.mjs --image ...`, prompt in
`tools/tripo/sheets/<name>.prompt.txt`), `crop_turnaround.py` into `sheets/<name>/`, `node tools/tripo/gen.mjs
sheets/<name>` (60 credits), rig in Tripo (Humanoid, Mixamo, 20), export FBX (Blender preset, 4K, skeleton), unzip into
`.work/people-src/<src>/`, `blender ... vh_blender.py -- dump <fbx> <src>/dump.npz`, landmarks with
`vh_headgrid.py <dump> <Color.jpg> <out.png>` (5 mm grid).

## Captures (`VhPeopleCapture`, `tools/tripo/vh-people-capture.ps1`)

Everyone is posed with AnimationMode (logged per clip: `JAMKIT_VH_PEOPLE pose <Name> '<clip>' ... via ...`), turned so
the FACE looks where the shot wants, framed from the baked (posed) mesh. Output `out/vh-people/<time>/`:
`<light>-lineup.png` (idle) and `-lineup-talk.png`; `-turn-<Name>.png` (the ward's walls hidden for it); `-face-<Name>.png`
(the whole head, from HeadTop_End to below the chin, at conversation distance: neutral, talking half and open, a blink,
the nine expressions, happy talking); `-seq-<Name>.png` plus `seq-<light>-<Name>/000-023.jpg` (one second of talking at
24 fps: warm, a blink at frame 10, worried from frame 15, the idle under it); `-pair-Elias-Rafael`, `-pair-Mags-
Representative` (both facing the camera); `hospital-bed-EliasBed(-close).png`; `paper-front/edge/back/close-walt-elias/
close-lena.png`; `watch-first-person.png`, `watch-close.png`, `watch-close-2-05.png`, `watch-along-arm.png` (the case on
the strap on the skin).

## The component (for World, House, Day and Battle): unchanged

Assembly `JamKit.VisitingHours.Characters`. Namespace `JamKit.VisitingHours.Characters`.
```csharp
var mags = VhCharacter.Spawn("Mags", position, rotation, parent);   // Resources/People/Mags.prefab
mags.Play("talk");  mags.PlayOnce("nod");  mags.Play("idle", 0.4f);
mags.LookAt(player.transform);  mags.StopLooking();
mags.Talk(true);  mags.Say(2.4f);  mags.SetExpression("worried");  mags.Blink();
```
Names: `Elias`, `EliasBed`, `Mags`, `Rafael`, `Representative`, `Lena`. New in `VhFace`: `BlinkAt(now)`,
`ResetClock(now)`, `ForgetWritten()` (captures and editor previews).

## Billboards: `DreamCutoutView` (JamKit.DreamEscape)

Unchanged API (`Create`, `Watch`, `Flip`, `PaperMesh`), plus:
- The back is mid-grey board (`DreamCutoutView.CardBack`), about 30% of the light comes through when backlit
  (`Translucency`); both are set through the paper's property block, so saved materials follow.
- **Overlays**: `Resources/People/Overlays/<picture name>.png` (picture-sized, transparent except what it changes) is
  printed over that picture by the shader. `dream_lena.png`'s overlay (made by `tools/tripo/make_lena_overlay.py`) puts
  the star watch with its wine-red strap over the round watch she was drawn with. Her drawing is not touched.
- `DreamCutoutView.Prewarm(extraPictures)` builds every paper mesh up front; it runs by itself after each scene load.

## Lena's watch: `VhKeepsakeWatch`

```csharp
var watch = VhKeepsakeWatch.Ensure(nadiaBodyRoot, owned: true);   // builds it on the right forearm once; shows / hides
watch.SetTime(9, 41);  watch.running = true;  watch.rate = 1f;    // the hands; or drive hourHand / minuteHand / secondHand
```
Fitted to the skin at attach time (`measured` says whether the wrist was measured or estimated). Attach it after the
body is posed and scaled, as the capture does. The World agent owns the first-person body and its material: the watch
needs the body's skinned mesh enabled when `Ensure` is first called, and the arm under it should carry Nadia's own
textures (the capture uses `Models/Nadia/.../Color.jpg` on URP Lit; round 1's grey mitten was the raw model without one).

## Unity steps (main)

1. Import (Unity writes the `.meta` for `Characters/Resources/People/Overlays/dream_lena.png`; commit it).
2. Build the prefabs: `powershell -NoProfile -ExecutionPolicy Bypass -File tools/tripo/vh-people-capture.ps1 -BuildOnly`.
   Log `JAMKIT_VH_PEOPLE_BUILD_OK`, then a line per character with "atlas 9 x 6" and "LOD1 yes". Commit
   `Characters/Resources/People/*.prefab` and `Characters/Generated/*`.
3. Captures (graphics on, one GPU job, through capped-run): `powershell -NoProfile -ExecutionPolicy Bypass -File
   tools/tripo/vh-people-capture.ps1`. Grep the capture log for `JAMKIT_VH_PEOPLE pose` (every line should end "via
   animation mode" or "via a playable graph", never "FAILED"), `JAMKIT_VH_PEOPLE bed`, `JAMKIT_VH_PEOPLE watch` ("measured").

## Resume here (2026-10-13, stopped for the usage reset)

**Done and pushed:** everything in the status table above; the six people built (`Models/Elias`, `EliasBed`, `Mags`,
`Rafael` (new), `Lena` (new), `Representative` (new, from Lena)); configs `tools/tripo/people/*.json` (the retired ones:
`Rafael-from-elias.json`; the Mags-based representative is in git history at `0d33082`).

**Not yet done (next session):**
1. Unity: main runs the import (writes metas for `Models/Lena/*` and `Characters/Resources/People/Overlays/dream_lena.png`),
   then `vh-people-capture.ps1 -BuildOnly`, then the full capture (see "Unity steps"). Nothing of round 3 has been seen
   in Unity.
2. Optional Blender re-runs (texture-only changes since the last build, so the committed FBXs are current; run only if a
   `people/*.json` face or geometry value changes): `node C:/Users/User/.claude/hooks/gpu-short-run.mjs --
   C:/Users/User/Desktop/yougotgamesonyophone/FableAnimationTests/BasilDownfall/tools/blender/blender.exe -b
   --factory-startup -P C:/Users/User/Desktop/yougotgamesonyophone/VibeJamKit/tools/tripo/vh_blender.py -- build
   C:/Users/User/Desktop/yougotgamesonyophone/FableAnimationTests/.work/people-src/out/<Name>/build-spec.json`, then
   `uv run --with pillow --with numpy python tools/tripo/vh_make.py finish <Name>`.
3. Polish left on the faces (numpy previews): the representative keeps a few freckles at the cheek edges, a dark edge of
   her hair lock beside the right eye and a slightly grey patch where her lips were painted out; Rafael keeps one small
   dark dot at the left brow; EliasBed a little of the painted gap under his lip; Mags faint ridges on the back of the
   head. The representative's high neck reads as a cream high-neck blouse (no buttons or collar points yet).
4. Mags's glasses cord is still Tripo's lumpy geometry (round 1's "worms"): replace with a clean skinned tube, or delete.
5. After the capture: judge round 2 (the judge's file names map onto the new stills; `-seq-` sheets and `seq-*` frames
   show talking and blinking).

No Tripo generations are running. Tripo's Chrome was closed after the exports.

## Risks and open points
- Nothing of round 3 has been seen in Unity: the AnimationMode posing, the decal over the cleaned skin, the watch's
  measured fit, the cut-out overlay. The numpy previews are the only evidence.
- Mags's back of head keeps faint ridges where the second face's glasses were (sinking them by colour engraved the whole
  second face; reverted).
- EliasBed: a little of the painted gap under the lip still shows through the beard fill; his fringe falls over the
  bandage (geometry, kept as hair falling over it).
- Mixamo clips are committed (30 MB); if the repo goes public, keep `Models/Animations/` out of it.
