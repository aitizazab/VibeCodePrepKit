# Argument battle prototype: "Take it apart"

> **Superseded for play, screen and timing (2026-10-12): see `docs/redesign/battle.md`.** The battle is now real-time:
> a debate where her statements fly as she says them and Nadia acts on them with her light (ask, concede, show a card on
> the glowing words, hold to press), "Because..." under her patience, a showdown for pressing, longer phases, the three
> rings, composure lost as a real end, every line typed one at a time. The menus, verb row, premise cards and focus panel
> described below are gone (`ArgumentView` was replaced by `ArgumentStage`), and so are the old controls table and
> transitions. **Still current here:** the logic (premises, reasons, outcomes, stances, the ledger, the hand-off API
> `ArgumentHost.BeginBattle`), the prose files and their format, fair attacks, the boil, the sprite slots.

> **Status: a playable prototype, reworked after the creator's playtest (2026-10-11).** Concept A from
> `ARGUMENT-BATTLES.md`, built on that doc's recommended defaults plus the creator's four additions. **Every line of
> dialogue is a placeholder**: the creator writes the real ones in a text file (below), not in code, and the screen
> says so. The slice's old battle (`ArgumentBattle.cs`, `VhBattleView`) is untouched and still runs the slice.

## Play it
- **From the title:** open the presenter panel (Ctrl+Shift+P, or hold L3+R3) and pick a chapter.
  - Visiting Hours' and the Dream Escape's chapter lists include **"Argument prototype (Dream-Lena)"**, which jumps
    to the prototype.
  - The prototype's own chapters: **Normal**, **Story** (nothing hurts), **Long Night** (scripted but beatable),
    **without Walt's truth**, **watch it play** (its autopilot).
- **In the prototype's scene** (`ArgumentPrototype`): a menu on black offers the same settings, shows what the save
  remembers, and can forget it.
- **The battle:** Dream-Lena at the kitchen table, Night 2 ("Lena isn't willing to let Elias go"). Claim: "He stays.
  This is his home." The premise map follows `ARGUMENT-BATTLES.md` section 5 (L1 to L4). L5 is a hidden deepest
  premise, revealed by the paper-plane slip or by asking L3 twice. Its placeholder words ("To him, you're the bad dream",
  from Theo's drawing) are a relic: the creator cut the drawing as an attack. The relic lines are listed in
  `ARGUMENT-PROSE-BRIEF.md`; the creator is rewriting the battle's prose.
- **The house:** the dream house (`DreamHouseGame.BeginArgument`) plays the same battle for its argument with Lena.

## Controls (one focus model everywhere)
Every screen of the battle and its menu works the same way:
- **Stick / d-pad / arrows** move a gold-edged focus between cards: up and down between rows, left and right along
  one. There's no wrap-around, and switched-off items are skipped.
- **A / Enter** chooses; **B / Backspace** goes back one step.
- The mouse hovers to focus and clicks to choose.
- Nothing uses UI Toolkit's own focus, so a click on empty space or a screen change can't lose it.

| Where | What A does | What B does |
|---|---|---|
| The premise list (the claim and the premise cards) | Opens that premise's verbs | Nothing (it's the top) |
| The verbs row | Does the verb (Press: hold A for 0.6 s) | Back to the premises |
| The evidence | Picks it: the reason cards, or her answer at once | Back to the verbs |
| The reasons | States it | Back to the evidence |
| The transcript (from the verbs row) | (up / down scroll) | Back to the verbs |
| The end card | Continue | |
| The prototype's menu | Starts that setting | |

Shortcuts on the premise list (rebindable in Settings > Controls, rows "Argue: ..."):

| Shortcut | Pad | Keyboard |
|---|---|---|
| Ask | X / Square | 1 |
| Challenge | RB / R1 | 2 |
| Concede | Y / Triangle | 3 |
| Press | hold B / Circle | hold 4 |
| Let it rest | View / Share | 5 |

Challenge's pad shortcut moved from A to RB, because A now confirms everywhere. Pressure phases move the light with
the stick, the d-pad, WASD or the arrows.

## The screen
- **Mostly black.** The scene behind is hidden. Lena's sprite stands large on the left, and her expression follows
  the last thing she said (neutral, angry, rattled, softened, hurt, pleading). She **starts in her antagonistic
  pose**, which holds until a reply changes her expression. Her name and expression sit above it, the heart
  rate, heat and anger beside it.
