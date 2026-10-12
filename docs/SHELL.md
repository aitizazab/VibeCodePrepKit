# The JamKit shell: how to build a jam game on it

Every JamKit game sits inside the same shell: title screen, settings, pause, game over, victory,
credits, How to Play cards, a HUD, ink dialogue, sound, game feel and a debug menu. You make the
game; the shell makes it complete. Press Play in **any** scene and the shell is there (it creates
itself before the scene loads).

## Start a new game (first 10 minutes)

1. Open **`Assets/JamKit/Resources/JamKit/GameConfig.asset`**. It's the one asset to edit first:
   - **Game Title** and **Tagline** (title screen), **First Gameplay Scene** (what Play loads).
   - **Title Music / Game Music**: sound ids (see "Add a sound").
   - **Credits**: a text file, one line per credit (`Assets/JamKit/Credits.txt`). Credit every
     sample and CC0 sound there.
   - **How To Play** pages (title, text, optional picture) and **Show How To Play On First Run**.
   - **Choose Difficulty On Play** (on): Play opens a Story / Normal / Hard picker before the run
     (each with its one-line description; Normal the first time, then the last choice; the same value
     as Settings' Difficulty, so the assists apply). Back returns to the title. It's only asked when
     **Show Action Settings** is on, so the story template never shows it. Off: Play starts at once.
   - **Show Control Legend** (on): templates with a small HUD control legend (the bullet hell) show it.
   - **Allow Debug Menu In Release**: leave off for the judged build.
2. Make your game scene (next section) and set **First Gameplay Scene** to its name.
3. Re-skin: edit the colours and sizes in **`Assets/JamKit/UI/Theme.uss`** (one block of
   variables). Layout lives in `Shell.uss`; each screen is a UXML file in `Assets/JamKit/UI/Screens`.

## Add a scene

1. Create the scene (Assets/JamKit/Scenes or your own folder). Don't add an EventSystem or an
   AudioListener: the shell has both (an extra camera AudioListener is fine, the shell steps aside).
2. Add it to Build Settings (File > Build Profiles > Scene List), after Boot and Title.
3. In a script, use the shell:

```csharp
using JamKit.Shell;

void Start() {
    GameRoot.Hud.Show();                        // HUD on for this scene
    GameRoot.Hud.SetHealth(3, 3);
    GameRoot.Hud.SetScoreText("Coins 0 / 3");
}
void Update() {
    if (!GameRoot.Input.GameplayAllowed) return; // paused, in dialogue, loading...
    Vector2 move = GameRoot.Input.MoveVector;    // WASD / arrows / left stick
    if (GameRoot.Input.Pressed(GameRoot.Input.Interact)) { /* E / X */ }
}
// When the player wins or loses:
GameRoot.Win();   // Victory screen (time, score, best time) -> Credits
GameRoot.Lose();  // Game Over screen (Try Again / Back to Title)
```

- `GameRoot.Run.score`, and `GameRoot.Run.Set("Coins", "2 / 3")` adds a line to the end screens.
- A run starts fresh on Play, Try Again and Restart: those reload the run's first scene, so set
  your level up in `Start()`. Go to the next level with `GameRoot.Instance.SceneService.Load("Level2")`.
- `Time.timeScale` belongs to the shell (0 when paused, in dialogue and on end screens). Use
  `Time.deltaTime` for gameplay and `Time.unscaledDeltaTime` for UI-like things.
- The sandbox (`Assets/JamKit/Sandbox/Scripts/SandboxGame.cs`) is the worked example. Delete the
  `Assets/JamKit/Sandbox` folder when you don't need it, and set First Gameplay Scene to yours.

## Hits and game feel

- **`Damageable`** component: health with i-frames (core `Health`), plus sprite flash, hit stop,
  screen shake, a sound and a floating damage number. Tick **Is Player** on the player (uses the
  difficulty's damage scale and the debug god mode). Call `GetComponent<Damageable>().Hit(1)`;
  listen to `Damaged` / `Died`.
- **`SpriteFlash`** on a sprite with `Assets/JamKit/Art/SpriteFlash.mat`: `Flash(Color.white)`.
- `GameRoot.Juice.HitStop(0.08f)`, `.Shake(0.4f)` (0..1, respects the Screen shake setting),
  `.Flash(color, seconds)` (full screen), `.FloatingText(worldPos, "+100", color)`.
- Difficulty: read the assists in `GameRoot.Settings` (`enemyBulletSpeedScale`,
  `enemyDensityScale`, `damageTakenScale`, `extraLives`, `aimAssist`), never the enum.

## Add a dialogue

1. Write a `.ink` file (Inky, or any text editor) in `Assets/JamKit/Dialogue`. It compiles on
   import; errors show in the Console. `Sandbox.ink` shows everything.
2. Play it: `GameRoot.Dialogue.Play(inkFile, "knot_name");` (field type `Ink.UnityIntegration.InkFile`).
   Game time stops while it runs; it returns to Playing at the end.
3. Tags:

| Tag | Does |
|---|---|
| `#speaker:Ava` | name in the box (stays until changed; `#speaker:` clears it) |
| `#portrait:Ava happy left` | show a character; expression and side optional; `#portrait:Ava hide` |
| `#sfx:coin` / `#music:music_game` / `#music:stop` | sounds by id |
| `#shake:0.4` / `#flash` / `#flash:0.3` | screen shake (0..1) / white flash (seconds) |
| `#wait:0.5` | pause before the line types |
| `#auto:2` | this line goes on by itself 2 s after it has typed |
| `#thought` | this line is a thought (the story template draws it in italics) |
| `#event:door_opens` | `GameRoot.Dialogue.EventRaised += name => ...` |

   Inside a line: `<b>`, `<i>`, `<color=red>` style the text, `<pause=0.4>` stops the typing for a
   moment and `<speed=0.5>...</speed>` types a part slower (2 = faster). The player's **Text speed**
   setting (up to Instant) applies to every dialogue.

4. Functions (declare with `EXTERNAL has_flag(name)` etc.): `has_flag(name)`, `set_flag(name)`,
   `clear_flag(name)`. Flags are kept in the save file, so they survive retries and restarts.
   Also `run_count()` (runs started on this save: 1 on the very first, a persistent playthrough
   counter any template can use for "back again?" lines), `runs_won()` and `best_floor()`.
   The story template adds its own memory functions (`playthrough_count()` and more).
   To show a line somewhere else than the dialogue box, `GameRoot.Dialogue.TryReadLine(json, knot,
   out speaker, out text)` reads a knot's first line without playing it, and
   `GameRoot.Run.SetQuote(speaker, text)` puts a line on the Game Over / Victory screen (the bullet
   hell's death line works this way). `StoryHooks` (core) builds "most specific first" knot names
   with silent fallbacks (docs/TEMPLATE-BULLETHELL.md, "Story hooks").
5. Events: `GameRoot.Dialogue.Ended += knot => ...` fires when a story finishes (with the knot it
   started at); `TagReceived` gets every tag, including your own; `StoryCreated` hands you each new
   ink `Story` (bind your own EXTERNAL functions there); `LineShown` and `ChoiceMade` follow the text.
   A template can draw the dialogue its own way (`UseView`) and steer it (`Presenter`): that's how
   the story template adds its stage, saves and point-and-click rooms (docs/TEMPLATE-STORY.md).
6. Portraits: duplicate `Assets/JamKit/Dialogue/Portraits/Guide.asset`, set **Character Id**, a
   **Base Sprite** and named **Expressions** (same size, transparent around the face), and add it
   to GameConfig's **Portraits** list.

Players advance with Enter / Space / click / (A) (first press finishes the line), and choose with
arrows + Enter, the mouse or the pad.

## Add a sound

- `GameRoot.Audio.Play("coin")` for effects (pooled, slight random pitch);
  `GameRoot.Audio.PlayMusic("music_game")` crossfades music; `GameRoot.Audio.PlayAmbience("amb_rain")`
  fades in a looping background sound (at the effects volume; `StopAmbience()`).
- In batch mode (Unity test runs, the `-jamSmoke` smoke run) every shell sound is muted
  (`GameRoot.Audio.Muted`), so tests stay quiet; normal play is unchanged.
- Ids live in **`Assets/JamKit/Audio/AudioRegistry.asset`**: add an entry (id, clip, volume,
  pitch variance, loop). Until an entry has a clip, these ids play a generated placeholder:
  `ui_move ui_select ui_back blip coin hurt win lose shoot dash explosion music_title music_game`.
  Giving one of them a clip replaces the placeholder everywhere.

## Add a How to Play page

GameConfig > **How To Play** > +: a title, a short text and (optional) a picture. Keep it to 2-4
cards: the blind tester reads them once.

## Controls the shell uses

| | Keyboard / mouse | Gamepad |
|---|---|---|
| Move | WASD, arrows | left stick (the d-pad only where a 2D template opts in: the bullet hell, Visiting Hours' argument battle) |
| Look | mouse | right stick |
| Fire / Alt fire | left / right mouse, J / K | RT / LT |
| Sprint (the bullet hell's sprint, the DOOM-like's walk) | Left Shift | LB (not in Visiting Hours: L3, below) |
| Focus (no default keys; the bullet hell's optional Focus mode) | - | - |
| Dash (also the first-person movement's jump) | Space | A, RB (not in Visiting Hours) |
| Crouch / slide, hold (first-person movement) | Left Ctrl, C | B |
| Crouch / slide, toggle | - | R3 |
| Sprint, toggle (first-person movement) | - | L3 |
| Peek left / right, hold (only where a template turns peeking on: Visiting Hours) | Q / F | LB / RB |
| Interact | E | X |
| Pause | Escape, P | Start |
| Restart run (the Movement Lab's Gauntlet) | R | View / Share |
| Show ghost (the Gauntlet) | G | d-pad left |
| Menus | arrows, Enter, Escape, mouse | stick/d-pad, A, B (the d-pad is Navigate's, everywhere) |
| Presenter mode (hidden) | Ctrl+Shift+P | hold L3+R3 for 1 s |

The default bindings are in `Assets/JamKit/Input/JamKitControls.inputactions` (edit freely); the
player changes them in **Settings > Controls** (below). Mouse sensitivity and invert Y are applied by
`GameRoot.Input.LookDelta(GameRoot.Settings)`.

**The cursor follows the device in use**, in every scene and menu, with no per-scene code: the moment a
gamepad is used (a button, the d-pad, a stick or trigger past half way), the mouse cursor hides; it comes back
when the mouse really moves (more than 2 px), clicks or scrolls. `GameRoot.Input.PadMode` says which one is in
use (`InputService`), and `PointerCursor` (added by GameRoot, runs after every scene script) applies it.
- A cursor a scene **locked** for mouse-look is left alone, never unlocked; the scene shows it again itself when
  it unlocks, and then the shell hides it at once if the pad is in use (a pause opened with Start).
- In pad mode the shell's UI root has the class `jk-pad`, which turns off every `:hover` look (the end of
  `Shell.uss`, which also lists the templates' hovered classes), so the spot under the hidden cursor doesn't look
  like a second focus. A new `:hover` rule needs its pad-mode line there.
- Code that moves focus on hover checks `GameRoot.Input.HoverAllowed` first (the argument battle and its menu do),
  so a list moving under the hidden cursor can't take the focus.
- Keyboard use doesn't change it. Tests: `CursorPlayTests` (PlayMode).

### Settings > Controls (rebinding)

Every template's Settings has a **Controls** button (shown with the action settings, so not in the
story template). It opens a table: a row per gameplay action (Move, Look, Jump / Dash, Crouch / Slide
hold and toggle, Sprint hold and toggle, Interact, Fire, Alt Fire, Pause; **Peek left / right (hold)**, shown where
a template turns peeking on; and **Restart run** and **Show ghost**, which only the Movement Lab's Gauntlet shows), a **Keyboard & Mouse** column and a **Controller**
column, and a Reset per row.

- **Rebind:** select a cell, then press the new key, mouse button or pad button. **Esc** cancels (and
  **Start**, or **B / Circle** for a keyboard cell, on a pad). The keyboard column takes keys and mouse
  buttons, the pad column pad buttons; mouse movement and stick directions are never taken for a button
  (Move and Look's pad cells take a whole stick or the d-pad, so you can swap sticks; Move's cell shows
  just the left stick by default). Move's keyboard
  cell asks for Up, Down, Left, Right in turn. Look's mouse cell can't change. Start can't be bound
  (it cancels; Reset brings it back on Pause).
- **Conflicts:** a key another action already uses asks **Swap** (that action gets your old key),
  **Keep both**, or **Undo**. A control inside another one counts too (a button on d-pad left while Move
  has the whole d-pad): that asks only **Keep both** or **Undo**, since a button can't swap with a
  stick-like binding. Shared keys you kept show **(!)**. Hidden rows don't count (R is free in a template
  without the Gauntlet). The defaults share nothing.
- **Contexts:** rows only clash with rows used at the same time. Walking-around rows are "play"; the argument
  battle's verbs ("Argue: Ask / Challenge / Concede / Press (hold) / Let it rest": 1-5; X, RB, Y, hold B, View),
  shown while a battle runs, are "argument"; Pause is "any" and clashes with both. So RB being both Dash and
  Challenge, or B both Crouch and Press, isn't flagged (docs/ARGUMENT-PROTOTYPE.md).
- **`GameRoot.Input.BlockWorld(owner, true)`** stops world gameplay (`GameplayAllowed` is false) while something
  modal runs in the world, such as an argument battle; a destroyed owner stops counting.
- **Reset** on a row puts that action back; **Reset all** puts everything back (stick clicks too).
- **Stick clicks:** "Crouch / Slide on R3" and "Sprint on L3" switch between **Toggle** (the default:
  R3 / L3 belong to the toggle actions) and **Hold** (they move to the hold actions: crouch while R3 is
  pressed in). This moves the default binding, so the player's own rebinds stay.
- Pad buttons are named for the connected pad: A / B / X / Y, LB, RT, Menu on Xbox and generic pads;
  Cross / Circle / Square / Triangle, L1, R2, Options on a DualShock or DualSense. Each cell shows the
  binding it rebinds, then the others in grey ("Left Ctrl  also C").
- Fully navigable: arrows / d-pad move around the table (left and right across a row, left and right
  on a stick-click line change it), Enter / A selects; focus starts on Move's keyboard cell.
- **Saved at once** in the save file (`save.json` in persistentDataPath; PlayerPrefs on WebGL), entry
  `jk_controls`: the Input System's `SaveBindingOverridesAsJson` plus the two Hold / Toggle choices.
  `GameRoot` loads it right after creating the input (before the first scene, in builds too) and again
  whenever the save is reloaded. Binding ids are fixed (the asset's own, and "map/action/index" ids for
  the actions added at runtime and the code defaults), so saved rebinds survive restarts and rebuilds.
- **Peeking on the bumpers** (a template's choice, like the d-pad on Move): `Bindings.SetPeekOnBumpers(true)` binds
  **Peek left / right (hold)** (Q / LB, F / RB), shows their rows, and takes LB off Sprint (hold) and RB off Jump, so a
  pad sprints with L3 (and the keyboard with Shift) and jumps with A. It moves default paths only (the player's rebinds
  stay) and isn't saved. Visiting Hours turns it on at boot (`VhBoot`); the Movement Lab turns it off while it runs
  (its LB + d-pad switches and pad tests keep LB as sprint); every other template leaves it off. The movement kit
  reads the peek actions (`MovementController`; docs/MOVEMENT.md "Peeking"). Test: `ControlsEditorTests.PeekingOnTheBumpersTakesThemOffSprintAndJump`.
- Code: `GameRoot.Input.Bindings` (`ControlBindings`: rows, `SetPath`, `FindConflict`, `ResetRow`,
  `ResetAll`, `SetStickHolds`, `SaveTo` / `LoadFrom`), `GameRoot.Ui.Controls` (`ControlsScreen`).
  A template without shooting can hide rows: `GameRoot.Ui.Controls.SetRowShown("fire", false)`; Restart run
  and Show ghost start hidden and `GauntletRun` shows them while it exists. To name the current keys in a
  hint, use `GameRoot.Input.Bindings.KeysText("restart", ControlNames.DetectPadStyle())` ("R or View"),
  rebuilt when `Bindings.Version` changes.

**The d-pad is not on gameplay Move** (since 2026-10-10): first-person games steer with the left stick, and
the d-pad is free for actions (the Gauntlet's ghost on d-pad left, the Movement Lab's HUD on d-pad down).
It stays on UI Navigate, so menus are unchanged. The asset, the code defaults and older assets (at load) all
lose it. A 2D template that steers with it opts in with `GameRoot.Input.Bindings.SetMoveDpad(true)` and
turns it off again when it ends: the bullet hell for its whole scene, Visiting Hours only during the
argument battle. A saved rebind of the removed binding is dropped quietly at load.

## Debug keys

**` (backquote) or F1** opens the debug menu (editor and development builds, or when GameConfig
allows it): god mode, time x0.25 / x1 / x2, Win, Lose, Next scene, FPS counter.
From code, in any build: `DebugMenu.Win()`, `DebugMenu.Lose()`, `DebugMenu.NextScene()`,
`DebugMenu.GodMode`, `DebugMenu.TimeScale`.

Player damage code checks `DebugMenu.PlayerInvincible` (god mode, presenter Invincible or attract
mode), not `GodMode` alone.

## Presenter mode (for presenting live)

**Ctrl+Shift+P**, or holding both stick clicks (L3+R3) for 1 s, opens the presenter panel in any
build. It's hidden until opened. It has chapter select, Invincible, Slow motion x1/x0.5/x0.25, Freeze
frame, Hide HUD/UI, Photo (a 2x PNG to Pictures/JamKit), Attract mode, and Exit (which undoes
everything). `-jamAttract` on the command line starts attract mode after the title. A game adds
chapters with `PresenterMode.Declare`/`Register` and an attract script and driver. Read input through
`GameRoot.Input` (`MoveVector`, `LookDelta`, `Pressed`, `Held`, `LookFromGamepad`) so attract mode
can drive it. The whole guide, the demo routes and the 4-minute pitch are in `docs/PRESENTING.md`.
Footage: menu **JamKit > Record** (Unity Recorder, editor only), `tools/record-attract.ps1`, and
`tools/obs-notes.md` for builds.

A game whose "next" isn't the next scene sets `DebugMenu.SkipHandler` (the bullet hell skips a room
or kills the boss), and can drive a few seconds of itself in the smoke run through
`SmokeRunner.GameplayDriver`. Set both in `Start`, clear them in `OnDestroy`.

## Templates

`JamKit > Use Template > Bullet Hell` (or `> DOOM-like`, `> Story`, `> Visiting Hours`, `> Movement Lab`, `> Dream Escape`, `> Sandbox`) points Play, the smoke
test and Build Settings at a template: it copies the template's preset (title, tagline, first scene,
music, How to Play cards, which Settings options show: Pixel look, the text options, the action
options, best time) into the active GameConfig. The bullet hell is documented in
`docs/TEMPLATE-BULLETHELL.md`, the DOOM-like in `docs/TEMPLATE-DOOMLIKE.md`, the story (visual novel,
point-and-click) in `docs/TEMPLATE-STORY.md`, the Visiting Hours slice (first person, a journal,
argument battles) in `docs/SLICE-VISITING-HOURS.md` and `docs/SLICE-NOTES.md`, the Movement Lab (Titanfall-style
movement for any first-person scene, with a feel-test course and a chase) in `docs/MOVEMENT.md`, and Visiting
Hours' first escape (the dream street chase, standalone until the story wires it in) in `docs/DREAM-ESCAPE.md`.

Templates can add to the shell's screens from code: `GameRoot.Ui.Title.AddButton(name, text, action,
beforeName)` (also on Pause and the other screens; `SetButtonShown`), `GameRoot.Ui.Title.Shown` to
change the title texts (`SetTitle`, `SetTagline`, `SetButtonText`), and `GameRoot.Ui.AddScreen(screen,
uxml)` + `GameRoot.Ui.OpenOverlay(screen)` for a whole menu of their own that Back closes.

## Rebuild, test, build

| | Command |
|---|---|
| Regenerate Boot/Title/Sandbox scenes, UI and shell assets (and every template's scene) | menu **JamKit > Rebuild Shell Scenes** (keeps GameConfig, AudioRegistry, the input actions and template data) |
| Switch the game to a template | menu **JamKit > Use Template > ...** |
| Fast C# check, no Unity | `dotnet build core-tests/UnityLayerEditor/UnityLayerEditor.Check.csproj` |
| Everything (tests, Windows build, smoke run) | `powershell -File tools/verify.ps1` (through the memory cap, see CLAUDE.md) |
| Smoke run of the Windows build | `powershell -File tools/smoke.ps1` |

Scenes generated by the Rebuild are overwritten each time; put your own scenes elsewhere or rename them.
