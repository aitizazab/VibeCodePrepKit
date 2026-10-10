# *Visiting Hours*: what the player does, night by night

> **Status: a proposal for the creator (overnight draft, 2026-10-11).** The story it serves is `STORY-BIBLE.md`;
> the argument battles are specified in `ARGUMENT-BATTLES.md`; the movement it uses is built and documented in
> `MOVEMENT.md` (numbers from `RESEARCH-MOVEMENT.md` sections 4, 6, 8 and 9). Everything here is a proposal.
> Gameplay is what players remember, so every night has one **peak** built to be shown to a judging panel.

## Decisions for the creator
| # | Decision | Recommendation |
|---|---|---|
| 1 | **Days run on time slots** (4 on Days 2 and 3, 3 on Day 4, 2 on Day 5): duties and trips each cost a slot | Keep: it makes "do your job or chase the threads" a real trade |
| 2 | **Duties are short playable rounds** (60–90 s: wristbands, meds, vitals) that also yield facts (patients' gratitude and gossip) | Keep: the job isn't just a cost, it's a different kind of evidence |
| 3 | **Night 3's badge flip** (one hospital in two moments, swapped by swiping Nadia's ID) | Keep as the signature, but it's the biggest build; the fallback is a single-moment hospital |
| 4 | **Night 4 has no argument battle** in the standard route; its peak is the chase, plus Theo's question (where **Tell** is taught) | Keep: one night without a battle is the variety the run needs |
| 5 | **The phone rings mid-chase** on Night 4, and answering it skips the reveal | Keep |
| 6 | **Morning (forced)'s escort run:** holding Elias's hand removes the double jump and caps speed; let go to make a move, catch him within 2 s | Keep: the hardest run in the game, and the one that means something |
| 7 | **Escape records** (fastest climb out per night) in the journal's back page | Optional; off by default for tone, on in presenter builds |
| 8 | **Scope:** for the practice game, build Tier A (section 10) first | Tier A is what a 4-minute pitch needs |

---

## 1. The verbs of the game
| World | Look and feel | What the player does | What they never do |
|---|---|---|---|
| **The day** (waking) | Grounded, muted, stylised 3D; first person; walking pace with a short jog | Examine and chart (facts), talk, do duties, take trips across the city, decide the evening's small things (the phone) | Run fast, jump, fight. No pressure phases in waking arguments |
| **The dive** (dream) | Pastel doodle 3D with billboard people; Titanfall-style movement | Explore, collect memories, make deductions, argue with a guardian, **escape** the dream's defences | Shoot. There are no weapons anywhere in the game |

The contrast is the point: in the waking world Nadia is tired and heavy; in her brother's dream she moves the way he
remembers her, the kid who jumped the creek on her bike.

## 2. The shape of a day
- **Time slots.** A day's slots are shown as the ward clock on the HUD (morning, midday, afternoon, evening). Duties
  and trips each cost a slot; conversations inside the hospital and examining things are free.
- **Duties.** A round is a short, pleasant routine: walk the ward, read a wristband, match the meds, chart the vitals.
  Each patient says something (gratitude for Elias, gossip about the corporations, the cost of things). Rounds done
  across the days build `fact_patients`, one of the ways to release Elias's "Purpose" hold at the end.
