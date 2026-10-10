# Work log

A running record of the work and the process, newest night on top, so nothing is lost if a Claude session is
compacted or restarted. Claude adds to it as each agent reports and after each verified step. Per the creator's
request (2026-10-10), the **morning report** is built from this log and must be complete on its own: no
"scroll up to find it".

---

## Night of 2026-10-10 → 11

### State at bedtime (about 06:00, 2026-10-10)
**The creator's decisions tonight** (also in `VISITING-HOURS.md`):
- **Structure:** five nights, of different lengths, and play time varies with each player's choices.
- **Threads hinted, not resolved:** Lena missing, the LeBaget/Larpaca crossfire, Elias's money.
- **Endings:**
  - the **Long Night** stays;
  - **Morning** has two paths: **forced** and **chosen** (the old "His Choice", now shown on screen);
  - **Evening**.
- **Argument battles rebuilt from the ground up:** unique art and arenas, stylised icons, no box of rectangles,
  and arguments that make logical sense.
- **The second corporation is Larpaca** (checked: no real company by that name). The news read uses it.
- **Lena:** blonde with long loose waves (she looked too much like Nadia in the bun).
- **The dream versions of characters shouldn't feel overwhelmingly wrong;** billboards against 3D is eerie enough.
- **The bedroom must be a real stylised 3D room,** not gritty or realistic.
- **Tripo credits approved:** Nadia's model done (80 credits, 550 left).
- **Battery:** the laptop stays plugged in; never worry about brief unplugging.
- **Pace:** the creator found today slow. Gameplay is what players remember, then the late game and endings.

**Done today (verified, committed, pushed unless noted):**
- **Presenter mode and footage capture:** all five templates build and pass smoke runs (EditMode 456/456,
  PlayMode 73/73).
- **The movement research** (`RESEARCH-MOVEMENT.md`): mechanics, Source formulas, controller choice, a blueprint,
  chase design, feel (section 8), and **measured Titanfall 2 numbers** (section 9) transcribed from community
  videos:
  - wallrun goal speed 8.64 m/s (31 km/h), max 1.75 s;
  - wall jump 5.21 m/s off the wall, plus up to 1.91 m/s along it, plus up to 4.37 m/s inward;
  - a wall kick within 83 ms gains about 3.1 m/s;
  - crouch kick +1.78 m/s;
  - slide boost cooldown about 2 s;
  - the "lean-in" hand on the wall is the timing cue.
- **Art:**
  - the style exploration (A–E), with **E chosen**;
  - style-E character sheets for Nadia, Elias, Lena, Theo, Mags and Walt (review page
    `FableAnimationTests/art/review/2026-10-10/visiting-hours-characters.html`).
- **Audio:**
  - the creator's voicemail (the earlier take) is in the game;
  - Nicole reads the news report with "her condolences" and "Larpaca".
- **Docs:**
  - `PLAN.md` rewritten;
  - `VISITING-HOURS.md` (the source of truth);
  - `CHARACTERS.md`;
  - `OPENING-AND-TITLE.md` updated;
  - status headers on superseded docs;
  - a README index of every doc.
- **The Rocket League kick:** Easy Anti-Cheat's download timed out at launch (about 120 KB/s), so it ran with no
  anti-cheat module. A temporary connection problem; the connection tested fine afterwards. ExitLag is a
  possible cause but unproven.
- **The battery:** the 80% cap reset after the battery drained to about 2% on 2026-10-09. The fix is in Legion
  Toolkit.

**Running overnight** (results are added below as they come in):
1. **Movement Lab:** round-1 fixes (respawn test timing, the wall catch at high frame rates, corner normals in
   Unity) being verified in the clean checkout `VibeJamKit-play` at 613473c. If the smoke run passes, the
   playable build goes to `VibeJamKit-play/Builds/Play/MovementLab/VibeJamKit.exe`.
2. **The first escape sequence:** a 60–90 s chase through the 3D dream street. The agent builds in
   `Assets/JamKit/Templates/VisitingHours/Dream/`.