- **The claim** is the centrepiece: a paper card in the display font, saying how many key premises still hold it up.
- **Premises** are designed cards in two columns: the label (L1, L1' ...), FACT or LINK, KEY, the status, and the
  words. Broken ones are struck through, conceded ones edged green, forced ones red, hidden ones dark with "?".
- **The focus panel** reads the focused premise in full: its words, and what she said about it when asked (and her
  question, if she asked one), so statements can be read and compared and inconsistencies found.
- **The dialogue box** runs along the bottom: her last line, Nadia's, and the step spelled out as a sentence ("You
  said X. The fourth chair shows: ... So X doesn't hold."). The hint line shows the keys.
- **The full transcript** opens from the verbs row: every exchange, scrollable.
- **Fonts** (open licence, OFL, in `Argument/Resources/ArgumentFonts`, licences in `Argument/Licenses`): DM Serif
  Display for the claim, names and headings; Crimson Text (regular, italic, bold) for everything else.

## Transitions
Timed, never a cut:
- **Into the battle:** the black fades up over the world (0.45 s), then Lena slides in and the claims settle (0.6 s).
- **Into a pressure phase:** the claims fold away (0.25 s), then the box opens from the middle (0.28 s).
- **Out of a pressure phase:** the box collapses, then the claims fold back in.
- **The end card** rises in (0.5 s).
- **Out of the battle:** Continue calls back at once (the house moves on). The screen fades to black and then back
  to the world by itself (about 0.85 s).

Input waits while a transition plays.

## Sound
**Why it was broken:** the shell keeps a scene's ambience bed and music playing across scene loads, and the battle
played nothing of its own. So the previous scene's ambience went on.

**Now:**
- **On Begin**, the battle remembers the world's ambience and music, fades them out (0.8 s) and fades in its own
  loop.
- **On Continue**, it fades its loop out and brings the world's ambience and music back.
- Everything follows the player's volumes and the shell's mute (batch mode).
- **The battle's sounds:**
  - navigating;
  - confirming a verb;
  - picking a card;
  - backing out;
  - a premise breaking;
  - a wrong reason;
  - an antagonised or forced reply;
  - a hit on Nadia, plus the heartbeat stepping up;
  - the pressure phase opening and closing;
  - each attack's warning (a soft double tick, `sfx_warn`);
  - a slip caught;
  - the end card (a warm chord for Convinced and Heard out, a dark one otherwise).
- **Swappable files** in `Argument/Resources/ArgumentAudio/`:
  - `argument_battle_loop`: the music. The placeholder is the round-2 sketch `out/vh-music-sketches/05-argument-battle.loop.wav` as OGG, 111 s.
  - `sfx_nav`, `sfx_confirm`, `sfx_pick`, `sfx_back`, `sfx_break`, `sfx_wrong`, `sfx_angry`, `sfx_hit`, `sfx_heartbeat`,
    `sfx_pressure_in`, `sfx_pressure_out`, `sfx_slip`, `sfx_end_good`, `sfx_end_bad`, `sfx_warn`.
  - A sound with no file is generated in code (a placeholder). Drop in a file with the same name (WAV or OGG) to
    replace it.

## Art: the sprite slot (a drop-in)
- **Files:** `Assets/JamKit/Templates/VisitingHours/Argument/Resources/ArgumentArt/lena_<expression>.png`:
  `lena_antagonistic` (the opening pose; until it lands, `lena_neutral` stands in), `lena_neutral`, `lena_angry`,
  `lena_rattled`, `lena_softened`, `lena_hurt`, `lena_pleading` (these six landed in 104f4ea; the code-drawn
  placeholder only shows if a file goes missing).
- **Treatments (a hook, no picker yet):** `ArgumentArt.SetTreatment("bw")` makes the slot look for
  `lena_bw_<expression>.png` first and fall back to the plain file.
- **Size:** portrait 2:3, 1024 x 1536 px, transparent background.
- **Framing:** the figure from the waist (or hips) up, filling most of the height, facing slightly right (towards
  the claims). The import settings are applied for you (`ArgumentArtImport`: Default texture, no mipmaps, no
  power-of-two rescale, alpha is transparency, clamp), so a GUID-only `.meta` is fine. (The first sprites came
  with GUID-only metas that Unity never filled in, and they never loaded: the placeholder showed. Their metas are
  complete now.)
- **Missing files:** a missing expression falls back to `lena_neutral`. With no file at all, the screen draws a
  sketch-style pencil silhouette (a different face per expression) and notes that it's a placeholder.
- The slot is 640 x 860 px on screen, bottom-aligned, scaled to fit.

## The boil (Lena's sprite)
Lily's hand-drawn distortion from the scene 2 prototype (`FableAnimationTests/demos/lily-warp.html` and
`scene2-prototype-v2.html`) runs on Lena's battle sprite.

