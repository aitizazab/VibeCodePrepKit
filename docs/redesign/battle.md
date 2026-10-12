# The argument battle, redesigned (Battle agent, 2026-10-12; round 2, 2026-10-13)

> **Status: round 2 built, not yet run in Unity.** Core green in dotnet (VisitingHours 181/181, All 856/856); the
> Unity layer compiles against Unity's DLLs (UnityLayerPlayer, UnityLayerEditor, UnityApi). Nothing here has been seen
> on screen yet: main runs the captures (section R7), a judge scores them, and this doc follows the findings. **Round 2
> (sections R1-R9, first) supersedes milestone 1 (sections 1-12, kept below) wherever they disagree**; milestone 1's
> debate is still playable behind the presenter switch. Older docs (`ARGUMENT-PROTOTYPE.md`, `ARGUMENT-BATTLES.md`) stay
> as history for the logic.

## Round 2: arguing and dodging take turns

The creator, 2026-10-12 (docs/REDESIGN.md section 8): *"I don't think the Debate style is worth copying anyways. Putting
it into the bullet-hell portion of the fight doesn't make the argument any more enticing, it should probably stay within
its own section, with only a slight overlap. Either way, I'd have to play it to be sure."*

### R1. The structure

A battle is now two sections taking turns, each its own kind of play, with a slight overlap at each seam:

