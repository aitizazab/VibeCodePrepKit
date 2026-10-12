# Nadia: rigged 3D model (Tripo, 2026-10-10)

The waking-world Nadia, made with Tripo Studio from her style-E turnaround
(`FableAnimationTests/art/incoming/sheet-nadia-turn-take3.jpg`, the pick in `docs/art/vh-sheets-picks.json`).

## Files
- `Nadia.fbx`: the mesh and its skeleton (Tripo's FBX export, "Blender" preset, Export Skeleton on). Skinned, rest pose
  is the sheet's A-pose. No animations.
- `tripo_convert_19cb9a38-aa63-491e-a145-4e82bb47065d.fbm/`: the textures, all 4096 x 4096: `Color.jpg` (base colour),
  `Normal.png`, `Nadia_roughness.JPEG`, `Nadia_metallic.JPEG`. The FBX finds them by this relative folder name, so
  keep the folder name as it is (renaming the FBX is fine).
- The pictures given to Tripo, and two screenshots of the result in Tripo's viewer: `docs/art/tripo/nadia/`
  (`front.png`, `side.png` = her right side, `back.png`; `tripo-viewer-turn.png`, `tripo-viewer-face.png`).

## What Tripo made
- **Input:** multi-view, our own three views cropped from the turnaround (Front, Right, Back slots; Left left empty).
  Tripo's own "Generate Multi-Views" was not used: it redraws every view as a glossy game render.
- **Settings:** HD Model, AI model H3.1, Ultra Mesh Quality, Texture 4K, Remove Lighting on, PBR on, topology Quad,
  polycount 30000, Private.
- **Mesh:** one mesh, one material. 27,527 vertices, 27,433 faces (27,319 quads), 54,782 triangles.
- **Rig:** Tripo Auto Rig, Humanoid, skeleton preset **Mixamo**: 65 bones named `mixamorig:Hips`, `mixamorig:Spine`,
  `Spine1`, `Spine2`, `Neck`, `Head`, `HeadTop_End`, `Left/RightShoulder`, `Arm`, `ForeArm`, `Hand`, five fingers of
  four bones each (`LeftHandIndex1..4`, ...), `Left/RightUpLeg`, `Leg`, `Foot`, `ToeBase`, `Toe_End`. 49 of them carry
  skin weights. It should map to a Unity Humanoid avatar automatically (Mixamo naming), and Mixamo animations retarget
  onto it.
- Checked headlessly in Blender 4.5 (`tools/tripo/inspect_fbx.py`): imports cleanly, upright, textures found.

## Known issues (look at these in Unity)
- **Scale: she is 0.98 m tall** as exported (Tripo normalises its models). For a ~1.65 m woman set the importer's
  Scale Factor to about **1.69**, or scale the instance.
- **Materials:** Tripo's PBR set; in URP make a Lit material with Color.jpg as Base Map and Normal.png as Normal Map
  (roughness/metallic are separate greyscale maps, not Unity's packed mask; leaving them off is fine: the outfit is
  all cloth, so smoothness about 0.2 and metallic 0).
- **Colours in Tripo's viewer looked washed out** (grey-green jacket, lavender-grey trousers) but the texture itself is
  the sheet's olive jacket and navy scrubs, slightly greyer. Skin is pale, close to the sheet.
- **Face:** reads as her: tired eyes with dark circles, flat neutral mouth, hair parted and pulled back with loose
  strands. Painted on, not modelled: no separate eyes, eyelids or mouth, so no blink or lip-sync without a face
  layer (blendshapes, or a decal face like BasilDownfall's comic faces).
- **Hair:** the bun is there at the back; the loose strands by the ears are spiky texture/geometry and look rough up
  close. One hair mass, no hair bones.
- **Hands:** open A-pose hands with all finger bones rigged. They were only seen at full-body distance in Tripo's
  viewer (they look fine there); not checked up close. Tripo's earlier characters had smeared, thick fingers, so
  expect the same in close-ups.
- **Lanyard and badge** are part of the body mesh (no physics). The badge is blank, as on the sheet.
- The jacket is skinned to the body: big arm raises will stretch it.

## Credits
80 Tripo credits: 60 model + 20 rig. 630 before, 550 after. Exports are free: the same asset can be re-exported
(e.g. with the "Mixamo" FBX preset, 21 MB with the textures embedded as 4K PNGs) from Tripo's Assets panel.

## How it was made (to repeat for the other characters)
Scripts in `tools/tripo/` (since 2026-10-12 they drive our automation Chrome on port 9333; see docs/redesign/people.md.
They used to reuse a logged-in Chrome profile
`FableAnimationTests/BasilDownfall/.chrome-tripo` on port 9472 and refuse to touch a Chrome they did not start):
1. `uv run --with pillow --with numpy python tools/tripo/crop_turnaround.py <sheet.jpg> docs/art/tripo/<name>`
   (front, side, back as 1536 squares).
2. `node tools/tripo/drive.mjs goto https://studio.tripo3d.ai/workspace/generate`; Model tab, the second (cube) input
   tab; `upload "input[type=file] >> nth=0" front.png`. The inputs renumber after each upload, so list them
   (`eval` on `input[type=file]` parents) before each further upload; Right takes the side view (sheets show her
   right side), Back the back.
3. Geometry & Texture as above; Generate (60). `node tools/tripo/drive.mjs waitgone Generating 480` (run in the
   background; it returns when the model is done).
4. Rig panel: Humanoid, Mixamo, Auto Rig (20).
5. Export: FBX, preset Blender, 4k, Export Skeleton on; `download 970 756 <dir>`; unzip. `node tools/tripo/drive.mjs close`.
