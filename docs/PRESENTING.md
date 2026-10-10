# Presenting the game live

At the hackathon three of us present the game to a judging panel. Nobody else plays it: what the
judges see is what we show them in about four minutes. They score five things: **Originality &
Creativity, Technical Implementation, Art & Design, Playability & Fun Factor, Conformity to Theme**.

JamKit helps in three ways:

- **Presenter mode** (in every build) jumps straight to the best moments, makes the player
  invincible, slows or freezes the action, hides the HUD and takes screenshots.
- **Attract mode** makes the game play its best moments by itself, so we can record b-roll footage.
- **Footage tools** record clips and trailer frames in the editor, and the OBS settings below record a build.

The research behind this page (sources, evidence, the full risk kit) is in
`docs/JAM-ASSETS-AND-PRESENTATION.md`, sections 5 and 6.

---

## 1. Presenter mode in 30 seconds

| | |
|---|---|
| **Open or hide the panel** | **Ctrl+Shift+P**. On a gamepad, **hold both stick clicks (L3+R3) for 1 second**. |
| Hide the panel | Escape, the same chord again, or "Hide panel". The settings stay on. |
| **Undo everything** | "Exit presenter mode". Invincibility, slow motion, freeze and hidden UI all go back to normal. |

Things to know:

- The chord works in **release builds** too. Nothing shows on screen until someone opens the panel.
- Game time **stops while the panel is open**, so nothing happens behind it.
- Nothing is saved. The settings last until the game is closed or someone presses Exit.

We didn't use Select+Start for the gamepad chord. Start pauses the game, and Select opens the journal
in Visiting Hours. Nothing uses the stick clicks.

### The panel

| Button | What it does |
|---|---|
| **Chapters** (left column) | Jumps to a named moment in this game: a floor, a boss, a knot, an ending. Jumping closes the panel. The game ends up clean: no menu, fade, flash, shake, hit stop or message left over, unpaused, with the right music. From the title, a game-over screen or the credits, it starts a fresh run in the game's scene and then jumps. |
| Next chapter | The chapter after the last one you jumped to. This is the "something went wrong" button. |
| Invincible | The player takes no damage and can't die. Nothing on screen shows it unless Corner indicator is on. |
| Slow motion | x1, then x0.5, then x0.25. It stacks with hit stop and pause: a paused game stays paused, and slow motion comes back when you resume. |
| Freeze frame | Stops game time and gameplay input. Menus and dialogue still work. Good for "look at this" moments and for photos. |
| Hide HUD / UI | Hides the HUD, the dialogue box, floating numbers and the debug overlay. The picture stays: the story template's stage and Visiting Hours' argument box. Menus still open if you pause. |
| Corner indicator | A small status line in the corner (`PRESENTER invincible x0.5`). Off by default. |
| Photo (2x PNG) | Hides all UI, saves a 2x supersampled PNG to `Pictures/JamKit/<game>_<timestamp>.png`, then flashes like a camera shutter. |
| Attract mode | The game plays itself (section 4). Escape, the chord or Exit stops it. |
| Hide panel / Exit | Hide keeps the settings on. Exit undoes everything. |

---

## 2. A demo route for each template

Each route lists its chapters in panel order. Rehearse the route twice with a timer before the slot.

### Bullet hell (`BulletHell`)

Chapters: **Floor 1, Upgrade draft, Encounter room (Forge), Floor 1 boss, Floor 2, Floor 2 boss,
Floor 3, Floor 3 boss.**

Route:
1. **Floor 1**, invincible on: weave through a wave and show grazing and the sprint.
2. **Upgrade draft**: pick a card and say why it changes the run.
3. **Encounter room (Forge)**: the gamble.
4. **Floor 2 boss**: phase changes and the boss's lines. Use slow motion x0.5 on a dense pattern.

Jumping to a floor heals the player and shows the floor card, colours and music, as if walking in.

### DOOM-like (`Doomlike`)

Chapters: **Level 1, Level 2, Level 3, The boss, End tally.**

Route:
1. **Level 1**: movement, a door, a fight.
2. **The boss**: you start with every weapon, a few metres away, facing it, and it's awake.
3. **End tally**: the last level's tally screen, with its enemies beaten.

