# The opening editor (Visiting Hours)

A tool for placing things in the opening's bedroom and on the title table yourself, inside the game (the build as
well as the Unity editor). You go to any beat of the opening, pick a thing, move, turn or size it, set each shot's
camera, then play on from that point to check. The edits are saved as a small file and used every time the sets are
built.

## How to

1. **Launch:** double-click **Edit the opening.cmd** next to the game's exe (or press **F9** on the title, during the
   opening or after it). The opening stops where it is. The bar at the bottom says `PAUSED | FREE CAMERA | ...`.
2. **Look around:** hold the **right mouse button** and move the mouse to look; while you hold it, **W A S D** fly
   and **Q / E** go down and up (Shift is 4x faster; held over a second it speeds up). The **mouse wheel** moves in
   and out, the **middle mouse button** pans, **Alt + left mouse** orbits around the selected thing, **F** frames it.
   Too dark to see? **L** turns on a work light (only while editing). **Tab** hides the panels for a clean view.
3. **Select:** move the mouse over a thing: a white box and its name show what a click would pick. Click it. Or
   click it in the list on the left (double-click selects it and frames it). It gets an orange box, and its
   numbers and notes show on the right.
4. **Move:** drag one of its arrows (red X, green Y up, blue Z) to move it along that arrow, or the small square in
   the middle to move it across the view. **E** gives rings to turn it, **R** boxes to size it, **W** the arrows
   again. Or click a number on the right and type (Enter applies). The status line says what happened
   ("Moved The tablet +5.0 cm along X (red)"). **Ctrl+Z** undoes.
5. **Save:** **Ctrl+S**, or just close with **F9** (closing saves too). The bar shows `* UNSAVED CHANGES` until you do.

## Opening it
- **F9** on the title (during the opening or after it). F9 again closes it.
- Or the presenter panel: **Edit the opening**. This works from anywhere: it goes back to the title first.
- Or start the game straight in it: double-click **Edit the opening.cmd**. Every Windows build with Visiting Hours
  in it writes this file next to its exe. It runs the exe with the **-edit-opening** flag, which you can also pass
  yourself. It opens on the opening's first moment (the room, dark); click the beat **The title** for the table.
- During the opening, "F9: edit this scene" shows in the bottom-left corner for its first few seconds.
- While it's open, the opening holds still and its black fades (and the dark under the blanket) are lightened so you
  can see the set at dark moments. **Play from here** shows the real blacks while it plays.
  The title table doesn't take clicks or keys (a click on the tablet selects it, it doesn't start the game).
- When you close it, the opening plays on from where you were, or the title takes input again if you were at the end.
- If the presenter panel or Settings opens on top, the editor steps aside until it closes.

## The screen
- **Left: the list.**
  - **What you've changed (n):** every changed thing, each with its own **Reset**. Clicking a name selects it.
  - **Everything you can move**, by plain names, in groups: in the bedroom, shots (the cameras), at the title table,
    lights. Changed ones are marked `*`. The wheel scrolls the list. **Hide** folds the panel away (the
    **Things >** button at the top left brings it back).
- **Top: the timeline.**
  - **Play from here (Space)** plays the opening from where you are, through its own shot cameras; Space again pauses.
  - **< Beat / Beat >** (or the left and right arrow keys) step between beats.
  - The time and the current beat's name.
  - The **scrub bar:** click or drag along it for any moment; the white marks are the beats.
  - The **beat buttons** jump straight to a beat (the current one is lit): the room from her pillow, the phone on the
    nightstand, she reaches, the grab, holding it (the voicemail), she puts it down, falling asleep, the phone from
    above, G'night, the news report, the pieces arrive, the title.
  - Until you move it, the free camera sits on each moment's shot as you jump and scrub (the mode strip says "on the
    shot until you move it"), so every beat shows what the opening shows. Once you've moved it (fly, pan, orbit, the
    wheel, F, Look through it), it stays where you put it while you scrub.
  - A jump to the other set (the bedroom or the title table) always takes the free camera to that moment's shot, so
    there's something to see, and lets go of a selection that isn't in that set; the status line says both.
- **Right: the selected thing.** **Hide** folds it away; selecting something brings it back.
  - Its name, where it is (in the bedroom or at the title table), and a note on what follows it or what can't.
  - **Numbers:** the button switches between its parent's space (the room, the bed, the table: what's saved) and
    world space.
  - **Position (cm)**, **Turn (degrees)**, **Size (%)** (only for things that can be sized). Click a box and type:
    Enter applies, Tab goes to the next box, Esc cancels; while you type, the shortcuts are off. Or drag left and
    right on a box to scrub it (Shift is finer; snapping applies).
  - For a shot: **Field of view (degrees)**, **Look through it**, **Set from my view**.
  - **Frame it (F)**, **Reset this one**.
