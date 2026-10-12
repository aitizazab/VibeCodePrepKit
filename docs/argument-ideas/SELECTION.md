# Lena's battle: the selection

> **Status: a selection for the creator (2026-10-12).** It picks from `BRAINSTORM.md` (40 original ideas, cited here
> as "brainstorm N") and `BORROWED-MECHANICS.md` (30 borrowed mechanics, "borrowed N"), chosen for how well they fit
> Lena. Nothing is built, and no code was changed. Every quoted line is a placeholder that shows what the line has to
> do. The creator writes the words.

## At a glance
| # | Mechanic | The Lena in it | Cost |
|---|---|---|---|
| 1 | **She sketches it first:** every attack is pencilled before it's inked | Curious, ordered: the pencil | S |
| 2 | **A gesture for every reply:** she teases, then stops joking when it matters | Quick, warm, teasing, plain-spoken | S–M |
| 3 | **Hush, and answer me:** her care passes if you hold still; her questions hurt if you don't move. Set piece: the three rings | Caring (and its flaw), plain-spoken | S |
| 4 | **She takes notes on you:** pencil crosses where you always go | Curious | M |
| 5 | **The seat she keeps:** a safe seat that nothing true ever reaches | Caring, and keeping | S–M |
| 6 | **Pages from her notebook:** catch what she said, and hold her to it | Curious, ordered, plain-spoken | M |
| 7 | **The sky:** after "He called you first" the ceiling lifts, and her case is a constellation you move along | The stars | **L** (the one L) |
| 8 | **The last constellation:** she draws a sky for you to follow, or erases her last line herself | Caring; the stars | S (on top of 7) |

**The shape in one line:** a kitchen act (what Elias kept of her: the table, the plates, the flour), the three rings of
"Why didn't you pick up?", the ceiling opening, a sky act (what she was: the pencil, the notebook, the stars), and a
closing phase that isn't against you.

---

## 1. A short read of Lena

**The creator's words for this step:** "her curious or caring nature, or something else, like her association with
Stars." Treated as canon. The character bible's personality line (quick, warm, teasing, plain-spoken, fierce) is used
too.

| Trait | Evidence | What the battle should do with it |
|---|---|---|
| **Curious** | The creator, above. Art notes, round 3 (`CHARACTERS.md`): "The pencil shows a curious, ordered mind: neat, sharpened, always in the same place ... the habit of someone who notes things down and keeps things in order." The closed notebook. *Not* in the bible's personality line | She watches, notes and sketches. Her attention is a force in the box |
| **Ordered** | The same pencil note | Her attacks are drawn before they're made: tidy, legible patterns. Fairness as her character, not a rule laid over her |
| **Caring** | The creator. The bible: "Fierce about the people she loves", calls Elias "love". Toward Nadia (bible matrix): "Fond; sees Nadia's guilt before Nadia does". Her Heard-out reply: "...You do, don't you." Her flaw: **"Protects by keeping out"** | Care that holds you in, and care that invites you to sit. Both can be a trap |
| **The stars** | The creator settled a stargazing hobby (note in `STORY-PROSE.md`: "We'd settled on giving her a stargazing hobby, so maybe a small observatory in the attic?"). `DREAM-HOUSE.md`: the attic observatory (approved by the creator, with a raised roof section over it) with its round window full of stars, "always night in Lena's attic". The creator's Morning, chosen: Nadia takes Elias out of the city to go stargazing | The sky is her place in the battle, and a constellation is the shape of her case |
| **Quick, warm, teasing** | The bible: "Laughs at her own jokes before the end. Stops joking mid-sentence when something matters." | The rhythm of her gestures: tease, then mean it |
| **Plain-spoken** | The bible: "Short, warm, direct." | What she says is what you dodge, and her own words can be held to |
| **Fierce** | The bible; "Get out of my kitchen." | The hard phases, when she means it |
| **Private** | The creator, round 3: "composed, guarded, a little unreadable, a private life, so that her going missing stays a real open question". The bible: dream-Lena knows none of her secrets, because she's built from what Elias knew | The box may show that she has a private life (a closed notebook, a sky), never what it is |
| **The house obeys her; she knows she's a memory** | The bible | She can reshape the room (the ceiling opening) |