3. **Design docs:** `STORY-BIBLE.md`, `CHARACTER-BIBLE.md`, `NIGHTS-GAMEPLAY.md`, `ARGUMENT-BATTLES.md`.
4. **The opening rebuild** in style E: a 3D bedroom, the shot list, the table title, Nadia's model.
5. **Art:** Lena redone; the opening's concept frames and textures.

**Reminders for the morning (first thing):**
1. **The battery cap:** Lenovo Legion Toolkit → Battery → Conservation on, then set it to start with Windows.
2. **Clean the mouse** with compressed air and WD-40 contact cleaner.

### Overnight results
(added as each piece of work reports)

#### Movement Lab: verified, playable build ready (about 07:00)
- **Run in** the clean checkout `VibeJamKit-play` at commit 613473c.
- **Scenes:** OK.
- **PlayMode:** 78 of 79 pass.
- **The real character controller now does what the simulation did.** The momentum chain keeps its speed:
  sprint 9.8 → slide 12.7 → air 11.3 → wallrun 11.3 m/s. The autopilot finishes the whole course.
- **Round-1 fixes that got there:**
  - wall probes read the wall face's own normal (a rounded corner normal peeled runs off at 45°);
  - the autopilot holds a wall's lane until its wall jump (at high frame rates it steered away just before
    contact);
  - the wall catch forgives a view about 12° off;
  - the tests wait for a respawn to really finish.
- **The one remaining failure:** after falling off the course, you respawn at **2.5 m/s instead of sprint
  speed (over 5)**. Sent back to the movement agent, along with a note that a mantle drops you to 0 m/s
  (Titanfall keeps some momentum).
- **Smoke run:** PASS (9.2 s).
- **Playable build: `C:\Users\User\Desktop\yougotgamesonyophone\VibeJamKit-play\Builds\Play\MovementLab\VibeJamKit.exe`**
  - **The course (about 500 m):** a run-up, a slide ramp, a bar to slide under, 5.5 m and 8.5 m gaps, wallrun
    walls of 8, 14 and 21 m, a momentum chain, a wall-jump chimney, mantle ledges of 1.0, 1.5 and 1.85 m, and a
    chase (a wall of dream-static about 12 m behind, two collapses, checkpoints).
  - **Controls:** WASD, mouse, Space (jump and double jump), Left Ctrl / C (crouch and slide; gamepad B),
    Shift is not needed (sprint is automatic). **F3 toggles the speed readout** (m/s and km/h, state, an 8 s
    speed graph, the chase gap). Esc pauses, with comfort settings (FOV, camera roll, head bob, speed effects,
    speed lines). Ctrl+Shift+P is presenter mode, with chapters: course start, wallruns, momentum chain,
    chimney, chase.
  - **Placeholders:** the arms are capsule-shaped, and all sounds are generated.

#### Movement Lab round 2: all green (about 07:45)
- **The slow respawn wasn't a game bug.** The respawn does launch you at sprint speed, but the test measured
  after the fade with no input held, by which time friction had stopped the player. The test now holds
  forward like a player and records the launch speed.
- **Mantles keep 80% of your run's speed,** like Titanfall's. Before, hitting the ledge face clipped your speed
  first, so you left at the 5 m/s minimum. A short jump at 9.5 m/s rescued by a mantle now leaves at 7.3 m/s
  or more.
- **Results:** PlayMode 79/79, Windows build OK, smoke run PASS (9.2 s). The playable build is refreshed (same
  path: `VibeJamKit-play\Builds\Play\MovementLab\VibeJamKit.exe`).
- **The Movement Lab's generated scene, materials, tuning data and config are now committed** to the main repo
  (they had only existed in the clean checkout).

#### Design docs: four proposals written (about 08:00)
Committed as dd72559. Design only: every invented detail is marked as a proposal, each doc opens with a table of
decisions for the creator, and every quoted line is a placeholder for the creator to rewrite. No code or
`script/` files were touched.
- `docs/STORY-BIBLE.md` (about 500 lines), `docs/CHARACTER-BIBLE.md` (about 370), `docs/NIGHTS-GAMEPLAY.md`
  (about 310), `docs/ARGUMENT-BATTLES.md` (about 415).