- **Bottom: the tools and what's going on.**
  - Move (W), Turn (E), Size (R), **Snap: on/off (X)**, Undo, Redo, **Camera: free/shot (C)**, Save,
    **Work light (L)**, **Reset everything...** (it asks first; Enter confirms, Esc cancels), **Rebuild with my
    edits**, Help (H), Close (F9). The active tool, snapping, the camera and the work light are lit.
  - The **mode strip:** playing or paused, free or shot camera (and which shot, or which shot you're looking
    through, or flying and how fast), the tool, snapping, the work light, panels hidden, and saved or
    `* UNSAVED CHANGES`.
  - The **status line:** what just happened (it flashes when it changes): "Selected...", "Moved...", "Saved 3
    changes to ...", "Undid: moved Tablet...", "Nothing to undo."
  - The **keys**, always shown.
- **Tips:** rest the mouse on a button or a number box and a tip says what it does, next to the mouse; on a handle,
  which way it moves; while flying, how fast.
- **Tab** hides every panel but the bottom bar (a clean view of the shot); Tab again brings them back.
- **L**, the work light: an even light over the whole set so you can place things at dark moments. It's only for
  editing (never saved, gone when you close); turn it off to judge the real lighting.
- **H** shows a help page with every control (click anywhere, H or Esc closes it).

## Moving things
- **Picking:** a click picks the surface under the mouse, as you see it: the walls, floor and window stay put, so a
  click on them selects nothing (the status line says so) rather than something behind them. Shots and lights draw
  as small marks (a camera box with a line where it films, a light with rays) you can click. A selected shot also
  shows the outline of what it films. When the selected thing is out of view, a tag says so: F frames it.
- **Handles** keep the same size on screen however far you are. Hovering one lights it yellow.
  - **Move (W):** the arrows use the world's directions; the middle square moves it across the view. Dragging an arrow
    follows the mouse along that line.
  - **Turn (E):** rings around the world's X, Y and Z.
  - **Size (R):** boxes along the thing's own directions; the middle box sizes it all at once.
  - **Snap (X):** 1 cm, 5°, 5%. Hold **Ctrl** while dragging to flip snapping for that drag.
  - **Esc** while dragging puts it back. A click on a handle without moving changes nothing.
- **The free camera:** right mouse held + W A S D / Q E (1.5 m/s to start; held over a second it speeds up to 3x
  over the next two); the wheel while flying sets the speed (shown in the mode strip); the wheel otherwise moves in and out; middle mouse pans; Alt + left mouse orbits; F frames. Any of these
  while on the shot camera switches to the free camera from the same view. **C** switches between the free camera and
  the opening's own shot camera.
- **Shots:** each shot's camera is one key.
  - To change it: select it, **Look through it**, fly to where it should be, set the field of view, then **Set from my
    view**. The free camera keeps the shot's roll while you look through it, so "Set from my view" keeps it too.
  - Changing one shot never changes another. The moves between shots follow the new keys.
  - The slow push-in from above and the drift over the table start from the shot.

## What follows what
- **The bed** carries Nadia, her blanket and the three shots from her eyes: on her pillow, looking at the nightstand,
  rolling away. Her rest pose and her arm's reach follow it.
- **Nadia** (a handle at her eyes) carries those same three shots. When you let go, her blanket reshapes over her.
- **The nightstand** carries the lamp, the phone, the photo and the shot from above.
- **The phone has two places, and the editor edits the one in use at that moment:**
  - **The phone (resting):** where it lies on the nightstand, before she picks it up and after she puts it down. Her
    hand follows it: she takes it by its bottom end, back to her palm, with no turn-over. Keep that end a few
    centimetres over the nightstand's edge so her fingers fit under it.
  - **The phone (held in her hand):** where she holds it up, from the lift to the drop (one place for all those
    beats). It's kept relative to her eyes, so it moves with the "looking at the nightstand" shot, the bed and Nadia.
    Her hand follows it. Her arm reaches only so far: past that the phone stops at her reach and the status line says
    so (the arrows stay where you put them).
  - A click on the phone picks whichever one is in use at that moment, and the panel and status line say which.
    Picking the other one in the list jumps to a beat where it's in use ("The phone on the nightstand" or "Holding it:
    the voicemail"). Scrubbing or pausing on a moment where the other one is in use switches to it, and the status line
    says so.
- **The table** carries the tablet, the mug, the notepad, the pen and the badge. The tablet doesn't turn by itself to
  face a moved title camera: turn it.
- **The streetlight's blind stripes** are worked out when the sets are built. Press **Rebuild with my edits** to see
  them follow a moved streetlight.