### Story (`Story`)

Chapters: **Prologue, The platform, The waiting room, Lost property office (hotspots), The train
arrives**, and the four endings: **Last Train, Missed Connection, Lost Property, Return Ticket**.

Each chapter starts its knot on a clean stage: no characters, text, hotspots or ending card left over.
It also brings its own music and ambience.

Route: **The platform** (the characters, a choice), then the **office** (point and click), then one
ending.

Jumping to an ending records it in the game's cross-playthrough memory, as playing it would. Use
"Story: reset memory" in the debug menu after rehearsals.

The chapter list is data: `StoryPresenter.List` in `Templates/Story/Scripts/StoryPresenter.cs`. A
test checks that every knot in it exists.

### Visiting Hours (`VisitingHours`, the active template)

Chapters: **Opening, Day 1, The booth, The street, The argument with Walt**, and **End card:
Convinced / Heard out / Pressed / Composure lost**.

How some of them start:
- **Opening** works from anywhere. It goes back to the title and plays the opening there.
- The others start with a fresh journal, holding the entries the player would have by then.
- **The argument** starts in front of Walt with everything found and worked out.
- Each **end card** shows on the dimmed street.

Route: the **Opening** (hold Escape to skip it), then **Day 1** (chart one thing), then **The street**,
then **The argument** (invincible on, if you want no risk), then the end card it reaches.

### Movement Lab (`MovementLab`)

The lab opens on **the Gauntlet**: a timed obstacle course with touch markers, splits and a ghost of your
best run.

Chapters, in order:
- **Gauntlet start** (on the start pad, from a standstill: a fresh run).
- **Gauntlet: teaching half** (the wallrun).
- **Gauntlet: combo half**.
- **Gauntlet: finish** (the last wall).
- The old course, kept as free roam: **Course start, Wallrun section, Momentum chain, Chimney, Chase.**

Every chapter is a checkpoint, and you arrive running at sprint, except at the two starts. A Gauntlet
chapter jump keeps the clock running, so the markers you skip cost 2 s each. Jump to **Gauntlet start**
for a clean time.

Route:
1. **Gauntlet start**, and run it. R or View restarts at once.
2. Then **Momentum chain** (watch the speed graph, F3).

Attract mode runs the Gauntlet with the autopilot, then the chain. Details: `docs/MOVEMENT.md`,
"The Gauntlet".

### Visiting Hours: the first escape (`DreamEscape`)

Standalone until the story wires it in: **JamKit > Use Template > Dream Escape**.
Chapters: **Escape start, Rooftops, Final run.** Each is a checkpoint of the dream street. Escape start is
Walt's driveway from a standstill: the static wakes when you run (or after 2.5 s). The other two arrive
running at sprint with the static 12 m behind.

Route: **Escape start** and play it through (about a minute: fences, a house-front wallrun, car roofs, the
garden wall, the garage slide, the porch climb, the roofs); if time is short, **Rooftops**, then **Final
run** (roof slide, the last wall, the jump into the light). Turn on **Invincible** to show it with no risk
of a catch. Details: `docs/DREAM-ESCAPE.md`.

### Sandbox

Chapters: **Start, The sign.** It's the worked example for adding chapters to your own game
(section 6).

---

## 3. When something goes wrong on stage

1. **Don't explain or apologise at length.** Say "let's jump ahead", open the panel and press
   **Next chapter**, or the chapter you want.
2. **Stuck in a menu, a fade or a dead end:** any chapter jump closes menus and clears fades.
3. **Losing in front of the judges:** turn on **Invincible** before the slot. Nothing on screen gives
   it away.
4. **Need to talk over a busy moment:** use **Freeze frame**, or slow motion x0.5.
5. **The build crashes or the laptop fails:** switch to the **backup video** (60-90 s, starting on
   gameplay in the first second). Keep it on the laptop desktop and on a USB stick, and record it on
   day 2 with attract mode (section 4).
6. Before the slot, test HDMI at 1080p and the audio output. Set the game volume so the music doesn't
   drown out the speaker. Present from the **Windows build**, not the editor.