1. **The argue section** (new state `Argue`). Her case is on the table: in the kitchen, a page of her notebook, each
   statement written where she put it (the claim at the top, what holds it up below, what hangs from each under it),
   pencil lines from each up to what it holds up. Nothing comes at Nadia's light. The stick (d-pad, WASD, arrows) steps
   from statement to statement, the mouse points at one; the one picked lifts, gold-edged, and is spelled out in full
   above the table (the words that matter in gold, its kind and state, her question if she's put one, what she said when
   asked). The same buttons act on it: X asks, Y concedes (held on the claim: give in), A shows the card in hand (LB / RB
   turn the cards), hold B forces it, View opens the case file. A card shown goes to "Because..." as before.
   **Her patience is the clock** (20 s plus 2.5 s for each statement standing, at most 34 s; a fifth less once she's
   angry; none in Story or Walt's tutorial): a pencil line draws itself along the near edge, and in its last 3 s she
   sketches the attack that's coming at the table's edges (the overlap into the dodge). If Nadia says nothing, it comes:
   her temper rises and a phase plays ("Well? Cat got your tongue, Nadia?").
2. **The talk**: the exchange is said, one line at a time, in the box at the bottom.
3. **The dodge** (`Pressure`): **every exchange's reply comes as a dodge** shaped by its feeling (SELECTION 2, now a
   section of its own): angry or hurt 10 s, rattled 8 s, softened 7.5 s (with a hush: her care), anything else 7 s
   (with an answer-me wave when her question stands on what Nadia acted on). Mistakes and key breaks still bring the
   full phases (6 s calm up to 14 s furious), the three rings 14 s. **Her line is said in a speech bubble over her while
   Nadia dodges** (the creator's note on SELECTION 2), and the reply's last words are still being read as the dodge
   starts (the overlap). The phase's last 1.2 s is empty; then the argue section is back, the statement last picked still
   picked.

The opening: her lines, the "laying the table" phase, then the first argue section. Walt: no opening phase, no clock.

### R2. Why each section is engaging on its own

- **Arguing** isn't a menu: **her own words are cards you catch as she says them** (SELECTION 6, "Pages from her
  notebook", done properly). When a line of hers holds words worth holding her to, they're underlined in her wine as
  they're typed and "A: hold her to it" pulses; A in that window (the words being typed and 1.2 s after) catches them:
  a torn page flies to the hand, "HER OWN WORDS". Too soon and they're gone (the first time ever, a press too soon is
  only ignored; nobody knows yet); in Story they collect themselves. Two in Lena's battle, both second ways to break a
  key premise, so listening well is a real alternative to the memories found in the house:
  - **"this life he had"** (the creator's L3.askReply, asking L3 why) against L3' "This is the life he wants. The life
    he has.": had, not has.
  - **"All of us"** (the creator's L3b.askReply) against L1' "So there's nothing left for him out there": four places
    at that table, and one of them is Nadia's.
  Plus: reading her case as a whole (what holds what up, what's key, what's true), choosing the card and the reason under
  her patience (Because), and her patience itself, which makes dithering cost.
- **Dodging** has its own vocabulary now, each one readable and each one hers:
  - **Hush** (SELECTION 3): a soft, round, dashed ring of her care closes in on the light and passes harmlessly **if
    Nadia holds still**; struggling hurts. Shown to the player by shape (dashed, soft paper, closing in, against the
    jagged dark answer-me waves going out), by a ring round the light that's whole while it holds still and broken while
    it moves, by her "Shh. Hold still, Nadia. Just this once." and the prompt "Hold still. Let it pass." the first time,
    and the first one can't hurt (a lesson on a clear table, nothing else in the phase). Nothing else spawns while a
    hush closes.
  - **Answer me**: a wave from her side of the table that hurts only a light standing still (the three rings' rule,
    smaller), with her questions; the first can't hurt ("Don't stand still: answer her.").
  - **Her notes** (SELECTION 4, as the creator read it: "a pencil attack that trails behind you, trying to catch up"): from
    the second full phase, her pencil follows the light's own path 2.2 s behind, closing to 1 s; every mark is pencilled
    (0.6 s) then inked (1.3 s). A light that stops is caught; one that uses the whole table never is. After it, one line
    about how Nadia moved (each at most once, never in Story): "You always keep your distance, Nadia." / "You stayed close
    to me. ...I noticed." / "Never still, are you. Not for one minute." Kitchen only (on her sky's lines, with their dead
    ends, a pencil on your heels would be a trap).
  - **Her sky** (SELECTION 7, the second phase the creator wanted): after the three rings of "He called you first" **the
    ceiling lifts** (3 s: her night comes down over the table, which sinks away; "You know they're not really a bear,
    right? Someone just looked up and drew the lines."). From then on her case is a constellation: the same places are
    stars, her reasoning is lines, and **in every phase the light moves only along her lines** (Undertale's strings),
    switching at a star (the first time: "Move along her lines. Switch at a star."). The sky is the argument's state:
    shooting stars come from a radiant beyond one upper corner, all at one angle, **only along the lines still lit**
    (statements standing); a broken or conceded link is a faint rubbed-out trace (walkable, nothing runs on it); a fact
    Nadia conceded glows and **stops streaks** (cover); a forced link **snaps** (a gap, splinters). Arguing well makes the
    sky quieter and safer. The fairness planner works on the lines (its grid masked to them).
- **The seat she keeps** (SELECTION 5) is **not built**: with the sections apart, its choice (sit safe and hear nothing,
  or leave it for a page) has nothing to choose between, since pages are caught by listening now, not in the box.

### R3. Timing (measured headless: `SectionsTests.LenasSectionsRunEightToTwelveMinutes`)

A first-time player (the human-paced autopilot: reads her case at ~11 letters a second, weighs each card, slips once on a
statement: three wrong cards, two wrong reasons, a hesitation, one of her quotable lines let go by):
**Lena: Convinced in 490 s (8.2 min): 142 s arguing, 165 s dodging (34%), 65 s Because and the showdown, 118 s talk;
76% in play; 21 exchanges, 21 dodges; the sky opens at 2:33, so more than two thirds of it is played under her sky.**
The fast autopilot finishes in 94 s of battle time (the house's skip plays it at 4x: about 24 s). Walt's tutorial
(sections, the slips Mags teaches through): 155 s.

The run (seconds into the battle): the opening dodge (6 s) · ask L1 · its dodge (7) · concede L1 · hush (7.5) · ask L1'
· dodge · a wrong card · its phase · the fourth chair breaks L1' · she holds on (10 s, her notes) · ask L1'' · concede it ·
**the three rings (14 s)** · **the ceiling lifts (153 s)** · L2 asked (an answer-me wave) and conceded (hush) · L2' ·
L3 (a wrong card, then the drawing breaks it) · L3' (her "All of us" caught; a wrong card; Saturday off breaks it) · L3''
(the same Saturday) · L4 conceded · Convinced.

### R4. The presenter switch

`ArgumentPresenter { Sections, Debate }` (core, `BattleFlow`). Every battle is played in `ArgumentHost.DefaultPresenter`:
the command line's `-battle-presenter debate|sections`, else the player's choice (PlayerPrefs
`jamkit.argument.presenter`), else the sections. **The prototype's menu has a line "Battle style: ... A to switch"** that
flips it for the whole game (the house's Lena and the hill's Walt too); the chapter "Dream-Lena: the debate (milestone
1)" always plays the debate. `ArgumentHost.Begin(def, setup, done, presenter)` and `UsePresenterForSession` (tests,
captures) pick one directly. The debate is exactly milestone 1 (its tests run on it unchanged), plus the quotes.

### R5. Lines written this round (all marked "(battle agent, round 2)" in the files; every creator line kept)

- **The watch, handed over** (`letItRestReply`; the creator's first half kept, their second half, "So take it.
  Something to help convince him, like you convinced me.", kept as its // note): *"...Yeah. And I see yours. Give me
  your wrist. The right one. I always wore it face-in, so I could check the time without anyone seeing me worry. Six
  weeks you've been watching the clock for him. It's yours now."* What they share: the worry, watched in secret
  (face-in, as her sheet draws it), and the clock (Night 4's 3:03 and 3:06). If Nadia asked about her Thursdays, she adds
  *"It kept my Thursdays, whatever they were."* `personalText`: *"Lena's watch: a gold star on a wine-red strap. You wear
  it on your right wrist, face-in, as she did."* Then the creator's heardOutReply.
- **Her star wheel** against L2 (an aside: it breaks nothing, costs nothing): Nadia *"Your star wheel was set to a
  Thursday night. Where did you go on Thursdays, Lena?"* Lena *"Thursdays. ...I don't know. Isn't that strange? It's my
  wheel. He never asked where I went, so I can't tell you either."* (Dream-Lena knows only what Elias knew; decides
  nothing about what the Thursdays were.)
- **Her own words' reason cards**: L3' with "this life he had": *"You said it yourself: the life he had. You already know
  it's over, Lena. This is a memory of it."* / *"...Had. I did say that, didn't I. ...Fine. Then let him have his wish."*
  (beside: "You've been married twelve years." / "Twelve good years. That's a reason to keep him, not to wake him."; doesn't
  follow: "You said "had", so you've stopped loving him." / "I said "had" because it was taken from him. Don't you twist
  me into the one who stopped loving him."). L1' with "All of us": *"All of us, you said. There are four places at that
  table, and one of them is mine. I'm out there, Lena. That isn't nothing."* / *"...All of us. You'd hold me to that.
  ...Then where were you last Sunday?"* (beside: "He always loved Sunday pancakes." / "He did. That's my point, Nadia, not
  yours."; doesn't follow: "You want all of us together, so you want him to wake up." / "I want all of us here. Out
  there, there's no all of us left.").
- **Barks**: the sky, the first hush, the three notes lines (above).
- **Mags (Walt's file)**: `coach.argue` (the sections' first lesson), `coach.quote`, and `[sections=1]` versions of
  ask, concede and challenge (no "put your light on it" in the sections). A new meter, `sections` (1 in the sections),
  and `asides` (cards that got an answer of their own) read in conditional lines.
- **Screen**: the argue keys, the quote prompt and its notes, hush / answer / sky / notes prompts, the banners, the
  menu's battle style line.

### R6. The cards (docs/redesign/house.md's new memories)

| Card | Now | In the battle |
|---|---|---|
| `mem_lena_thursdays` | **Stays a star wheel** ("her star wheel": Lena's planisphere in the attic, set to a Thursday night. Where did she go on Thursdays?). The keepsake is the watch now, a different object that she gives; the star wheel is one the house keeps, and it points at time too. | An aside against L2: she can't say. Costs nothing; changes her hand-over line. |
| `mem_bad_dream` | The redrawn picture: "Dad in his surgeon's scrubs, walking out of the front door. Mum and Theo at the table." | Breaks L3, brings out L5 (as before). |
| `ded_same_saturday` | Elias's radio (the same forecast, word for word) or the street replaying Night 1. | Breaks L3''. |
| `ded_saturday_off` | The fridge calendar plus today's paper, folded into Theo's hat (or Night 1's paper). | Breaks L3'. |
| `quote_had`, `quote_allofus` | Her own words (new). Never in the tray until caught, whatever the setup gives. | Second ways to break L3' and L1'. |
| `pers_lena_watch` | The keepsake (replaces `pers_lena_planisphere`; `DreamLenaDemo.Planisphere` is kept as the same id for code that reads it). | Heard out. |

**The keepsake in the save:** when Heard out with Lena is saved, `ArgumentHost` sets the save's flag `vh_lena_watch`
(`ArgumentBattles.KeepsakeFlag`, the same text as the Day agent's `Chain.KeepsakeWatchFlag`; `ArgumentBattles.OwnsKeepsake`
reads it or the ledger). **World** shows it with `DreamStreetGame.SetKeepsakeOwned(ArgumentBattles.OwnsKeepsake(save))` (or the Day agent's `Chain.HasLenaWatch`, which reads the same flag) when the hill
loads (World's file, not mine).

### R7. Unity steps (main; one GPU job at a time)

1. **Import** (or let the build import): Unity reads the two new scripts' metas (`CaseMap.cs.meta`,
   `SectionsTests.cs.meta`, copied from neighbours with fresh GUIDs).
2. **Build** the Visiting Hours player as before (the argument prototype scene must be in it).
3. **Stills, the sections** (31, `shots.txt`):
   `VisitingHours.exe -battle-shot "<out>/battle-stills" -screen-width 1920 -screen-height 1080 -screen-fullscreen 0`
   01 the argue section, 02 the case file, 03 a wrong card, 04 Because, 05 her words underlined (the prompt), 06 caught
   (the page flying), 07 a reply as a dodge with her speech bubble, 08 hush (the light holding still), 09 her notes (the
   pencil following), 10 a break, 11 the three rings, 12 the ceiling lifting, 13 a sky phase on her lines, 14 her case as a
   constellation, 15-16 Convinced, 17-19 the showdown and Pressed, 20 her patience running out (the sketch), 21-23
   composure, 24 the watch handed over, 25-26 Heard out, 27-28 Yielded, 29-31 Walt with Mags. About 15-20 minutes.
4. **Stills, the debate** (milestone 1's 24, for the comparison): add `-battle-presenter debate`.
5. **The recorded run** (sections): `VisitingHours.exe -battle-record "<out>/battle-run" -screen-width 1920 -screen-height 1080 -screen-fullscreen 0`
   (about 8-9 minutes; `summary.txt` now has the presenter, the seconds arguing and dodging, and whether the sky opened);
   Walt: add `-battle-record-battle walt_night1`; the debate: add `-battle-presenter debate`.
6. **Tests**: EditMode `ArgumentPrototypeEditorTests` (the stage draws both presenters through to the end, with her sky;
   seven chapters) and PlayMode `ArgumentPrototypePlayTests` (the debate's tests pinned to the debate; new: the sections on
   a gamepad alone, the sections' fast autopilot to her sky and the end, Heard out setting the keepsake flag).
7. The house's `TheArgumentBattlePlayedThroughDecidesTheOutcome` now plays the sections by default (the fast autopilot:
   94 s of battle at 4x).

### R8. Perf's list (docs/redesign/perf.md, Battle 1-3)

1. **Per-frame strings** (`ArgumentStage`): her temper, the case strip's chips, the heart rate, the Because label, the
   showdown's count and words, the pressure words' capitals and the placeholder line are rebuilt only when their values
   change; the sections' new text (her case's notes, the statement picked, her bubble, the quote line) likewise.
2. **Repaints**: her glow (the backdrop) only when her heat or anger changes; the monitor at 30 Hz; the table every frame
   (its hazards move). Her patience bar (Because) is a scale, not a width (no layout).
3. **Blips** (`ArgumentAudio`): each speaker's clip found once (no string per blip; the host lowers a speaker's name once,
   not per blip), and every voice's first blip is loaded and played silent before the first line (`Prewarm`).
`VhBattleView`'s readable textures (perf.md item 22) are in `VisitingHours/Scripts/UI`, not the battle's folder: not
touched.

### R9. Risks (unseen until main runs it)

- **Nothing has been on screen.** Her case's layout on the page (12 notes at most, wrapped at 240 px), the statement
  picked above the table, the bubble over her name, the sky texture and the constellation, the hush and the pencil are all
  drawn blind.
- **Rich text**: the notes use `<size=70%>`, the quote line `<u>` and `<color>`: if UI Toolkit's text settings ignore one,
  it shows as plain text (no clipping).
- **The timing sits at 8.2 minutes** for the first-time autopilot: a real first-timer who reads more slowly or slips more
  will be longer; an efficient one about 5. The measure is the autopilot's, not a person's.
- **The lines are mine**: the hand-over, the Thursdays, the quote reasons and the barks wait on the creator's pass. The
  "this life he had" quote depends on the creator's L3.askReply keeping "had" (a test checks each quote is in one of her
  lines).

## Milestone 1 (2026-10-12): the debate (now behind the presenter switch)

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