- **The lamp over the title table, and the lamps the lighting adds itself**, are placed by the lighting. Your moves are applied on
  top of that.
- These notes show in the right-hand panel when the thing is selected.

## Saving
**Ctrl+S**, or the Save button. Closing the editor also saves any unsaved edits.

**What's saved:** `VisitingHoursOpeningEdits.json`. It holds only what differs from the built scene, keyed by stable
ids such as `phone`, `phone-held`, `nightstand`, `bed`, `title-tablet`, `title-lamp` and `shot:table`. The format hasn't changed since the
first version of the editor, so files saved then still load.

**Where it's saved:**
- **In a build:**
  - next to the build's data: `<build>/VisitingHours_Data/StreamingAssets/VisitingHoursOpeningEdits.json`
  - **and** into the repository, when it finds one. It looks for a `VibeJamKit` folder beside one of the folders
    the build sits in. For a build in `VibeJamKit-play/Builds/Play/VisitingHours`, that's
    `VibeJamKit/Assets/JamKit/Templates/VisitingHours/Resources/JamKit/VisitingHoursOpeningEdits.json`.
- **In the Unity editor:** the same file in the project.
- The status line names every file it wrote.

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
    applying, capturing and saving them. `VhSceneEdits.FileOverride` points saving and loading at one other file
    (the tests and the capture tour; never the repository).
  - Things made of parts get one empty handle ("Edit: the lamp", "Edit: the bed"...) standing where they stand, so
    their parts keep their own placement.
- **The builder:** `VhStageBuilder.Build` applies the overrides around the lighting:
  - everything else **before** `VhLighting.Apply`, so its cookies trace the moved lights;
  - the lights it places itself **after** it.
- **The interface:** `Scripts/Opening/VhOpeningEditor.cs` (state, input, the camera, edits), `VhOpeningEditorUi.cs`
  (the panels), `VhOpeningEditorView.cs` (picking, outlines, handles), on the GameRoot. It's a UI Toolkit overlay in
  the title's layer.
  - **Input is read straight from the Input System** (Mouse.current, Keyboard.current, Keyboard.onTextInput for typing)
    every frame. The shell's UI input module only carries the left mouse button to UI Toolkit (no right or middle
    click actions), which is why the first version's free camera never moved: it waited for a right-button
    PointerDownEvent that never came.
  - The editor's controls are plain elements, hit-tested by the editor itself against their `worldBound`. One
    full-screen element under them takes the shell's own pointer events, so nothing reaches the title behind.
  - `VhTitleTable.Live` is false while the editor is open, and the opening's hold-Esc skip is off.
  - The editor turns the shell's pad mode off while it's open, so the mouse cursor shows.
  - The work light is `VhStage.workLight`, an ambient boost in `VhStage.ApplyAmbient` (the waking shader reads the
    stage's sky and ground colours, so it lights everything).
  - Right-mouse look locks the cursor and puts it back where it was when you let go.
- **The timeline:** `VhOpening.Seek` replays every cue up to a time, at each cue's end state. `VhOpening.PlayFromHere`
  restarts the voices mid-way.
- **Adding a thing:** register it in `VhSceneEdits.Register` with a new, stable id. Never rename an existing id: saved
  edits use it.
- **Tests:**
  - EditMode `VisitingHoursOpeningEditsTests`: round trip, apply at build, stable ids, unknown ids, carrying.
  - PlayMode `VisitingHoursOpeningEditorInputTests`: the editor worked through a test keyboard and mouse fed to the
    Input System (`VhVirtualInput`), never by calling its methods: F9 open and close, nothing leaking to the title,
    the pad not hiding the cursor, right mouse + W flying at the fly speed while the opening holds still, the mouse
    turning the view, middle-mouse pan, the wheel, F, clicks in the view and on the panels, the X arrow moving only X
    in whole centimetres, Esc cancelling a drag, snapping off, Ctrl+Z / Ctrl+Y, typing a number (and E not switching
    tools meanwhile), the beat buttons, the arrow keys and the scrub bar, "Set from my view" changing only that shot,
    Ctrl+S writing the file and the sets building with it, closing mid-opening playing on.
  - PlayMode `VisitingHoursOpeningEditorPlayTests`: move the phone and the grab is still back-to-palm; the launch
    flag; the presenter entry.
- **Checking it (the capture tour):** `tools/vh-editor-capture.ps1` runs a Windows build with
  `-edit-opening -opening-editor-tour <folder>`. `VhOpeningEditorTour` works the editor with a virtual mouse and
  keyboard (the real ones are switched off for that minute), saves a screenshot after each step and `captions.txt`
  (each step, the status line, CHECK ok / FAIL lines), undoes everything and quits without saving. Output:
  `out/opening-editor-ux/<time>/`.
