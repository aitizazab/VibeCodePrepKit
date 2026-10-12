
# *Visiting Hours*: what the player does, night by night

> **Status: a proposal for the creator (overnight draft, 2026-10-11; brought in line with the creator's notes on
> 2026-10-12).** The creator's own writing (their `//` notes and prose in `STORY-PROSE.md`, their scripts, their
> endings) outranks everything here. The story it serves is `STORY-BIBLE.md`; the argument battles are specified in
> `ARGUMENT-BATTLES.md`; the movement it uses is built and documented in `MOVEMENT.md` (numbers from
> `RESEARCH-MOVEMENT.md` sections 4, 6, 8 and 9). Everything not marked as the creator's is a proposal.
> Gameplay is what players remember, so every night has one **peak** built to be shown to a judging panel.

## Decisions for the creator
| # | Decision | Recommendation |
|---|---|---|
| 1 | **Days run on time slots** (4 on Days 2 and 3, 3 on Day 4, 2 on Day 5): duties and trips each cost a slot | Keep: it makes "do your job or chase the threads" a real trade |
| 2 | **Duties are short playable rounds** (60–90 s: wristbands, meds, vitals) that also yield facts (patients' gratitude and gossip) | Keep: the job isn't just a cost, it's a different kind of evidence |
| 3 | **Night 3's badge flip** (one hospital in two moments, swapped by swiping Nadia's ID) | Open: the creator questioned what the contrast between the dreamed and the real hospital is for (PROSE:160). Keep, change or drop |
| 4 | **Night 4 has no argument battle** in the standard route; its peak is the chase, plus Theo's question (where **Tell** is taught) | Waits on the Night 4 truth, which is undecided |
| 5 | **The phone rings mid-chase** on Night 4, and answering it skips the truth | The creator likes the idea of an escape without a timer; what skipping the truth costs is undecided (PROSE:184) |
| 6 | **Morning (forced)'s playable part** | Undecided: the creator wants both Morning paths redone (PROSE:244) |
| 7 | **Escape records** (fastest climb out per night) in the journal's back page | Optional; off by default for tone, on in presenter builds |
| 8 | **Scope:** for the practice game, build Tier A (section 10) first | Tier A is what a 4-minute pitch needs |

---

## 1. The verbs of the game
| World | Look and feel | What the player does | What they never do |
|---|---|---|---|
| **The day** (waking) | Grounded, muted, stylised 3D; first person; walking pace with a short jog | Examine and chart (facts), talk, do duties, take trips across the city, decide the evening's small things | Run fast, jump, fight. No pressure phases in waking arguments |
| **The dive** (dream) | Pastel doodle 3D with billboard people; Titanfall-style movement | Explore, collect memories, make deductions, argue with a guardian, **escape** the dream's defences | Shoot. There are no weapons anywhere in the game |

The contrast is the point: in the waking world Nadia is tired and heavy; in her brother's dream she moves the way he
remembers her, the kid who jumped the creek on her bike.

## 2. The shape of a day
- **Time slots.** A day's slots are shown as the ward clock on the HUD (morning, midday, afternoon, evening). Duties
  and trips each cost a slot; conversations inside the hospital and examining things are free.
- **Duties.** A round is a short, pleasant routine: walk the ward, read a wristband, match the meds, chart the vitals.
  Each patient says something (gratitude for Elias, gossip about the corporations, the cost of things). Rounds done
  across the days build `fact_patients`, one possible way to release Elias's "Purpose" hold at the end.
