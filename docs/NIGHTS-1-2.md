# Nights 1 and 2: what they are, what they hold, and the build plan (2026-10-12 night)

**Status:** the plan the agents build from overnight (2026-10-12 to 13). The goal is that by the end of 2026-10-13
**Night 2 is mostly finished** and **Night 1 is close to the finished product**, both playable as one chain:
opening, title, Day 1, the booth, Night 1, Day 2, the booth, Night 2, an end card.

**Precedence.** The creator's writing outranks this doc: their `//` notes in `STORY-PROSE.md`, their Night 2 battle
script (`Argument/Resources/ArgumentText/lena_night2.txt`, rewritten 2026-10-12), their Mags scene (STORY-PROSE
83-92) and their notes in `DESIGN-DECISIONS-STATUS.md`. The calls marked **[MAIN'S CALL]** are mine, made so the
work can start tonight. The creator can veto any of them in the morning, and every one is built so it can be swapped.
**All dialogue an agent writes is a placeholder** for the creator to rewrite, as they did with Lena's battle.

---

## The five nights, by what each one is for **[MAIN'S CALL, the frame Night 1's restructure waits on]**
Each night is one layer of Elias's mind, deeper and closer to the crash. Each has a guardian the dream uses to keep
Nadia out, opens one of the four things that hold him (STORY-PROSE 207-213), and moves Nadia one step.

| Night | Layer | What it's about | What Nadia learns | What it opens | Guardian | Where Nadia is |
|---|---|---|---|---|---|---|
| 1 The Street | The refuge: the street they grew up on | **"This is memory, and it repeats."** | The rules: it's a dream, it loops, the dead live here, only the truth is on her side | Nothing yet; it seeds **Purpose** (Walt, the man who fixed things) | **Walt**, a gentle gatekeeper | Sure of herself: this is a rescue |
| 2 The House | The family he's protecting | **"What he's keeping."** | She's the intruder. He called her first. Lena won't let him go | **Lena** and **Theo** (opened, not released) | **Lena** | Tempted: she sits down for pancakes |
| 3 The Shift | The man | **"Who he was."** | The remark, his hands, the hiring file | **Purpose** | **The Doctor** (Elias himself) | Proud and hurt |
| 4 The Road | The night | **"What happened."** (the truth: undecided, pitches A and B) | The truth of Route 9 | **Guilt**; Theo's blessing | None, a chase | Ashamed |
| 5 The Car | The decision | **"What he wants."** | — | All four, granular | Elias, not fighting | Decides |

---

## Night 1: The Street

**What it represents:** the place Elias goes to feel safe: the street of his childhood, at golden hour, where Walt fixed
anything and nothing bad had happened yet. **The player's job:** learn the rules and find him. **The emotional beat:**
she finds him, and the dream closes the door on her.

### Day 1 (waking, style E, about 6-8 minutes, guided: no time slots)
1. Handover with **Rafael** (an intern now): one line, and the first duty round as the tutorial (wristband, meds,
   vitals).
2. **Room 14:** the chart ("Charting: GCS 6..."), the effects bag (the keyring tagged "W.A. workshop", the memorial
   card for Walter Abernathy, 1941-2015), the family photo, LeBaget's bouquet ("Community Partnerships").
3. **The station:** the police envelope. The call log reads 3:03 to Nadia, unanswered (the opening's phone), then 3:06 to Lena, 2 minutes.
   Nobody comments on it.
4. **The phone:** "Not now."
5. **11 PM, the booth:** **the creator's Mags scene, word for word** (STORY-PROSE 83-92: "Dream Walking isn't that
   easy...", "Five Nights, maybe Six...", "only the Truth is on your side", "stay calm"). The phone-charging ritual is
   dropped (the creator rewrote the scene without it).

### The dive: Larch Street, on a hill **[MAIN'S CALL, from the creator's "unless you're willing to work vertically and set it on a hill"]**
- **The shape:** the street climbs a hill in two switchbacks. **Walt's bike shop** is at the bottom, by the creek.
  **The Lasname house** with its tower (the round 5 house) is at the top, and it's **visible from everywhere**: it's
  the landmark you navigate by, the way the Distillery's chimney is in Dunwall. From the bottom you see the whole
  climb; from the top you see the whole street. That answers "it didn't reveal how large the setting was".
- **Layout A's four routes, laid on the slope:** the road (switchbacks), the garden stairs and back lanes (short, steep,
  hedged: Night 2+ stealth space), the rooftops (stepped down the slope, so double jumps chain uphill), and the creek
  ravine (slide under the culvert). The deep-block greybox content is reused, not thrown away.
- **Movement taught by the climb:** the creek she jumped at seven is the first double jump (Mags: "You're lighter in
  here."); the shop's half-open garage door is the first slide.
- **Elias, glimpsed:** at the barbecue in the top garden, always just too far to reach (the creator kept this).

**Memories (5), and what each one is for:**

| Memory | Where | Feeds |
|---|---|---|
| Walt alive, waving | The shop | `ded_walt_dead` (with the memorial card from Day 1): **the key deduction** |
| Saturday's paper | A porch halfway up | `ded_same_saturday`, and **Night 2's `ded_saturday_off`** |
| The neighbours' loop (the sprinkler, the dog's ball, the same line, on a timer) | Along the street | `ded_same_saturday` |
| Walt's ledger (optional, inside the shop) | The workbench | The count of nights (see "the flat tyre" below) |
| The house on the hill, the porch | The top | The last beat |

- **Deductions:** `ded_walt_dead` = "Walt died in 2015. This is memory." (key); `ded_same_saturday` = "He's living one
  Saturday, over and over." (carried into Night 2's battle).

### The argument tutorial **[MAIN'S CALL; replaces Walt's tutorial as built, which the creator rejected]**
- **Who teaches: Mags, on the earpiece.** She's the one who knows the rig, and her briefing already says "only the
  Truth is on your side". She coaches each verb the first time it's needed (Ask, then Challenge with evidence, Concede
  a true premise, Press), in a line or two. **Walt doesn't teach**: he argues, which is what was wrong before (a
  guardian coaching the intruder against himself).
