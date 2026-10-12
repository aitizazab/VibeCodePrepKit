# Argument battle: original ideas for the dodge phase (Lena, Night 2)

> **Status: a brainstorm for the creator (2026-10-12).** Original ideas only. The survey of other games' mechanics is
> in `BORROWED-MECHANICS.md`, next to this file. Nothing here is built, and no code was changed. Every quoted line is a
> placeholder that shows what the line has to do. The creator writes the words.

## Shortlist: the eight strongest
| # | Idea | Why |
|---|---|---|
| 29 | **The constellation** | Lena's case drawn as stars (the facts, which are true) joined by lines (the links, the "so"s someone drew). Breaking a link erases a line. It's the argument model itself, in her hobby |
| 12 | **Answer under fire** | Her direct questions ("Then tell me where I am", "Why didn't you pick up?") become phases where you answer by where you stand when the beat lands |
| 15 | **The table remembers** | What's settled stays on the surface: conceded truths become cover, forced pieces leave shards for the rest of the battle |
| 14 | **What a bad reason looks like** | Each kind of wrong reason has its own phase shape: "doesn't follow" splits the box with a gap you can't cross; "beside the point" comes from the side you aren't watching |
| 21 | **Every reply has a gesture** | Dodging stops being only a punishment. Every exchange gets a short 2–3 s volley; mistakes still get the long phases |
| 33 | **The pencil marks** | She studies how you dodge, marks where you always go, and aims there. Curiosity as a threat, with its own telegraph |
| 32 | **The empty Thursday** | The fridge calendar as a seven-lane attack. Thursday's lane is always empty, because Elias never asked what was there |
| 36 | **The seat she keeps** | Breaking "nothing left out there" with the fourth chair opens a chair-shaped safe spot. It's safe, but nothing she isn't saying ever reaches it |

---

## 0. What's there now, and why the dodging feels small
Read from `PressurePhase.cs`, `ArgumentEngine.cs` and `ArgumentView.cs`:
- **The box** is 6 x 3.2 surface units, drawn 1200 x 640 px beside Lena's sprite. **Nadia's light** has a radius of
  0.1 (20 px) and moves at 3 units/s: it crosses the box in 2 s side to side and in 1 s top to bottom. Plates have a
  radius of 0.15. A hit gives 0.9 s of invulnerability, and five hits take her from 70 to 160 bpm.
- **Three shapes:** walls of plates sliding in from the sides (each next gap is placed so it can be reached), flour
  rings blooming **from a point inside the box** with no warning (the unfair ones; being fixed separately), and batter
  lobbed from the side edges at where she is.
