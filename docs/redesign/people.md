# People: the 3D cast (the People agent, 2026-10-12)

The redesign's 3D characters (REDESIGN.md, section 5: People). The bar is REDESIGN.md section 3; the look is stylized AAA
(It Takes Two, Psychonauts 2, Life is Strange: True Colors): stylized shapes and colour at high fidelity, not photoreal,
keeping what the creator valued in the 2D designs (a sketchbook-like individuality, unique posing, a strong defining
colour per character).

## Status (2026-10-12, first pass)

| | State |
|---|---|
| Access: Tripo | **BLOCKED.** Our automation Chrome (port 9333) is not signed in to Tripo. Signing in from automation was refused by the permission system, so a person has to do it once (below). The earlier Tripo work (Nadia) used a different Chrome profile that belongs to BasilDownfall, which is off-limits to this project's agents. |
| Access: Mixamo | **BLOCKED** the same way: not signed in on port 9333. |
| Access: CC0 animation library (Quaternius Universal Animation Library) | **BLOCKED**: its free download goes through itch.io's download flow, which the permission system refused to script. A person can download `Universal Animation Library[Standard].zip` (free, CC0, 15 MB) from quaternius.itch.io/universal-animation-library. |
| Access: Gemini (Flow, Nano Banana Pro) | Works (0 credits). |
| Model sheets for Tripo | Made: Lena, Theo, Elias (dream), Walt, Elias in the hospital gown. Picks below. |
| Component, builder, capture | Written and compile-checked (dotnet); not yet run in Unity. |
| Meshes, rigs, animations, prefabs | Waiting on the sign-ins above. |

### Unblocking (one-time, a person at the keyboard)
1. `powershell -File FableAnimationTests/art-tools/chrome.ps1 start`, then bring that Chrome window on screen (it lives
   left of the monitors; Win+Shift+Arrow moves it), and in its one tab:
   - https://studio.tripo3d.ai: sign in to the Tripo account with the credits (the one Nadia was made on).
   - https://www.mixamo.com: sign in with the Adobe account.
2. `powershell -File FableAnimationTests/art-tools/chrome.ps1 hide` (or `close`).
3. Optional, for a CC0 animation set that needs no account: download the Quaternius Universal Animation Library
   (Standard) zip into `FableAnimationTests/.work/downloads/` and say so.

After that, the People agent runs the whole pipeline below without anyone.

## The pipeline

1. **Model sheets (Gemini).** One 16:9 sheet per character: front, right profile, back, in a neutral A-pose with open
   empty hands, rendered as a stylized 3D game character with hand-painted textures and flat even light (Tripo's input
   wants clean silhouettes and no baked lighting). Each sheet is made from the approved 2D designs, attached as
   pictures. Prompts: `tools/tripo/sheets/<name>.txt` (+ `_common.txt`, joined into `<name>.prompt.txt`); the batch:
   `sh tools/tripo/sheets/run-sheets.sh` (FableAnimationTests/art-tools/flow.mjs, Nano Banana Pro, 0 credits). Outputs:
   `FableAnimationTests/art/incoming/vh3d-<name>-take<N>.jpg`.
2. **Views.** `uv run --with pillow --with numpy python tools/tripo/crop_turnaround.py <sheet> tools/tripo/sheets/<name>`
   gives `front.png`, `side.png`, `back.png` (1536 squares).