- **Skipping a duty.** Rafael (an intern, covering her extra hours) covers it. The ward remembers (discovery risk +1).
  - Risk 2+: Rafael confronts her on Day 3 (a short waking-world argument).
  - Risk 4+: on Day 4 the closed wing is locked, and Mags brings her in by the service lift (a one-minute waking sneak:
    no running, just timing the guard's walk).
  - Either way, the charge nurse post is decided by her record (it's in the epilogues).
- **Trips** (optional, two slots across the river, one slot nearby): a Day 2 trip about Lena's Thursdays (what it is
  is undecided: no studio and no friend, but "something else that shows a hole in who Lena is"), the empty Lasname
  house (Day 3, needs Lena's key), Route 9 (Day 4).
- **The evening.** The booth and Mags. The Long Night's lock-in is a strange, curious choice that sneaks up on the
  player (the creator's rule, like Deltarune's Snowgrave); which choice, and on which evening, is undecided.

## 3. The shape of a dive
1. **Arrive** at the top of the layer. Mags on the earpiece.
2. **Explore** a small, dense 3D space. Memories glow faintly; the dream's people talk.
3. **Deduce:** combine two journal entries that can't both be true (the slice's system). A **rushing check** warns,
   never blocks: walking to the guardian with open questions makes Nadia think *"I don't know enough yet."*
4. **Argue** with the layer's guardian (`ARGUMENT-BATTLES.md`). The outcome changes the escape.
5. **Escape:** the layer reacts and the dream drives her out. A set-piece chase on the movement system.
6. **Surface** in the booth. Mags's water. The next day.

**Failing, and the cost.**
- **In a chase:** a "dream drop". A 0.4 s fade, back to the last checkpoint at running speed, no penalty
  (checkpoints every 10–20 s of chase, as in `MOVEMENT.md`). After two falls at one spot, the spot quietly widens.
- **In an argument:** composure runs out, Nadia panics, Mags pulls her up, and she re-dives the same night from the
  guardian. It costs Elias's heart a little (shown on the booth monitor). Vitals never end the game; they shape the
  epilogues.
- **Assist:** at any time, "Mags, turn it down" lowers the dream's pressure (slower pursuer, wider forgiveness, gentler
  patterns). No story cost, ever. (The memory-for-a-shortcut offer is a separate, deliberate story choice.)

## 4. Teaching plan (one tutorial per mechanic, by someone it suits)
Withhold meaning and stakes, never mechanics.