**A small fix for the docs (not made here):** the bible's personality line should add *curious* and *caring*, since the
creator calls her that and this selection leans on both.

**Conflicts the creator must settle**
1. **Her passion: settled.** She's a stargazer (the creator, 2026-10-12: the ceramics material is scrapped). Her
   art notes (round 3) say **"no workwear, no apron, no clay"** and that she doesn't need to work. The Heard-out keepsake
   (the apron hair tie in the prototype) needs replacing; what replaces it is undecided. Nothing selected uses clay.
2. **The flour and batter.** They don't depend on clay; they depend on her being the cook in the dream's kitchen, which
   the creator endorsed ("Sunday pancakes", `DREAM-HOUSE.md`). They survive if the kitchen is read as **Elias's memory of
   Sundays** (it is his dream). What doesn't survive "no apron": the apron hair tie, and wind-ups like "dusts flour from her
   hands". Also, borrowed 19's "notebook to her chest" is out: the creator rejected that pose (it read religious; she
   holds the notebook loosely in one hand). **This selection's answer:** the kitchen things are the *hazards* (what he
   kept of her), and her own things (the pencil, the notebook, the stars) are the *telegraphs, the pages and the sky*
   (what she was). If she isn't the cook at all, the hazards need new material, but mechanics 1 to 6 still stand,
   because they're about *how* she attacks, not *with what*.
3. **Who her care covers.** Fierce for Elias and Theo. For Nadia: fond, and she makes it personal (her
   flaw). Mechanics 3, 5 and 8 show a care that reaches Nadia (a hug if you hold still, a seat, a
   shared sky). If it doesn't reach her, they still work, but they read as **control** (holding her still, sitting her
   down) rather than kindness. See question 2.

---

## 2. The selection

Each entry has the same headings: what it is, the Lena in it (and how a player feels that without being told), how it
reads as argument, how it keeps the fairness rule, how it answers "the dodge feels small", where it sits, the tutorial
moment, and the cost and risks.

**What the fairness fix already gives us.** The separate fix in progress (`PressurePhase.cs`, `DodgePlanner.cs`)
already gives every attack a harmless warning state (`warn`, 0.65 s on Normal and 1 s in Story), refuses any attack
that starts inside the box without one (`PressureRule.Admit`), and drops any attack that would leave no way out (the
planner). Several picks below are mostly *how that warning is drawn* and *what it's for*, which keeps them cheap.

### 1. She sketches it first (S)
*Sources: borrowed 17 (Just Shapes & Beats' warning lines), the brainstorm's "ghost first" rule, brainstorm 3 (batter
with shadows), brainstorm 1 (laying the table) as the teaching pattern, borrowed 19 (wind-ups) cut down to her pencil.*

**What.** Everything that will appear inside the box is first drawn in **pencil**: a quick, slightly sketchy graphite
line in its exact shape (a circle with the gap left out for a ring, a cross where a lob will land, a placemat where a
plate will stop). Then it's **inked**, solid, in her wine red. Anything that enters from outside gets a short pencil tick
at the edge where it will come in. On her sprite, she takes the pencil from behind her ear before a pattern (one
wind-up pose).

- **Lena:** the pencil is her most recognisable detail, and the creator gave it a meaning: "a curious, ordered mind".
  The player watches her *draw* each attack and learns she plans everything neatly before she does it. Nobody has to
  say so.
- **As an argument:** she's someone who worked her case out in advance. The sketch is her reasoning; the ink is her
  saying it.
- **Fair:** it *is* the rule, as a style. Every attack inside the box gets at least 0.5 s of pencil (the fix's own
  minimum); anger may shorten it, never remove it. Pencil and ink differ in texture, not only colour.
- **Small:** it makes denser patterns inside the box fair, so later phases can fill the box without ambushing anyone.
  And it gives the player a reading job in the half second before every attack: the dodge starts before the hazard
  does.
- **Where:** every phase, from the opening.
- **Teach:** the opening phase is **laying the table**, at half speed, under "Sit down, Nadia. Pancakes are nearly
  done." Four placemats are pencilled; four plates slide in from the edges and stop on them; cutlery lines are pencilled
  between them, inked, and swept away. Nothing reaches the middle. If a prompt is wanted, one: "Pencil first. Then it's
  real."
- **Cost and risks:** S. Drawing only, over the warning state the fix already has; the wind-up is one more expression on
  her billboard (art). Risk: the pencil must read against the table at full speed and with "reduce effects" on.

### 2. A gesture for every reply: teasing, then she means it (S–M)
*Sources: brainstorm 21 (every reply has a gesture), brainstorm 37 (tease, then mean it), borrowed 7 (what you said
changes how the attack looks), borrowed 22 (her expression picks the pattern), borrowed 11 (Touhou's spell-card
declarations) for the full phases.*

**What.** After **every** exchange (an answer, a concession, a fall back), Lena makes one **gesture** in the box: 2–3 s,
a single wall, throw or ring, played *under her reply line*. Its shape is the reply's feeling (her expression already
follows her last line):
- **warm or teasing:** the gesture teases. A plate slides in, stops short of the middle, slides back out; she laughs it
  off;
