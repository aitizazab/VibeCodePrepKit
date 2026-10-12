# People: the 3D cast, the paper cut-outs, Lena's watch (the People agent)

The redesign's people (REDESIGN.md, section 5: People; section 7: who is 3D). The bar is REDESIGN.md section 3; the
look is stylized AAA (It Takes Two, Psychonauts 2, Life is Strange: True Colors), keeping what the creator valued in the
2D designs: individuality, unique posing, a strong defining colour per character.

- **3D (Tripo models, rigged, animated, talking faces):** Elias (the dream, at the barbecue), EliasBed (the waking Elias,
  asleep), Mags, Rafael (altered from Elias), LeBaget's representative (a woman, altered from Mags). Nadia's existing
  model stays the World agent's first-person body.
- **Paper cut-outs (billboards):** Lena, Theo, Walt, the neighbours, the dog: `DreamCutoutView`.
- **Lena's keepsake:** her star-shaped watch on Nadia's right wrist: `VhKeepsakeWatch`.

## Status (2026-10-12, milestone 1: built, compile-checked, NOT yet seen in Unity)

| | State |
|---|---|
| Models | Five FBX in `Models/<Name>/` (rigged, Mixamo skeleton, shape keys, face decal mesh, accessories), with textures and a sidecar. Previewed in numpy renders (front/side/back, the face sheets); never in Unity. |
| Animations | 31 Mixamo clips (no skin, 30 fps, in place where it matters) in `Models/Animations/`. |
| Prefabs | `VhPeopleBuild` builds them; **main must run it** (below). |
| Billboards | `DreamCutoutView` + `JamKit/Dream/Cutout` rewritten: paper with a cut edge, lit and shadowed, sway, boil, flips. Existing call sites unchanged. |
| Lena's watch | `VhKeepsakeWatch` (code-built). |
| Captures | `VhPeopleCapture` rewritten on the Look agent's rigs; **main runs them**. |
| Compile | UnityLayerEditor, UnityLayerPlayer, UnityApi: green (2026-10-12). Shaders are not compile-checked outside Unity. |

Tripo credits: 350 before this pass, 310 after (two auto-rigs, 20 each). No new generations: Rafael and the representative
cost nothing.

## The models

| Prefab | Height | Defining colour | What they look like | Made from |
|---|---|---|---|---|
| Elias | 1.83 m | cornflower-blue apron on white | brown swept hair, full beard, rolled white sleeves, grey trousers, brown shoes, steel watch | Tripo (sheet `vh3d-elias-dream-take1`) |
| EliasBed | 1.83 m | pale hospital blue | the same man in a patterned gown, head bandage, eyes painted shut, wristband, cannula, socks; idle `lie` | Tripo (`vh3d-elias-bed2-take2`) |
| Mags | 1.62 m | oatmeal cardigan over pale-blue scrubs, brick-red beaded glasses cord | close-cropped grey curls, dark-framed glasses low on the nose, kind lined face, grey clogs | Tripo (`vh3d-mags-take2`) |
| Rafael | 1.76 m | surgical-teal scrubs under a white apron | 31; olive skin, black hair (flattened, shorter), beard trimmed close, slimmer chest and arms, smaller head, white trainers, a pen behind each ear (blue and black, silver caps), a white ID badge with a teal stripe | **altered from Elias** |
| Representative | 1.72 m | aubergine blazer-cardigan over a cream blouse; gold | mid-forties; fair skin, dark brown hair, berry lipstick, gold glasses on a gold chain, charcoal trousers, black shoes, a gold wristband, taller and slimmer than Mags (longer legs, narrower body), a red-striped visitor badge and a gold pin on the lapel | **altered from Mags** |