3. **Tripo** (as Nadia's README, Models/Nadia/README.md "How it was made"): multi-view input (Front, Right, Back), HD
   Model, H3.1, Ultra mesh, 4K texture, Remove Lighting on, PBR on, Quad topology, about 30k polygons, Private; then Auto
   Rig, Humanoid, Mixamo skeleton; export FBX (Blender preset, 4K, skeleton on). About 80 credits a character.
   `tools/tripo/chrome.mjs` now drives **our** automation Chrome on port 9333 (it used to start a second Chrome on
   BasilDownfall's profile). Each `drive.mjs` / `steps.mjs` call holds `FableAnimationTests/.work/gemini.lock` while it
   runs, the same lock flow.mjs takes, so the two never fight over the one tab.
4. **Animations** (Mixamo, or a CC0 humanoid library): FBX files dropped into `Models/Animations/`, one clip per file,
   named for the state they become (below). Unity's Humanoid retargeting plays any of them on any character.
5. **Faces.** Tripo paints the face into the texture (no eyes, lids or mouth geometry). Two backends are built into
   `VhFace` (blendshapes, or a face decal with an expression atlas); which one each character gets is decided once the
   meshes exist and the close-ups have been judged. Plan: blendshapes for mouth-open and blink, made in Blender around
   the face, if they read at conversation distance; otherwise the decal (the eyes and mouth painted out of the base
   texture and drawn on an atlas: rows = expressions, columns = mouth closed / half / open, then the same with eyes
   closed).
6. **Unity:** `JamKit > Visiting Hours > People > Build the Cast` (below) makes the prefabs; `Capture the Cast` makes the
   judge's stills.

## The component (for World, House, Day and Battle)

Assembly `JamKit.VisitingHours.Characters` (`Characters/Scripts/`, no dependencies). **To use it, add
`"JamKit.VisitingHours.Characters"` to your asmdef's `references`** (JamKit.VisitingHours, JamKit.DreamEscape, ...).
Namespace `JamKit.VisitingHours.Characters`.

```csharp
var lena = VhCharacter.Spawn("Lena", position, rotation, parent);   // Resources/People/Lena.prefab
lena.Play("idle");                    // cross-fade to a looping state (default fade 0.25 s; Play("talk", 0.4f))
lena.PlayOnce("wave");                // a one-shot, then back to idle;  PlayOnce("sit-down", "sit")
lena.Has("sit");  lena.IsPlaying("walk");
lena.LookAt(player.transform);        // or LookAt(worldPoint); StopLooking(). Chest, neck and head, eased, clamped
lena.Talk(true);  lena.Talk(false);   // mouth moving / still
lena.Say(2.4f);                       // mouth moving for 2.4 s (e.g. per line of text), then still
lena.SetExpression("hurt");           // blend to a named expression (VhFace.expressions); false if she has none
lena.Blink();
```

- Root: `VhCharacter`, `VhFace`, a `CapsuleCollider` (the character's height). Child `Body`: the model, with the
  `Animator` (Humanoid avatar, the character's controller, root motion off). Clips are baked in place: code moves the
  character; play "walk" while moving it at walking speed.
- Look-at and breathing run in LateUpdate after the animation, so they work over any clip. Tuning fields: `lookWeight`,
  `maxYaw` (70), `maxPitch` (30), `lookSharpness`, `lookShares` (chest/neck/head), `breathe`.
- A missing state logs one warning and is ignored (no exception), so code can ask any character for any state.
- No per-frame allocation.

### State names (the same on every character)
Lower-case, from the clip file names (`Talk Gesture 2.fbx` → `talk-gesture-2`). Looping: names starting with idle, talk,
walk, run, jog, sit, lie, sleep, listen, think, breath, mow, wash, grill, water, ride, bike, browse, sweep, rake, type,
write, read. Everything else is a one-shot. The planned set (filled in when the animations exist):

| Who | States |
|---|---|
| Everyone | `idle`, `talk`, `listen`, `walk`, `sit`, `sit-down`, `stand-up`, `nod`, `shake-head`, `wave`, `shrug`, `point` |
| Lena | + `idle-arms-crossed`, `notebook` (writing in it), `hurt`, `angry` |
| Theo | + `idle-fidget`, `hide` (crouched), `laugh` |
| Elias (dream) | + `grill` (turning things with the tongs), `laugh` |
| Elias (bed) | `lie` (asleep in the bed: the one state) |
| Walt | + `wipe-hands` (the rag), `lean`, `fix-bike` |
| Neighbours | `mow`, `wash-car`, `grill`, `water`, `ride-bike`, `browse` (yard sale), `sweep` |

## The cast

Prefabs: `Assets/JamKit/Templates/VisitingHours/Characters/Resources/People/<Name>.prefab` (built by the builder; not in
git until main has run it). Models: `Models/<Name>/<Name>.fbx` with Tripo's texture folder beside it.

| Prefab | Height | Defining colour | Sheet pick (art/incoming) | Notes |
|---|---|---|---|---|
| Lena | 1.88 m (6'2") | deep wine-red trousers on cream | `vh3d-lena-take1` | the pencil on her right ear; the watch face-in; nothing on her chest; notebook a separate prop |
| Theo | 1.38 m | mustard hoodie | `vh3d-theo-take1` | ten; patched right knee, red sneakers |
| Elias | 1.83 m | cornflower-blue apron on white | `vh3d-elias-dream-take1` | the dream Elias at the barbecue; tongs a separate prop |
| EliasBed | 1.83 m | pale hospital blue | (see "Sheets") | the waking Elias, hospital gown, head bandage, eyes closed; posed lying |
| Walt | 1.86 m | tan apron, red rag, brown tweed cap | retake pending (see "Sheets") | tall, thin; the rag tucked in his apron so the hands rig cleanly; a hand-held rag prop for `wipe-hands` |
| Nadia | 1.65 m | olive jacket, navy scrubs | (existing) | `Models/Nadia/Nadia.fbx` stays as the World agent's first-person body (Generic import). The builder does **not** touch it; a third-person Nadia gets a copy. |

Not started (after the first milestone): Mags, Rafael, 6-10 Saturday neighbours, 2-3 ward visitors and staff.

## Sheets (2026-10-12)

Take 1 of Lena, Theo and Elias (dream) is the pick: closer to a stylized 3D game render (clean forms, hand-painted cloth)
than take 2, which drifts towards painterly realism (the creator turned that down for portraits). Their views are cropped
for Tripo in `tools/tripo/sheets/<name>/{front,side,back}.png`. Weak spots, to check on the meshes: Lena's face is a
little generic (her freckles and wide mouth are faint), Elias's dream face is cuter than the waking design.

**Walt: retake needed.** Both takes (`vh3d-walt-take1/2`) came out realistic and gritty, with a stern face (take 2 angry),
out of style with the rest of the cast and not the kind old man of his design. The retake prompt attaches Elias's sheet
as a style reference and asks for a kind face; not run yet:

    cd FableAnimationTests
    node art-tools/flow.mjs --image --out vh3d-walt2 --prompt-file ../VibeJamKit/tools/tripo/sheets/walt2.prompt.txt \
      --attach "art/incoming/sheet-walt-bb-take1.jpg|art/incoming/vh3d-elias-dream-take1.jpg" --takes 2

**Elias in the gown:** `vh3d-elias-bed-take1/2` were generating when this pass stopped; not reviewed yet (if they drift
out of style, retake with the same style-reference trick).

## Building the prefabs (main, in Unity)

1. Let Unity import the new folders (it writes the .meta files; commit them).
2. `JamKit > Visiting Hours > People > Build the Cast`, or batch:
   `-executeMethod JamKit.VisitingHours.Characters.EditorTools.VhPeopleBuild.BuildFromCommandLine` (log line
   `JAMKIT_VH_PEOPLE_BUILD_OK` then one line per character: prefab path, measured height, avatar check, states,
   blendshapes). It skips characters whose model isn't there yet. Safe to re-run.
   Per character it: sets the FBX to Humanoid at the character's height, makes `Characters/Generated/<Name>.mat`
   (URP Lit: base colour, normal map, and `<Name>_MetallicSmoothness.png` built from Tripo's roughness/metallic maps),
   imports every FBX in `Models/Animations/` as a Humanoid clip baked in place, builds `Characters/Generated/<Name>.controller`
   (one state per clip, default `idle`) and saves the prefab.

## Captures (for the judge)

`JamKit > Visiting Hours > People > Capture the Cast`, or batch with graphics:
`powershell -NoProfile -ExecutionPolicy Bypass -File tools/tripo/vh-people-capture.ps1` (through capped-run).
Writes `out/vh-people/<time>/`, all 1920×1080, for two lights (a neutral stand-in until the Look agent's lighting exists):
`golden` (low warm sun, cool rim, procedural sky, haze) and `hospital` (cool overhead light, ward-green wall):
- `<light>-lineup.png`: the cast side by side in their idle;
- `<light>-turn-<Name>.png`: eight angles, 45° apart;
- `<light>-face-<Name>.png`: conversation-distance close-ups: neutral, mouth half open, mouth open, a blink, then each
  expression.