| Mechanic | When | Taught by | How |
|---|---|---|---|
| Walk, look, examine (E) | Day 1, first minute | The game | A controls line at the nurses' station |
| Charting (facts into the journal) | Day 1 | **Nadia's habit** | Examining the chart writes "Charting: GCS 6..." |
| Duties | Day 1 | **Rafael**, at handover | "Bed nine's meds are due. You or me?" |
| Deductions | Night 1, arrival | **Mags** | "Two things that can't both be true: put them side by side." |
| Jump, double jump | Night 1 | **Nadia's memory** and **Mags** | The creek she jumped at seven; "You're lighter in here. Again, in the air." |
| Slide | Night 1 | **Walt** | The garage door half up: "Duck, Bell." |
| Escapes (pursuer, dream drop, checkpoints) | Night 1 | **The dream** | The first pursuer is slow; the first fall visibly puts you back (what Night 1's exit is: undecided) |
| Mags's Foresight cue | Night 1 escape | **Mags** | "It's folding left!" |
| Assist | Night 1, after a second fall | **Mags** | "I can turn it down. No shame in it." |
| Argument: Ask, Challenge, Concede, Press, Let it rest | Night 1 | **Undecided** | The creator rejected Walt's tutorial "the way it's currently built" (PROSE:104), to discuss after the setting talk |
| Tether (E) | Night 2 | **Mags** | "If it gets bad, say so. I can pull you halfway. It costs him." |
| Wallrun | Night 2 | **Mags**, then the house | "Walls are only as solid as he remembers them." First over carpet, then over the stairwell |
| Stealth | Night 2 | **Theo** | Hide-and-seek in the house (the creator's stealth intro) |
| A premise that can't be broken | Night 2 | **Lena** | "You don't know where I am either." Concede or let it rest |
| The memory-for-a-shortcut offer | Night 2 escape | **The dream**, with **Mags** warning | "It'll let you through. It keeps what you give it." |
| The umbrella (a dream tool) | Night 2 exit | **The house** | A closed umbrella by the door, picked up with the same Dream Tool she has learnt; it lets her stand in the rain |
| Wall jump, chimney | Night 3 | **Mags** | The lift shaft: "Kick off one, catch the other." |
| The badge flip (if kept) | Day 3 → Night 3 | **Nadia's routine** | She swipes her badge at the ward door every day; in the dream the reader shows "the shift as it was" |
| Concede, then challenge the link | The first battle, mastered Night 3 | Undecided, then **the Doctor** | A true premise, and what he hangs on it |
| Following a guide; light reveals the route | Night 4 | **Theo** | "Follow me, I know the shortcuts." His torch lights paths that are only there in the light |
| Tell | Night 4 | **Theo** | His question: only honest answers count |
| The final talk (no pressure) | Night 5 | **The car** (if the car is the final arena: undecided) | Nothing attacks. Nadia: *"He isn't fighting me."* |

## 5. The curve: variety and difficulty
| | N1 The Street | N2 The House | N3 The Shift | N4 The Road | N5 The Car |
|---|---|---|---|---|---|
| **Exploration** | The street, dense enough for stealth (the setting is undecided) | Impossible house (rooms rearrange) | Two-moment hospital (open: keep, change or drop) | Linear road | A car interior |
| **Puzzle** | Walt's garage key (or force it); count the ledger | Follow Theo's height marks to the real hallway; hide-and-seek | Route through two moments (an open door in one, a gurney ramp in the other) | Light reveals the path | None |
| **Deductions** | 2 (1 key) | 2 (+1 hidden) | 2 (+1 hidden) | The Night 4 truth (undecided) | — |
| **Argument** | The tutorial (undecided) | Hidden premises; one unbreakable | Undecided (the Doctor's battle needs expanding) | Tell (Theo) | His decision is granular |
| **Escape** | Undecided (the stage-strike exit is rejected) | ~75 s; + wallrun, a turning house | ~90 s; + wall kicks, chimney, badge flips mid-run | ~3 min in 3 legs; everything at speed, a guide, a phone | Ending-specific |
| **Pursuer** | Undecided | The house folding shut | A flatline wave down the corridor | The car's headlights | Ending-specific |
| **Peak (the clip)** | Undecided | Running along a hallway as it turns into the floor | The badge flip mid-wallrun: crowded corridor to empty and back | The phone ringing while you outrun the headlights | Opening the car door |

## 6. Night by night

### Night 1: The Street (20–30 min)
**Day 1 (St. Brigid's; guided, no slots).** Handover with Rafael; one duty round (the tutorial round); room 14 (chart,
effects bag, photo, LeBaget's flowers); the police envelope (the call log, never commented on); the phone ("Not now.");
11 PM, the booth, Mags's briefing (the creator's scene: "Five Nights, maybe Six before it's too dangerous"; only the
truth is on her side in there; stay calm, or Elias is harmed).

**The dive.**
- **Explore** Larch Street at golden hour: the street, Walt's garage, the houses, the creek, the chained gate of the
  hill house at the top. Neighbours' lines change each night. The creator wants the setting dense enough for a stealth
  section later (PROSE:51); how the repeating golden-hour street becomes that dense, or what denser place it links to,
  is undecided.
- **Memories:** Walt waving, Saturday's paper, the shop sign, the neighbours' "Saturday", **Walt's ledger** (inside the
  garage: 41 identical lines).
- **Puzzle:** the garage is locked. The key is on the keyring in Elias's effects bag, and its dream twin hangs on the
  hook by the back door of house 3. Or force the lock (Force +1, quicker).
- **Deductions:** `ded_walt_dead` (key), `ded_same_saturday` (optional, from the ledger and the paper).
- **Argument:** undecided. Walt at the workbench was the tutorial (written out in `ARGUMENT-BATTLES.md` as the worked
  example of the rules); the creator rejected it as built.
- **Escape:** undecided. The street rolling up behind her like a stage being struck is rejected (PROSE:104), so what
  ends Night 1 is open.
- **Optional:** the porch swing, the lemonade, the neighbours' chatter, the hill house gate (it never opens).
- **Build:** Day 1 (exists in the slice; restyle to style E); the booth; Larch Street in real 3D (not flats); Walt's
  garage interior; Walt, neighbours, Biscuit, soft Elias as billboards; the ledger prop.

### Night 2: The House (25–45 min)
**Day 2 (4 slots).** An unwelcome visitor (the creator: "It can be LeBaget, but I'm open to other ideas"); Rafael's "Did Lena get out?" (free, if she talks to him); duty rounds; the Lena's-Thursdays trip
(undecided, see section 2). Typical choices: do 2 rounds and take the trip, or do all 4 and skip it.

**The dive** (the creator's re-plot, PROSE:124-140).
- **Street** (short): "Morning, Nadia!" The dream knows her name.
- **The house:** a two-storey family home, rebuilt so it misbehaves (the movement intro: the house playing with her).
  Hallways stretch; doors loop back to the kitchen.
- **Pancakes:** Lena and Theo are making pancakes, and the table has a chair for Nadia. She sits down, accepting the
  excuse to spend time with the family she lost, thinking Elias will be there soon to join them. Lena is keeping
  Elias away from her, afraid Nadia would convince him to leave the place where he's happy.
- **Puzzle:** Theo's height marks are only on the real kitchen doorframe. Follow doors that have them to reach the
  stairs.
- **Wallrun, taught safely:** the stretched upstairs hallway (an 8 m wallpapered wall over carpet), then over the open
  stairwell (a 14 m wall over a drop).
- **Hide-and-seek with Theo** (the stealth intro; 3–6 min): he hides, and memories are scattered around the house for
  Nadia to find. The player must know how many there are, or at least whether any are left; a line like "I'm not sure
  if there's anything left to find. I could let Theo find me here..." tells them they're done. Its ending differs
  between the Despair route and the normal route ("...but what does it matter, when he's not even real?" or "...it
  would be nice to see his smile again."); which belongs to which is undecided.
- **Theo's first question:** "Is Dad going to be sad?" (honest, comfort, or silence).
- **Deductions:** `ded_she_knows`; `ded_saturday_off` (key); the fridge calendar's Thursday stars (her stargazing nights).
- **Argument:** Elias never comes. Nadia questions Lena, and Lena admits she isn't willing to let Elias go
  (`ARGUMENT-BATTLES.md`). There's an option that pacifies her and one that antagonises her further (Dream-Lena is in
  the wrong for acting like she's real). The battle ends with Lena **unsure** of her conviction (open to giving her
  blessing in later battles) or **condemning** Nadia. "He called you first" stays in it (the creator rewrote the battle
  script).
- **Escape, "The house tips"** (~75 s, 4 checkpoints). Either way, the house kicks Nadia out:
  1. The kitchen floor tilts; run the length of the tilting table and through the hatch.
  2. The hallway turns over **while she runs it**: wallrun the wall as it becomes the floor, wall-to-wall across the
     doorframes.
  3. The staircase flattens into a ramp: **slide** down it for a boost and jump out of the slide into the hall.
  4. The front door. A **closed umbrella** sits next to the exit: pick it up with the Dream Tool and she can stand in
     the rain and go on normally. If she goes on without it, the Despair route continues (if it isn't already
     aborted). If Lena condemned her, the Despair route gets easier to get onto.
  - The pursuer is the house folding shut behind her, room by room.
  - **The memory offer:** at the hardest spot, after two falls, the dream offers to let her through for a memory.
- **Peak:** running along a hallway as it rolls into the floor.
- **Build:** the house (modular interior, a rotating section for the escape), Lena and Theo billboards (style E,
  Lena blonde with long waves), the kitchen arena, Theo's drawings as memories, the calendar, height marks, the
  umbrella, the collectible count, the visitor.

### Night 3: The Shift (25–40 min)
The creator flagged that this night needs expanding: the Doctor's battle is "possibly the most important battle,
since it's the player's first introduction to Elias' character" (PROSE:167). The plan below is the draft.

**Day 3 (4 slots).** The hand note in room 14 (fixed); **Elias's office** (1 slot: the hiring file); **trip: the empty Lasname house** (2 slots,
needs Lena's key: Theo's room, Lena's coat; whether the family trust's letter stays is undecided); Rafael's
confrontation (if risk 2+); duties. The badge swipe at the ward door every time she enters (the teaching for tonight).

**The dive.**
- **Street and house** (short, shuttered; Lena in the window).
- **Dream St. Brigid's:** pastel, empty, quiet. A loop of three wards, a staff room, the trauma bay, and a central
  atrium with a lift shaft. (The creator questioned the "perfect line" monitors: a straight line reads as a flatline.
  Keep, fix or cut: undecided.)
- **The badge flip (open):** swipe at any reader to switch between **the dream's shift** (empty corridors, doors
  locked) and **the shift as it was** (crowded, loud, doors open, gurneys everywhere). Same geometry; the dressing, the
  doors and the obstacles change. Puzzle: reach the trauma bay using an open door in one moment and a gurney stack (a
  ledge to mantle) in the other.
- **The remark** plays behind the staff-room door, in "as it was" only. Watching is optional.
- **The locker letter** ("Appointed on merit"): the creator questioned it (it reads as mocking her). Keep, fix or
  cut: undecided.
- **Wall jump and chimney:** the lift shaft (two walls 3.5 m apart, 4 m to climb), taught by Mags.
- **Deductions:** `ded_shift_deleted` (the dream's empty shift and the remark that's only in the real one: "He didn't
  dream a memory. He dreamed the day without what he said."); optional `ded_he_paid_it_back` (only if the hiring file
  and Rafael's post stay).
- **Argument:** the Doctor (`ARGUMENT-BATTLES.md`): what he argues and what truth wins it are undecided.
- **Escape, "Code blue"** (~90 s, 5 checkpoints):
  1. The monitors flatline in sequence down the corridor (the pursuer: a wave of light and sound). Sprint.
  2. A locked door: **flip** to "as it was" mid-stride; the door's open but gurneys fill the hall. Slide under one, use
     a tilted one as a ramp.
  3. The atrium: wallrun a 15 m wall, **flip** while on the wall (the wall stays; the drop below changes from floor to
     crowd), wall jump to the opposite balcony.
  4. The lift shaft: **chimney** up 4 m between the walls.
  5. The ambulance ramp: slide out under the canopy as the lights go out behind her.
  - Force route: Lena appears in the corridor as a second pursuer, which breaks the layer's rules.
- **Peak:** flipping the hospital while running on a wall.
- **Build:** dream St. Brigid's as one geometry with two dressings (toggle sets of props, doors and lights), the
  flatline wave, the badge reader prop, the Doctor (billboard), the trauma bay arena; the hand note, the office, the
  hiring file, the empty house (waking, 3D). **Fallback if over budget:** one moment only, and the remark as a single
  scene; keep the chimney and the flatline chase.

### Night 4: The Road (15–30 min)
**Day 4 (3 slots).** The vitals drop (fixed); **trip: Route 9** (1 slot: the cordon, still up six weeks later, the
shrine, the skid marks; in the Force route, the glimpse of blonde hair); duties. What earning Mags's trust means, and
what it unlocks, is undecided (Julian Cho is cut, with no replacement).

**The dive.**
- **Leg 1, the dark street** (~40 s): the first time the dream is dark. Theo waits with a torch (if earned, and if
  Theo stays). His beam lights paper walkways that only exist in the light: follow him or lose the route. Without
  Theo, the creator likes the idea of navigating in low light or no light (PROSE:180).
- **Breather:** the house's lit windows; Lena at the door (Force route: she steps out and blocks it; go round by the
  roofs).
- **Leg 2, Route 9** (~70 s): the pursuer is the car's headlights coming down the road behind her. Vault cars, wallrun
  the corporate billboards along the verge (12–15 m each), slide under a fallen barrier, cross the road between the
  two sweeping searchlights (being caught in one throws long shadows that hide the route, so you lose the line, never
  your life). Tracer fire falls as slow paper streamers to weave through.
  - **The phone rings** mid-leg: "Elias". Answer (slow motion; "You still good for picking me up?"; say "On my way" and
    the dream rewrites the night, she's driving, the leg ends early and **the truth is skipped**; what that costs is
    undecided) or let it ring and keep running.
- **Breather:** a bus shelter, rain on the roof, Theo catching his breath. (Without Theo, Mags on the earpiece.)
- **Leg 3, the crash point** (~40 s): wall-to-wall between two lorries, a double gap, a long slide down the wet
  camber. Then **the Night 4 truth**: what the crash memory reveals is undecided (Lena walking into the light is cut;
  the creator wants it to reveal something major to the player, even if Nadia doesn't realise it).
- **Theo's question** at the guardrail: the **Tell** tutorial. Honest answers only count; an honest
  one gives his blessing.
- **Force route:** Lena's rule-break argument in the road (her patterns ignore the arena and cover the buttons).
- **The end:** Elias on the guardrail: "Nads?" Mags pulls her up.
- **Peak:** the phone ringing while you outrun the headlights. **The best clip in the game.**
- **Optional:** Route 9 by day; the faster lines (a billboard chain that skips the barrier).
- **Build:** the Road (the largest level: about 600 m of chase, three legs and two breathers), the headlights pursuer
  (rubber-banded, `MOVEMENT.md`'s pursuer), Theo's torch (light-gated walkways), searchlights, paper tracer streamers,
  corporate billboards (fictional brands only), the car (also Night 5's arena, if the car stays), the truth's moment
  (once decided); Route 9 by day (3D, waking), Larpaca gang members (3D, faces hidden: cheap to model).

### Night 5: The Car (10–30 min)
Whether the car is the final arena is undecided: the creator wants a reason they end up back at the beginning.

**Day 5 (2 slots, all optional).** Goodbyes: Rafael, Mags, room 14. In the Long Night route the game itself stops
behaving normally (the creator's rule); billboards leaking into the waking world is one form it could take (open).

**The dive.** No defences. The street, house and hospital are dim and empty; a walk, not a run (about 2 minutes, with
callbacks: Walt's garage light, a drawing on the pavement). The car on the shoulder of Route 9. She gets in.
- **The final talk** (`ARGUMENT-BATTLES.md`, "The car"). His decision is granular: anything that could affect it does,
  and he mentions the major ones.
- **The ending's playable part** (the endings are the creator's, PROSE:56):

| Ending | What you play | Feel |
|---|---|---|
| **Morning, chosen** | She convinces him Lena wouldn't want either of them to succumb to their darkness; she takes him out of the city to go stargazing, to show him Lena lives on in his heart. The rest is undecided: the creator wants it redone | Undecided |
| **Morning, forced** | She forces him out when he won't see reason. How it plays and ends is undecided: the creator wants it redone | Undecided |
| **Evening** | She fails to convince him. He dies in his sleep, and she has another dead relation to carry guilt for. It ends with a memorial and a funeral, Nadia left on her own as it rains. (The creator liked the old draft from the point where Elias goes inside the house; whether a walk up Larch Street at dusk, where the double jump does nothing, stays is undecided) | Stillness, then grief |
| **The Long Night** | On the Despair route the final dive is a **scripted loss**: Elias doesn't let her finish her argument, and the battle is technically beatable but near impossible. Defeated, he asks her to stay; no dialogue gets her off the path, and she takes his hand while Mags screams at her not to. The last image is undecided (the default is Nadia's exhausted face, letting go) | Wrong, and inevitable |

- **Build:** the car interior (arena, if kept), the ending sequences (once the Morning paths are redone), the memorial
  and funeral in the rain (Evening), the scripted loss (the Long Night), soft Elias's billboard animations.

## 7. Escape sequences: shared rules
From `RESEARCH-MOVEMENT.md` section 6 and `MOVEMENT.md`.
- **The next move is visible on entry** to every corner; the route is never behind the player.
- **One "go" colour per dream**, used only for traversal: runnable walls, ledges, slide gaps. In the pastel dream: a
  soft warm glow on wallpaper, hedges, billboards. Never on decoration. Traversal highlight Off / Classic / Full.
- **The collapse points the way:** floors dissolve behind, walls fold toward the route.
- **Pursuers rubber-band** to about 12 m and only catch a player who stops or falls (under 2 m for 0.75 s). Big moments
  fire by position, never by timer.
- **Rhythm:** 20–40 s legs, then a breather (a closed door, a line from Mags, a bus shelter).
- **Escalate the vocabulary:** each move is introduced safe (no drop), then over a drop, then chained, then in a chase.
- **Outcomes change the chase:** a guardian who's on her side opens a shortcut; pressing brings the pursuer closer.
- **Comfort:** FOV, camera roll, bob, speed effects, speed lines (all in `MOVEMENT.md`); the assist option.

## 8. Optional content (why two players' nights differ)
| Night | Optional | Time | What it gives |
|---|---|---|---|
| 1 | Neighbours, porch swing, lemonade, hill house gate, the ledger | 0–8 min | `ded_same_saturday` |
| 2 | The Lena's-Thursdays trip (undecided); hide-and-seek; the calendar | 0–15 min | Lena's key, Theo's trust, the scattered memories |
| 3 | Hiring file; the empty house; watching the remark; the locker letter (open); Rafael's confrontation | 0–15 min | `fact_hiring_file`, thread seeds, an ally or a reporter |
| 4 | Route 9; the phone; faster lines | 0–10 min | `fact_shrine`, the truth (or not) |
| 5 | Goodbyes; the walk's callbacks | 0–8 min | Closure lines |
| Every day | Duties vs trips | — | `fact_patients` vs the threads; discovery risk; the charge nurse post |

Replays differ by ending, by route (Force or Despair) and by what Nadia carried into the final talk.

## 9. What has to be built, in one list
| Kind | Items |
|---|---|
| **Waking levels (3D, style E)** | St. Brigid's: nurses' station, room 14, staff room, a ward for duties, Elias's office, the closed wing with the booth. Nadia's flat (a corner). The Lena's-Thursdays place (undecided). The empty Lasname house (reuses the dream house's layout, dusty and real). Route 9 by day |
| **Dream levels (3D, pastel)** | Larch Street (+ garage interior), the house (with a rotating section, and the attic observatory), dream St. Brigid's (two dressings, if kept), the Road (the chase), the car interior (if kept) |
| **Waking characters (3D models)** | Nadia (done), Elias in bed, Rafael, Mags, the Day 2 visitor (possibly LeBaget's), a few patients, Larpaca gang members |
| **Dream characters (billboards)** | Walt, Lena, Theo, soft Elias, the Doctor, two neighbours, Biscuit |
| **Systems** | Day slots and duties; discovery risk; Force and despair (hidden values); vitals; the Long Night lock-in (rule undecided); Argument battles v2 (`ARGUMENT-BATTLES.md`); journal v2 (battle icons, Tell entries); chase pursuers (house fold, flatline wave, headlights); the badge flip (if kept); Theo the guide and light-gated walkways; collectible counts; the umbrella; the ending director |
| **Audio** | The movement's sound set (`MOVEMENT.md`); per-layer ambience; the score's stems per speed tier (composed with the creator) |

## 10. Scope tiers (for the practice game)
| Tier | Contents | Why |
|---|---|---|
| **A: the presentable spine** | The opening and title (style E), Day 1, **Night 1** (street in 3D; its tutorial and its exit are undecided), **Night 4's chase** (with Theo and the phone; it waits on the Night 4 truth), and the car's first beat as a teaser | Everything the 4-minute demo needs; proves the movement, the new battles and the art split |
| **B: the inversion** | Day 2 and Night 2 (the house, pancakes, hide-and-seek, Lena, the tipping house, the umbrella) | The story's turn and the wallrun set piece |
| **C: the full game** | Day 3 and Night 3, Days 4–5, the final talk and the endings | The whole arc |

## 11. The demo route (about 4 minutes, presenter mode)
| Time | Show | Say (one line, roughly) |
|---|---|---|
| 0:00–0:35 | **The opening:** the phone, the voicemail; Nadia falls asleep as it plays | "His sister heard him ask. She fell asleep." |
| 0:35–0:50 | **The title:** the table, the tablet's news, the mug's swirl | "Every menu is a thing on her table." |
| 0:50–1:05 | **Day 1:** room 14, the chart writes a fact; the call log | "Days are the real world: grounded, 3D, investigation." |
| 1:05–1:25 | **Night 1:** the dream street appears; a double jump over the creek | "Nights are his dream: pastel, billboards, and Titanfall movement." |
| 1:25–2:25 | **The first argument** (which battle, undecided): a premise, a piece of evidence and a stated reason; one wrong reason and its pressure phase; one right one | "Arguments are real arguments: claims, premises, evidence, reasons." |
| 2:25–2:55 | **The first escape** (undecided) | "Then the dream throws you out." |
| 2:55–3:50 | **Night 4:** the Road, Theo's torch, the headlights behind, **the phone ringing**; let it ring | "Five nights deeper, to the night of the crash." |
| 3:50–4:00 | **Night 5:** she opens the car door. Elias: "Nads?" Black | "And the ending is whatever you do in that car." |

**Presenter chapters to add:** Opening, Day 1: room 14, Night 1: the street, the first argument, Night 2: the house
tips, Night 3: code blue, Night 4: the road, The car. Attract mode: the course autopilot runs the Road (as it already
does the Movement Lab).
