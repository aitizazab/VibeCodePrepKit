# Argument battle prototype: "Take it apart"

> **Status: a playable prototype (overnight, 2026-10-11)** of concept A from `ARGUMENT-BATTLES.md`, using that
> doc's recommended defaults plus the creator's four additions. **Every line of dialogue in it is a placeholder.**
> The creator writes the real ones; the screen says so. The slice's old battle (`ArgumentBattle.cs`,
> `VhBattleView`) is untouched and still runs the slice.

## Play it
- **From the title:** open the presenter panel (Ctrl+Shift+P, or hold L3+R3) and pick a chapter.
  - Visiting Hours' and the Dream Escape's chapter lists include **"Argument prototype (Dream-Lena)"**, which
    jumps to the prototype.
  - The prototype's own chapters: **Normal**, **Story** (nothing hurts), **Long Night** (scripted but beatable),
    **without Walt's truth**, **watch it play** (its autopilot).
- **In the prototype's scene** (`ArgumentPrototype`): a menu offers the same settings, shows what the save
  remembers, and can forget it.
- **The battle:** Dream-Lena at the kitchen table, Night 2 ("Lena isn't willing to let Elias go"). Claim:
  "He stays. This is his home." Its premise map follows `ARGUMENT-BATTLES.md` section 5 (L1 to L4), plus L5, a
  hidden deepest premise ("To him, you're the bad dream") revealed by the paper-plane slip or by asking L3 twice.

| Do | Keyboard (default) | Pad (default) | Mouse |
|---|---|---|---|
| Pick a premise or the claim | arrows | stick / d-pad | click a tag |
| **Ask** ("Why?") | 1 | X / Square | the verb button |
| **Challenge**: pick evidence, then a reason | 2, then Enter | A / Cross | the verb button, then the cards |
| **Concede** ("That's true"; on the claim: give in) | 3 | Y / Triangle | the verb button |
| **Press** (a 0.6 s hold; on the claim: ends it) | hold 4 | hold B / Circle | hold the verb button |
| **Let it rest** (once every premise is heard) | 5 | View / Share | the verb button |
| Back out of a choice | Backspace | B / Circle | click a tag |
| Dodge in a pressure phase | WASD / arrows | stick / d-pad | |

All five verbs are rebindable in **Settings > Controls**, where their rows ("Argue: ...") show while a battle
runs. They live in their own context, so A being both Jump and Challenge isn't flagged as a clash; Pause clashes
with everything (`SHELL.md`, Controls). The verb buttons show the current keys.

## What's built (the doc's defaults)
- **The case as an object.** The claim is the centrepiece; each premise is a tag that shows its kind and status
  as shape and text, never colour alone:
  - kind: a square for a fact, the infinity sign for a link, "KEY" on a key premise;
  - status: hidden ones show as "?"; then standing, BROKEN (struck through), conceded (a check mark), or FORCED.