---

## 4. The 4-minute pitch, mapped to the criteria

Three roles, and every member speaks at least once:

- **Director**: owns the idea and the theme. Opens and closes the pitch, and narrates what the
  player *feels* during the demo, not the controls.
- **Builder**: owns the "how". Presents the technical part and is the backup operator: keeps the
  backup video ready and knows the chord.
- **Player / Producer**: plays the demo through presenter mode chapters, keeps time, and shows the
  credits slide.

| Time | What happens | Who | Criterion it feeds |
|---|---|---|---|
| 0:00-0:20 | Title and a **one-sentence pitch**. Say how the game reads the theme, in plain words. No logos or intro. | Director | Conformity to Theme, Originality & Creativity |
| 0:20-2:20 | **Live demo** of the best two minutes, starting mid-action from a chapter. One person plays and one narrates the feeling. Use invincible, and slow motion on the big moment. | Player plays, Director narrates | Playability & Fun Factor, Art & Design |
| 2:20-3:10 | **Under the hood**: one or two things the judges can't see (procedural rooms, the bullet patterns, the dialogue system, the journal's deductions). Freeze the frame, or show it side by side. | Builder | Technical Implementation |
| 3:10-3:40 | **The twist, ending or strongest moment** (jump to it), then the **credits slide** naming every asset and AI tool. | Player / Producer, then Director | Originality & Creativity, honesty |
| 3:40-4:00 | What we'd build next, and thanks. Everyone speaks once. | All three | (presentation) |

Common mistakes: explaining controls instead of playing, a long intro, demoing half-finished
features, talking about features that don't exist, no theme sentence, mixed art styles, running over
time, and one person doing all the talking. If you can choose the slot, ask for a late one (the
order effect is in the research doc).

---

## 5. B-roll and screenshots

### Attract mode (the game plays itself)

Two ways to start it:
- In a build: run `JamKit.exe -jamAttract`. It starts after the title is up and loops until Escape.
- From the panel: press **Attract mode**.

Each template has an attract script: a list of chapter jumps and driver segments, about 70-85 s a
loop. They run at **normal speed** with natural-looking input: a synthetic gamepad whose sticks move
at a limited rate, like a hand. The player is invincible while it runs.

| Template | What it shows |
|---|---|
| Bullet hell | Floor 1: the ship weaves through bullets with fire held, and panels are picked after a look. Then a draft, the Forge, the Floor 2 boss and Floor 3. |
| DOOM-like | Walks Levels 1 and 2 towards the exit along the NavMesh, turning like a stick and shooting what it sees. Then the boss fight and the tally. |
| Story | The prologue, the platform and an ending, read at the auto-mode pace (choices after a pause). The office is clicked through by its walkthrough, pointing at each thing first. |
| Visiting Hours | The opening over the title. Nadia walks up to the Day 1 things, turns to them and charts them. Then the street at night, the argument played by its autopilot at real speed, and the Convinced end card. |
| Dream escape | The course autopilot runs the whole escape from Walt's driveway to the light (about 50 s), then the final run again. |

The scripts live in `<Template>Presenter.cs` (`Attract()`). Tests check that every jump in a script
names a real chapter and every drive names a segment the game plays.

### Recording in the editor (Unity Recorder 5.1.7, editor only)

Use the menu **JamKit > Record**. Everything goes to `Recordings/`, which git ignores.

| Preset | Output |
|---|---|
| Gameplay clip 1080p60 MP4 | Enters Play mode if needed and records the Game view until **Stop recording**. Variable frame rate, so a human playing feels real time. |
| Trailer frames 1440p PNG sequence | Lossless 2560x1440 frames at a constant 60 fps. Smooth even if the editor is slow. |
| Attract mode clip (60 s MP4) | Starts attract mode and records 60 s at a constant 60 fps. |

**Headless b-roll** (the main conversation runs it):

```powershell
uv run python ~/.local/bin/capped-run.py --session VibeJamKit -- powershell -File tools/record-attract.ps1 -Seconds 60
```