- **Why Walt argues:** he's the gatekeeper at the foot of the hill. "Your brother's having a good day. Let him have it.
  Come back tomorrow." It's the dream's gentlest defence. He's kind, and he won't let her up.
- **The battle:** small. Three premises: one true fact (concede it), one false key link (broken by `ded_walt_dead`),
  one optional (broken by `ded_same_saturday`). Gentle pressure; composure can't drop below 1 (the tutorial can't be
  lost). The usual outcomes; Walt steps aside in all of them, and how she treated him is remembered for the ending.
- **Text:** `ArgumentText/walt_night1.txt`, in the same format as Lena's, **an agent placeholder** for the creator
  to rewrite.

### The ending **[MAIN'S CALL; replaces the stage-strike exit]**
No chase. Walt steps aside; Nadia climbs to the top (the vista of the whole street at golden hour is the night's
reward and its peak). At the garden gate she sees them through the window: Theo setting the table, Elias at the stove.
Elias looks up and sees her, and **Lena steps between them, takes him inside, and closes the door.** The porch light
goes out. Mags: "His heart's up. That's enough for tonight. You found the house." The last image is the shut door.
This sets up Night 2 in one shot: the house is the goal, and Lena is keeping him from her.

**The night's clip:** the rooftop route up the hill with the tower ahead, at golden hour.

---

## Night 2: The House

**What it represents:** the family Elias is protecting, and the temptation it is for Nadia too. **The player's job:**
get in, find what the house hides, and face Lena. **The emotional beat:** she sits down for pancakes with the family
she lost, and finds out she's the nightmare. This is the creator's re-plot (STORY-PROSE 124-140) and their battle.

