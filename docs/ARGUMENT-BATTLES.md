
# *Visiting Hours*: argument battles, rebuilt from the ground up

> **Status: a proposal for the creator (overnight draft, 2026-10-11; brought in line with the creator's notes on
> 2026-10-12).** The creator's own writing (their `//` notes and prose in `STORY-PROSE.md`, their scripts, their
> endings) outranks everything here. Replaces the slice's battle (the box with a Listening half and a Pressing half,
> objects drifting in to be touched; `SLICE-VISITING-HOURS.md`). The story context is `STORY-BIBLE.md`; who's arguing
> is `CHARACTER-BIBLE.md`; when each battle happens is `NIGHTS-GAMEPLAY.md`. **All quoted lines are placeholders**
> that show the logic each line must carry; the creator writes the words.

## Decisions for the creator
| # | Decision | Recommendation |
|---|---|---|
| 1 | **Which concept** (section 2) | **A, "Take it apart"** (approved by the creator: the prototype's "structure isn't bad"): the opponent's case is a physical object in their arena, built from labelled premises; Nadia breaks premises with evidence **and a stated reason** |
| 2 | **Reasons as a choice of three** ("because..." cards: one sound, one true but beside the point, one that doesn't follow) | Keep: it's the step that makes the player say *why*, which is exactly what the old box never asked |
| 3 | **Keep a short action part** (pressure phases: 5–8 s of the opponent's emotion, themed to them, played on their arena's surface) | Keep, but only as a consequence (wrong reasons and pressing make it worse; good arguing makes it calmer), and with a meaning: **slips** in the pattern reveal what they aren't saying. The creator's rule: except in the Long Night, every attack enters from off-screen or is telegraphed. They find the dodge phase small next to the thinking |
| 4 | **Composure is Nadia's heart rate** (70 to 160 bpm in five steps) | Keep: a nurse would read it that way, and it's legible to a judge. It fits Mags's "stay calm" |
| 5 | **Press is a hold** (0.6 s), never a click | Keep: forcing should be a decision, never an accident |
| 6 | **"Let it rest" is how you get Heard out**: available once you've heard every premise | Keep |
| 7 | **Tell** (offering a truth instead of attacking) arrives on Night 4 with Theo | Yes: Theo stays (the creator) |
| 8 | **No pressure phases in waking-world arguments** (only the dream turns feelings into things) | Keep |
| 9 | **The final talk decides the ending** | The endings are the creator's (`STORY-PROSE.md`, PROSE:56) and Elias's decision is granular; see section 5, "The final talk" |

---

## 1. What was wrong, and what an argument actually is
**The old box** asked the player to stand in the green or the red half and dodge rectangles, and to touch an object
that "related to" a statement. Nothing on screen said **why** a piece of evidence answered a statement, and the
opponent never answered back with a reason. It conveyed the mood of an argument, not an argument.

**A real argument** (in plain words, after Toulmin's model of claims, grounds and warrants):
- a **claim**: what they want you to accept ("He's fine here. Go home.");
- **premises** holding it up, of two kinds:
  - **facts**: things about the world that are true or false ("I've always been on this street.");
  - **links**: the "so" that turns facts into the claim ("Happy is the same as fine.");
- some premises are **hidden** (assumed, unsaid), and the real disagreement is often there;
- you answer a premise with **evidence plus a reason**: "You said X. But this shows Y. So X is false.";
- the other side answers **logically**: they concede, fall back to a weaker premise, dig down to a deeper one, or
  point out that your reason doesn't follow;
- and sometimes the honest move is to **concede** a true premise and fight the link hung on it instead.

Every battle in this design is built from those parts, and every step is shown as a sentence a judge can read.

## 2. Three concepts
| | **A. Take it apart** (chosen) | **B. Testimony** | **C. The floor** |
|---|---|---|---|
| **Idea** | The opponent's case is an object in their arena (Walt's wheel, Lena's breakfast table, the Doctor's lightbox). Premises are its pieces (spokes, plates, X-ray films). Ask, challenge with evidence and a reason, concede, press, or let it rest. Short themed pressure phases in between | Cross-examination: the opponent's statements scroll as testimony; press a line for detail or present evidence at a contradiction (the *Ace Attorney* model) | First person, in the real 3D arena: premises are objects in the room; you run (with the movement system) to carry evidence to them while the room moves with their emotion |
| **Logic on screen** | High: structure, reasons and replies are all visible | High for single contradictions; no structure (one line at a time) | Low: fetching hides the reasoning |
| **Action** | Short, meaningful (slips reveal subtext; bad arguing makes it worse) | None | High |
| **Originality** | A new mix: argument mapping, skinned per character, with emotional patterns | Judges will know *Phoenix Wright* | High |
| **Unique arenas** | Built in: the arena *is* the argument | Backdrop only | Built in |
| **Teach in one battle** | Yes | Yes | Hard |
| **Build (UI Toolkit + core)** | Yes: core rules, a UI layer over a 3D arena view, the existing pattern emitters for pressure | Easiest | No: per-arena 3D gameplay, physics, camera |
| **Verdict** | **Chosen** | A fallback if time runs out | Borrow one idea: the arena is the real 3D set behind the UI. And the Force route's rule-break can spill into the 3D world |

**Why A:** it makes the logic visible as a structure, gives each opponent a place and a metaphor of their own,
and keeps the action short and meaningful.

---

## 3. The chosen concept, "Take it apart", in full

### 3.1 The screen
```
+----------------------------------------------------------------------------------+
|  THE ARENA (the 3D set, fixed camera)      opponent billboard, expression state   |
|                                                                                    |
|                        [ CLAIM: "He's fine here. Go home, Bell." ]                 |
|                     (the wheel's hub; wobbles while the claim stands)              |
|         [A] fact          [B] fact            [C] link           [ ? ] hidden      |
|     "He's happy."   "I've always been     "You can fix          (dashed outline)   |
|                       on this street."      anything..."                           |
|   ~~~~~~~~~~~~~ the arena's surface: pressure phases play here ~~~~~~~~~~~~~~~~~   |
|                                                                                    |
|  HEART 84 bpm ~^~^~        transcript (last 3 exchanges) >                          |
|  VERBS:  [? Ask]  [pin Challenge]  [hand Concede]  [hold: Press]  [Let it rest]     |
|  EVIDENCE: (memorial card) (chart) (ledger) (newspaper) (photo) (call log) ...      |
+----------------------------------------------------------------------------------+
```
- **The claim** is the arena's central object, with its text on a tag. It visibly **wobbles** while premises stand
  and steadies as they fall.
- **Premise tags** hang on the object's pieces. Shape tells the kind: **facts** are square stamped tags; **links** are
  chain-link tags; **hidden** premises are dashed outlines with a "?".
- **Status** is shown by shape and text, never colour alone:

| Status | Looks like |
|---|---|
| Standing | The piece in place, tag upright |
| **Broken** (by a sound reason) | The piece removed cleanly (spoke pulled, plate cleared, film taken down); text struck through |
| **Conceded** (true, acknowledged) | The piece glows steady; a check mark; it can't be challenged again, but its link can |
| **Forced** (pressed) | The piece bent or cracked; a "forced" stamp |
| Hidden | Dashed outline, "?" |

- **Evidence** sits in a tray at the bottom: every journal entry has a **battle icon** in the split style:
  - waking facts are **stamped** (a hospital stamp border, muted 3D-render look);
  - dream memories are **doodled** (crayon border, pastel);
  - deductions are **pinned** (two small icons joined by a thread);
  - truths are **gold-leafed** (the strongest cards);
  - personal entries (objects given to her) are **tagged** like a luggage label.
- **The transcript** keeps every exchange as plain sentences (for judges, for accessibility, and for the creator's
  writing pass).
- **Heart rate** (composure): 70 → 88 → 106 → 124 → 142 → 160 bpm. At 160 she panics: Mags pulls her out.

### 3.2 The verbs
| Verb | Icon | What it does | The opponent's reply |
|---|---|---|---|
| **Ask** ("Why?") | a question in a speech bubble | On any premise: they explain. Reveals the hidden premise beneath it, if there is one | Their reasoning, in their words. Free; counts toward Heard out |
| **Challenge** | a pin and a thread | On a premise: pick evidence, then pick the reason (three "because..." cards) | **Sound:** they concede, **fall back** (a weaker premise grows in its place) or **deepen** (the premise under it appears). **Unsound:** they say exactly why it doesn't follow, and a pressure phase follows |
| **Concede** ("That's true.") | an open hand | On a true premise: acknowledge it. Heart rate down one step; often reveals the link hung on it | They relax and say what they hang on it. Conceding something false closes that line (a misplay, never a dead end) |
| **Press** (hold 0.6 s) | a hand pushing flat | Force a premise down without evidence. Pressing the claim ends the battle | They're hurt. The piece breaks "forced". Force +1. A harsh pressure phase |
| **Let it rest** ("I hear you.") | a closed journal | Appears once every premise (hidden ones included) has been heard: asked, conceded or answered | They soften and step aside (Heard out) |
| **Tell** (Night 4 on) | a phone with a speech line | Place a truth **beside** a premise, not on it: an offering, not an attack | Used with Theo and in the final talk (section 5) |

**The reason step is the heart of it.** For a sound challenge the player must pick, from three cards, the one reason
that actually connects that evidence to that premise:
- one is **sound** (it follows);
- one is **true but beside the point** (it's real, but it doesn't touch this premise);
- one **doesn't follow** (a leap, a misreading, an insult dressed as a reason).

Some premises also get an **antagonising** card: Nadia dismisses the opponent instead of answering (the creator asked
for one against Dream-Lena: she's in the wrong for acting like she's real). It angers them.

Evidence that has nothing to do with a premise gets no reason cards, just the opponent's one-line "That doesn't touch
what I said" (in their voice) and a small heat rise. So guessing is possible, but costly and visible.

### 3.3 Pressure phases (the action, and why it's there)
**What they are:** 5–8 seconds in which the opponent's feeling takes over the arena's **surface** (the workbench top,
the table, the lightbox) and Nadia's presence, a small light, moves to avoid it. Hits raise her heart rate. The shapes
are **the opponent's things moving the way their emotion moves**, never rectangles: Walt's ball bearings roll in
patient rows; Lena's flour blooms and her plates slide; the Doctor's ECG traces sweep on the beat. Except in the Long
Night, every attack enters from off-screen or is telegraphed (the creator's rule).

**When they happen** (they're a consequence, not a timer):
| Trigger | Intensity |
|---|---|
| The opening statement (their feeling as they make the claim) | Low |
| After an unsound challenge | +1 heat |
| After a press | +2 heat |
| When a painful hidden premise surfaces | High |

Heat (0–3) falls by one for each concession and each sound challenge. **Arguing well makes the room calmer.** Arguing
badly or forcing makes it louder. A judge can see that in ten seconds.

**Slips (why dodging means something).** Each pattern carries one or two **slips**, small glowing things of theirs
(Walt's brass bell, a patient wristband). Catching a slip reveals a hidden premise: what they aren't saying. Most
hidden premises can also be revealed by Ask; the **deepest** one in each battle is revealed only by a slip or by asking
twice in the right place. So the action is *listening under pressure*: avoid the hurt, catch the subtext. In Story
mode slips collect themselves.

### 3.4 Outcomes
| Outcome | How | Gives | Story effect |
|---|---|---|---|
| **Convinced** | Every **key** premise broken by a sound reason, or conceded and its link broken. No presses | A **truth** (the opponent's revised claim, in their words) | Truths can sway Elias's decision in the final talk. The guardian **helps** the escape (a shortcut) |
| **Heard out** | Let it rest after hearing every premise. No presses | A **personal** entry (an object of theirs) and their trust | They soften; the escape is normal; some personal entries open optional routes later |
| **Pressed** | Any press | Nothing; Force +1 per press | They're hurt; the escape is harsher (the pursuer runs closer); doubt lines (`STORY-BIBLE.md`, section 7) |
| **Composure lost** | Heart rate reaches 160 | — | Mags pulls her out; she re-dives from the guardian; a small cost to Elias's heart |

Antagonising pushes the opponent toward condemning Nadia. For Lena, the creator's outcome is a stance: **unsure** of
her conviction (open to giving her blessing in later battles) or **condemning** Nadia; condemning makes the Despair
route easier to get onto.

Progression never needs Convinced. Truths only shape the endings.

### 3.5 Teaching
What teaches the argument, and where, is undecided. The creator rejected Walt's tutorial "the way it's currently
built" and wants to discuss it after the setting talk (PROSE:104). Whatever teaches it should teach each verb at the
moment it's needed, in the teacher's own metaphor, with a practice pressure phase that can't hurt.

---

## 4. Walt's battle, written out (the worked example of the rules)
Whether Walt's battle survives, in another form, and whether it stays the model of an argument that makes sense, is
undecided. It's kept here because it shows every rule working.

**Setting.** Walt's garage at golden hour. Elias's rear wheel on the truing stand; through the open door, Elias
laughing at the barbecue. Walt wipes his hands on his red rag. Walt is a bike mechanic, and his argument is a wheel:
the claim is the hub, the premises are spokes, a weak premise is a loose spoke, and getting to the truth is **truing**
the wheel.

**Evidence Nadia can have by now:** the chart (`fact_chart`), the memorial card and keyring (`fact_effects`), the police
report and call log, the family photo, the newspaper, the shop sign, "Saturday", Walt waving, **Walt's ledger**
(`mem_ledger`), and the deductions `ded_walt_dead` and `ded_same_saturday` if she made them. The voicemail is greyed:
"Not now."

### 4.1 The map
```
CLAIM (hub): "He's fine here. Go home, Bell."
 ├─ A  fact  TRUE   "Look at him. He's happy."
 │    └─ A' link (revealed by conceding A)   "Happy's the same as fine."                 KEY
 ├─ B  fact  FALSE  "I've always been on this street. I'd know if something was wrong."   KEY
 └─ C  link  TRUE   "You can fix anything, if you're patient enough."
      └─ C' link (hidden: Ask C, or catch the bell)  "So give him time. He'll come back when he's ready."   KEY
```
Key premises for Convinced: **A'**, **B**, **C'**. A and C are true: the honest moves are to concede them.

### 4.2 The flow (the sound path, with every branch)
**0. The claim.** Walt: "He's fine here. Go home, Bell." Ball bearings roll down the tilted workbench in slow rows.

**1. Walt sets three spokes** (A, B, C). Nadia asks about B.
> Walt: "Forty years at the bottom of this hill. I know every house, every dog. If something was wrong here, I'd
> know it."

**2. Challenge B.** Evidence: the memorial card (or `ded_walt_dead`).

| Reason card | Kind | Walt's answer |
|---|---|---|
| "You died in February 2015. You can't have always been here. This street is a memory." | **Sound** | Long pause. "...February. Cold one." The spoke comes out clean. **Fall back:** "Alright. I'm a memory. Memories can still be right." |
| "You're dead, so you don't get a say." | True, beside the point | "Dead men give advice all the time, Bell. It's most of what we do." (+heat: the wheel spins and its spoke shadows sweep the bench) |
| "The sign says 1971, so you haven't always been here." | Doesn't follow | "Since '71 is as close to always as a street gets." (+heat) |

**3. A is true.** If she challenges A ("Look at him. He's happy.") with anything, Walt: "You can see him, Bell. Is he
faking? That's a true spoke. Leave it." She **concedes**: "He is happy." Heart rate down a step. Walt relaxes and says
what hangs on it, revealing **A'**:
> Walt: "Then what's the trouble? Happy's the same as fine. Fine's all anyone wants for him."

**4. Challenge A'** with the chart. Nadia speaks as a nurse.

| Reason card | Kind | Walt's answer |
|---|---|---|
| "His body's in bed 14, losing muscle every day, two chest infections already. Happy in here isn't fine out there." | **Sound** | "...I don't know about out there." The link snaps. **Fall back** to C' if it isn't already showing: "Then he's resting. He'll come back when he's ready." |
| "His scans are normal, so he's faking." | Doesn't follow | "Normal pictures don't make a man a liar." (+heat) |
| "He's in a coma, so he can't be happy." | Contradicts what she just conceded | "You just told me he's happy, Bell." (+heat) |

**5. C, the lesson** ("You can fix anything, if you're patient enough."). It's Walt's life and it's true, so the honest
move is to **concede** it ("You can. You taught me."). **Ask** about it, or catch **the brass bell** as it rolls across
the bench during a pressure phase (Nadia's own childhood bell, the slip), and Walt says the hidden part, half to
himself: "...same flat every morning..." Revealed: **C'**:
> Walt: "So give him time. He'll come back when he's ready."

**6. Challenge C'** with the ledger (or `ded_same_saturday`). This is the key.

| Reason card | Kind | Walt's answer |
|---|---|---|
| "You've patched his rear tyre forty-one times, all on the same Saturday. Time isn't passing here. He isn't getting ready for anything; the day just starts again." | **Sound** | He looks at the ledger for a long time. "...Forty-one. Same tyre." **Deepen:** "When a tyre keeps going flat, there's a thorn in it. You can patch it forever." |
| "The newspaper's six weeks old, so you're lying." | Doesn't follow (old isn't lying) | "I've never told you a lie in your life, Bell." (+heat) |
| "He has to come back because I need him." | True, beside the point (it's about her) | "That's about you, Bell. I asked about him." (+heat; Nadia's journal gets one line, scribbled out: *"He's right."*) |

**7. The wheel trues.** With A' and C' broken and B broken, the claim has nothing left. The wheel stops wobbling and
spins true. Walt states his **revised claim**, which becomes the truth:
> **`truth_walt`:** "Patching it isn't fixing it. There's a thorn in that tyre, and it isn't on this street. It's
> wherever he was going that night."

He opens the garage's back door: "Go on. I'll keep patching till you're done." (The escape's shortcut.)

**Other ways it ends.**
- **Heard out:** at any point after all five premises are revealed, **Let it rest**: "I hear you, Walt." He nods, hands
  her his spoke key: "For the next thing that wobbles." (`pers_spoke_key`; it loosens one bolted barrier on Night 4,
  an optional shortcut.)
- **Pressed:** hold Press on any spoke. Nadia: "He's NOT fine." The spoke bends instead of coming out. Walt flinches:
  "...That's how you strip a thread." A chain whips across the bench (the harsh pattern). If she presses the hub,
  he steps aside without another word.
- **Composure lost:** five hits. Nadia can't breathe. Mags: "I've got you." The booth. She goes back in.

### 4.3 Walt's pressure patterns
| Pattern | When | What it is | Reads as |
|---|---|---|---|
| **Bearings** | Opening; heat 1 | Rows of ball bearings rolling down the tilted bench, gaps between them | Patience: slow, regular, inevitable |
| **Truing** | Heat 2 | The wheel spins; its spoke shadows sweep the bench like clock hands; stand in the gaps. Grease drips on the beat | Time going round and round |
| **Chain** | Heat 3 (after a press) | A loose chain snakes across the bench, plus bearings | Something that's been forced, slipping |
| **The bell** (slip) | In any pattern | Nadia's brass bike bell, rolling and ringing | What he isn't saying |

### 4.4 Why this is an argument and not an association
Every exchange in the transcript reads as a syllogism with a reply:
> **Walt:** I've always been on this street. **Nadia** *(memorial card)*: You died in February 2015, so you can't
> have always been here; this street is a memory. **Walt:** ...February. Cold one. Alright, I'm a memory. Memories
> can still be right.

The evidence attacks one stated premise, for a stated reason, and the opponent concedes the premise and falls back to a
weaker claim, exactly as a person would.

---

## 5. The other battles
Each in outline: the claim, the premise map, the key evidence and why it works. Same rules, new arena, a new twist.

### Lena, at the kitchen table (Night 2). Twist: a premise that can't be broken
**How it starts (the creator's re-plot, PROSE:140):** Nadia has sat down for pancakes and played hide-and-seek with
Theo, waiting for Elias to join them. He never comes. Nadia questions Lena, and Lena admits she isn't willing to let
Elias go: she's been keeping him away from Nadia, afraid Nadia would convince him to leave the place where he's happy.

**Arena:** breakfast for four. **Claim** (the centrepiece, the pancake stack): "He stays. This is his home." **Pieces:**
plates at each seat (cleared and turned over when broken, smashed when forced). **Win image:** she puts the spatula
down and sits; the fourth chair is pulled out.

| Premise | Kind | The honest move and why |
|---|---|---|
| L1 "Out there, Theo is dead." | fact, TRUE | **Concede.** Reveals L1' "So out there, there's nothing left for him." |
| L1' "There's nothing left for him out there." | link, KEY | Challenge with the fourth chair (`mem_fourth_chair`): "He sets a place for me in here. There's someone out there." What Lena falls back to is undecided (Nadia never stopped coming to Sunday pancakes) |
| L2 "You don't know where I am. Neither does he." | fact, TRUE | **Concede.** Reveals L2' "So waking means searching forever." **It can't be broken tonight**: nothing Nadia has answers it, and that's the lesson (not key) |
| L3 "He chose this. I know him." | link, KEY | **Ask** reveals L3' "This is the life he wanted." Challenge L3' with `ded_saturday_off`: "He worked that Saturday. This is the day he didn't have. That's a regret, not a choice." Lena **falls back**: "Then let him have his wish." (L3'') |
| L3'' "Then let him have his wish." | link, KEY | Challenge with `truth_walt` or `ded_same_saturday`: "He's had it forty-one times, and Walt patches the same flat every morning. A wish you have to repair every night isn't peace." **Without either, it can't be broken** (Heard out at best) |
| L4 (hidden, surfaces by script) "He called you first. You didn't pick up. So he called me." | fact, TRUE | It stays in the argument (the creator). The big pressure phase; with `ded_why_lena_came` (the call log and the voicemail, Day 1) she can **Concede** at once: "I know." |

- **Antagonising:** alongside the options that pacify her, there's one that antagonises her further: arguing that
  Dream-Lena is in the wrong for acting like she's real, avoiding her question. It pushes her toward condemning Nadia.
- **Outcome:** Lena ends **unsure** of her conviction (open to giving her blessing in later battles) or **condemns**
  Nadia. Either way, the house kicks Nadia out.
- **Truth (`truth_lena`, a placeholder):** "The Lena he remembers wouldn't let him hide. She'd want him to live, even
  without her." What Lena's truth really is, and where she gives it (a later battle, or the back seat), is undecided.
- **Personal (Heard out):** undecided (no apron, so not the hair tie). **Pressure:** flour blooms (slow rings), batter
  arcs, pan spatter, plates sliding across the table (walls with gaps). **Slips:** undecided (Theo's drawings as an
  attack are out of character for how Elias would remember his son, PROSE:134).

### The Doctor, at the lightbox (Night 3). Twist: the half truth
Undecided, and it needs expanding: the creator calls it "possibly the most important battle, since it's the player's
first introduction to Elias' character" (PROSE:167). The proxy form it rested on is cut. What the Doctor argues, and
what truth wins it, are open (the creator asked whether the paperwork about Rafael's position could be it, PROSE:213).

**Arena:** the trauma bay; a lightbox of X-ray films; the patient board. **Pieces:** X-ray films (taken down when
broken, cracked when forced). The claim and the win image are undecided.

What survives from the draft, as material:
| Premise | Kind | The honest move and why |
|---|---|---|
| D1 "His right hand is finished. He won't practise again." | fact, TRUE | **Concede** (`fact_hand`). Reveals D1' "A doctor who can't practise has nothing to come back to" (link; breakable with `fact_patients`: "They come to St. Brigid's for him, not his hands") |
| D2 "His family is dead." | fact, FALSE | Challenge with the police report or `fact_did_lena_get_out`: "Lena is missing, not dead. You asked if she got out." He **deepens**: "Missing is worse. A search that never ends." (true: concede) |
| D4 "**You only got this job because you're my sister.**" | fact, HALF TRUE | Challenging it fails. **Concede** the half truth. What link hangs on it is undecided |

- **Personal (Heard out):** his pen light. **Pressure:** ECG traces sweeping across on the beat (the spike is the
  danger), pill capsules ricocheting, curtain rails closing in. **Slips:** patient wristbands.

### Rafael, at the nurses' station (Day 3, optional, waking). Twist: no pressure, real stakes
Only if she's skipped duties. **Claim:** "Tell someone where you go at night, or I will." Premises: "You've missed
three rounds" (true), "You haven't slept" (true), "Whatever it is, it's hurting you" (link). Concede everything and
let it rest: he becomes an ally (covers for her honestly; discovery stops rising). Press ("It's none of your
business"): he reports her (the wing is locked on Day 4). No pressure phase: in the waking world, feelings stay
inside people. **Arena:** the shift board; med cups on a tray as pieces.

### Lena breaks the rules (Night 4, Force route only). The rule-break
She steps into the road and starts an argument nobody asked for. **Claim:** "Leave him alone." Nothing works as taught:
her premise tags slide away from the cursor; her patterns come from **outside** the arena and slide over the verb
buttons; she takes an evidence icon out of the tray and rewrites it (the memorial card now reads "1941–"). Only two
verbs answer: **Press** (she breaks, and comes back later) or **Let it rest** (she steps aside, and comes back later).
It teaches nothing, on purpose: it's the one thing in the game that knows the game's rules and ignores them.

### Theo, at the guardrail (Night 4). Teaches Tell
Not a battle: a conversation with one rule. Theo asks; each answer is a card. Only **Tell** works (honest answers);
comforting lies are allowed but don't count. "Is Dad going to be sad?" → Tell: "Yes. For a long time." → Theo, after a
while: "Okay. Tell him it's okay to be sad." (`truth_theo`). No arena object, no heat: just the rain and his torch.

### Elias, the final talk (Night 5)
**Arena:** the car's interior, if the car is the final arena (undecided: the creator wants a reason they're back at the
beginning); rain on the windscreen; the dashboard clock at 3:48 (the claim object: it moves only when something is
decided). **Claim:** "Let me stay." (Despair route: "Stay with me.") **No pressure phases** in the standard route: he
isn't fighting her. An idea of the creator's: Lena and Theo appear in the back seat to support Nadia's arguments,
instead of Nadia passing on their wishes.

**What holds him** (the draft's four holds; what each really is and what releases it is undecided):
| Hold | Object | His premise | Released by |
|---|---|---|---|
| Guilt | His hands on the wheel | "Guilty for getting his wife and son killed in his mind" (the creator); the driving doesn't matter (PROSE:38) | Undecided. Nadia's confession that she heard him ask and went back to sleep is rejected (PROSE:208) |
| Theo | The booster seat | "If I go, I leave him." | `truth_theo`, or Theo in the back seat |
| Lena | A keepsake of hers (undecided) | "She's here. Out there she's nowhere." | `truth_lena`, Lena in the back seat, or the hope (waits on the Night 4 truth) |
| Purpose | His ID on the sun visor | "What am I without my hands?" | The Doctor's truth (undecided), `fact_patients` or `fact_shrine` |

**His decision is granular** (the creator, SB:22): anything that could affect it does, and for the major ones he says
so. There's no threshold. The endings are the creator's (PROSE:56):
| Ending | How it's reached |
|---|---|
| **Morning, chosen** | She convinces him that Lena wouldn't want either of them to succumb to their darkness ("...Drive me home, Nads?" is a placeholder). The rest of the ending is to be redone |
| **Morning, forced** | She forces him out when he won't see reason (Press). How it plays is to be redone |
| **Evening** | She fails to convince him, or she lets it rest (both, the creator) |
| **The Long Night** | On the Despair route, Elias doesn't let her finish her argument, seeing her lack of conviction. The battle is a scripted loss: technically beatable, near impossible. Defeated, he asks her to stay; no dialogue gets her off the path, and she takes his hand |

---

## 6. Arenas and art, battle by battle
| Battle | Arena (3D set, fixed camera) | Claim object | Pieces and their states | Opponent billboard expressions | Pressure sprites | Slip | Win image | UI skin |
|---|---|---|---|---|---|---|---|---|
| **Walt** (the worked example) | Garage workbench, truing stand, pegboard, radio, ledger | Bicycle wheel (hub tag) | Spokes: standing / pulled / bent / glowing | Neutral, rattled, conceding, hurt, softened | Bearings, spoke shadows, chain, grease drops | Brass bell | The wheel spins true | Stamped metal tags |
| **Lena** | Kitchen table set for four, fridge with drawings | Pancake stack centrepiece | Plates: in place / cleared / smashed / glowing | Same five | Flour clouds, batter arcs, spatter, sliding plates | Undecided | She sits; the fourth chair pulled out | Recipe cards |
| **The Doctor** | Trauma bay, lightbox, patient board, curtains | Undecided | X-ray films: up / taken down / cracked / glowing | Same five | ECG sweeps, capsules, curtain rails | Wristbands | Undecided | A clipboard |
| **Rafael** (waking) | Nurses' station, shift board | The shift board | Med cups: full / emptied / knocked over | 3D model's face (no billboard) | None | None | He hands her the marker | Ward forms |
| **Lena** (rule-break) | The rainy road, kitchen tiles spreading over the asphalt | — | Plates on the car bonnet | Hurt, furious | Plates and flour from outside the arena, over the buttons | None | None (she leaves) | The UI itself, defaced |
| **Theo** | The guardrail, his torch | — | — | Curious, serious, brave | None | None | He switches the torch off | Crayon |
| **Elias** | The car interior (if kept), 3:48 | The dashboard clock | Hands, booster seat, Lena's keepsake (undecided), ID | Soft Elias: warm, tired, breaking, decided | None (rain streaks only if pressed) | None | The clock moves | The dashboard |

**Shared art:** the five verb icons plus Tell; a battle icon for every journal entry (in the four styles above); Nadia's
presence light; the heart-rate monitor; the reason cards; the "argument line" that draws from evidence to premise with
the reason written along it; crack, pull and glow effects.

## 7. Accessibility
- **No clock on thinking.** Time only runs in pressure phases; choosing evidence and reasons is untimed.
- **Pressure settings:** speed 50–100%; **Story mode** (no hits; slips collect themselves after 3 s); **Off** (each
  phase becomes the line spoken over a still arena).
- **Never colour alone:** status is shape plus text (section 3.1). Colour-blind-safe palette per arena.
- **A full transcript** of every exchange; adjustable text size.
- **Parity:** everything works on mouse, keyboard and gamepad (UI Toolkit focus navigation). Proposal: arrows or stick
  move between premises; South = Challenge, West = Ask, North = Concede, hold East = Press, View = Let it rest; keyboard
  1–5 for the verbs, hold P to press. Press's hold time is adjustable.
- **Evidence filter:** "Show only entries that could apply" (off by default after the tutorial, on in Story mode).
- **Flashes:** crack flashes at most 3 per second; "reduce effects" turns them into a soft pulse.

## 8. Building it (UI Toolkit plus the core)
**Core, pure C#** (`Runtime/Core/VisitingHours/`, tested with dotnet, replacing the slice's `ArgumentBattle` stance and
touch model):
| Type | Holds |
|---|---|
| `ArgumentDef` | id, arena id, claim (text, icon), premises, opening pattern, heat patterns (1–3), slips, outcome entries (truth, personal), flags (no pressure, rule-break, final) |
| `PremiseDef` | id, kind (Fact, Link), text, truth (True, False, Half), key?, starts hidden?, revealed by (Ask on a parent, a slip, a concession), Ask reply, Concede reply and reveals, challenges, a generic "doesn't touch it" line, a press line |
| `ChallengeDef` | evidence id(s), three reasons (text, kind: Sound / Beside / NonSequitur, plus an optional Antagonise card), each reason's reply, and the sound one's effect (Break, FallBack to a premise, Deepen to a premise) |
| `ArgumentState` | premise statuses, heat, heart-rate step, presses, heard set, anger, the transcript |
| `ArgumentEngine` | Ask, Challenge, Concede, Press, LetItRest, Tell; events for the view; the outcome rules (section 3.4); the final talk's decision (granular, once its rules are designed) |

- **Pressure phases** reuse the core `Patterns` / `Emitter` and the bullet hell's `BulletPool`, drawn with each arena's
  sprites instead of rectangles, inside a **surface mask** (a polygon per arena: the bench top, the round table, the
  lightbox), not a box. Likely new: a **sweep** emitter (a rotating or travelling line with a gap) for spoke shadows
  and ECG traces.
- **Tests (dotnet):** Walt's battle as data: the sound path reaches Convinced; each wrong reason gets its own reply and
  raises heat; conceding reveals A'; Ask and the bell both reveal C'; Let it rest appears only when all five are heard;
  any press gives Pressed; heart rate 160 gives Composure lost. Lena: L3'' can't break without `truth_walt` or
  `ded_same_saturday`. The final talk: once its granular rules are written, every route reaches the right ending. A
  pilot plays each battle's sound path for the smoke run (as `BattlePilot` does today).

**Unity side (UI Toolkit):**
- `ArgumentView.uxml` / `.uss` over the arena: the 3D arena set renders behind the UI (its own camera, fixed framing);
  premise tags are VisualElements placed at anchor points the arena prefab exposes (one per piece).
- An `ArenaDef` per battle: the prefab, the anchors, the surface polygon, the piece animations (pull, bend, glow), the
  win animation, the UI skin (a USS class).
- The evidence tray reads the journal; battle icons come from each entry's art.
- The ink dialogue service plays replies with the opponent's expression state.
- Authoring cost, roughly: Walt about 25 reply lines, Lena about 30, the Doctor about 30, the final talk about 20 per
  route.

## 9. What a judge sees in 60 seconds
1. An old man in a garage with a bicycle wheel; his claim is on the hub, his reasons are on the spokes.
2. The player drags a memorial card onto a spoke; three "because..." cards appear; she picks one.
3. A line draws from the card to the spoke with the sentence on it: "You died in 2015, so you can't have always been
   here." The spoke comes out. He says: "...February. Cold one. Alright, I'm a memory."
4. She tries a bad reason; he names why it doesn't follow, and the bench fills with rolling bearings and sweeping
   spoke shadows while she dodges and catches a ringing bell. He mutters what he wasn't saying.
5. The last spoke comes out and the wheel spins true.