**The story bible**
- **The core idea:** the crash has an external cause, but the two people who love each other most each think it
  was their fault.
  - Nadia heard "You still good for picking me up?" and went back to sleep. She has never played the rest of the
    voicemail, so the apology and "maybe call Lena" are known only to the player.
  - Elias stayed late to avoid her, called Lena and their son out at 3 AM, and insisted on driving.
- **New facts it proposes:**
  - **The 2:47 AM form:** 17 minutes before the call, Elias named Nadia his health-care proxy and left "what I'd
    want" blank. Rafael was the only witness, so it is legally void. It answers the job remark without her knowing.
  - **The deadline:** LeBaget moves Elias, free of charge, to its care facility "Halcyon" on day 6.
  - **The family money:** the Lasnames were rich; Nadia refused her share.
- **The five nights:**

| Night | Dream layer | Guardian | Peak |
|---|---|---|---|
| 1. The Street | Their childhood street; the same Saturday, 41 times | Walt | The street rolls up behind her like a stage set |
| 2. The House | Lena and Theo, pancakes | Lena | The twist that Nadia is the nightmare: Lena says "He called you first", and Theo has drawn her as "THE BAD DREAM" |
| 3. The Shift | The hospital shift his dream deleted | "The Doctor" (Elias as he was at work) | The job remark, and the half truth |
| 4. The Road | Route 9 at 3:48 AM | No battle; Theo's question | The big chase, with the phone ringing |
| 5. The Car | Nadia gets in the passenger seat: she finally picks him up | Elias | The ending |

- **The three threads** (Lena, the crossfire, the money) each get a table of clues across the nights, with no
  answers. The one reframing reveal is on Night 4: the car was stopped, and Lena got out and walked toward the
  lights with her hand raised. It's Elias's memory, so it may be wrong.
- **Force and the Long Night** are two hidden scores at opposite ends of one line, with sources listed per night.
  The slope starts small on Night 1 (pressing Walt, resting on the porch swing). Each route has one deliberate
  rule-break: on the Force route Lena stops obeying the game's rules; on the Long Night route the waking world
  turns into dream billboards, breaking the art style's own rule.

**The endings.** The four things Walt teaches you to do in an argument on Night 1 become the endings on Night 5.
- **Morning, forced:** Nadia presses in the car. You play the hardest run in the game, dragging Elias by the hand
  up through every collapsing layer. He wakes into fresh grief and stays distant. She finally hears the apology
  alone, in the car. Her guilt isn't resolved; it's doubled.
- **Morning, chosen (the old "His Choice", shown on screen):** she tells him the truth, then asks what he wants.
  - **He wakes if** her confession is told plus two of three other things that hold him: Theo's blessing, Lena's
    truth, a reason to live without his hands. On the Force route he needs all three.
  - **You play** a joyful run up through the layers with him beside you.
  - **The last scene mirrors the opening:** the phone rings and she answers on the first ring. "I'm already outside."
  - Her guilt is resolved between them; she concedes the remark was "half true".
- **Evening:** she lets it rest, or she asks and he chooses to stay. The endless Saturday finally reaches dusk; a
  slow walk up the street where the double jump does nothing. He dies nine days later. She plays the full
  voicemail at his bedside: forgiven, too late.
- **The Long Night:** locked in by the phone prompt ("Your phone is at 3%... Leave it?") on Day 3 or 4, if her
  hidden comfort score is high. The dream offers no resistance; you play a reversed chase, running away from
  waking. In the dream she answers his call and picks him up, forever. Mags plugs in Nadia's dead phone:
  "1 voicemail. Played to 0:09."
- **Is "His Choice" shown? Yes, both ways:** if he chooses to wake, Morning plays in full; if he chooses to stay,
  Evening plays. The journal shows what she has before she gets in the car, so the outcome is earned, not random.

**The character bible**
- Nadia, Elias (with his two dream versions), Lena, Theo, Mags and Walt. For each: personality, history, wants
  and needs, flaws, skills, hinted secrets, an arc table across the five nights, voice notes and three
  placeholder sample lines. A relationships matrix for the six, plus a table for the supporting cast.