This opens the editor with graphics, enters Play mode, runs attract mode and records an MP4. The
Recorder **can't capture under `-batchmode` or `-nographics`**, because there's no graphics pipeline.
So this script is not `Invoke-UnityBatch`. It needs a desktop session and the GPU, so run it as the
only GPU job. It prints `JAMKIT_RECORD_OK <file>` and logs to `out/record-attract.log`.

### Screenshots

Press **Photo** in the panel. It hides the UI, takes a 2x PNG and saves it to `Pictures/JamKit`.
For a freeze-frame screenshot, turn on Freeze frame first, close the panel, then open it and press
Photo.

### Recording a build

Use OBS: see `tools/obs-notes.md`.

### Assembling with ffmpeg

```bash
# PNG frames -> 1080p60 MP4 (judge-safe yuv420p)
ffmpeg -framerate 60 -i frame_%04d.png -vf scale=1920:1080 -c:v libx264 -crf 18 -preset slow -pix_fmt yuv420p shot.mp4
# Trim without re-encoding
ffmpeg -ss 00:00:12 -to 00:00:20 -i take.mp4 -c copy clip.mp4
# Join clips (same codec and size); list.txt holds lines like: file 'clip1.mp4'
ffmpeg -f concat -safe 0 -i list.txt -c copy backup_silent.mp4
```

Keep the backup video at 60-90 s, with gameplay in the first second.

---

## 6. Adding chapters and attract mode to a jam game

The bullet hell's `BulletHellPresenter.cs` and the sandbox's `SandboxGame.cs` are the examples.

```csharp
// 1. Names, declared at load so the panel lists them on the title screen.
public static readonly string[] Chapters = { "Level 1", "The boss" };
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
static void Declare()
{
    PresenterMode.Declare("MyScene", Chapters);
    PresenterMode.SetAttractScript(new AttractScript("MyScene").Jump("Level 1", 2f).Drive("play", 20f).Jump("The boss", 1f).Wait(8f));
}

// 2. Live jumps and the attract driver while the scene runs.
void Start()
{
    PresenterMode.Register("MyScene", "Level 1", () => LoadLevel(0), this);
    PresenterMode.Register("MyScene", "The boss", JumpToBoss, this);
    PresenterMode.RegisterDriver(this, "MyScene", AttractDrive); // IEnumerator (segment, seconds, SyntheticInput)
}
void OnDestroy() => PresenterMode.Unregister(this);
```

Rules:
- A jump must leave the game clean: stop its dialogue and coroutines, close its panels, and set its
  music. The shell already closes menus and clears the fade, flash, shake, hit stop and HUD message.
- A driver plays through the `SyntheticInput` it gets: `move` and `look` sticks, `Hold(action)` and
  `Tap(action)`. Use `NaturalStick.Step` / `Turn` so it moves like a hand, at real time. No speed-ups
  and no instant dialogue: the shell's attract loop already reads dialogue at the auto-mode pace.
- Read input through `GameRoot.Input` (`MoveVector`, `LookDelta`, `Pressed`, `Held`,
  `LookFromGamepad`), not `action.IsPressed()`, so attract mode can drive it.
- Check player damage against `DebugMenu.PlayerInvincible`, which covers god mode, Invincible and
  attract mode.
- To keep a UI-drawn picture visible when the UI is hidden, add the class `UiHider.KeepClass` to it.

---

## 7. Known limits

- Presenter mode is quiet in the console: it logs its jumps, photos and attract steps only with
  `-jamPresenterLog` (or in a `-jamSmoke` run, or while JamKit > Record films attract mode), so a game's
  own tests aren't tripped by unexpected log lines. Warnings and errors always log.

- Hide UI restores what it hid exactly, except in one case: game code that hides one of those
  elements *while* the UI is hidden gets it shown again on restore. The next update usually fixes it.
- The photo's 2x supersampling uses `ScreenCapture`. Under `-nographics` (batch tests) it writes a
  blank PNG of the right size instead. WebGL can't save photos.
- Attract mode's DOOM-like walk follows the NavMesh to the exit. If it gets stuck the segment's time
  just runs out. Visiting Hours cuts to the spot after a few stuck seconds (like an editor's cut).
- Unity Recorder works only in the editor. Record builds with OBS.