### How Rafael and the representative differ from their bases
All in `tools/tripo/people/<Name>.json` (the recipe), run by `tools/tripo/vh_make.py`:
- **Texture recolour by class** (`vh_alter.py`): every texel is classified by colour (nearest prototype sampled from the
  model at given 3D points; for the representative a shading-invariant metric, luminance + hue ratios) and gated by its
  3D position (height, depth), then recoloured to the target keeping its shading (`flatten` drops some of it, e.g. the
  apron's old print). Majority-filter despeckle; UV padding takes its island's class.
- **Geometry**: per-vertex moves (Rafael's beard pulled in 4.5 mm, hair 7 mm), **bone-scale proportions baked into the
  rest pose** in Blender (Rafael: spine 0.92 wide, arms 0.92, head 0.96; the representative: thighs 1.07 long and 0.95
  slim, spine 0.9 wide and 1.04 long, arms 0.94, head 0.94), then the importer scales each to its height.
- **Accessories**: small meshes skinned 100% to a bone (pens, caps, badges, a pin), own materials from the sidecar.
- Faces: the same face pipeline as the bases, so they talk and emote the same way.

### Fixes applied to every Tripo model
- **Two-faced heads.** Tripo's multi-view generation put a second, mirrored face on the back of every head (in the mesh
  and the paint) and painted the front of the clothes onto their backs (the apron bib, the cardigan's opening). The
  pipeline rounds the back of the skull onto a fitted ellipsoid curving into the neck, repaints it (hair with a strand or
  curl pattern above a wavy nape line, skin below, the bandage carried round for EliasBed), flattens the normal map
  there, clears Tripo's custom normals, and repaints the garment backs plain (`back_of_head`, `back_paint`). Seen in the
  numpy renders: clean. **Not yet seen in Unity.**

### Faces (talking and emoting)
Both backends at once, built in Blender by `vh_blender.py` from landmarks read off a gridded front render
(`vh_headgrid.py`; eyes found automatically in a search box):
- **Shape keys** on the body mesh: `MouthOpen` (the jaw drops, everything under the lip line), `Smile`, `Frown`,
  `BrowsUp`, `BrowsDown`, `BrowsSad`.
- **A face decal**: a skinned copy of the face's front (pushed out 0.6 mm, carrying the same shape keys), UV-projected
  from the front onto an atlas (`<Name>_FaceAtlas.png`, 6 cells: mouth closed / half / open, then the same with the
  eyes shut). The cells add the mouth's inside (teeth, tongue, lip rim), which stretches open with `MouthOpen`, and
  closed lids drawn from the lid's own skin with a lash line. EliasBed's eyes are painted shut (no lids, no blinking).
- Expressions (blendshape weights, from the sidecar): neutral, warm, happy, sad, worried, hurt, angry, surprised,
  doubtful. Talking: `VhFace`'s syllable rhythm drives `MouthOpen` and the decal's mouth cell together.
Judged on numpy renders only (`.work/people-src/out/<Name>/faces.png`): the talking mouth and the blink read; the
brows read; the smile is modest. Judge again on the Unity capture.

## Animations (`Models/Animations/`, one clip per file, state = file name)
Mixamo, exported without skin through Mixamo's own web API by `tools/tripo/mixamo.mjs` (list: `mixamo-clips.json`):
`idle` (breathing idle), `idle-happy`, `idle-sad`, `idle-tablet`, `listen` (weight shift), `talk` (general conversation),
`talk-2` (asking with one hand), `talk-amused`, `talk-meeting`, `talk-phone`, `think`, `nod`, `nod-thoughtful`,
`shake-head`, `agree`, `wave`, `shrug`, `point`, `laugh`, `disappointed`, `walk` (in place), `walk-female`,
`walk-briefcase`, `sit`, `sit-talk`, `sit-down`, `stand-up`, `lie` (asleep on a bed), `lie-awake`, `type`, `write`.
Every prefab's controller has them all; EliasBed's default state is `lie`. The Quaternius library (CC0, in
`FableAnimationTests/.work/ual-unpacked/`) is not used yet.

## The component (for World, House, Day and Battle): unchanged

Assembly `JamKit.VisitingHours.Characters` (add it to your asmdef's `references`). Namespace `JamKit.VisitingHours.Characters`.

```csharp
var mags = VhCharacter.Spawn("Mags", position, rotation, parent);   // Resources/People/Mags.prefab
mags.Play("talk");  mags.PlayOnce("nod");  mags.Play("idle", 0.4f);
mags.LookAt(player.transform);  mags.StopLooking();
mags.Talk(true);  mags.Say(2.4f);  mags.SetExpression("worried");  mags.Blink();
```
Names: `Elias`, `EliasBed`, `Mags`, `Rafael`, `Representative`. New in `VhFace`: `followers` (meshes that copy the face's
blendshape weights by name; the prefab builder sets the decal there). Missing states warn once and are ignored.

## Billboards: `DreamCutoutView` (JamKit.DreamEscape)

Every cut-out the builders already make (a Quad child "Card" with a `JamKit/Dream/Cutout` material, the component's
`card` pointing at it) now becomes paper by itself, in play mode and in edit mode (a DontSave child, so saved scenes keep
only the quad). Nothing to change at the call sites. New code can use:
```csharp
var walt = DreamCutoutView.Create("Walt", waltTexture, parent, feet, 1.86f, restYaw: 180f, material: null);
walt.Watch(playerCamera);              // optional: it finds Camera.main
walt.Flip(waltWavingTexture);          // the paper flip: edge-on, swap, back (0.32 s); draw poses at the same height
walt.thickness = 0.016f; walt.sway = 0.9f; walt.breath = 0.006f;   // tuning
DreamCutoutView.PaperMesh(texture, thickness);   // the shared mesh for a picture
```
- **Paper:** the picture on the front, card stock on the back (the print faintly showing through, mirrored), and an
  edge of real thickness (1.6 cm, foam-board) cut along the figure's outline, traced from the picture's alpha
  (marching squares at 192 px tall, read back through the GPU, so the pictures need not be readable; one mesh per
  picture, shared).
- **Light:** `JamKit/Dream/Cutout` is now a lit URP shader: the sun with wrap diffuse and its cascaded soft shadows,
  the additional lights through the Forward+ cluster loop (the house's lamps), ambient from the probes/sky, SSAO, fog,
  light through the paper when backlit (warm), a faint fibre grain, a sheen; it casts and receives shadows; depth and
  depth-normals passes. Properties kept (`_MainTex`, `_Color`, `_Cutoff`, `_BackShade`), so existing materials work.
- **Alive:** a slow sway and breath about the feet; the boil (the picture shifts by a hair 8 times a second, per
  cut-out phase); the turn to watch you (as before: a beat late). In edit mode the transform stays exactly as built.

## Lena's watch: `VhKeepsakeWatch` (JamKit.VisitingHours.Characters)

```csharp
var watch = VhKeepsakeWatch.Ensure(nadiaBodyRoot, owned: true);   // builds it on the right forearm once; shows / hides
watch.SetOwned(false);                                           // hide (the keepsake not earned / lost)
watch.SetTime(9, 41);  watch.running = true;  watch.rate = 1f;    // the hands; or drive hourHand / minuteHand / secondHand
```
A gold five-pointed star case (35 mm point to point, bevelled), a cream enamel star dial with a fine gold inner star,
twelve markers and a tiny wine-red star under twelve, hour, minute and second hands (each its own Transform, rotating
about the face's normal: free for the Night 4 mechanic), a centre cap, lugs, and a wine-red leather strap (Lena's colour)
round the wrist. It finds the Mixamo bones (`RightForeArm`, `RightHand`, `RightHandMiddle1`, `RightHandThumb1`), sits on
the back of the wrist 3.5 cm up the forearm, twelve toward the little finger (upright when she lifts the forearm across
her body), real size whatever the rig's scale. `faceIn = true` before attaching wears it face-in, as Lena did.
The World agent owns the first-person body: call `Ensure(body, owned)` wherever that body is built or the keepsake changes.

## Unity steps (main)

1. **Import:** open the project (or let the batch run import): Unity writes the `.meta` files for `Models/Elias`,
   `EliasBed`, `Mags`, `Rafael`, `Representative`, `Models/Animations` and `Characters/Scripts/VhKeepsakeWatch.cs`.
   Commit those metas.
2. **Build the prefabs** (no graphics needed):
   `-executeMethod JamKit.VisitingHours.Characters.EditorTools.VhPeopleBuild.BuildFromCommandLine`, or
   `powershell -NoProfile -ExecutionPolicy Bypass -File tools/tripo/vh-people-capture.ps1 -BuildOnly`.
   Log `JAMKIT_VH_PEOPLE_BUILD_OK`, then a line per character: prefab, height, avatar check, states (default),
   blendshapes, "face decal yes", expressions, accessories. Writes `Characters/Resources/People/*.prefab` and
   `Characters/Generated/*` (materials, controllers, metallic maps): commit them.
3. **Captures** (graphics on; one GPU job): `powershell -NoProfile -ExecutionPolicy Bypass -File tools/tripo/vh-people-capture.ps1`
   (build + capture), through capped-run. Output `out/vh-people/<time>/`: `golden-` and `hospital-` lineup, pairs
   (Elias/Rafael, Mags/Representative, wide and close), turntables, face sheets; `hospital-bed-EliasBed*.png`;
   `paper-front/edge/back/close-walt-elias.png`; `watch-first-person.png`, `watch-close.png`, `watch-close-2-05.png`.
   Logs `out/vh-people-build.log`, `out/vh-people-capture.log` (grep `JAMKIT_VH_PEOPLE`, `Shader error`).

## The pipeline (to make or change a person)

Sources: Tripo's FBX exports (free to re-export from Tripo's Assets panel) and their mesh dumps live in
`FableAnimationTests/.work/people-src/<src>/` (not in git). Per character, `tools/tripo/people/<Name>.json`:
```
uv run --with pillow --with numpy python tools/tripo/vh_make.py prep <Name>      # recolour, back fixes, face atlas, build spec
node C:/Users/User/.claude/hooks/gpu-short-run.mjs -- <blender> -b --factory-startup -P tools/tripo/vh_blender.py -- build <spec>
uv run --with pillow --with numpy python tools/tripo/vh_make.py finish <Name>    # into Models/<Name>/, preview renders
```
(`prep` prints the exact Blender line; Blender is `FableAnimationTests/BasilDownfall/tools/blender/blender.exe`, used in
place.) A new Tripo model: `tools/tripo/gen.mjs sheets/<name>` (60 credits), rig in Tripo (Humanoid, Mixamo, 20),
export FBX (Blender preset, 4K, skeleton), unzip into `.work/people-src/<src>/`, then
`blender ... vh_blender.py -- dump <fbx> <src>/dump.npz`; read landmarks with
`uv run ... vh_headgrid.py <dump> <Color.jpg> <out.png>` (5 mm grid, mm labels). Tools: `vh_tex.py` (mesh dump
renderer and UV rasteriser), `vh_alter.py`, `vh_face.py`, `vh_preview.py`, `mixamo.mjs`.

## Risks and open points
- **Nothing here has been seen in Unity.** The FBX round-trip through Blender (axes, scale, the Humanoid avatar from the
  re-exported skeleton), the decal's transparency sorting over the skin, the paper shader (not compiled outside Unity),
  and the watch's placement on Nadia's real rig are all unverified until the captures come back.
- **Mixamo clips are committed** to the repo (30 MB). Mixamo's licence allows them in a game; it doesn't allow
  redistributing the files on their own. If the repo is public, consider keeping `Models/Animations/` out of it.
- Mags's back of head: a few tiny folded triangles remain where the second face's glasses were (visible only very close
  from behind). The representative's lenses keep a pale glare from Mags's paint; her glasses' arms stay dark.
- The talking mouth is a painted inside on the decal plus the jaw shape key: it reads at conversation distance in the
  numpy renders; at extreme close-up the teeth are a flat band.
- Rafael's proportions are subtle (he's still clearly built on Elias's frame; the colour, skin, hair, beard and
  accessories carry most of the difference).
- `DreamCutoutView` now runs in edit mode (`[ExecuteAlways]`); its edit-mode work is building the DontSave paper child
  and keeping the card at its built transform. Cut-outs created and captured in one batch call without a scene reload
  get their paper only if the builder sets `card` before adding the component (use `Create`), else they render as the
  lit two-sided quad until the scene is reopened.
- No billboards were redrawn: the paper look uses the existing art (`Art/Dream/dream_*.png`, with its white sticker
  border, which now reads as the paper's printed margin). Pose pictures for `Flip` don't exist yet.