- **Proposed supporting characters:**
  - **Rafael Duarte:** a colleague and the other candidate for Nadia's job. He witnessed the proxy form and heard
    Elias say "Did Lena get out?" in the trauma bay.
  - **Corinne Vasse:** LeBaget's community-relations director, who brings pears and asks "Has he said anything?"
  - **Ines Varga:** Lena's studio-mate, who knows Lena lied about her Thursdays.
  - **Julian Cho** in bed 3: a diver who stayed in a dream (the Long Night shown before it can happen).

**Nights gameplay**
- **Days run on time slots.** Short playable duty rounds also give evidence; trips across the city chase the
  threads; skipped duties raise the risk of being found out.
- **One teacher per mechanic:** Mags teaches the rig, the double jump and the wallrun; Walt teaches the slide and
  the argument verbs; Nadia's badge habit teaches Night 3's switch; Theo teaches following a guide, and Tell.
- **Each night has a signature escape:** the street rolling up; the house tipping over while you run its hallway;
  "code blue", flipping the hospital between two moments with Nadia's ID badge mid-wallrun; the Road chase with
  the phone ringing and Theo's torch lighting the way.
- Also: an optional-content table, a build list and scope tiers. **Tier A** is Night 1 complete plus the Night 4
  chase. **The demo route** runs about 4 minutes: opening → Day 1 → the dream street → Walt's argument → the
  escape → the Road chase and the reveal → the car door.

**The recommended battle concept: "Take it apart"**
- **The opponent's case is a physical object in their own arena.** Walt's is a bicycle wheel with the claim on
  the hub and his premises on the spokes; Lena's is the breakfast table, with plates as premises; the Doctor's is
  a lightbox of X-ray films. Premises are facts or links, and some are hidden.
- **The verbs:** Ask, Challenge, Concede, Press (a hold) and Let it rest; Tell arrives on Night 4.
- **A challenge:** pick a piece of evidence, then one of three "because…" reasons: one sound, one true but beside
  the point, one that doesn't follow. The opponent replies logically: concedes, falls back, digs in, or says
  exactly why the reason fails.
- **Action parts:** short themed bursts of the opponent's emotion (Walt's ball bearings, Lena's flour). Arguing
  badly or pressing makes them worse; arguing well calms the room. Catching "slips" (Walt's bike bell) reveals
  what the opponent isn't saying.
- **Nadia's composure** shows as her heart rate. **Teaching:** Walt teaches it all in one battle, as truing a wheel.
- **Walt's battle is written out in full:** every premise, every reason card and each of his replies. The doc
  also covers the other battles, an art and arena table, accessibility, the pure-C# core types and tests, the UI
  Toolkit build, and "what a judge sees in 60 seconds".

**Decisions for the creator** (the agent's list):
1. Keep the unheard apology as the spine (Nadia never listened past "you're probably asleep").
2. The family money: Nadia refused her share.
3. The one reveal: Lena walks toward the lights from the stopped car.
4. "His Choice" leads to Evening as well as Morning (recommended: yes).
5. The 2:47 proxy form, void with one witness.
6. The Halcyon transfer on day 6 as the deadline.
7. Night 3's guardian is "the Doctor".
8. The final argument happens in the car.
9. In Evening he dies, rather than staying in a coma.
10. The phone prompt is the Long Night lock-in, on Day 3 or 4.
11. Allow "the hope" in the final talk: telling him "Lena got out" to make him wake.
12. The invented names: Rafael, Corinne, Ines, Julian, Halcyon, Lena as a ceramicist, Walt calling Nadia "Bell".
13. Gameplay: time-slot days; the Night 3 badge switch (the biggest build, with a fallback); no battle on Night
    4; the escort run; the scope tiers.
14. Battles: concept "Take it apart"; the three-card reason step; keep the short action parts (driven by
    consequences, with slips); composure as heart rate; Press as a hold; no action parts in waking-world
    arguments; the verbs as the endings.

**The agent's view of the risks:** the Night 3 badge switch is the costliest piece to build; the full five nights
is far more than a practice game (Tier A is what a 4-minute demo needs); the docs leave Nadia's mother out
entirely, for the creator to decide.

**My note:** one thing to check against the creator's script: the bible has Elias calling Lena and Theo out at
3 AM and driving them himself. That goes beyond the voicemail's "I'll see if I can get a cab back home, or maybe
call Lena", so it's a proposal, not the creator's fact.
