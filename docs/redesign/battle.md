# The argument battle, redesigned (Battle agent, 2026-10-12)

> **Status: milestone 1 built, not yet run in Unity.** Core green in dotnet (VisitingHours 160/160 at the time of
> writing); the Unity layer compiles against Unity's DLLs (the only compile errors in the tree are the House agent's
> in-progress `DreamHouse*` files). Nothing here has been seen on screen yet: main runs the captures below, a judge
> scores them, and this doc follows the findings. Older docs (`ARGUMENT-PROTOTYPE.md`, `ARGUMENT-BATTLES.md`) stay as
> history for the logic; where they disagree about play, screen or timing, this doc wins.

The creator, after playing it (2026-10-12): *"It was mostly just talking ... there was absolutely no gameplay. And the
stretches of which there were, were both too short. It was more of just an argument, than an argument battle."* Plus:
long text clipped in the boxes, and lines popped in all at once while her face held an extreme expression as they
thought.

## 1. The shape in one paragraph

The logic stays (premises; ask, concede, press, challenge with an evidence card and a reason; true / false / half;
hidden premises; Convinced / Heard out / Pressed / Yielded / Composure lost; unsure or condemns; Force and Despair), but
every choice is now made **inside real-time play**. Lena says her case round the table, one statement at a time; each
statement **flies across the table as she says it**, its weak words glowing gold, while her pressure (plates, rings,
lobs, inked in her wine red after a graphite sketch of where they'll be) comes at Nadia's light. Nadia acts on a
statement by **putting her light on it**: X asks "Why?", Y says "That's true", A shows the card in hand (it has to land on
the glowing words), holding B forces it. The right card opens **"Because..."** (pick the reason that follows before
she loses patience); forcing opens a **showdown** (cut her words as they reach you). Wrong choices cost something you
feel at once. A round of silence heats her up. Her heart-rate is health, and **at 160 bpm Nadia leaves the table: the
battle is over, Lena condemns her, Despair +1.** Every spoken line is typed one at a time with voice blips, and her face
reacts to each line, then eases back to listening.

## 2. The parts (and where they live)

| Part | What it does | Code |
|---|---|---|
| **The debate** | Rounds of her standing premises, then the claim. Statements fly in three patterns (in from the right, from the left, or settling in place and drifting), never twice in one lane. Each premise **pushes back as she says it**: a ring blooms from its weak words (once; twice for a key premise; once more when she's heated). The claim drifts last; as it goes the pressure **drains** (a breath), and the next round starts at the heat and anger she has then. From round 2, something she isn't saying (a slip: the paper plane "THE BAD DREAM", Walt's ledger page) drifts through once a round: touch it to catch it. | `Core/.../Argument/Debate.cs` |
| **Verbs** | X / 1 / right click: ask. Y / 3: concede (held 1.1 s on the claim: give in). A / Enter / left click: show the card in hand. Hold B / 4 (0.6 s): press. LB / RB, Q / E, 2, the wheel: change card. View / 5: the case file (the battle waits). The cards in hand are the found evidence, plus "Answer back" while her question stands (a retort) and "I hear you" once everything's heard (put it on the claim: let it rest). | `BattleFlow.cs`, `ArgumentHost.cs` |
| **Because...** | Evidence on the weak words of a premise it answers: the table holds still, the reason cards come up (sized to their text) and **her patience runs down** (reading time at ~14 letters a second plus 5 s, less with anger and heat; Walt's twice as long; none in Story). | `BattleFlow.cs`, `ArgumentStage.cs` |
| **The showdown** | Pressing: her words (`showdown.N` in the prose file) slide at Nadia's mark one at a time; A cuts a word in its window, a swing at nothing staggers you, a word you miss lands (a heartbeat). 4 words, +1 per anger, +2 on the claim; faster and closer together with anger. The press always lands unless composure goes first; the showdown replaces the harsh phase a press used to bring (the heat stays up). | `Showdown.cs` |
| **Pressure phases** | The engine's phases, now **6 s calm, up to 14 s furious** (6 + 2 x heat + 1.5 x anger), still fair by the standing rule (every attack warned or from outside, every spawn checked for an escape). New: a **key premise she holds on to** as it breaks (a phase at heat + 1); **gestures** (SELECTION 2): an angry or hurt reply brings a 4.5 s phase, a rattled one 3 s. | `PressurePhase.cs`, `ArgumentEngine.cs` |
| **The three rings** | "He called you first" (the painful premise) brings SELECTION 3's set piece: a phone at the table's edge rings three times; each ring sends a wave (1, 2, then 3 rings) that **hurts only a light standing still** ("answer me": Nadia stood still that night), with a few calm plates between. 14 s. | `PressureSpec.Call`, `PressurePhase` |
| **The last stand** | With every key premise down, the battle still won't end while "He called you first" stands unanswered: Lena says so ("No. You don't get to take him until you answer me.") and the banner reads "Lena wants an answer". Concede it, force it, or answer back. | `ArgumentEngine.LastStand` |
| **Delivery** | Every line typed letter by letter (~45 a second; beats after commas, longer after stops, a slow trail on "..."), voice blips per speaker. The `~` drawl: "ju~st" types out "juuust", slowly, with slowed, lower blips (each extra `~` adds two letters). The untyped part is laid out but transparent, so the box is sized for the whole line from the first letter and words never jump. Exchange lines hold the debate while they're typed and for 0.8 s after; the last one then stays to be read while play resumes. A statement said in flight never holds play. A line over 300 letters is said in pages (split at a sentence); every line in both files fits one page today (a test checks). | `Typewriter.cs`, `Delivery.cs` |
| **Her face** | Each of her lines shows its expression as it starts (a small lift; the old face cross-fades out over 0.22 s, never a pop), a talking bob while it's typed, then 1.1 s after it's done her face **eases back to listening**: neutral, or her folded-arms opening pose once she's angry. Nothing extreme is held while Nadia thinks. Each layer is boiled (Lily's boil, by mood). | `Portrait` in `Delivery.cs`, `ArgumentStage.cs` |
| **The end beats** | Convinced: she strikes her own claim through (SELECTION 8, "she erases it herself"). Heard out: it warms to gold. Pressed: it cracks. Yielded: it stands. Composure lost: the room has gone dark. Then the end card: outcome, stance, what was won, **the costs in red** (Nadia left the table / Despair +1 / Force +1), about how long it took. | `ArgumentStage.EndBeat`, `ArgumentHost.EnterEnd` |

## 3. Wrong choices cost (the creator: "I even made some wrong choices ... there was absolutely no gameplay")

| The choice | What it costs, at once |
|---|---|
| A card that doesn't touch what she said, or challenging something true | A heartbeat; the card **out of hand until the round after next**; her reply (her face reacts); the caption says what it cost. Heat +1 (the next round's pressure is denser). |
| A reason that doesn't follow (Because) | All of that, plus a pressure phase at the new heat (her anger sharpens it). |
| Hesitating past her patience | A heartbeat, the card out of hand, her line ("...That's what I thought."). |
| Evidence off the glowing words | Nothing but the moment (it glances off; the caption says hit the glowing words). |
| A whole round of silence (after the first round, which is for listening) | Heat +1, her line ("Well? Cat got your tongue, Nadia?"). |
| Pressing | The showdown (missed words land), anger +1 (Lena), Force +1, and her stance: she condemns. |
| Answering back (retort / antagonising card) | Anger +1, heat +2, a harder phase, and she condemns (the creator's two endings). |
| Conceding something false | That line is closed for good (Convinced may be out of reach). |

**Composure lost is a real end**: at 160 bpm the room closes in (2.4 s: the vignette, the heartbeat, the shake), Nadia
says the creator's line ("I can't... I can't breathe- I need to get out-"), Lena answers ("Go, then. You were always so
good at not being there."), and the battle is over: **no retry**, Lena condemns her, the game gets Despair +1 (through
`ArgumentResult.despairAdded`, as before), and the end card says "Nadia left the table". Concessions of true premises
still calm a step, so a careful player recovers from slips.

## 4. Timing (the target: Lena 8-12 minutes, 60%+ in active play)

Measured headless (dotnet, `BattleFlowTests.LenasBattleRunsEightToTwelveMinutesMostlyInPlay`), with the human-paced
autopilot playing like a first-time player: it hears each new statement out before acting on it, reads at ~11 letters a
second while dodging and ~18 over the reason cards, reads each card as it turns them, and makes a first-time player's
seven slips (three wrong cards, a glance off the words, two wrong reasons, a hesitation):

**Lena: Convinced in 544 s (9.1 min), 419 s in play, 125 s talk: 77% in play.** (Heart 106 bpm at the end; 12 rounds.)

An efficient player who knows the case finishes in about 4.5-5 minutes; the fast autopilot (tests, the house's skips)
in under 3 minutes of battle time, played at 4x. Walt's tutorial (the human-paced pilot with a wrong card, a wrong reason, a press and a phase stood still) runs about 3.5 minutes.

## 5. Walt's tutorial (Night 1), Mags teaching each mechanic in turn

Walt's file is written for real now (`walt_night1.txt`, header "WRITTEN BY THE BATTLE AGENT"): the gentle gatekeeper at
the foot of the hill, his ledger ("Sat. E., rear tyre, flat. Patched.", 41 times), the street that repeats. Four
premises: W1 he's having a good day (true), W2 nothing bad ever happens on Larch Street (the key: "You died in 2015,
Walt. February, the cold snap."), W3 come back tomorrow (optional: the same Saturday), **W4 the ledger** ("I patch his
tyre every Saturday. He rides off happy.": a page drifting through the debate, or once W3 falls; conceding it is his
own admission: "Forty-one times. Same tyre. ...You'd think I'd have looked for the thorn."). Mags, on the earpiece, a line
each the first time it comes up, with the player's own keys filled in: the debate (move, keep clear), ask (the first
statement), concede, challenge, Because, a wrong card's cost, the first pressure phase, press (the second misstep), the
showdown, the first break, the slip, let it rest. Gentler: sparser debate (0.4 density), slower pressure (0.7),
twice the patience, 0.75-speed showdown, and composure stops a step short (can't be lost).

## 6. The bedside (Day agent, 061b371) feeds Lena's battle

Through `DreamBattles.Context(evidence, battleId)` (the house's `StartBattle` already uses it), read with
`Chain.HasDreamChange`:
- **Day 2 "Lena"** (`n2_lena_wary`): her anger starts at 1 (sharper pressure from the start; her angry lines open up).
- **Day 2 "just sit"** (`n2_lena_softer`): a step gentler: the first thing that would anger her doesn't, and the
  debate's pressure is sparser (x0.85).
- **Day 1 "the phone"** (`n2_lena_sorry`): Nadia already said sorry at his bed: "He called you first" is on the table
  from the start (its three rings still come when the night would have brought it out), and the `sorry` meter switches
  in her ask reply ("You've already said it to him, haven't you? ...Then say it to me.") and Nadia's admission ("I said
  it to him today. I'll say it to you. I didn't pick up, and I'm sorry.").
`ArgumentContext.angerStart` (-1..1) and `.admitted` carry them; any battle can use them.

## 7. Staging and art direction

2D and cinematic, in the sprites' own style (Lena's and Walt's battle drawings are ink and screentone on paper): the
stage is ink black with a paper grain; her **kitchen table** (Walt: the step outside his shop) is drawn once in code as
halftone, with plank seams, the morning light pooling from the window, four place settings (Nadia's nearest, with her
place card), a coffee ring. One accent each: Lena's **wine red** (her pressure, her glow, which warms with her temper),
Nadia's **gold** (her light, the weak words, her heart monitor). Her pressure is sketched in shaky graphite first, then
inked (SELECTION 1). Plates are wine with a paper rim; the three rings are dark, jagged waves from a ringing phone.
Pressure words ("SIT DOWN", "MY KITCHEN", "SIX WEEKS" / Walt: "NOT TODAY", "TOMORROW") run faintly up her walls.
A camera on the whole stage drifts like a held shot, shakes on hits (Settings > screen shake; none with reduce
effects), leans in for the showdown, the panic and exchanges, punches in on a break. A drop-in painted table replaces the
drawn one: `Resources/ArgumentArt/stage_kitchen_table.png` (1200 x 640).

**3D at the table:** not in this milestone. The People agent's 3D Lena hasn't landed, and a 3D stage can't be checked
without running Unity; when she lands, the stage's portrait slot is the one place to swap (the flow already says which
face, when she's speaking and when she's listening).

## 8. What SELECTION.md picked, and where it is

| Pick | Status |
|---|---|
| 1 She sketches it first | Built: graphite warnings with a boil-like jitter, then ink |
| 2 A gesture for every reply | Built for angry / hurt (4.5 s) and rattled (3 s) replies; warm replies play over the debate itself |
| 3 Hush, and answer me; the three rings | The three rings built (answer-me waves); "hush" (rings that pass a still light) not yet |
| 4 She takes notes on you | Not yet (milestone 2) |
| 5 The seat she keeps | Not yet |
| 6 Pages from her notebook | The debate's slips are the start of it (they reveal, they don't yet become quotes) |
| 7 The sky | Not yet (the fallback, a night sky over the table after "He called you first", is the next piece) |
| 8 The last constellation | Its Convinced half (she strikes her own claim out) built as the end beat |
The selection had ruled out a Nonstop Debate ("judges will call it Danganronpa"); main's direction asked for one, so the
debate is built as this battle's own: her words fly because she's saying them, they push back, and the kitchen is the
arena.

## 9. Lines written this milestone (all marked "(battle agent)" in the files)

- **L5 rewritten** to the bad dream as settled: "He isn't hiding from out there. He's hiding from *him*." Half true,
  **can't be broken tonight** (carried into Night 3: the Doctor). Ask: "The man in the scrubs. The one who took every
  Saturday shift and called it duty, while his son waited at the window. He chose that man for years, Nadia. Now his
  hands won't even let him *be* him. So he stays here, where that man never comes home." Concede (Nadia): "...He was
  never any good at being off duty. Not even for you." / "No. So don't ask him to walk out of that door with you. You'd
  be asking him to put the scrubs back on." Can't-break: "With a child's crayons? He has to forgive that man himself,
  Nadia. Nothing you're holding tonight can make him." Press: "Then he faces him. He doesn't get to hide forever." /
  "Spoken like a true Lasname. You both think pushing harder is the same as being right."
- **Theo's drawing now breaks L3** ("He chose this dream ... he's happy here") and brings L5 out: "Happy men don't wake
  up shaking. Theo drew his bad dream, Lena: his dad in scrubs, walking out of the front door on a Saturday, while you two
  sit at the table." / "...So you went in his room. Fine. Then look at who's leaving, Nadia. He isn't hiding from out
  there. He's hiding from *him*." (with a beside and a non-sequitur card and replies).
- **L3.askAgainReply:** "...He has one bad dream. He wakes up shaking and won't tell me what it was. Theo knows. Theo
  draws everything."
- **The drawing's card:** "THE BAD DREAM", in crayon: Dad in his scrubs, walking out of the front door. Mum and Theo at
  the table. Found in his room.
- `composureLostReply`, `<id>.weak` for every premise, `showdown.1-8`, the barks (`idle`, `hesitate`, `glance`, `ring`,
  `stand`), `pressureWords`, the two `[sorry=1]` lines; **all of Walt's file**; Mags's twelve coach lines; the screen's
  new words (`screen.txt`, "The real-time battle").
- Every creator line kept word for word, "chose to yield" included.

## 10. Captures (main runs these; the outputs go to the judge)

From a Visiting Hours build (the argument prototype scene must be in it, as before):
```
VisitingHours.exe -battle-shot "<out>/battle-stills" -screen-width 1920 -screen-height 1080 -screen-fullscreen 0
VisitingHours.exe -battle-record "<out>/battle-run" -screen-width 1920 -screen-height 1080 -screen-fullscreen 0
VisitingHours.exe -battle-record "<out>/battle-run-walt" -battle-record-battle walt_night1 -screen-width 1920 -screen-height 1080 -screen-fullscreen 0
```
- **Stills** (24, `shots.txt` lists each and what it shows; log `JAMKIT_BATTLE_SHOT_OK` / `_FAIL`, a moment that never
  came is listed MISSING): 01 debate mid-flight, 02 the case file, 03 a wrong card, 04 Because, 05 a reply mid-line, 06 a
  break, 07 a pressure phase, 08 the three rings, 09-10 Convinced (end beat, end card), 11-12 the showdown and a cut, 13
  Pressed's end card, 14 her heart racing, 15 the panic, 16 composure lost (the lines), 17 its end card, 18-19 Heard out,
  20-21 giving in and Yielded, 22 Walt with Mags, 23 Walt's debate, 24 Walt's end card. About 10-15 minutes.
- **The recorded run**: Lena start to end in real time with the human-paced autopilot and the first-time slips, a frame
  every 0.5 s (`frame_00001.png`...), `timeline.txt` (each frame's time, state, bpm, round, heat, anger, line) and
  `summary.txt` (outcome, seconds, share in play). About 9-10 minutes, ~1,100 frames. Log `JAMKIT_BATTLE_RECORD_OK`.
- The boil capture (`-argument-shot`) still works.
- **In the editor:** the prototype's "watch it play" chapter plays the human-paced autopilot with the first-time slips.

## 11. Tests

| Suite | What |
|---|---|
| dotnet `VisitingHours`: `BattleFlowTests` (20) | The typewriter (one letter at a time, the drawl, balanced tags), pages, her face easing back, the debate's rounds, evidence on/off the weak words, the costs of a wrong card / reason / hesitation, Story's no-clock, the showdown, the three rings (still hurts, moving passes), composure lost as a real end with its cost, Walt can't be lost and Mags teaches in turn, the bedside wiring, **the 8-12 minute / 60% timing**, the fast autopilot |
| dotnet `ArgumentEngineTests`, `ArgumentNightsTests`, `ArgumentProseTests`, `PressureFairnessTests` | Updated deliberately: the painful phase is 14 s and the three rings; asking L3 twice points to the drawing instead of revealing L5; the drawing breaks L3 (Deepen to L5) and L5 can't be broken; Mags's first lesson is the debate; missteps are coached by the flow; Walt's file is written, not a placeholder |
| Unity EditMode `ArgumentPrototypeEditorTests` | The stage builds on black with her sprite and the real fonts; the dialogue box has no fixed height and nothing clips the line; the intro is timed; every state of a battle renders; the drawn table; the boil and its label on the stage; the capture flags |
| Unity PlayMode `ArgumentPrototypePlayTests` | It fades in and the debate runs; the whole battle's verbs on a gamepad alone (X, Y, RB, A, the d-pad, View, B, hold B, A cuts); the keyboard (1, 3, E / Q, hold 4); the ambience; save + A on the end card; Continue on the ending frame; the fast autopilot to the end |

## 12. Risks (unseen until main runs it)

- **Nothing has been on screen.** Layout, legibility of statements over the halftone, the hazard colours and the boil
  on two layers are all guesses until the stills come back.
- **UI Toolkit rich text:** the typewriter hides the untyped tail with `<color=#00000000>`; if the panel's text
  settings ignore 8-digit colour alpha, the tail would show (a quick fix: switch to `<alpha=#00>`).
- **Statement measuring** reads `layout` a frame after a statement appears; until then hits use an estimate.
- **The house's play test** (`TheArgumentBattlePlayedThroughDecidesTheOutcome`, House's file) expects the battle to
  finish within 150 s on the fast autopilot: it plays at 4x, so it should, but it hasn't been run.
- **Performance:** the arena's mesh is redrawn every frame (Painter2D); the boil now runs two layers during a 0.22 s
  cross-fade. Should be well inside budget; the frame-time probe will tell.
- **Old tools:** `ArgumentView` is gone; anything outside my paths that used it would break (nothing did at the time).
