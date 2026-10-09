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
| Move | WASD, arrows | left stick, d-pad |
| Look | mouse | right stick |
| Fire / Alt fire | left / right mouse, J / K | RT / LT |
| Sprint (the bullet hell's sprint, the DOOM-like's walk) | Left Shift | LB |
| Focus (no default keys; the bullet hell's optional Focus mode) | - | - |
| Dash | Space | A, RB |
| Interact | E | X |
| Pause | Escape, P | Start |
| Menus | arrows, Enter, Escape, mouse | stick/d-pad, A, B |

The bindings are in `Assets/JamKit/Input/JamKitControls.inputactions` (edit freely).
Mouse sensitivity and invert Y are applied by `GameRoot.Input.LookDelta(GameRoot.Settings)`.

## Debug keys

**` (backquote) or F1** opens the debug menu (editor and development builds, or when GameConfig
allows it): god mode, time x0.25 / x1 / x2, Win, Lose, Next scene, FPS counter.
From code, in any build: `DebugMenu.Win()`, `DebugMenu.Lose()`, `DebugMenu.NextScene()`,
`DebugMenu.GodMode`, `DebugMenu.TimeScale`.

A game whose "next" isn't the next scene sets `DebugMenu.SkipHandler` (the bullet hell skips a room
or kills the boss), and can drive a few seconds of itself in the smoke run through
`SmokeRunner.GameplayDriver`. Set both in `Start`, clear them in `OnDestroy`.

## Templates

`JamKit > Use Template > Bullet Hell` (or `> DOOM-like`, `> Story`, `> Visiting Hours`, `> Sandbox`) points Play, the smoke
test and Build Settings at a template: it copies the template's preset (title, tagline, first scene,
music, How to Play cards, which Settings options show: Pixel look, the text options, the action
options, best time) into the active GameConfig. The bullet hell is documented in
`docs/TEMPLATE-BULLETHELL.md`, the DOOM-like in `docs/TEMPLATE-DOOMLIKE.md`, the story (visual novel,
point-and-click) in `docs/TEMPLATE-STORY.md`, the Visiting Hours slice (first person, a journal,
argument battles) in `docs/SLICE-VISITING-HOURS.md` and `docs/SLICE-NOTES.md`.

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