**How it works:**
- Fractal noise (two octaves, like `feTurbulence type="fractalNoise"`) pushes each pixel, like `feDisplacementMap`.
- The noise is re-seeded in steps, `rate` times a second, so the lines jitter like a redrawn drawing rather than
  swim. That's the boil.
- The shader is `Argument/Resources/ArgumentShaders/ArgumentBoil.shader`; the settings and presets live in
  `ArgumentBoil.cs`.
- It distorts whatever `ArgumentArt.Sprite` returns, so it stacks with a treatment (`SetTreatment`).
- The sprite is redrawn into a render texture only when the boil steps (8 times a second at the defaults).
- Without a graphics device (batch test runs) the plain sprite shows.

**The four variables** (the same as Lily's):
- `strength`: the furthest a pixel moves, in pixels on a 1080p screen.
- `freq`: noise cycles per screen pixel. Lower means broader waves; 0.01 is a 100 px wave.
- `rate`: re-rolls per second.
- `speed`: only for the smooth "breathe" style, which is there but not used.

**Tuning.** Lena stands about 860 px tall on screen, waist up; Lily stood about 362 px tall, full figure, on the
1280 x 720 stage. Lily's 4 px at 0.02 scaled by that ratio is 9.5 px at 0.0084. On Lena's face, which is much
bigger, that's a strong wobble, so the default is gentler.

| Preset | strength | freq | rate | |
|---|---|---|---|---|
| **Gentle** (default) | 4 px | 0.011 | 8/s | a light hand-drawn tremble; the ink lines stay crisp |
| Subtle | 2.5 px | 0.012 | 6/s | barely there |
| Lily-equivalent | 9.5 px | 0.0084 | 8/s | Lily's own, scaled to this sprite |
| Strong | 8 px | 0.016 | 12/s | busier and quicker |
| Slow and heavy | 12 px | 0.007 | 3/s | broad, slow lurches |
| Off | | | | the plain sprite, to compare |

**Her mood picks the boil** (the creator's choice, 2026-10-13; the mapping is mine, any can be swapped in
`ArgumentBoil.MoodPreset`):
- neutral and pleading: Gentle;
- antagonistic (her opening pose): Lily-equivalent, her guard up, a steady strong tremble;
- angry: Strong, busy and quick;
- rattled: Slow and heavy, lurching and off balance;
- softened and hurt: Subtle, almost still.

**Keys in the battle (debug overrides):**
- **F10** overrides her mood with each preset in turn, then goes back to her mood. The name and values show above
  her name for a few seconds.
- **Shift+F10** goes straight back to her mood.

  F10 because every other function key is taken: F1 the debug menu, F3/F4/F6/F7 movement, F5/F9 the Story
  template, F7/F8 the dream house's argument skips, F9 the opening editor. F11 is a browser's fullscreen.

**Capture** (from a Visiting Hours build, like the title's `-title-shot`):

    VisitingHours.exe -argument-shot "<folder>" [-argument-expression angry] [-argument-boil strong] [-argument-react] [-argument-frames 12] [-argument-fps 12]

It:
1. jumps to the prototype's Story battle;
2. waits for the premise list;
3. holds Lena on the expression with the preset (the label stays on);
4. saves `boil_<preset>_<expression>_NN.png` (12 frames at 12 fps by default: one second);
5. quits.

The log line is `JAMKIT_ARGUMENT_SHOT_OK` or `_FAIL`. Expressions: antagonistic (the default), neutral, angry,
rattled, softened, hurt, pleading. Presets by name: gentle, subtle, lily, strong, slow-heavy, off.

## The prose files (for the creator)
Every word of the battle is in two text files, read at runtime from
`Assets/JamKit/Templates/VisitingHours/Argument/Resources/ArgumentText/`:
- **`lena_night2.txt`:** every line of Dream-Lena's battle.
  - The claim and the frame lines: `claim`, `opening`, `letItRestReply` ...
  - Each premise's lines: `L1.text`, `L1.askReply`, `L1.concedeLine` ...
  - Each premise's reason cards: `L1b.sound`, `L1b.sound.reply`, `L1b.beside`, `.nonsequitur`, `.antagonise`.
  - Its retort: `L2.retort`, `L2.retort.reply`.
  - The evidence cards: `mem_fourth_chair.name`, `.note`.
  - A reason can differ per piece of evidence: `L3c.truth_walt.sound` overrides `L3c.sound` for Walt's truth.
- **`screen.txt`:** the screen's own words: prompts, labels, verb names, Nadia's default lines, the transcript's
  sentences (with `{premise}`, `{evidence}`, `{reason}` filled in), the end card, the menu.

**The format:** one entry per line, `id: the line`, with an indented `? note` under it saying what the line must do
(ignored by the game). `#` lines are headings, and `\n` is a line break. The ids match
`docs/ARGUMENT-PROSE-BRIEF.md`, which explains the flow and every line.

**Editing:** edit the text after the colon, keep the id, and press Play again (the files are read once per session). A
missing or misspelt id shows on screen as `[its.id]`, so a gap is visible, never silent. A test checks that both
files have a line for every id and no stray ones.

## What's built (the doc's defaults)
- **The case as an object:** a claim held up by premises (facts and links; some hidden, shown as "?").
- **Breaking a premise** takes evidence **plus** a reason. Picking evidence brings up "because..." cards: one sound,
  one true but beside the point, one that doesn't follow. Other evidence is answered at once, with a little heat:
  - evidence that doesn't touch it;
  - a true premise;
  - an unbreakable one (L2', "can't be broken tonight").
- **She answers logically.** A sound reason makes her concede (it breaks), fall back (a weaker premise appears) or
  deepen. Each wrong reason gets its own reply, saying why it doesn't follow.
- **Pressure phases** (5–8 s) come only as consequences:
  - the opening (low);
  - an unsound reason (+1 heat), a press (+2), an antagonising reply (+2);
  - a painful premise surfacing (L4, "He called you first": the high phase).

  Heat falls with concessions and sound challenges. On the surface: plates sliding in walls with gaps, flour
  blooming in rings with gaps, batter lobbed at Nadia's light. **Slips** (the paper plane) reveal what she isn't
  saying.
- **Every attack is fair** (the creator's standing rule, below: "Fair attacks").
- **Composure is Nadia's heart rate**: 70, 88, 106, 124, 142, 160 bpm. A hit is a step, a true concession calms a
  step, and 160 ends it.
- **Press** is a 0.6 s hold. **Let it rest** opens once every premise is heard. **Concede** works on any premise, and
  on the claim it gives in (Yielded).
- **Outcomes:** Convinced (every key premise broken, nothing forced: a truth), Heard out (a keepsake), Pressed,
  Composure lost, Yielded.

## Fair attacks (the standing rule)
The creator, 2026-10-12: "Aside from Long Night, as a standing rule, attacks either come in from outside the screen,
or get a telegraph beforehand." And no attack may be impossible to avoid on any difficulty.

**The rule, in the code:** `PressureRule` (in `PressurePhase.cs`). Every spawn goes through `PressureRule.Admit`,
which refuses an attack that starts inside the box without a warning of at least 0.5 s. Only the Long Night is
exempt, and only because its spec is flagged (`PressureSpec.exemptFromRule`). Its walls come in from outside anyway.

**The warnings** (the attack is harmless until its warning runs out):
- 0.65 s on Normal, 1 s in Story.
- Every attack is warned, even those coming in from outside. Otherwise a light hugging the edge had a tenth of a
  second.
- **Plates:** a ghost column at the edge they'll come in from, with their gaps.
- **Flour rings:** the centre, a faint outline with the gaps, and the lanes out through the gaps to the edge of the box.
- **Batter:** its path across the box.
- Each warning grows stronger and pulses faster as it runs out, with a soft double tick (`sfx_warn`).

**Every spawn leaves a way out.** Outside the Long Night, a new attack only spawns if a planner
(`DodgePlanner`, a search over every spot her light can reach) finds an escape from everything on the surface for a
deliberately weaker player:
- 80% of Nadia's speed;
- extra room around her light;
- 0.25 s late to react: checked both as if she started moving at once with 0.25 s less warning, and as if she kept
  doing what she was doing (standing, or moving the same way) for 0.25 s first.

If there's no escape it tries another placement (up to 4). If none works, the attack is dropped. The check costs
about 0.1 ms per spawn (2 ms at worst in 99 of 100).

**What was unfair, and what changed:**
- **Flour rings** bloomed anywhere, even right beside her, and their gaps pointed in random directions. A gap could
  point off the edge of the box on her side, so it was unreachable. Or the ring started so close that there was no
  time to move. Now:
  - a ring never blooms within 1 unit of her;
  - its gap is aimed within her reach (she can get to it before the ring gets to her);
  - it's warned;
  - the check above has the last word.
- **Plates** came in at up to 4.8 units a second with a gap that left a lane only 0.16 wide for her light. A light
  near the edge they came from had about 0.1 s. Now they're warned, and the narrowest gap on Normal leaves 0.4 of
  room (it was 0.12). The narrowest gap only binds at high heat and anger; calmer gaps are unchanged.
- **Batter** was lobbed at her from the nearest edge with no warning: about 0.04 s at the edge. Now it's warned.
- **Several attacks at once** could close every way out between them (two plates from opposite sides, rings
  overlapping). The spawn check refuses any attack that would.

**Density:** angrier phases still throw twice as many attacks. The check drops the ones nobody could escape, about
1 in 8 when calm and up to 1 in 3 when furious, so furious phases land about 1.5 to 2 times as many as calm ones.
Phase lengths are unchanged.

**The proof** (`PressureFairnessTests`, dotnet):
- **Every pattern** (plates, rings, batter, all three mixed), on Normal from calm to furious and in Story at its
  hardest, is played by `PlanningDodger`.
- **The dodger** searches its reachable spots for an escape every time it sees a new warning, and waits between
  attacks in an awkward spot: each corner, each edge, the middle (nine phases per case). It has to finish without
  a single touch. A touch counts even in Story.
- **No warning-less spawns:** another test checks that nothing appears inside the box without a warning. Others
  check the rule's refusals and that rings never bloom beside her.
- **A wider sweep** of 1,260 phases (more seeds and settings) was also untouched.

## The creator's additions
1. **Antagonising replies.** Two forms: a fourth "because..." card ("You're a dream..."), or the **Retort** card
   when she asks Nadia a question ("Then tell me where I am" → "You're not real. Stop acting like you are."). Either
   dodges the question and attacks her: anger +1 (0–3), heat +2, and a pressure phase.

   Every later phase is **noticeably harder**:

   | Per point of anger | Effect |
   |---|---|
   | Speed | +30 % |
   | Density | +40 % |
   | Gaps | 15 % narrower, and one gap instead of two |

   Anger pushes the outcome towards **condemns**.
2. **Outcomes carry forward.** Each battle ends in a stance, **amenable**, **unsure** or **condemns**, kept in the
   save (entry `vh_arguments`). A later battle reads it, and a truth won earlier counts as evidence later.
3. **Scripted but beatable** (per battle; for the Long Night):
   - Nadia starts at 124 bpm.
   - A brutal walls-only phase follows every exchange, with a gap only just wider than her light.
   - It's winnable by construction: a perfect dodger survives every phase, and standing still loses.
4. **Supporters** (`SupporterDef`): a data slot for a line and a card. No content yet.

## Nights 1 and 2 (2026-10-12; the plan: docs/NIGHTS-1-2.md)

### Starting a battle from the game (the hand-off)
```csharp
// Unity, JamKit.Argument. Ids: ArgumentBattles.Walt ("walt_night1"), ArgumentBattles.Lena ("lena_night2").
var context = new ArgumentContext { force = forceTally, despair = despair0to3 };
context.found.AddRange(journalIdsFound);        // memories, deductions, facts, truths: they gate the cards
ArgumentHost.BeginBattle("lena_night2", context, result =>
{
    // result.outcome     Convinced, HeardOut, Pressed, ComposureLost, Yielded (None: closed before it ended)
    // result.stance      Lena: Unsure or Condemns. Walt: Amenable, Unsure or Condemns
    // result.Condemns    true when she condemns Nadia
    // result.forceAdded  presses this battle: add to the Force tally
    // result.despairAdded 1 when Lena condemns (ArgumentBattles.DespairOnCondemn), else 0: add to Despair
    // result.truth / result.keepsake  journal ids won ("" if none)
    // result.record      already saved in the ledger (vh_arguments)
});
```
- **Cards appear only once found:** a card is in the tray when its id (or one of its `unlockedBy` ids) is among
  `context.found`. Other ids are ignored, so pass the whole journal. `allEvidence` is for debug and the prototype.
- **Lena's cards** (the plan's memory-to-card table), by journal id:

  | Card | Where it comes from | What it does |
  |---|---|---|
  | `mem_fourth_chair` | the place card | breaks L1' (key) |
  | `ded_saturday_off` | the calendar + Night 1's paper | breaks L3' (key) |
  | `ded_same_saturday` | the street replay (+ Walt's ledger) | breaks L3'' (key) |
  | `mem_bad_dream` | Theo's drawing, in his room | against L5 (half true) |
  | `mem_height_marks` | the height marks | backs L1 (true: concede it) |
  | `fact_photo` | the photo wall | worldbuilding |
  | `fact_police_report` | Day 1's police report | L4 (true, painful) |
  | `truth_walt` | won from Walt (Convinced) | also breaks L3'' |
- **Walt's cards:** `ded_walt_dead` (the key), `ded_same_saturday` (the optional one), `mem_walt_alive` (touches
  nothing: a lesson).

### The creator's notes, bold and conditional lines (the prose files)
- **`//` is a note**, never shown, whether on its own line or after a line, even inside brackets.
- **Replacement lines:** after `claim`, `opening` and `noTouchReply`, the creator's note is their rewrite of the line,
  so the note is used instead (`ArgumentText.ReplacedByNote`).
- **`*word*` is bold.**
- **Conditional lines:** `id[condition]: text` replaces the plain line while the condition holds. The first one in
  file order that holds wins.
  - Comparisons: `>= <= > < = !=`. `,` is *and*, `|` is *or*.
  - Meters:
    - `anger`: Lena's, this battle, 0-3. Antagonising, retorts and, in her battle, presses raise it.
    - `force`: the game's tally plus this battle's presses.
    - `despair`: 0-3.
    - `presses`, `antagonised`, `heat`.
  - Thresholds used for her variants:
    - angry is anger 2+, max anger is 3, low anger is 1 or less;
    - low Force is 1 or less, high Force is 3+;
    - max despair is 3, no despair is 0;
    - "close to condemning" is any antagonising or press so far (either already means she condemns).
- **Lena's variants** (pulled out of the creator's notes, in `lena_night2.txt`; their originals stay as `//` notes,
  so nothing they wrote is lost):
  - `L1b.nonsequitur.reply`: the latter part only at Despair 2+ with anger 1 or less (my call; theirs to change);
  - `L1c.trueReply`: the "silver tongue" line when angry or close to condemning;
  - `L2.pressLine`: a tamer line (an agent placeholder) while Nadia's been gentle (no anger, no presses, low Force);
  - `L3b.beside.reply`: the latter part only at max Despair with low anger and low Force;
  - `L3b.nonsequitur.reply`: "I should cut your tongue out for that." at max anger and high Force;
  - `L4.concedeReply`: the forgiveness at low anger and no Despair.

### Lena's two endings
- **Unsure:** Convinced, Heard out, or Yielded.
- **Condemns:** Pressed, Composure lost, or any antagonising card or retort that landed (`StanceRule.TwoEndings`).
  Condemning hands the game +1 Despair (`result.despairAdded`).
- **Her keepsake** (Heard out) is a placeholder: her planisphere (`pers_lena_planisphere`).

### Night 1: Walt's tutorial (`WaltNight1`, `walt_night1.txt`, an agent placeholder for the creator to rewrite)
**Walt** is the gatekeeper: "Let him have his good day. Come back tomorrow." He argues and doesn't teach.

**Mags teaches** on the earpiece, one line per exchange, the first time each moment comes (`ArgumentCoach`):
1. ask (at the start);
2. concede (once she's heard something true);
3. challenge (once she holds something that breaks a premise);
4. the first pressure phase;
5. press (at the first misstep, or once only premises she can't break are left);
6. the first break;
7. let it rest.

**The premises:**
- W1 is true: asking it reveals W2, conceding it reveals W3.
- W2 is the false key link: `ded_walt_dead` breaks it.
- W3 is false and optional: `ded_same_saturday` breaks it.

**Gentle:**
- no opening pressure phase;
- pressure at 0.7 speed;
- plates only until the heat rises;
- composure stops a step short of panic, so it can't be lost.

**The ending:** Walt steps aside in every outcome, and the ledger remembers his stance for the ending.
- Convinced wins `truth_walt`, which also works against Lena's L3''.
- Heard out wins his wrench (`pers_wrench`).
- The prototype has a "Walt" chapter.

## For code (calling a battle)
```csharp
using JamKit.Argument;
var host = ArgumentHost.Begin(DreamLenaDemo.Build(),
    new ArgumentSetup { difficulty = ArgumentDifficulty.Normal, allEvidence = true },
    record => { /* record.outcome, record.stance, record.truth ... */ });
```
- **Where it runs:** any scene.
- **While it runs:**
  - world gameplay is blocked (`InputService.BlockWorld`);
  - the world's sound gives way to the battle's;
  - the cursor is unlocked: shown for the mouse, hidden while the pad is in use (the shell's rule, `SHELL.md`), and
    hover never moves the focus in pad mode;
  - the d-pad is on Move for the dodging;
  - the argument's Controls rows show.
- **When it ends:** it saves. Continue calls back with the record at once, even on the frame it ended.
- **Also:**
  - `ArgumentHost.Running` and `Current`;
  - `Close(false)` stops a battle without a callback;
  - `Autopilot` plays it;
  - `ArgumentHost.LoadLedger().StanceOf(id)` reads a past result.
- **From another assembly:** add `JamKit.Argument` to its asmdef references.

| Code | Where |
|---|---|
| Rules, pressure, ledger, pilot, prose loading, the demo's structure | `Packages/com.vibejam.jamkit/Runtime/Core/VisitingHours/Argument/` (pure C#; `ArgumentText` reads the prose files) |
| Host, screen, focus model, sound, art, menu | `Assets/JamKit/Templates/VisitingHours/Argument/Scripts/` (assembly `JamKit.Argument`): `ArgumentHost`, `ArgumentView`, `ArgueNav` (with `NavInput` and `ArgueUi`), `ArgumentAudio`, `ArgumentArt`, `ArgumentContent`, `ArgumentPrototype` |
| Prose, fonts, music | `Assets/JamKit/Templates/VisitingHours/Argument/Resources/` (`ArgumentText`, `ArgumentFonts`, `ArgumentAudio`; `ArgumentArt` for the sprites when they land) |
| Scene builder | `Assets/JamKit/Templates/VisitingHours/Argument/Editor/ArgumentPrototypeBuilder.cs` (a step of Rebuild Shell Scenes) |

## Tests
| Suite | What |
|---|---|
| dotnet, `ArgumentEngineTests` (28) and `ArgumentProseTests` (6) | The rules: breaking, the reasons, her replies, slips, reveals, Let it rest, Press, the antagonising escalation, persistence, the Long Night, supporters. The prose: both files have every line and no stray ids, the format, per-evidence overrides, export round-trips, nothing left in code, the transcript's sentences come from screen.txt |
| Unity EditMode, `ArgumentPrototypeEditorTests` | The screen builds on black with the sprite and the real fonts, with no UI Toolkit buttons; the transitions are timed and finish; every expression has a sprite (a placeholder until the art lands); the prose, music and sounds load from their files; the focus model (no wrap, disabled skipped, one item lit); the scene and the chapters |
| dotnet, `PressureFairnessTests` (25) | Fair attacks: every pattern at every difficulty survived untouched by a planning dodger from every corner, edge and the middle; nothing appears inside the box without a warning; the rule refuses an unwarned in-box attack (the Long Night is the only exception); rings never bloom beside her |
| Unity PlayMode, `ArgumentPrototypePlayTests` | **The whole battle on a gamepad alone**, to Convinced: the d-pad to each premise, A into its verbs, B back, Y concede, X ask, A through the evidence and the sound reason, the pressure box opening and closing, A on Continue, then the menu by d-pad. The world's ambience is silent during the battle and back after, and the battle loop starts. Keyboard shortcuts, Backspace stepping back, the 0.6 s Press hold. A won battle saved and Continue back to the menu. Continue on the frame it ends still gives the record |

## Not done (yet)
- **Lena's sprite:** the slot shows a pencil placeholder until the art lands (file names above).
- **The music and sounds:** placeholders until the sound pass (file names above).
- **All dialogue is placeholder:** the creator's, in `lena_night2.txt`.
- **Not wired into the slice:** the evidence tray doesn't read the slice's journal yet (the setup lists ids), the
  ink dialogue service isn't used, and there's no Tell verb (Night 4) and no "Off" pressure setting (Story is the
  gentle one).