### Day 2 (waking, short)
- **Rafael**, if she talks to him: he was on duty when they brought Elias in. Elias said one thing: "Did Lena get out?"
- **The unwelcome visitor:** LeBaget's "Community Partnerships" representative (the creator: "It can be LeBaget"),
  with fruit he can't eat and a gentle "Has he said anything?". **No transfer clock** (the creator questioned it).
- One duty round. **Room 14:** Nadia can sit and talk to him (this is what the dream hears: see "the bad dream").
- The booth, then the dive.

### The dive
1. **The street (short):** "Morning, Nadia!" The neighbours know her name now. **The loop, seen:** the street replays
   Night 1 to the second (the same sprinkler, the same ball, the same line), so the player sees the repeat for
   themselves. Walt waves her up, warmly or coolly, by how Night 1 went.
2. **The house misbehaves** (the movement intro): hallways stretch, doors lead back to the kitchen; the stretched
   upstairs hallway teaches the **wallrun** over carpet, then over the stairwell.
3. **Pancakes:** Lena and Theo, the table set for four, **the fourth chair is Nadia's** (her place card, in Theo's
   handwriting). She sits down, thinking Elias will be down soon.
4. **Hide-and-seek with Theo** (the stealth intro, 3-6 minutes). Memories are scattered through the house, with a
   **counter** ("2 of 5"). When all are found, the creator's line: *"I'm not sure if there's anything left to find. I
   could let Theo find me here..."*, ending:
   - **normal route:** "...but what does it matter, when he's not even real?"
   - **Despair route:** "...it would be nice to see his smile again."
   **[MAIN'S CALL on which is which:** the Long Night is the pull of the dream family, so the longing line leads into
   it; the clear-eyed one belongs to the Nadia who's there on a job.**]**
5. **Theo's first question,** when he finds her: "Is Dad going to be sad?" (honest, comfort or silence). He remembers.
   **Theo stays in the game** (the creator: "I think he may need to stay").
6. **Elias never comes down.** Nadia asks Lena where he is. Lena admits she won't let him go. **The battle.**
7. **The house tips** (the escape, ~75 s, 4 checkpoints): the kitchen floor tilts, the hallway turns over while she runs
   it, the stairs flatten into a slide. **The front door:** the closed umbrella beside it (the Dream Tool); without
   it, the Despair route continues. Lena watches from the window.

### The memories, mapped to the creator's battle

| Memory (found in hide-and-seek, or carried in) | The evidence card | What it breaks in `lena_night2.txt` |
|---|---|---|
| The fourth chair: Nadia's place card | `mem_fourth_chair` | **L1b** (KEY): "There's a fourth seat at this table... And I'm still out there." |
| The fridge calendar: "Pancakes!" on every **Sunday** + Night 1's **Saturday** paper → `ded_saturday_off` | `ded_saturday_off` | **L3b** (KEY): "Sunday Pancakes. Except, it's not Sunday... It's Saturday." |
| The street replaying Night 1 (and Walt's ledger, if found) → `ded_same_saturday` | `ded_same_saturday` | **L3c** (KEY): needs a line rewrite (see "the flat tyre") |
| Theo's drawing, "THE BAD DREAM", **found in his room** (no longer slid across the table: the creator found that out of character) | `mem_bad_dream` | **L5** (HALF) |
| Theo's height marks, stopping at ten | `mem_height_marks` | **L1** (TRUE: concede it) |
| The photo wall: four of them at the lake | `fact_photo` | Worldbuilding; the "four" includes Nadia |
| Day 1's police report, the call log | `fact_police_report` | **L4** (TRUE, PAINFUL: "He called you first" stays, as the creator's script has it) |

**Convinced** = L1b, L3b and L3c broken. **L2b** can't be broken tonight (it waits on the Night 4 truth).
**Unsure / condemns** (the creator's two endings for Lena): Convinced or Heard out leave her unsure, open to giving her
blessing later; Pressed, an antagonising card that lands, or composure lost leave her condemning Nadia, and the
Despair route gets easier to reach.

### What the battle needs built (from the creator's notes in the script)
- **`//` notes are notes:** never shown. Where the creator's note is a replacement line (the claim, the opening,
  `noTouchReply`), the note's line is used.
- **`*word*` is bold.**
- **Conditional lines**, on three meters:
  - **Lena's anger** (this battle: antagonise, retort, press raise it);
  - **Force** (the game-wide tally);
  - **Despair** (the hidden value).
  The variants the creator asked for: `L1b.nonsequitur.reply`'s second half; `L1c.trueReply` when she's angry or near
  condemning; `L2.pressLine`'s tamer form when Nadia's been gentle; `L3b.beside.reply`'s second half only at max
  despair with low anger and force; `L3b.nonsequitur.reply` at max anger and force ("I should cut your tongue out for
  that."); `L4.concedeReply`'s addition at low anger, no despair. Format: `id[condition]: text`.
- **The evidence cards rewritten** to the table above.
- **Blanks to flag, not fill:** `L1c.askReply` is empty. Typos to ask about: "flonudering", "with all his hearts",
  "Are you sure you're feeling Nadia?".
- **Lena's keepsake** (Heard out) **[MAIN'S CALL, placeholder]:** her **planisphere**, the star wheel, set to her
  last Thursday night. It's what Nadia brings to the stargazing in Morning, chosen.

### Two meanings the creator asked about
- **The bad dream [MAIN'S CALL, proposal]:** Nadia sits at Elias's bed every day, in scrubs, talking to him. That
  leaks into the dream as a nightmare inside it: *a woman in scrubs who comes to take the family away*. It's waking up,
  wearing her face. Waking is where Theo is dead and Lena is gone, so to the dream she's the thing that takes them.
  That's why L5 is only half true: he does fear her, but she isn't what took them. The crash did.
- **The flat tyre (L3c):** in the old Night 1, every Saturday a young Elias wheeled his bike to Walt with a flat rear
  tyre; Walt patched it and wrote it in his ledger. Because the day repeats, the ledger has the same line 41 times and
  Walt never notices. It proved the loop and counted the nights. The new Night 1 proves the loop by letting the player
  see the street repeat; the ledger stays as an optional find. **L3c's line needs the creator's rewrite** around that.

---

## Who builds what tonight (file ownership)
| Work | Agent | Owns |
|---|---|---|
| Lena's battle (comments, bold, conditions, cards, the anger meter), Walt's tutorial battle + Mags's coaching | Argument (a385c228) | `Argument/**`, `ArgumentText/*` (once the creator's tab is closed) |
| Night 2 house flow: the place card, calendar, Theo's room and drawing, the counter, the line, Theo's question, Elias never comes, the hand-off to the battle, the tip, the umbrella, Lena at the window | House (a953eef) | `Dream/Scripts/House/**`, the house builder |
| Night 1: Larch Street on a hill (layout A on a slope, the tower at the top), memories, Walt's shop, the ending | Movement (abd54a5) | `DeepBlock/**` and a new `Dream` street scene |
| The waking days (Day 1 and Day 2 in style E), the booth with the creator's Mags scene, and **the chain** from the opening to the end card | Opening (a40885a) | `Scripts/**` outside Dream and Argument, `Opening/**`, the VH scene |
| Walt's battle sprites, to match Lena's version A | Sprites (a097a2a) | `Argument/Resources` art + the review page |
| Docs: swap `_pending` in once the tabs are closed; point VISITING-HOURS.md here | Docs (adf8db0) | `docs/**` except this file and the open tabs |

Unity runs (Edit, PlayMode, captures, builds) go through main, one at a time; agents hand the commands back.