- **Slips** (Theo's paper plane, which reveals the deepest hidden premise) bounce around inside the box from 1.5 s.
- **Difficulty:** Normal; Story (nothing hurts, slips collect themselves); Scripted-but-beatable (the Long Night: full
  heat, walls only, gaps that only just fit). A speed setting runs from 50 to 100%.
- **When phases happen:** the opening (heat 0, 5 s); after a wrong reason, a retort or a press (5–8 s at the current
  heat, harder with anger); and when "He called you first" surfaces (8 s, full heat).

**Why it feels small (both readings are true):**
1. **A good player barely dodges.** A clean run, with no wrong reasons and no presses, gets the opening phase (about
   3 hazards in 5 s, since spawns come every 1.5 s at heat 0) and the painful phase (about 8 hazards in 8 s). That's
   **around eleven hazards in a battle of ten minutes or more of thinking.** The dodge only happens when you've done
   something wrong, so the better you argue, the less battle there is.
2. **The box is big but empty.** At 1200 x 640 px it isn't small on screen. It's sparse: a 20 px light in a box thirty
   lights wide, with one hazard on screen at a time. It *reads* small because nothing fills it.

So the ideas below mostly do three things: give the dodge a reason to exist when you argue well, fill the box with
fewer but more meaningful things, and let what happens in the box feed back into the argument.

**One fairness vocabulary for every idea** (so the standing rule is taught once and then trusted):
- **Enters from outside:** it starts beyond the box's edge (the code already uses a 0.4-unit entry margin).
- **Ghost first:** a faint outline of the hazard, in Lena's wine red, 0.6–0.8 s before it becomes solid, plus a
  sound (a plate's clink, a pan's hiss). Anger may shorten the ghost, but it never removes it.
- **Soft things are drawn soft:** anything that can't hurt (slips, cover, safe zones) has a different outline
  (dashed or glowing), never just a different colour.

---

## 1. Attack patterns that are fair under the rule

### 1. Laying the table (S–M)
Plates slide in from the edges and **stop** at set places, like someone laying a table: four place settings, then
cutlery lines between them. For a few seconds they're fixed walls that shrink the room you have, then she clears them
away with one sweep.
- **As an argument:** she's assigning places: "You sit *here*." The settings make a shape with exactly one free seat.
- **Fair:** they slide in from outside; the placemat each plate stops on is drawn (ghost) before the plate arrives.
- **Lena:** care as control; the Sunday table.
- **Teach:** the opening phase, slowly: "Sit down, Nadia. Pancakes are nearly done."

### 2. The embrace (S)
Flour rings that **close in** from outside the box instead of blooming out from inside it, centred where Nadia stood a
moment ago, each with a gap that's visible as soon as it crosses the edge.
- **As an argument:** her claim made physical: "He stays." It holds you in rather than knocking you away.
- **Fair:** it always starts beyond the edges, and the gap is in view the whole way in. It's the natural fair
  replacement for the bloom.
- **Lena:** protects by keeping in (her flaw, the other way round).

### 3. Batter with shadows (S)
The lobbed batter becomes an arc through the air seen from above: its shadow appears where it will land and grows
until the splash, and the splash leaves a small puddle for a second.
- **As an argument:** short, sharp asides, the things she throws in mid-sentence.
- **Fair:** the shadow is the telegraph, and it can be 0.8 s at calm and 0.5 s when she's angry.
- **Lena:** teasing, quick.

### 4. Syrup (M)
A slow pour from the top edge leaves puddles that **slow** Nadia but don't hurt. Other hazards are timed so that
crossing a puddle makes them harder to dodge, never impossible.
- **As an argument:** comfort that keeps you. Sweet, and it costs you your speed. "Syrup's on the left."
- **Fair:** the bottle tips at the edge before each pour; puddles are drawn as soft (glossy outline).
- **Lena:** care as a trap; also a quiet hook for the comfort choices that lead toward the Despair route.

### 5. Crossing walls (S)
Two walls of plates come from opposite sides at once. Their gaps line up only for a moment as they cross, so you go
through where the two gaps meet.
- **As an argument:** two of her premises that agree only at one point ("Theo is dead out there" and "he chose this"
  meet at "so stay").
- **Fair:** both enter from outside, and both gaps are visible from the start. The existing reachability rule extends
  to "the crossing point is reachable".
- **Lena:** her case's structure.

### 6. The pan's hiss (S)
Spatter fans out from one edge in short bursts, **on the music's beat**. The pan is drawn at the edge, and it hisses
louder before each burst.
- **As an argument:** small irritations, kept up: the rhythm of an argument that's gone on too long.
- **Fair:** it comes from an edge, the beat is audible, and the pan glows before each burst.
- **Lena:** the domestic morning.

---

## 2. Tying the dodge to the argument

### 7. Words as walls (M)
The walls are built from the **words of the premise she just said**: each plate carries a word, and the gap falls
where the weak word is. For a link, that's the "so". You literally pass through the leap in her reasoning.
- **As an argument:** strong. The player feels where the link is thin, and conceded premises come back as solid
  walls with no gap (true things don't have holes).
- **Fair:** it enters from outside; the words are readable as they come in; the gap is wide.
- **Lena:** plain-spoken: her words are her weapons.
- **Teach:** the first link she states ("So there's nothing left for him out there"): the "so" is the gap, and the
  prompt says so once.

### 8. The crack in her pattern (S)
When Nadia breaks a premise with a sound reason, that premise's attacks come back **with a lasting flaw**: a plate
missing from every wall it builds, or a lane where it can't reach.
- **As an argument:** a broken premise can't hold her defence up any more. The argument you won makes the room
  kinder, which the heat system already says in numbers; this says it in shapes.
- **Fair:** it only ever makes things easier.
- **Lena:** none in particular (it's the rule of every battle).

### 9. Fall back, physically (S–M)
When she **falls back** to a weaker premise, her attacks fall back too: for the next phase they come only from her
side of the table (the edge nearest her sprite), and the far half of the box is quiet.
- **As an argument:** retreat shown as retreat. "Deepen" (when a premise reveals a deeper one) does the opposite: the
  attacks come from **below**, the table's underside.
- **Fair:** the active edges glow before the phase starts.
- **Lena:** none in particular.

### 10. The table follows the mood (M)
The box **is** the table, and it changes with the argument. Each concession adds a leaf (the box widens). Heat pulls
the leaves out (narrower). Anger decides how many edges she attacks from: her own side when calm, all four when
furious.
- **As an argument:** the room's shape is the conversation's state, readable in a second without a meter.
- **Fair:** the table changes between phases, never during one. Edges she can attack from are drawn in wine red.
- **Lena:** the kitchen and the house obey her.

### 11. Your own evidence turned back (M)
After a wrong reason, the next phase is made of **the evidence Nadia misused**: the fourth chair sliding across,
calendar pages, Walt's patched tyre rolling.
- **As an argument:** she turns your point back on you, which is exactly what a good opponent does with a bad
  argument. The player remembers what went wrong because it's what is coming at them.
- **Fair:** it uses the same entry and ghost rules as the plates.
- **Lena:** quick, plain-spoken.
- **Cost note:** one sprite per evidence card (eight in Lena's tray today).

### 12. Answer under fire (M–L) *(shortlist)*
When Lena **asks Nadia a question** ("Then tell me where I am, Nadia." / "Why didn't you pick up?"), the phase is the
question. The box is divided into **answer zones**, each labelled with an answer (the honest one, the retort, and
silence). The hazards are thickest over the honest answer. Where you're standing when the third beat lands is your
answer.
- **As an argument:** the hardest truth is the hardest place to stand. You can take the easy answer and stay safe,
  or go where it hurts and say it. A real choice, made with your body and not a menu.
- **Fair:** the zones and the beat count ("one... two... three") are shown from the start, and the hazards follow the
  walls and rings rules.
- **Lena:** she asks; she wants the truth from you (her "where am I" and "he called you first" premises already carry questions and retorts in the data).
- **Teach:** the first question ("Then tell me where I am"): the zones float in first, with one line of
  instruction, and no hazards until you've moved once.

### 13. Her own words (M)
**Pages from her notebook** blow through some phases. Catching one adds **a thing she said** to the evidence tray, as
a quote. Some of her quotes contradict later premises, and challenging with her own words is a new kind of challenge.
Example (placeholder): a page reads "Elias drew those keys. I never asked." Against "He chose this. I know him", the
sound reason is "You never asked him about the Thursdays. You know what he let you see."
- **As an argument:** **inconsistency is evidence**, the Ace Attorney heart, but you have to catch it while she's
  upset, the way people say true things when they're angry.
- **Fair:** pages enter from the edges, and they're drawn soft (catching is optional: in Story mode they collect
  themselves).
- **Lena:** the notebook and pencil: a curious, ordered mind that writes everything down, which is also her weakness.
- **Teach:** the first page drifts in alone, in a quiet moment, with no hazards near it.

### 14. What a bad reason looks like (M) *(shortlist)*
Each kind of wrong reason has its **own phase signature**, so the player learns the logic by feel:
- **Doesn't follow:** a gap opens across the middle of the table that the light can't cross. You're stuck on one
  side while the other side stays out of reach. A non sequitur, as a floor.
- **Beside the point:** the attacks come from **the side you weren't watching** (the edge opposite where you were
  standing when the phase opened).
- **Antagonising:** the full harsh pattern, and the box's border turns wine red for the rest of the battle (anger
  remembered).
- **As an argument:** strong. It makes the three "because..." cards three different experiences, not just three
  shades of failure.
- **Fair:** the gap is drawn as a crack that opens over 0.8 s; "beside" walls still enter from outside with a ghost.
- **Lena:** none in particular (it works for every battle).

### 15. The table remembers (M) *(shortlist)*
What's settled **stays on the surface** for the rest of the battle:
- a **conceded** truth becomes a glowing plate fixed on the table. Hazards stop against it, so it's **cover**;
- a **broken** premise leaves clean space (its pattern stops);
- a **forced** premise leaves **shards** where its plate smashed: small static hazards that never leave.
- **As an argument:** honesty literally shelters you; forcing leaves damage you have to live with. The press costs
  something you can see, beyond a counter.
- **Fair:** cover and shards are both permanent and visible before any phase starts.
- **Lena:** the table she keeps; "Saying it louder doesn't bring him back."

### 16. Hold the line (S–M)
During a phase, **holding Press** (the same 0.6 s hold) sends a push out from Nadia's light that clears everything
near her, at the cost of counting as a press: Force +1, the transcript reads "Nadia shouted over her", and the shards
of idea 15 appear where things broke.
- **As an argument:** pressing a point mid-dodge, with its real price: when you can't take it any more, you can make
  her stop. The game lets you, and remembers.
- **Fair:** it's the player's choice, so it adds no unfair hazards.
- **Lena:** her wounded "You'd take that from him too?"
- **Teach:** once, the first time Nadia is hit twice in one phase: "Hold to make her stop. She'll remember."

### 17. Ask in the storm (S)
Once per phase, tapping **Ask** makes Lena **pause the volley** for one line: what this feeling is ("I just want one
morning."). Everything freezes for a beat, then resumes. The line goes in the transcript and counts toward hearing
that premise.
- **As an argument:** curiosity under pressure. Asking "why" in the middle of a fight changes the fight.
- **Fair:** it only ever freezes things.
- **Lena:** her care: she always answers a real question.

### 18. Name one thing (M)
When Lena says "Name one thing. You can't." (her reply when Nadia presses "nothing left out there"), the phase gives
Nadia small lights coming in through the kitchen window. Each one she touches **names** something: "Walt", "his
patients", "the fourth chair". Three named lights end the phase early and calm her heart a step.
- **As an argument:** a direct answer to a direct taunt, made by acting rather than choosing.
- **Fair:** the lights enter from the window edge; the hazards are the normal ones.
- **Lena:** the stars through the window (those lights *are* stars).

### 19. You're the intruder (M)
When the deepest hidden premise surfaces (that to Elias's dream, Nadia is the intruder; the prototype's Theo-drawing
version of it is cut as out of character), one phase **inverts**: Lena's things (Theo's planes, the cups, the
chairs) flee from Nadia's light, and touching them **hurts them**: heat rises, Lena flinches. The task is to cross the
table **without touching anything she loves**.
- **As an argument:** the buried root, felt: to this dream, Nadia is the intruder. The player is the danger.
- **Fair:** nothing hurts Nadia in this phase; it can only go badly for Lena. Things enter from the edges.
- **Lena:** fierce about the people she loves.

### 20. The question she keeps asking (S)
"Why didn't you pick up?" repeats through the painful phase as **three rings of a phone**: three telegraphed waves, one
per ring, each harder. After the third ring there's silence, and then the line: "So he called me."
- **As an argument:** the night itself, as a structure the player can count.
- **Fair:** each ring is heard and shown (the phone icon at the box's edge pulses) before its wave.
- **Lena:** the truth she carries; she says it plainly.

---

## 3. Making the dodge weigh more, without dragging

### 21. Every reply has a gesture (S) *(shortlist)*
After **every** exchange (an answer, a concession, a fall back), Lena makes **one short gesture**: 2–3 s, a single
wall or a single throw, played over her reply line. Mistakes still bring the full 5–8 s phases.
- **As an argument:** each reply has a feeling, and the gesture is that feeling: a concession's gesture is soft (one
  slow plate), a sound challenge's is rattled (a dropped cup). It turns the box into her body language.
- **Fair:** single hazards, all entering from outside with ghosts.
- **Lena:** quick and warm.
- **Why it fixes "small":** a clean run goes from about eleven hazards to about forty, without any phase getting
  longer.
- **Cost note:** a "gesture" level below heat 0 (one spawn, 2–3 s) in the engine's queue.

### 22. Lean in (M)
**Grazing**: passing close to a hazard without touching it fills a "listening" ring around the light. A full ring
**lowers the heart rate one step**, or brings in a slip early.
- **As an argument:** getting close to what hurts her without getting hurt is listening. Keeping well away is safe and
  hears nothing.
- **Fair:** it only adds reward; the hitbox doesn't change.
- **Lena:** care, both ways.
- **Teach:** the first graze plays a soft chime and shows the ring filling, with one line: "You're close. Keep
  listening."

### 23. Stay a little longer (S)
When a phase ends, if a slip or a page was missed, a prompt: "**Stay**" (4 more seconds, denser, the slip comes back)
or "**Step back**" (it ends). It's opt-in.
- **As an argument:** she has more to say if you can bear it. The player chooses how much to hear.
- **Fair:** it's a choice, and the extension uses the same rules.
- **Lena:** what she isn't saying.

### 24. Phrased phases (S)
Shape each phase like a musical phrase that matches the battle loop: a **setup** (one telegraphed thing), a **peak**
(the pattern at full density), and a **release** (the last second is quiet, and the slip floats across it).
- **As an argument:** a feeling rising and passing. A phase that ends on a slip says "and then she let something slip".
- **Fair:** structure is a telegraph in itself; players learn the arc.
- **Lena:** stops joking mid-sentence when something matters (the peak is that moment).

### 25. Nadia's pulse (S–M)
The hazards move **on Nadia's heart rate**. At 70 bpm the walls step in on a slow beat; every hit makes the beat faster
(88, 106...), and the box's border shows her ECG line.
- **As an argument:** composure becomes physical. A nurse reads her own pulse, and the player feels panic as tempo.
- **Fair:** the beat is visible on the border and audible, so every step is announced.
- **Lena:** none (it's Nadia's). It works for every battle.

### 26. Let it land (S–M)
Some of her hazards are drawn **soft**: dusty flour, her grief rather than her anger. They don't raise the heart
rate. Taking one on purpose counts as **hearing her** (toward Let it rest); dodging it is also fine.
- **As an argument:** absorbing someone's grief is a choice you can make. A player heading for "Heard out" might walk
  into every soft one.
- **Fair:** soft hazards have a different outline and texture (not just colour), taught in a calm phase first.
- **Lena:** caring; she's grieving, not only defending.

### 27. She noticed (S)
After each phase, Lena says **one line about how Nadia dodged**, chosen from what the light did: stayed near her side
("You stayed close."), hugged the far wall ("You always keep your distance, Nadia."), caught the slip ("...You saw
that."). It goes in the transcript.
- **As an argument:** how you move is read back as behaviour, and it can sting (Nadia's absence is the case against
  her).
- **Fair:** it doesn't change difficulty.
- **Lena:** observant and curious; she knows people.
- **Cost note:** six to eight short lines, plus tracking the light's average position.

### 28. Steady hands (S)
Hold a button to **move slowly**, with the light's real hitbox drawn as a sharp dot: a nurse's steady hands.
- **As an argument:** a choice between steadiness and reach.
- **Fair:** a player tool, so nothing to telegraph.
- **Lena:** none (Nadia's).
- **Note:** this makes denser, tighter patterns fair, and denser patterns are what make the box feel full.

---

## 4. Ideas specific to Lena

### 29. The constellation (M–L) *(shortlist)*
Lena's case is drawn as a **constellation**: the facts are **stars** (true, fixed, they can't be broken, only
acknowledged), and the links are **lines someone drew between them**. Breaking a link **erases a line**; the stars stay.
In the dodge, the lines that still stand are the hazards: lit threads that rotate slowly around their stars. Every
broken link leaves fewer.
- **As an argument:** the best fit in this file. A constellation is a picture people draw between stars that have
  nothing to do with each other. That's exactly what a false link is: true facts ("Theo is dead out there") joined by a
  line that isn't there ("so there's nothing left for him"). Conceding a fact makes its star glow; breaking a link
  wipes its line. Convinced is the sky with no lines: just the true things, unconnected.
- **Fair:** lines grow from their stars over a second (the ghost), and the rotation is slow and steady.
- **Lena:** stargazing; a curious, ordered mind that connects dots.
- **Teach:** she teaches it herself, teasing: "You know they're not really a bear, right? Someone just drew a line."
- **Scope:** as a dodge pattern, M. As the whole battle's skin (see idea 37), L.

### 30. Old light (M)
Lena's hazards leave **afterglow**: a harmless trace of where they were, which fades over a second. After she reaches
"You don't know where I am," the after-images get longer, and the box fills with light from things already gone.
- **As an argument:** her own premise, "You don't know where I am. Neither does he." The light you see left a star
  long ago, and it may not be there any more. She's light from a star nobody can find.
- **Fair:** the afterglow is outlined and hollow; solid hazards are filled. It must be taught in a calm phase and
  never get close to confusing.
- **Lena:** the stars, and her being missing.
- **Teach:** "You know the light from some of those stars left before anyone was born? Some of them aren't there any
  more."

### 31. The round window (M)
The box's shape becomes hers: at first the **round kitchen table**; once the argument reaches what she's hiding,
the **round window of the observatory**, full of stars. Walls become chords across a circle; rings enter from the rim.
- **As an argument:** reshaping the box with the argument's depth: the further in, the more the room is hers.
- **Fair:** a circle has no corners to get trapped in; everything enters from the rim.
- **Lena:** the attic observatory (still a proposal in `DREAM-HOUSE.md`).
- **Cost note:** a circular surface mask and collision; the reachability rule needs redoing for chords.

### 32. The empty Thursday (M) *(shortlist)*
The **fridge calendar** as an attack: seven columns, one per day, with things dropping down the columns day by day.
**Thursday's column, marked with a small star, is always empty.** It's the one safe lane.
- **As an argument:** the hole in her week is the hole in the story. Dream-Lena can't attack from Thursday because
  Elias never knew what was there ("Elias drew those. I never asked."). The safe place is the mystery.
- **Fair:** each column's day letter lights up before something falls in it.
- **Lena:** her private life, the Thursdays and their small stars.
- **Teach:** the first time, the Thursday lane glows and the light is placed in it, then the other lanes fill.

### 33. The pencil marks (M) *(shortlist)*
Lena **watches where you go**. Across phases she keeps a tally of where Nadia's light spends the most time, and
before a volley she marks those spots on the table with **pencil crosses**. The next volley lands there.
- **As an argument:** "I know him", and she knows you too. She's curious, she notes things down, and that's a threat.
  The counterplay is to change your habits, which is also the lesson about Nadia.
- **Fair:** the pencil marks are the telegraph (0.8 s or more), and the volley only lands where they're drawn.
- **Lena:** the pencil behind her ear, the notebook; a curious, ordered mind.
- **Teach:** the first marks appear with her line: "You always go left, Nadia." (placeholder)

### 34. Meteor shower (S–M)
Streaks fall from a single **radiant** outside the box. The radiant glows at the edge before each shower, and every
streak shares its angle, so one look tells you the whole shower.
- **As an argument:** all her points come from one place: love for him. Name the radiant, and you've named her case.
- **Fair:** off-screen origin, shown in advance, one shared direction.
- **Lena:** stars; night outings.

### 35. Star trails (M)
Arcs of light turn slowly around a pole star **beyond the box's corner**, like a long-exposure photo of the night sky:
concentric arcs with gaps, sweeping in from the edge.
- **As an argument:** time going round and round, the same morning forty-one times; and for her fall-back, "let him have his
  wish": a wish that circles forever.
- **Fair:** the centre is outside the box, so arcs always enter from an edge, and their gaps are visible all the way.
- **Lena:** stargazing, and its patience.

### 36. The seat she keeps (S–M) *(shortlist)*
Once Nadia breaks "nothing left out there" with the fourth chair, the box grows a **chair-shaped alcove** on one edge:
her seat. Nothing can reach it. But slips, pages and the window's lights never go there either.
- **As an argument:** a real choice. Sitting down is safe and comfortable and hears nothing; staying at the table's
  edge is where the truth comes out. It's the fourth chair turned into a choice about how to spend the night.
- **Fair:** a safe zone, drawn soft; nothing to telegraph.
- **Lena:** care as an invitation ("Sit down, Nadia"); it can also feed the Despair route's comfort choices.

### 37. Tease, then mean it (S)
Lena's calm patterns **tease**: a plate slides in, stops, and slides back out (it never crosses); a ring starts, and
she laughs it off. Then, when the argument touches something real, the teasing **stops mid-pattern**: everything
drops off the table, a beat of nothing, and the real attack comes.
- **As an argument:** her character exactly: she laughs at her own jokes before the end, and stops joking mid-sentence
  when something matters. The silence is the telegraph.
- **Fair:** teases never touch the box's middle; the real attack follows the normal rules after a full second of
  empty table.
- **Lena:** teasing and warm, and serious when it counts.

### 38. Every door leads back to the kitchen (S–M)
The box's edges become **doorways**: leave one side and you come in at the other. Her hazards wrap the same way.
- **As an argument:** "He stays. This is his home." You can't get out. The house obeys her (it already does this in the
  movement section, where doors lead back to the kitchen).
- **Fair:** door frames are drawn on the edges, and a hazard about to wrap shows a ghost on the opposite edge.
- **Lena:** the house obeys her.

---

## 5. Wild ideas

### 39. The ceiling opens (L)
At the painful premise ("He called you first"), the kitchen ceiling lifts away to **the attic's night sky** (it's
always night in Lena's attic). From then on the battle is **the constellation** (idea 29) in full: the claim is
the brightest star, premises are stars and lines, and the evidence tray is a star chart. Convinced ends with Lena
erasing the last line herself, and the stars left unconnected.
- **As an argument:** the whole argument model in her hobby, at the moment she stops performing the domestic scene.
  It also sets up the Morning ending (stargazing with her telescope, from `STORY-TALK.md`).
- **Fair:** as idea 29.
- **Lena:** everything: the stars, the observatory, the private life the kitchen hides.

### 40. Her side of the table (L)
For one phase, **the player plays Lena**: you place the plates and pour the flour, while an AI Nadia-light dodges and
tries to reach the table's edge. Whatever you do, the AI gets through (it's scripted to), and Lena says: "...That's
what it's like. Watching someone walk out."
- **As an argument:** perspective taking: you feel what she's doing and why it can't work. It earns "Heard out" as an
  experience, not a menu choice.
