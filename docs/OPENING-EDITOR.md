# The opening editor (Visiting Hours)

A tool for placing things in the opening's bedroom and on the title table yourself, inside the game (the build as
well as the Unity editor). You scrub to any beat of the opening, pick a thing, move, turn or size it, set each shot's
camera, then play on from that point to check. The edits are saved as a small file and used every time the sets are
built.

## Opening it
- **F9** on the title (during the opening or after it). F9 again closes it.
- Or the presenter panel: **Edit the opening**. This works from anywhere: it goes back to the title first.
- Or start the game straight in it: double-click **Edit the opening.cmd**. Every Windows build with Visiting Hours
  in it writes this file next to its exe. It runs the exe with the **-edit-opening** flag, which you can also pass
  yourself.
- During the opening, "F9: edit this scene" shows in the bottom-left corner for its first few seconds.
- While it's open, the opening holds still and its black fades are lightened so you can see the set at dark moments.
  When you close it, the opening plays on from where you were, or the title takes input if you were at the end.

## The screen
- **Left:** everything you can move, by plain names, in groups:
  - in the bedroom
  - shots (the cameras)
  - at the title table
  - lights
- **Top:** the timeline.
  - **Play from here** (Space) plays the opening from where you are (and pauses it again).
  - The **beat buttons** jump straight to a beat:
    - the room
    - the phone on the nightstand
    - she reaches
    - the grab
    - holding it
    - she puts it down
    - falling asleep
    - the phone from above
    - G'night
    - the news report
    - the pieces arrive
    - the title
  - The **slider** scrubs to any moment. **Left/right arrows** step between beats.
- **Right:** the thing you picked.
  - A line on what follows it, or what can't.
  - Its **position**, **turn** (degrees) and **size**, as numbers in its parent (the room, the bed or the table).
  - **Frame it in my view**.
  - **Reset this one**.
  - For a shot: its **field of view**, **Look through it** and **Set from my view**.
- **Bottom:**
  - the tools: Move (W), Turn (E), Size (R), Snap (X: 1 cm, 5°, 5%)
  - Undo/Redo, Camera (C), Reset everything
  - Rebuild with my edits, Save, Help (H), Close (F9)
  - the status line (it says where things were saved)

## Moving things
- **Pick a thing:** click it in the view, or pick it from the list. Shots and lights show as small circles you can
  click.
- **Drag the coloured handles:** red is X, green is Y, blue is Z.
  - Move and turn use the world's directions.
  - Size uses the thing's own directions; the white dot in the middle sizes it all at once.
- **Or type numbers:** press Enter (or click away) to apply them.
- **The free camera:**
  - Hold the right mouse button to look around.
  - While holding it: W A S D move, Q and E go down and up, Shift is faster, and the mouse wheel sets the speed.
  - **C** switches between the free camera and the shot's own camera.
  - **F** frames the selected thing.
- **Shots:** each shot's camera is one key.
  - To change it: **Look through it**, fly to where it should be, set the field of view, then **Set from my view**.
  - Changing one shot never changes another. The moves between shots follow the new keys.
  - The slow push-in from above and the drift over the table start from the shot.

## What follows what
- **The bed** carries Nadia, her blanket and the three shots from her eyes: on her pillow, looking at the nightstand,
  rolling away. Her rest pose and her arm's reach follow it.
- **Nadia** (a handle at her eyes) carries those same three shots. When you let go, her blanket reshapes over her.
- **The nightstand** carries the lamp, the phone, the photo and the shot from above.
- **The phone** is where it rests. Her hand follows it: she takes it by its bottom end, back to her palm, with no
  turn-over. Keep that end a few centimetres over the nightstand's edge so her fingers fit under it.
- **Where she holds the phone up** follows the "looking at the nightstand" shot. Her arm keeps it within her reach.
- **The table** carries the tablet, the mug, the notepad, the pen and the badge. The tablet doesn't turn by itself to
  face a moved title camera: turn it.
- **The streetlight's blind stripes** are worked out when the sets are built. Press **Rebuild with my edits** to see
  them follow a moved streetlight.
- **The morning sun, and the lamps the lighting adds itself**, are placed by the lighting. Your moves are applied on
  top of that.

## Saving
**Ctrl+S**, or the Save button. Closing the editor also saves any unsaved edits.

**What's saved:** `VisitingHoursOpeningEdits.json`. It holds only what differs from the built scene, keyed by stable
ids such as `phone`, `nightstand`, `bed`, `title-tablet`, `sun` and `shot:table`.

**Where it's saved:**
- **In a build:**
  - next to the build's data: `<build>/VisitingHours_Data/StreamingAssets/VisitingHoursOpeningEdits.json`
  - **and** into the repository, when it finds one. It looks for a `VibeJamKit` folder beside one of the folders
    the build sits in. For a build in `VibeJamKit-play/Builds/Play/VisitingHours`, that's
    `VibeJamKit/Assets/JamKit/Templates/VisitingHours/Resources/JamKit/VisitingHoursOpeningEdits.json`.
- **In the Unity editor:** the same file in the project.

**Committing:** commit `Assets/JamKit/Templates/VisitingHours/Resources/JamKit/VisitingHoursOpeningEdits.json`, plus
the `.meta` Unity makes for it. Every build after that starts with these edits.

**How they're read back:**
- **A build** reads its own StreamingAssets copy first (your latest edits in that build), then the committed one.
- **The Unity editor** reads the project's file.
- **With no file**, the opening is exactly as built.

**When ids stop matching:** if an id no longer matches anything (a thing was renamed or removed), that edit is skipped,
with a warning in the log.

## For developers
- **The model:**
  - `Scripts/Opening/VhSceneEdits.cs` holds what can move (`Register`), the overrides (`VhSceneEditsData`), and
    applying, capturing and saving them.
  - Things made of parts get one empty handle ("Edit: the lamp", "Edit: the bed"...) standing where they stand, so
    their parts keep their own placement.
- **The builder:** `VhStageBuilder.Build` applies the overrides around the lighting:
  - everything else **before** `VhLighting.Apply`, so its cookies trace the moved lights;
  - the lights it places itself **after** it.
- **The interface:** `Scripts/Opening/VhOpeningEditor.cs`, on the GameRoot. It is a UI Toolkit overlay, and its
  handles are drawn and picked in screen space.
- **The timeline:** `VhOpening.Seek` replays every cue up to a time, at each cue's end state. `VhOpening.PlayFromHere`
  restarts the voices mid-way.
- **Adding a thing:** register it in `VhSceneEdits.Register` with a new, stable id. Never rename an existing id: saved
  edits use it.
- **Tests:**
  - EditMode `VisitingHoursOpeningEditsTests`: round trip, apply at build, stable ids, unknown ids, carrying.
  - PlayMode `VisitingHoursOpeningEditorPlayTests`: move the phone and the grab is still back-to-palm; the presenter
    entry.