- **rattled** (a sound challenge landed): a dropped cup, a badly aimed lob;
- **hurt or angry** (a wrong reason, a retort, a press): the full 5–8 s phase, as now;
- **when the argument touches something real** (a key link breaks, she falls back): the tease **stops mid-pattern**.
  Everything drops off the table, a full second of nothing, then the real attack.

Full phases open with the line they come from, written across the box in her hand for 0.6 s ("He chose this."), as
Touhou names its set pieces.

- **Lena:** the bible's voice, played: she "laughs at her own jokes before the end" and "stops joking mid-sentence when
  something matters". The player learns that the sudden empty table means she's about to be serious. Nobody explains it.
- **As an argument:** the box becomes her body language: every reply has a feeling you can see. A concession gets a
  soft gesture, a sound challenge a rattled one. The written line ties each full phase to one sentence of hers, so
  dodging it is *hearing that sentence*.
- **Fair:** single hazards, entering from outside with a pencil tick. Teases never reach the middle. The serious turn
  is preceded by the empty second (the silence is the telegraph), and then the normal rules apply.
- **Small:** **this is the volume fix.** A clean run goes from about 11 hazards to about 50 (section 3) without any phase
  getting longer, and the dodge stops being only a punishment for arguing badly.
- **Where:** every exchange, the whole battle. The kitchen act's gestures are plates and cups; the sky act's are single
  shooting stars.
- **Teach:** the first Ask. Its gesture is a tease that can't hurt, and her reply laughs it off. No prompt: nothing hits.
- **Cost and risks:** S–M. A "gesture" level below heat 0 (one spawn, 2–3 s) in the engine's queue, a preset per
  expression, the stop-mid-pattern rule, the written line. Risk: a gesture after every click could feel like a toll. It
  plays *while her reply plays*, so it adds no time; the speed setting and Story mode cover the rest. "No clock on
  thinking" holds: gestures come after a choice, never during one.

### 3. Hush, and answer me (S), with the three rings
*Sources: borrowed 1 (Undertale's blue and orange attacks), brainstorm 2 (the embrace: rings that close in from outside,
the fair replacement for the blooms), brainstorm 20 (the question she keeps asking) as its set piece.*

**What.** Two kinds of her feeling, shown by shape:
- **Hush** (soft, round, a dashed stroke): her care. Flour rings that **close in from outside**, centred where Nadia
  stood a moment ago, like a hug. They pass through harmlessly **if Nadia holds still**, and hurt if she struggles.
- **Answer me** (sharp, a jagged stroke): her challenges. Plates and lobs that hurt **only if Nadia stays still**.

Most patterns stay ordinary. Hush and answer-me come with particular lines.