- **Breaking a premise** takes evidence **plus** a reason. Picking evidence brings up "because..." cards: one sound,
  one true but beside the point, one that doesn't follow. The order is shuffled, the same for the same pair every
  time. Other answers come at once, with a little heat:
  - evidence that doesn't touch the premise: "That's not what I said";
  - a true premise: "Then where am I? Go on.";
  - an unbreakable one (L2'): "can't be broken tonight".
- **The opponent answers logically.** A sound reason makes her concede (the premise breaks), **fall back** (a
  weaker premise appears), or **deepen** (the one under it appears). Each wrong reason gets its own reply, saying
  why it doesn't follow.
- **Every step as a sentence** for a judge, in the transcript: "You said "X". The fourth chair shows otherwise:
  ... So "X" doesn't hold.", "... doesn't follow from ...", "Nadia conceded "X": it's true."
- **Pressure phases** (5–8 s) come only as consequences:
  - the opening (low);
  - an unsound reason (+1 heat), a press (+2), an antagonising reply (+2);
  - a painful premise surfacing by script (L4, "He called you first": the high phase, 8 s).

  Heat falls with each concession and each sound challenge. The phase's shapes, on the tablecloth:
  - plates sliding across in walls with gaps;
  - flour blooming out in rings with gaps;
  - batter lobbed at Nadia's light.

  **Slips** (the paper plane) reveal what she isn't saying.
- **Composure is Nadia's heart rate**: 70, 88, 106, 124, 142, 160 bpm. Each hit is a step; conceding something
  true calms her a step; 160 ends the battle (Composure lost).
- **Press** is a 0.6 s hold, never a click. **Let it rest** opens only once every premise, hidden ones included,
  has been heard (asked, conceded, challenged or pressed). **Concede** works on any premise; on the claim it ends
  the battle as **Yielded**.
- **Outcomes:** Convinced (every key premise broken, nothing pressed: a truth), Heard out (a personal entry),
  Pressed, Composure lost, Yielded.

## The creator's additions
1. **Antagonising replies.** Two forms:
   - a fourth "because..." card ("You're a dream. A dream doesn't get a say in his life.");
   - a **Retort** card in the tray when she's asked Nadia a question. On L2 she asks "Then tell me where I am,
     Nadia"; the retort is "You don't get to ask me that. You're not real. Stop acting like you are."

   Either one dodges the question and attacks her: **anger +1** (0–3), heat +2, and a pressure phase. Every
   later phase is **noticeably harder** (`PressureSpec.For`):

   | Per point of anger | Effect |
   |---|---|
   | Speed | +30 % |
   | Density | +40 % more often |
   | Gaps | 15 % narrower, and one gap instead of two from the first point on |

   Anger pushes the outcome towards **condemns**: three antagonisms always condemn, and two leave even a Convinced
   Lena only unsure.
2. **Outcomes carry forward.** Each battle ends in a stance: **amenable**, **unsure** or **condemns**.
   - Kept in the save (entry `vh_arguments`, `ArgumentLedger`, one line per battle; damaged lines are skipped),
     with the outcome, anger, presses, the truth or personal entry won, and a play count.
   - A later battle reads it (`ArgumentHost.LoadLedger().StanceOf("lena_night2")`: Lena's blessing, or not).
   - A truth won in one battle counts as evidence in later ones: L3'' breaks only with Walt's truth or "the same
     Saturday".

   | Outcome | Stance |
   |---|---|
   | Convinced | amenable (anger 2: unsure) |
   | Heard out | amenable (any anger: unsure) |
   | Pressed | unsure (two presses, or any anger: condemns) |
   | Composure lost, Yielded | unsure (anger 2: condemns) |
   | Anger 3, any outcome | condemns |
3. **Scripted but beatable** (`ArgumentDifficulty.ScriptedButBeatable`, per battle; for the Long Night).
   - Nadia starts at 124 bpm: two hits from panic.
   - **Every** exchange brings an 8 s full-heat phase: faster still, twice as dense, walls only.
   - Each gap is only 5 cm wider than her light.
   - It stays technically winnable by construction: each wall's gap is placed where the light can reach in time.
     `PressurePilot` (a perfect dodger) clears every phase untouched, across seeds and anger levels; standing
     still loses.
4. **Supporters** (`SupporterDef`): a data slot for Lena or Theo in the back seats. Each can add:
   - a line, at the start or when a premise appears;
   - an evidence card for the tray;
   - an extra reason card on one challenge.

   Present supporters are listed in `ArgumentSetup.supporters`. There's no content yet; a test exercises the slot.

## For code (calling a battle)
```csharp
using JamKit.Argument;
var host = ArgumentHost.Begin(DreamLenaDemo.Build(),
    new ArgumentSetup { difficulty = ArgumentDifficulty.Normal, allEvidence = true },
    record => { /* record.outcome, record.stance, record.truth ... */ });
```
- **Where it runs:** any scene.
- **While it runs:**
  - world gameplay is blocked (`InputService.BlockWorld`: `GameplayAllowed` is false, so the player stands
    still);
  - the cursor is free;
  - the d-pad is on Move for the dodging;
  - the argument's Controls rows show.
- **When it ends:** it saves to the ledger. Continue on the end card closes it and calls back.
- **From another assembly:** add `JamKit.Argument` to its asmdef references (the Dream Escape will need this).
  `ArgumentHost.Running` says whether one is up; a scene that locks the cursor every frame should skip that while
  it's true.
- **Setup:**
  - `difficulty`;
  - `evidence` (journal ids) or `allEvidence`;
  - `supporters`;
  - `ledger` (null loads the save);
  - `pressureSpeed` (accessibility, 0.5–1);
  - `seed`.

| Code | Where |
|---|---|
| Data, rules, pressure, ledger, pilot, demo | `Packages/com.vibejam.jamkit/Runtime/Core/VisitingHours/Argument/` (namespace `JamKit.Argument`; pure C#) |
| Host, screen, prototype scene | `Assets/JamKit/Templates/VisitingHours/Argument/Scripts/` (assembly `JamKit.Argument`) |
| Scene builder | `Assets/JamKit/Templates/VisitingHours/Argument/Editor/ArgumentPrototypeBuilder.cs` (a step of Rebuild Shell Scenes) |
| Verbs and the world block | `InputService` (`ArgueAsk` ... `ArgueRest`, `BlockWorld`), `ControlBindings` (rows with contexts) |

## Tests
| Suite | What |
|---|---|
| dotnet (`core-tests/VisitingHours`, `All`), `ArgumentEngineTests`, 28 tests | Premise breaking (only the sound card breaks; fall back; each wrong reason's own reply and heat); true, irrelevant and unbreakable answers; Ask, and asking twice, reveal hidden premises; conceding reveals links and calms her; conceding something false closes the line; a slip reveals the hidden one; the painful premise surfaces with the high phase; Let it rest only when all is heard; Press forces a piece and pressing the claim ends it; Yielded; five hits lose composure; **antagonising** (the retort dodges her question, anger, faster / denser / narrower / fewer gaps, twice as dense when furious, pushes the stance to condemns); **persistence** (the ledger round-trips, plays count, damaged lines skipped, a later battle reads the stance and an earlier truth); **the Long Night** (starts at 124, the gap only just fits, standing still loses, a perfect dodge wins, every phase dodgeable over 8 seeds and 4 anger levels); **supporters** (line, card, extra reason, nothing when absent); the honest path is Convinced, and without Walt's truth it's Heard out at best |
| Unity EditMode, `ArgumentPrototypeEditorTests` | The screen builds without the shell (the claim, the placeholder badge, steps as sentences, heart rate, no focusable buttons, the end card); the scene is built and in Build Settings; the chapters' setups. `ControlsEditorTests`: the verb rows are hidden, in their own context, and don't clash with walking-around keys |
| Unity PlayMode, `ArgumentPrototypePlayTests` | The chapter opens the scene and the battle; the world waits and the d-pad steers; a won battle is saved and Continue returns to the menu; the keys drive Ask, Challenge (evidence, then the sound reason, by arrows and Enter), Concede and the 0.6 s Press hold |

## Not done (yet)
- **Art:** the arena is a placeholder kitchen of primitives and a capsule for Lena. There's no billboard, no
  expressions on screen (the expression shows as a word), and no per-arena UI skin. Premise tags are cards, not
  plates on the table.
- **Pressure shapes:** coded shapes, not the existing `Patterns` emitters (which have no guaranteed gaps). There's
  no "Off" pressure setting (Story is the gentle one), and no Tell verb (Night 4).
- **Not wired into the slice:** the evidence tray doesn't read the slice's journal (the setup lists ids), and the
  ink dialogue service isn't used (the lines are in code). The Dream Escape house can call it once its asmdef
  references `JamKit.Argument`.
- **All dialogue is placeholder.**