- **Fair:** nothing can hurt the player.
- **Lena:** protects by keeping out, and fails.
- **Risk:** it breaks the rules once, and only once; the Force route's rule-break (`ARGUMENT-BATTLES.md`) is already
  Lena's. This could be its mirror on the kind route.

---

## How these fit together (a suggested set for Lena)
A set that's coherent and buildable for the jam, if the creator wants a starting point:
- **The base:** 21 (every reply has a gesture) and 2 (the embrace, replacing the unfair blooms), so the dodge is
  constant and fair.
- **The argument in the box:** 15 (the table remembers) and 14 (what a bad reason looks like).
- **Her identity:** 33 (the pencil marks) for curiosity, 36 (the seat she keeps) for care, 32 (the empty Thursday)
  for the mystery, 29 (the constellation) for the stars.
- **The set piece:** 12 (answer under fire) for "Why didn't you pick up?", with 20's three rings.
- **Each one taught once**, by Lena, in a calm volley before it can hurt (the "Teach" lines above).

## What the docs leave unclear about Lena
- **Is "curious" really her?** The bible calls her quick, warm, teasing, plain-spoken and fierce. Curiosity is implied
  only by the art notes (the pencil "shows a curious, ordered mind", the notebook). If curiosity is central, the
  bible's personality section should say so, because several ideas here lean on it.