**The set piece: three rings.** When "He called you first" surfaces, the phase is a phone ringing. A phone pulses at the
box's edge and rings; each ring brings one wave, each harder than the last, and **every wave is answer-me: standing
still hurts.** Nadia stood still that night. After the third ring, silence, and her line: "So he called me."

- **Lena:** her caring and her flaw in one rule. Her care holds you still ("Shh. Just listen, Nadia."); her plain-spoken
  questions demand you move. The player feels the hug by holding still and letting the ring pass through them. They
  don't need telling that it's her tenderness, or that it's also her keeping you in.
- **As an argument:** the dodge asks the argument's own choice, listen or answer back, with the body. At the painful
  premise, the rule *is* the night: not answering is what hurts.
- **Fair:** the kind is shown by shape and stroke (never colour alone) as it enters from the edge. Hush rings close in
  from outside, with their gap in view the whole way. The phone rings and pulses before every wave. The planner checks
  that answer-me waves leave a way through for a light that keeps moving.
- **Small:** holding still becomes a decision, so even a sparse box asks something every second. And the battle's
  heaviest moment becomes something you can count: 9 s, three waves.
- **Where:** **hush** comes with her grief (conceding "Out there, Theo is dead" brings a single hush ring as its
  gesture); **answer-me** comes with her questions ("Then tell me where I am, Nadia."); the **three rings** come with "He
  called you first".
- **Teach:** each kind first comes alone and can't hurt. Hush: after conceding Theo, one ring closes in slowly, with her
  "Shh." and the prompt "Hold still." Answer-me: with her first question, one slow plate that stops at the light if it
  doesn't move, and the prompt "Move." By the three rings both are known.
- **Cost and risks:** S. A flag on rings, walls and lobs and a "moving this tick" check; the three rings are a scripted
  three-wave phase, a phone icon and a ring sound (S). Risks: "still" on a gamepad needs a dead zone, and "moving" a
  minimum speed, so stick drift doesn't count; the planner must know the moving rule. Story mode: nothing hurts, and the
  phone still rings.

### 4. She takes notes on you (M)
*Sources: brainstorm 33 (the pencil marks), brainstorm 27 (she noticed).*