- **Skipping a duty.** Rafael covers it. The ward remembers (discovery risk +1).
  - Risk 2+: Rafael confronts her on Day 3 (a short waking-world argument).
  - Risk 4+: on Day 4 the closed wing is locked, and Mags brings her in by the service lift (a one-minute waking sneak:
    no running, just timing the guard's walk).
  - Either way, the charge nurse post is decided by her record (it's in the epilogues).
- **Trips** (optional, two slots across the river, one slot nearby): Lena's studio (Day 2), the empty Lasname house
  (Day 3, needs Lena's key), Route 9 (Day 4).
- **The evening.** The booth; Mags's briefing; **the phone ritual** ("Plug it in?"), which is where the Long Night can
  lock in (see `STORY-BIBLE.md`, section 7).

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
  epilogues (his recovery in Morning, the number of days in Evening).
- **Assist:** at any time, "Mags, turn it down" lowers the dream's pressure (slower pursuer, wider forgiveness, gentler
  patterns). No story cost, ever. (The memory-for-a-shortcut offer is a separate, deliberate story choice.)

## 4. Teaching plan (one tutorial per mechanic, by someone it suits)
Withhold meaning and stakes, never mechanics.

| Mechanic | When | Taught by | How |
|---|---|---|---|
| Walk, look, examine (E) | Day 1, first minute | The game | A controls line at the nurses' station |
| Charting (facts into the journal) | Day 1 | **Nadia's habit** | Examining the chart writes "Charting: GCS 6..." |
| Duties | Day 1 | **Rafael**, at handover | "Bed nine's meds are due. You or me?" |
| The phone ritual | Day 1 evening | **Mags** | "Plug it in if you like. Outlet's there." |
| Deductions | Night 1, arrival | **Mags** | "Two things that can't both be true: put them side by side." |
| Jump, double jump | Night 1 | **Nadia's memory** and **Mags** | The creek she jumped at seven; "You're lighter in here. Again, in the air." |
| Slide | Night 1 | **Walt** | The garage door half up: "Duck, Bell." |
| Escapes (pursuer, dream drop, checkpoints) | Night 1 | **The dream** | The first roll-up is slow; the first fall visibly puts you back |
| Mags's Foresight cue | Night 1 escape | **Mags** | "It's folding left!" |
| Assist | Night 1, after a second fall | **Mags** | "I can turn it down. No shame in it." |
| Argument: Ask, Challenge, Concede, Press, Let it rest | Night 1 | **Walt** | His wheel and spokes (`ARGUMENT-BATTLES.md`) |
| Tether (E) | Night 2 | **Mags** | "If it gets bad, say so. I can pull you halfway. It costs him." |
| Wallrun | Night 2 | **Mags**, then the house | "Walls are only as solid as he remembers them." First over carpet, then over the stairwell |
| A premise that can't be broken | Night 2 | **Lena** | "You don't know where I am either." Concede or let it rest |
| The memory-for-a-shortcut offer | Night 2 escape | **The dream**, with **Mags** warning | "It'll let you through. It keeps what you give it." |
| Wall jump, chimney | Night 3 | **Mags** | The lift shaft: "Kick off one, catch the other." |
| The badge flip | Day 3 → Night 3 | **Nadia's routine** | She swipes her badge at the ward door every day; in the dream the reader shows "the shift as it was" |
| Concede, then challenge the link | Night 1 (Walt), mastered Night 3 | **Walt**, then **the Doctor** | A true spoke, and what he hangs on it |
| Following a guide; light reveals the route | Night 4 | **Theo** | "Follow me, I know the shortcuts." His torch lights paths that are only there in the light |
| Tell | Night 4 | **Theo** | His question: only honest answers count |
| The final talk (no pressure) | Night 5 | **The car** | Nothing attacks. Nadia: *"He isn't fighting me."* |
| Escort run / the run together | Night 5 | In place | Elias stumbles; she catches him. Or he runs beside her |

## 5. The curve: variety and difficulty
| | N1 The Street | N2 The House | N3 The Shift | N4 The Road | N5 The Car |
|---|---|---|---|---|---|
| **Exploration** | Open street, small | Impossible house (rooms rearrange) | Two-moment hospital | Linear road | A car interior |
| **Puzzle** | Walt's garage key (or force it); count the ledger | Follow Theo's height marks to the real hallway; hide-and-seek | Route through two moments (an open door in one, a gurney ramp in the other) | Light reveals the path | None |
| **Deductions** | 2 (1 key) | 2 (+1 hidden) | 2 (+1 hidden) | 1 (the reveal) | — |
| **Argument** | Tutorial (4 premises) | Hidden premises; one unbreakable | Half truths; concede-then-challenge | Tell (Theo) | The verbs become the endings |
| **Escape** | ~50 s; jump, double jump, slide | ~75 s; + wallrun, a turning house | ~90 s; + wall kicks, chimney, badge flips mid-run | ~3 min in 3 legs; everything at speed, a guide, a phone | Ending-specific |
| **Pursuer** | Lawns rolling up | The house folding shut | A flatline wave down the corridor | The car's headlights | The dream collapsing / the morning |
| **Peak (the clip)** | The street struck like a stage set behind you | Running along a hallway as it turns into the floor | The badge flip mid-wallrun: crowded corridor to empty and back | The phone ringing while you outrun the headlights; Lena walking into the light | Opening the car door |

## 6. Night by night

### Night 1: The Street (20–30 min)
**Day 1 (St. Brigid's; guided, no slots).** Handover with Rafael; one duty round (the tutorial round); room 14 (chart,
effects bag, photo, LeBaget's flowers); the police envelope (the call log, never commented on); the phone ("Not now.");
the staff-room poster (Advance Care Planning Week, background); 11 PM, the booth, Mags, bed 3's curtain.

**The dive.**
- **Explore** Larch Street at golden hour: about 120 m of street, Walt's garage, five houses, the creek, the chained
  gate of the hill house at the top. Neighbours' lines change each night.
- **Memories:** Walt waving, Saturday's paper, the shop sign, the neighbours' "Saturday", **Walt's ledger** (inside the
  garage: 41 identical lines).
- **Puzzle:** the garage is locked. The key is on the keyring in Elias's effects bag, and its dream twin hangs on the
  hook by the back door of house 3. Or force the lock (Force +1, quicker).
- **Deductions:** `ded_walt_dead` (key), `ded_same_saturday` (optional, from the ledger and the paper).
- **Argument:** Walt at the workbench (the tutorial; written out in `ARGUMENT-BATTLES.md`).
- **Escape, "The street rolls up"** (~50 s, 3 checkpoints):
  1. Lawns roll up behind her like carpet (the pursuer, rubber-banded 12 m behind). Sprint the pavement; **slide** under
     the half-open garage door (1.15 m bar).
  2. **Jump** the creek (5.5 m), then **double jump** a fallen fence over a ditch (8.5 m). House flats fold flat
     toward the route, pointing the way.
  3. The last stretch to the bus-stop light at the end of the street. **Convinced:** Walt holds the garage's back door
     open (a shortcut through the shop). **Pressed:** the static swarms thicker and the roll-up runs closer (10 m).
- **Peak:** the street being struck like a stage set behind her, in pastel, at golden hour.
- **Optional:** the porch swing (rest; Belonging), the lemonade (Belonging), the neighbours' chatter, the hill house
  gate (it never opens).
- **Build:** Day 1 (exists in the slice; restyle to style E); the booth; Larch Street in real 3D (not flats); Walt's
  garage interior (it's also the argument arena); the roll-up and fold-flat collapse; static swarm particles; Walt,
  neighbours, Biscuit, soft Elias as billboards; the ledger prop; Walt's battle (art in `ARGUMENT-BATTLES.md`).

### Night 2: The House (25–45 min)
**Day 2 (4 slots).** Corinne's visit and the Halcyon offer (fixed); Rafael's "Did Lena get out?" (free, if she talks to
him); duty rounds; **trip: Lena's studio** (2 slots: Ines, Lena's shelf, the Thursdays, Lena's key); **bed 3** after
dark (free, before the booth). Typical choices: do 2 rounds and visit the studio, or do all 4 and skip it.

**The dive.**
- **Street** (short): "Morning, Nadia!" The dream knows her name.
- **The house:** a two-storey family home, rebuilt so it misbehaves. Hallways stretch; doors loop back to the kitchen.
- **Puzzle:** Theo's height marks are only on the real kitchen doorframe. Follow doors that have them to reach the
  stairs.
- **Wallrun, taught safely:** the stretched upstairs hallway (an 8 m wallpapered wall over carpet), then over the open
  stairwell (a 14 m wall over a drop).
- **Hide-and-seek with Theo** (optional, 3–6 min): he hides in five places; each find shows a memory (his drawings, the
  calendar's keys, the fourth chair, the photo wall). Letting him keep hiding is Belonging.
- **Theo's first question:** "Is Dad going to be sad?" (honest, comfort, or silence).
- **Deductions:** `ded_she_knows`; `ded_saturday_off` (key); optional `ded_thursdays` (the calendar's keys and Ines's
  story: "Lena was somewhere else on Thursdays").
- **Argument:** Lena at the kitchen table (`ARGUMENT-BATTLES.md`). "He called you first." The drawing of THE BAD DREAM.
- **Escape, "The house tips"** (~75 s, 4 checkpoints):
  1. The kitchen floor tilts; run the length of the tilting table and through the hatch.
  2. The hallway turns over **while she runs it**: wallrun the wall as it becomes the floor, wall-to-wall across the
     doorframes.
  3. The staircase flattens into a ramp: **slide** down it for a boost and jump out of the slide into the hall.
  4. The front door, into the rain. The pursuer is the house folding shut behind her, room by room, with Theo's
     drawings flapping in the static.
  - **Convinced:** Lena sends Theo to open a window shortcut. **Pressed:** the house folds faster.
  - **The memory offer:** at the hardest spot, after two falls, the dream offers to let her through for a memory.
- **Peak:** running along a hallway as it rolls into the floor.
- **Build:** the house (modular interior, a rotating section for the escape), Lena and Theo billboards (style E,
  Lena blonde with long waves), the kitchen arena, Theo's drawings (6–8, including THE BAD DREAM), the calendar,
  height marks, Corinne (3D), Ines and the studio (3D), bed 3 (reuse room 14's bed and rig).

### Night 3: The Shift (25–40 min)
**Day 3 (4 slots).** The hand note in room 14 (fixed); **Elias's office** (1 slot, required: the proxy form; the hiring
file optional); **trip: the empty Lasname house** (2 slots, needs Lena's key: Theo's room, Lena's coat and parking stub,
the lawyers' letter); Rafael's confrontation (if risk 2+); duties. The badge swipe at the ward door every time she
enters (the teaching for tonight).

**The dive.**
- **Street and house** (short, shuttered; Lena in the window).
- **Dream St. Brigid's:** pastel, empty, every monitor perfect. A loop of three wards, a staff room, the trauma bay, and
  a central atrium with a lift shaft.
- **The badge flip (the signature):** swipe at any reader to switch between **the dream's shift** (empty corridors,
  doors locked) and **the shift as it was** (crowded, loud, doors open, gurneys everywhere). Same geometry; the
  dressing, the doors and the obstacles change. Puzzle: reach the trauma bay using an open door in one moment and a
  gurney stack (a ledge to mantle) in the other.
- **The remark** plays behind the staff-room door, in "as it was" only. Watching is optional.
- **The locker letter** ("Appointed on merit"): Belonging.
- **Wall jump and chimney:** the lift shaft (two walls 3.5 m apart, 4 m to climb), taught by Mags.
- **Deductions:** `ded_shift_deleted` (the dream's empty shift and the remark that's only in the real one: "He didn't
  dream a memory. He dreamed the day without what he said."); optional `ded_he_paid_it_back` (the hiring file and
  Rafael's post).
- **Argument:** the Doctor, at the lightbox (`ARGUMENT-BATTLES.md`).
- **Escape, "Code blue"** (~90 s, 5 checkpoints):
  1. The perfect monitors flatline in sequence down the corridor (the pursuer: a wave of light and sound). Sprint.
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
  proxy form, the hiring file, the empty house (waking, 3D). **Fallback if over budget:** one moment only, and the
  remark as a single scene; keep the chimney and the flatline chase.

### Night 4: The Road (15–30 min)
**Day 4 (3 slots).** The vitals drop, Corinne's papers (fixed); **trip: Route 9** (1 slot: the Larpaca cordon, the
shrine, the skid marks; in the Force route, the glimpse of blonde hair); Mags's story of bed 3 (if Nadia has her trust:
she didn't press Mags about it on Day 2, and went back to the booth on time); duties; the evening phone ritual (the
Long Night lock-in, if not already locked).

**The dive.**
- **Leg 1, the dark street** (~40 s): the first time the dream is dark. Theo waits with a torch (if earned). His beam
  lights paper walkways that only exist in the light: follow him or lose the route.
- **Breather:** the house's lit windows; Lena at the door (Force route: she steps out and blocks it; go round by the
  roofs).
- **Leg 2, Route 9** (~70 s): the pursuer is the car's headlights coming down the road behind her. Vault cars, wallrun
  the corporate billboards along the verge (12–15 m each), slide under a fallen barrier, cross the road between the
  two sweeping searchlights (being caught in one throws long shadows that hide the route, so you lose the line, never
  your life). Tracer fire falls as slow paper streamers to weave through.
  - **The phone rings** mid-leg: "Elias". Answer (slow motion; "You still good for picking me up?"; say "On my way" and
    the dream rewrites the night, she's driving, the leg ends early and **the reveal is skipped**) or let it ring and
    keep running.
- **Breather:** a bus shelter, rain on the roof, Theo catching his breath. (Without Theo, Mags on the earpiece.)
- **Leg 3, the crash point** (~40 s): wall-to-wall between two lorries, a double gap, a long slide down the wet
  camber. Then **the freeze** (the reveal): the stopped car, the passenger door, Lena walking into the light.
- **Theo's question** at the guardrail: the **Tell** tutorial. Honest answers only count; an honest one gives his
  blessing.
- **Force route:** Lena's rule-break argument in the road (her patterns ignore the arena and cover the buttons).
- **The end:** Elias on the guardrail: "Nads?" Mags pulls her up.
- **Peak:** the phone ringing while you outrun the headlights. **The best clip in the game.**
- **Optional:** Route 9 by day; Mags's story; the faster lines (a billboard chain that skips the barrier).
- **Build:** the Road (the largest level: about 600 m of chase, three legs and two breathers), the headlights pursuer
  (rubber-banded, `MOVEMENT.md`'s pursuer), Theo's torch (light-gated walkways), searchlights, paper tracer streamers,
  corporate billboards (fictional brands only), the car (it's also Night 5's arena), the reveal freeze; Route 9 by
  day (3D, waking), Larpaca guards and drones (3D, faceless helmets: cheap to model).

### Night 5: The Car (10–30 min)
**Day 5 (2 slots, all optional).** Goodbyes: Rafael, Ines (a bowl, unfired), Corinne, Mags, room 14. In the Long Night
route the day is wrong: billboards in the ward, "Saturday" on the board.

**The dive.** No defences. The street, house and hospital are dim and empty; a walk, not a run (about 2 minutes, with
callbacks: Walt's garage light, a drawing on the pavement). The car on the shoulder of Route 9. She gets in.
- **The final talk** (`ARGUMENT-BATTLES.md`, "The car"): Tell, Press, Let it rest, Concede.
- **The ending's playable part:**

| Ending | What you play | Length | Feel |
|---|---|---|---|
| **Morning, chosen** | **The run together:** road, hospital, house, street, in reverse, with Elias running beside you (the course autopilot drives him). No pursuer; every layer folds away gently behind you. He keeps up; he whoops on a wall kick | ~2 min | Joy. The movement at its best, with company |
| **Morning, forced** | **The escort collapse:** the same route, collapsing. Holding his hand: no double jump, top speed capped at about 7 m/s. Let go to make a move, catch him within 2 s or he falls back to the last checkpoint. Guardians help or hinder per their outcomes. Force route: Lena pursues, then appears ahead | ~3 min | The hardest run in the game, and the heaviest |
| **Evening** | **The walk:** up Larch Street at dusk. The double jump does nothing; there's nowhere to go | ~2 min | Stillness |
| **The Long Night** | **The reversed chase:** run away from the morning, toward the house. The pursuer never catches you, even if you stop | ~1.5 min | Wrong, in a way you feel in your hands |

- **Build:** the car interior (arena), the four ending sequences (three reuse the layers in reverse; the walk reuses the
  street at dusk), the epilogue shots (graves, the lobby, the phone, the booth), soft Elias's run animation for his
  billboard (or a simple 3D stand-in in motion), the escort tether.

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
- **Outcomes change the chase:** Convinced opens a shortcut; Pressed brings the pursuer closer.
- **Comfort:** FOV, camera roll, bob, speed effects, speed lines (all in `MOVEMENT.md`); the assist option.

## 8. Optional content (why two players' nights differ)
| Night | Optional | Time | What it gives |
|---|---|---|---|
| 1 | Neighbours, porch swing, lemonade, hill house gate, the ledger | 0–8 min | `ded_same_saturday` (helps Walt's battle), Belonging |
| 2 | Lena's studio; bed 3; hide-and-seek; the calendar | 0–15 min | `fact_lena_thursdays`, Lena's key, `fact_bed3`, Theo's trust, `ded_thursdays` |
| 3 | Hiring file; the empty house; watching the remark; the locker letter; Rafael's confrontation | 0–15 min | `fact_hiring_file`, thread seeds, Belonging, an ally or a reporter |
| 4 | Route 9; Mags's story; the phone; faster lines | 0–10 min | `fact_shrine`, `fact_car_stopped`, the reveal (or not) |
| 5 | Goodbyes; the walk's callbacks | 0–8 min | Closure lines; Ines's bowl |
| Every day | Duties vs trips | — | `fact_patients` vs the threads; discovery risk; the charge nurse post |

Replays differ by ending, by route (Force or Long Night) and by which truths you carried into the car.

## 9. What has to be built, in one list
| Kind | Items |
|---|---|
| **Waking levels (3D, style E)** | St. Brigid's: nurses' station, room 14, staff room, a ward for duties, Elias's office, the closed wing with the booth and bed 3. Nadia's flat (a corner). Lena's studio. The empty Lasname house (reuses the dream house's layout, dusty and real). Route 9 by day |
| **Dream levels (3D, pastel)** | Larch Street (+ garage interior), the house (with a rotating section), dream St. Brigid's (two dressings), the Road (the chase), the car interior, the street at dusk |
| **Waking characters (3D models)** | Nadia (done), Elias in bed, Rafael, Corinne, Ines, Mags, a few patients, Larpaca guards (helmets) |
| **Dream characters (billboards)** | Walt, Lena, Theo, soft Elias, the Doctor, two neighbours, Biscuit; Long Night route: billboard versions of Rafael and a patient |
| **Systems** | Day slots and duties; discovery risk; Force and Belonging (hidden); vitals; the phone ritual and lock-in; Argument battles v2 (`ARGUMENT-BATTLES.md`); journal v2 (battle icons, Tell entries); chase pursuers (roll-up, house fold, flatline wave, headlights); the badge flip; Theo the guide and light-gated walkways; the escort tether; the ending director |
| **Audio** | The movement's sound set (`MOVEMENT.md`); per-layer ambience; the score's stems per speed tier (composed with the creator) |

## 10. Scope tiers (for the practice game)
| Tier | Contents | Why |
|---|---|---|
| **A: the presentable spine** | The opening and title (style E), Day 1, **Night 1 complete** (street in 3D, Walt's battle v2, the roll-up escape), **Night 4's chase** (with Theo and the phone), and the car's first beat as a teaser | Everything the 4-minute demo needs; proves the movement, the new battles and the art split |
| **B: the inversion** | Day 2 and Night 2 (the house, Lena, the tipping house) | The story's turn and the wallrun set piece |
| **C: the full game** | Day 3 and Night 3, Days 4–5, the final talk and the four endings | The whole arc |

## 11. The demo route (about 4 minutes, presenter mode)
| Time | Show | Say (one line, roughly) |
|---|---|---|
| 0:00–0:35 | **The opening:** the phone, the voicemail, Nadia drops it at "you're probably asleep" | "His sister heard him ask. She went back to sleep." |
| 0:35–0:50 | **The title:** the table, the tablet's news, the mug's swirl | "Every menu is a thing on her table." |
| 0:50–1:05 | **Day 1:** room 14, the chart writes a fact; the call log | "Days are the real world: grounded, 3D, investigation." |
| 1:05–1:25 | **Night 1:** the dream street appears; a double jump over the creek | "Nights are his dream: pastel, billboards, and Titanfall movement." |
| 1:25–2:25 | **Walt's argument:** a premise, a piece of evidence and a stated reason; one wrong reason (his pressure: ball bearings and the spinning wheel); one right one (a spoke snaps; he concedes); the wheel spins true | "Arguments are real arguments: claims, premises, evidence, reasons." |
| 2:25–2:55 | **The street rolls up** behind her | "Then the dream throws you out." |
| 2:55–3:50 | **Night 4:** the Road, Theo's torch, the headlights behind, **the phone ringing**; let it ring; the freeze: Lena walks into the light | "Five nights deeper, to the night of the crash." |
| 3:50–4:00 | **Night 5:** she opens the car door. Elias: "Nads?" Black | "And the ending is whatever you do in that car." |

**Presenter chapters to add:** Opening, Day 1: room 14, Night 1: the street, Walt's argument, The street rolls up,
Night 2: the house tips, Night 3: code blue, Night 4: the road, The car. Attract mode: the course autopilot runs
the Night 1 escape and the Road (as it already does the Movement Lab).