- **The stars are settled, and so are the Thursdays.** The creator settled stargazing in `STORY-PROSE.md` and
  approved the attic observatory (with a raised roof section over it). The fridge calendar marks Lena's Thursdays
  with small stars, not keys (2026-10-12), so the stars are where she went on Thursdays. Ideas 31, 32 and 39 can lean
  on that.
- **Her keepsake.** She's a stargazer, with no clay, no workwear and no apron, so the Heard-out keepsake (the apron
  hair tie in the prototype) needs replacing; what replaces it is undecided. The flour and batter patterns may need
  rethinking if she isn't the one cooking.
- **Is she the cook?** Related: the battle is staged around her making pancakes. If she "doesn't need to work" and has
  a private, guarded style, is the domestic scene hers or Elias's memory of Sundays?
- **How much does dream-Lena know about her own secrets?** The bible says none (she's built from what Elias knew). So
  her star life should appear in the dream only as Elias saw it from outside: a closed notebook, a locked attic, an
  empty Thursday. Ideas 32 and 39 assume that; idea 13 (her own words) has to respect it too.
- **"Caring" toward whom?** She's fierce about Elias and Theo, and "fond" of Nadia. Whether
  her attacks ever **protect** Nadia (say, from the dream's static) isn't settled, and it would change ideas 26 and 36.