**What.** Lena keeps a tally of where Nadia's light spends its time. Once she's seen enough, some phases open with
**pencil crosses** on the table where Nadia usually goes, and the next volley lands there. After each full phase she says
**one line about how Nadia moved**: stayed near her side ("You stayed close."), kept to the far wall ("You always keep
your distance, Nadia."), caught her page ("...You saw that."), sat down (see 5: "You sat down. ...Thank you.").

- **Lena:** curiosity turned on you. She watches, she notes it down, and she's right. A player feels it the second time a
  cross lands exactly where they always hide.
- **As an argument:** her case is "He chose this. I know him." This shows she knows Nadia too. Her line about distance
  is her case against Nadia, read off how the player actually played. The counterplay (change your habits)
  is Nadia's lesson too.
- **Fair:** the crosses are the telegraph (0.8 s at least), the volley lands only where they're drawn, and the planner
  checks for a way out.
- **Small:** it stops a player from sitting in one safe corner, so the whole box gets used; and how you dodge now has a
  consequence you can read in the transcript.
- **Where:** from the second full phase on (in a clean run, the three rings); a line after each full phase.
- **Teach:** the first crosses come with her line "You always go left, Nadia." and a long pencil (1.2 s).
- **Cost and risks:** M. The tally is cheap; the crosses are a pencilled volley (S); six to eight short lines and the
  rule that picks one. Risk: the lines must not nag: one per full phase at most, and none in Story mode unless the
  creator wants them there.

### 5. The seat she keeps (S–M)
*Source: brainstorm 36.*

**What.** Once Nadia breaks "So there's nothing left for him out there" with the fourth chair, the box grows a
**chair-shaped alcove** on one edge: Nadia's seat. Nothing can reach it. But her pages, Theo's planes and the sky's lights
never go there either. The seat is **closed during the three rings**: that question can't be sat out.

- **Lena:** care as an invitation ("Sit down, Nadia" was her first line), and her flaw, protecting by keeping. A player
  who sits feels both: safe, and left out of everything that matters.
- **As an argument:** a real choice. Sitting is safe and comfortable and hears nothing; staying at the table's edge is
  where the truth comes out. It's the fourth chair turned into a choice about how to spend the night. It can also count
  toward the Despair route's hidden value, if the creator wants.
- **Fair:** a safe zone, drawn soft (dashed); nothing to telegraph.
- **Small:** it adds no hazards; it adds a decision to every phase after it (sit, or go out for the page), and it makes
  the pages something you leave safety to get.
- **Where:** from the fourth chair's challenge to the end. In the sky act it's her chair by the telescope.
- **Teach:** it draws itself while her rattled reply to the fourth chair plays,
  and the light is free to try it. In the next phase, a page drifts past outside it.
- **Cost and risks:** S–M. A zone the spawns and the planner leave alone, slip paths that avoid it, a seat sprite, a
  line. Risk: a cautious player could sit out every gesture; mechanic 4's lines notice, and the three rings close it.

### 6. Pages from her notebook (M)
*Sources: brainstorm 13 (her own words), borrowed 18 (a slip from her star log), borrowed 28 (the discordant feeling).*

**What.** In some full phases one slip is **a page from her notebook**, not one of Theo's planes. It moves wrongly
for the pattern (slow when everything else is brisk). Catching it adds **a thing she said tonight** to the evidence tray,
as a quote in her handwriting. A quote can be shown against a premise it contradicts: her own words against her case.
Example (placeholder content): catch "Pancakes. Sunday. All of us at the table." (her answer when asked about "This is
the life he wanted") and show it against that same premise, with the sound reason "You said all of us. He set a place
for me every Sunday. The life he wanted had me at the table, and you're putting me out of the kitchen." That's a second
way to break it, beside Saturday off.

- **Lena:** a curious, ordered mind that writes everything down, which is also her weakness. Plain-spoken: what she
  said, she said. It respects what dream-Lena knows: pages only hold what she's said in the dream, never her secrets.
- **As an argument:** the brief's "challenging inconsistencies", played. Inconsistency is evidence, and you catch it
  while she's upset, the way people say true things when they're angry.
- **Fair:** pages enter from the edges and are drawn soft; catching is optional; in Story mode they collect themselves.
- **Small:** the most direct line from the dodge to the thinking: what you catch under pressure is a card you can play.
  The box becomes a place where you gather, not only where you survive.
- **Where:** one in the opening's quiet last second, one in each full phase of the sky act. A page carries the most
  recent quotable line.
- **Teach:** the first page drifts in alone in the opening's last second, nothing near it. Catching it shows the card
  landing in the tray, labelled "Her own words".
- **Cost and risks:** M. A new slip kind, a new evidence style (quote), one or two extra challenges in the data, and
  their reason cards and replies (writing for the creator). Risks: the writing load, and a quote must contradict
  something real or it's noise. Keep it to one or two quotes in this battle.

### 7. The sky: her case as a constellation (L, the one L)
*Sources: brainstorm 29 (the constellation), borrowed 2 (Undertale's purple SOUL moving on strings), brainstorm 39 (the
ceiling opens) cut down to the phases, brainstorm 34 (meteor shower) as its hazard, brainstorm 15 and borrowed 29 (the
box shows the state of the argument).*

**What.** After "He called you first", the moment she stops performing the domestic scene, the kitchen ceiling lifts
away and the box becomes a night sky. From then on, full phases are played on **her case drawn as a constellation**:
- the facts are **stars**: fixed and true;
- the links are **lines** someone drew between them;
- Nadia's light **travels along the lines** and switches line at a star (Muffet's strings);
- **shooting stars** come in along and across the lines from a **radiant** beyond the box's edge, which glows before
  each shower; every streak in a shower shares its angle, so one look reads the whole shower.

**The sky is the argument's state:**
- a link Lena still holds is a **lit line**, and her showers run along it;
- a link Nadia broke stays as a faint **rubbed-out trace**: still walkable, and nothing runs on it;
- a fact Nadia conceded **glows**, and streaks stop against it: it's **cover**;
- a link forced by pressing **snaps**, leaving a gap to hop and splinters that stay.

So arguing well makes the sky quieter and safer, and pressing leaves damage you can see.

- **Lena:** the stars, directly: her hobby, the attic window, the one place in the house the dream exaggerates. And
  curiosity's habit of joining dots. The player feels it without a word: the room you fight in turns into her sky.
- **As an argument:** the best fit in either list. A constellation is a picture people draw between stars that have
  nothing to do with each other, which is exactly what a false link is: true facts ("Out there, Theo is dead") joined by
  a line that isn't there ("so there's nothing left for him"). Her case's shape becomes the room, and its layers read
  both ways: the kitchen was what Elias kept; the sky is the part of her he never asked about.
- **Fair:** lines are pencilled before they appear; showers come from beyond the edge, with the radiant glowing first;
  the planner works on the lines by masking its grid to the cells on them (the light can only be there).
- **Small:** it changes what dodging *is* for the second half (choose a line, not a spot), so the late battle doesn't
  repeat the early one, and every sound argument visibly changes where you can stand.
- **Where:** every full phase after "He called you first", and that act's gestures (single streaks). In every good
  ending the painful premise surfaces before the end, so every Convinced or Heard-out run reaches the sky.
- **Teach:** she teaches it herself, teasing, as the ceiling lifts: "You know they're not really a bear, right? Someone
  just drew a line." The first sky phase opens with a second of nothing, the light on a star, and the prompt "Move along
  the lines. Switch at a star." Then one pencilled streak on one line.
- **Cost and risks:** L. Movement on lines, the premise map laid out as a star chart (a fixed position per premise),
  the drawing, the state rules, the planner mask, the ceiling-lift transition, and the autopilot. It's the biggest item.
  **If it slips, the medium fallback:** the night-sky backdrop with normal movement, and the standing lines as the hazards
  (brainstorm 29's original: lit threads turning slowly round their stars, fewer with every broken link). The sky doesn't
  need the observatory (still a proposal) to exist: only a night above the kitchen.

### 8. The last constellation (S, on top of 7)
*Sources: borrowed 20 (Sayonara Wild Hearts' duet), brainstorm 39's ending.*

**What.** The battle's last phase, once the claim falls or is let rest:
- **Heard out:** Lena draws a new constellation across the box, star by star, and Nadia follows it, **collecting the
  stars instead of dodging**. Missing one costs nothing: the line waits. Nothing hurts.
- **Convinced:** one line of hers is left in the sky, and **she erases it herself**. Just the true things, unconnected.
- **Pressed, composure lost, yielded:** no closing phase.

- **Lena:** caring and the stars: she shows Nadia how she sees the sky. Felt as the first phase in the battle that isn't
  against you.
- **As an argument:** each good ending gets its own beat. Hearing her out is played as following her; convincing her is
  watching her take her own line down.
- **Fair:** nothing hurts.
- **Small:** it gives the gentle ending play, not only a keepsake.
- **Where:** the ending.
- **Teach:** none needed. The first star glows beside the light, and touching it draws the next line (the sky already
  taught moving on lines).
- **Cost and risks:** S. A scripted path of pickups, the erasing animation, an end hook. It depends on 7; the fallback
  sky can still do the duet with normal movement.

### Build order and the cut line
**1, 2, 3** first (cheap, and they fix fairness, volume and choice together). Then **7 with 8** (the identity piece).
Then **5** and **4**. **6** last, because it waits on writing. If time runs short, cut 6 first, then 4, then drop 7 to
its medium fallback.

---

## 3. A proposed battle shape
One clean run to **Convinced**, in a typical order. The order of the middle beats is the player's; what's fixed is that
the sky opens with "He called you first", which every good ending reaches.

| # | Beat | In the box | Dodge | Mechanics (taught here in bold) |
|---|---|---|---|---|
| | **The kitchen act** | | | |
| 1 | The opening: "Sit down, Nadia. Pancakes are nearly done. He stays. This is his home." | Laying the table; a page in the quiet last second | 6 s | **1**, **6**, 2 (the written line) |
| 2 | Nadia asks why "He chose this" → "This is the life he wanted" appears | A tease | 2–3 s | **2** |
| 3 | She asks again → the deepest hidden premise (its words undecided) | She stops joking mid-pattern: one real throw | 3 s | 2 |
| 4 | She takes up "You don't know where I am" → "Then tell me where I am, Nadia." | One answer-me plate | 3 s | **3 (answer me)** |
| 5 | Concedes "Out there, Theo is dead" → "So there's nothing left for him" | One hush ring | 3 s | **3 (hush)** |
| 6 | Breaks "nothing left" with the fourth chair → her rattled fallback (its words undecided) | Rattled; the seat draws itself | 3 s | **5**, 2 |
| 7 | Concedes the fallback | Soft; the first pencil crosses | 3 s | **4** |
| 8 | "He called you first" surfaces | **The three rings**; the seat closed | 9 s | 3, 1, 4 |
| | **The sky act** | | | |
| 9 | Concedes it ("...I know. I didn't pick up.") → "Thank you for saying it." The ceiling lifts | The first sky phase; a page | 6 s | **7**, 6, 4 |
| 10 | Concedes "You don't know where I am" → "So waking means searching forever" | One streak | 2–3 s | 7 |
| 11 | Concedes "searching forever" (it can't be broken tonight) | One streak along her line | 2–3 s | 7 |
| 12 | Breaks "the life he wanted" with Saturday off → "Then let him have his wish." | She stops joking; a line rubbed out | 3 s | 7, 2 |
| 13 | Breaks "his wish" with the same Saturday → "...I thought it was the first time. Every time." | Soft; one line left | 3 s | 7, 2 |
| 14 | **Convinced** | **The last constellation**: she erases her last line | 6–8 s | **8** |

**The count, clean run:** about **55 seconds of dodging** and **about 50 hazards** (the opening about 6, ten gestures
about 20, the three rings about 12, the first sky phase about 10), against about 13 s and 11 hazards today. Plus two
pages, Theo's plane if the player wants it, and the closing. No single phase is longer than today's longest (9 s). The
dodge is **present after every reply** rather than only after mistakes.

**Other paths:**
- **Heard out:** the same up to beat 9; Let it rest once everything is heard; the closing is the duet (10–12 s).
- **Mistakes** still bring the full 5–8 s phases, now shaped by 2 (she means it) and 3 (answer-me after a retort), and,
  in the sky, by what's still standing.
- **Pressed:** the forced piece shows up in the box (splinters in the sky), and the battle ends with no closing.

**The first dial, if it still feels small after a playtest:** give each broken key link in the sky its own 4–5 s phase
(the line being rubbed out while you play), instead of a gesture. About +15 s and +15 hazards, no new mechanics.

**A build rule for every phase** (brainstorm 24): shape it like a phrase, with a setup (one pencilled thing), a peak,
and a quiet last second where the page or plane floats across.

---

## 4. Left out
| Idea | Why not here | Where it would suit |
|---|---|---|
| **What a bad reason looks like** (brainstorm 14) | Strong, but it's about the three kinds of reason, not about Lena | Whichever battle teaches the reasons (Walt's tutorial is rejected as built; what teaches the argument is undecided); every battle could then inherit it |
| **Grazing feeds the thinking** / **Lean in** (borrowed 8, brainstorm 22) | The most systemic answer to "small", but a meter and two ways to spend it are M, and it's Nadia's skill, not Lena's character | Game-wide, after the jam |
| **Locks before the painful phase** (borrowed 21) | It's Nadia preparing, not Lena | **The Doctor**: foreshadow "You only got this job because you're my sister" |
| **Nadia's pulse** (brainstorm 25) | Nadia's, not Lena's | **The Doctor**, whose patterns are ECG traces on the beat |
| **Star trails** (brainstorm 35) | Starry, but redundant with 7, and its meaning (the same morning, round and round) is someone else's | **Walt**, if his battle survives in another form: his spoke shadows already sweep like clock hands |
| **The empty Thursday** (brainstorm 32, on the brainstorm's shortlist) | Very her; the Thursdays' mark is now settled (small stars, 2026-10-12), and dream-Lena doesn't know what they were. A safe lane could say more than the story has decided | This battle, later, if question 5 says yes (M) |
| **Answer under fire** (brainstorm 12) | M–L, and hush, answer me and the three rings carry the same choice for less | — |
| **Old light** (brainstorm 30) | Beautiful and very her ("You don't know where I am"), but hollow after-images in a dodge box are a readability risk the jam can't test enough | A cutscene or the Morning ending's sky, not a hazard |
| **The round window** (brainstorm 31), **the box changes shape** (borrowed 6) | The sky act takes their place; a round box needs new collision and a new planner | — |
| **You're the intruder** (brainstorm 19) | The inversion (Nadia is the danger) is about Nadia, and a rule where nothing can hurt you is a different register | **The Long Night** ("wrong, in a way you feel in your hands") or the Force route |
| **Her side of the table** (brainstorm 40), **Rebuttal Showdown** (borrowed 24) | L each | **Lena's Night 4 rule-break** and the Force route (borrowed 24 already says Force only) |
| **Every door leads back to the kitchen** (brainstorm 38) | The house obeys her, but wrapping edges muddles "enters from outside" | **Lena's Night 4 rule-break**, where breaking the rules is the point |
| **Nonstop Debate** (borrowed 23) | L, text as hazards, and judges will call it Danganronpa | — |
| **Pit two statements against each other** (borrowed 27) | Pages (6) find inconsistencies in her own words with no steering physics | **The Doctor**, whose battle turns on a half truth |
| **Focus / steady hands** (borrowed 12, brainstorm 28) | Nadia's tool, not Lena's | Game-wide accessibility setting (S); it makes denser patterns fair |
| **Breathe** (borrowed 15) | Lena's "Take it." is care toward Nadia: small and good, but it waits on question 2 | This battle (S), if her care reaches Nadia |
| **Syrup** (brainstorm 4), **the tether** (borrowed 10) | Her flaw as a trap, covered by the seat and the hush; a tether also makes the box smaller, the opposite of the note | — |
| **The shield, gravity, shooting back, polarity** (borrowed 3, 5, 4, 13) | Nadia's feelings and modes, not Lena's; polarity is close to the Listening and Pressing halves the creator replaced | Gravity: a Nadia-centred night, if one gets a pressure phase |
| **Words as walls** (brainstorm 7) | Clever (the gap falls on the "so"), but words on plates this small won't read at speed, and 7 already makes the "so" a line | — |

The plain fair patterns (crossing walls, the pan's hiss) aren't picks, but they're the kitchen act's material; batter
with shadows and laying the table are part of 1.

---

## 5. Questions for the creator
1. **The kitchen and the keepsake.** She's a stargazer (settled). Is the kitchen Elias's memory of Sundays (the flour
   and plates stay as hazards)? And what is the Heard-out keepsake, now that the apron hair tie is gone (her pencil? a
   page from her notebook?).
2. **Does her care reach Nadia?** The hug that passes if you hold still, the seat, and the shared sky at the end all
   assume some of it does. If it's only for Elias and Theo, they stay but read as control, and "Take it" stays out.
3. **The sky.** Should the ceiling open after "He called you first", and is the constellation worth the battle's one
   large build? The fallback is the night sky with her lines as hazards and normal movement (medium).
4. **Her own words.** May a page of hers open a second way to break "This is the life he wanted", or should each key
   link keep only the evidence the nights give you? (How "Then let him have his wish" breaks depends on what Night 1 becomes, which is undecided.)
5. **The Thursdays.** Can they appear in this battle at all (for example as the one empty lane on the fridge
   calendar), or should they stay out until their mark and meaning are settled?
