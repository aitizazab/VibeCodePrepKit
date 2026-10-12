# Borrowed mechanics for the argument battle

> **Status: research for the creator (2026-10-12).** A survey of bullet-hell, dodge and debate games, looking for
> mechanics to borrow for the pressure phases of *Visiting Hours*' argument battle (Dream-Lena, Night 2). A separate
> brainstorm of original ideas is being written alongside this one. Nothing here is built, and no code was changed.
> The selection by fit to Lena comes after both reports.

## What the borrowings have to work with
**The creator's notes (2026-10-12):** the structure "isn't bad"; some attacks were impossible to avoid (being fixed
separately); the dodging feels slight next to the time spent thinking.

**The standing fairness rule:** except in the Long Night, every attack either **enters from outside the box** or
**gets a telegraph beforehand**.

**The box as built** (`PressurePhase.cs`, pure C#):
- A 6 x 3.2 surface; Nadia's light has a 0.1 radius and moves at 3 units/s; 0.9 s of invulnerability after a hit.
- **Phases** last 5–8 s. They come only as consequences: the opening, a wrong reason, a press, an antagonising reply,
  and the painful premise ("He called you first").
- **Three hazards:**
  - plate **walls** slide in from the sides with gaps: they enter from outside, so they're fair;
  - flour **rings** bloom from a point inside the box with no warning: these break the rule;
  - batter **blobs** are lobbed from the edges at her: they enter from outside, so they're fair.
- **Slips** (Theo's paper planes) bounce around; catching one reveals a hidden premise.
- **Heat** (0–3) and **anger** (0–3) scale speed, density and gaps. Each hit is one heart-rate step.
- **Story mode:** nothing hurts.

**Rules from `ARGUMENT-BATTLES.md` that every borrowing must keep:**
- no clock on thinking;
- status is never shown by colour alone;
- every pattern has a Story-mode version;
- the pressure is the opponent's **feeling**, drawn with their things.

**Lena, for the "trait" lines:**
- **Curious:** she watches the night sky and logs what she sees in pencil, in a dark notebook; there's a telescope and
  star charts in the attic observatory, under the house's one window full of stars.
- **Caring:** fierce about the people she loves; calls Elias "love". Her flaw is that she *protects by keeping out*.
- **Voice:** quick, warm and teasing; stops joking mid-sentence when something matters.
- **What she knows:** she knows she's a memory. The dream house obeys her (rooms rearrange).
- **Her things:** the kitchen (flour, plates, the pancake stack), her stargazing (the telescope, the star charts, the
  pencil log) and Theo's paper planes.

**Build cost** is measured against that code:
- **S:** a new hazard or rule inside `PressurePhase`, with its drawing, in about a day;
- **M:** also touches the engine (heat, reveals, verbs) or the host's flow, or needs a new input;
- **L:** a new mode of play, or text-as-hazards.

Where a detail couldn't be confirmed on the web, the entry says so.

---

## Undertale and Deltarune

### 1. Blue and orange attacks
- **Source:** *Undertale*.
- **How it works:** A light-blue attack only hurts you while you **move**; an orange one only hurts you while you
  **stand still**. Both can be in the same pattern.
- **Why it works there:** It turns dodging into a rule you read: the right answer is sometimes *don't move*, which no
  ordinary bullet hell asks. The colour tells you the rule before the attack arrives.
- **The argument version:** Lena has two kinds of feeling.
  - **"Hush" hazards** (blue, drawn soft and round): her care, the things she says when she wants him left alone.
    They pass harmlessly *if Nadia holds still and listens*.
  - **"Answer me" hazards** (orange, drawn sharp): her challenges, which only hurt if Nadia stays put.
  - The dodge becomes the same choice as the argument, listening or pushing back. Lena's lines can name it: "Shh. Just
    listen, Nadia."
- **Fairness:** The type is shown by shape (a soft round stroke or a jagged one) as it enters from the edge, so
  colour alone never carries it (the accessibility rule). Teach it with one harmless volley of each.
- **Lena trait:** Caring that keeps out: "hold still, love" is a protective command.
- **Cost:** **S**. A flag on walls and blobs, plus a "moving this tick" check in `Tick`.

### 2. The purple SOUL on strings, as constellation lines
- **Source:** *Undertale* (Muffet).
- **How it works:** The SOUL is confined to a few horizontal strings (the purple lines) and hops between them, while
  attacks travel along the strings.
- **Why it works there:** It changes what dodging *is* (choose a line, not a spot), and it's Muffet's web, so the
  mode is the character.
- **The argument version:** Lena's star chart is the box. Nadia's light can only travel along **the lines between
  stars**, a constellation from Lena's pencil log, and hops from star to star.
  - Hazards are flour or plates crossing the lines.
  - Each standing premise could own one line, so the map *thins* as premises fall: arguing well opens the sky.
- **Fairness:** Hazards travel along or across lines from the box's edges. A line about to be cut is first traced in
  pencil (see 17).
- **Lena trait:** **The stars**, directly. It's also her curiosity: she draws the lines between things.
- **Cost:** **M**. A new movement mode in `Tick` (a graph of points and segments) and its drawing. The pilot needs a
  path search on the graph.

### 3. The green SOUL shield
- **Source:** *Undertale* (Undyne).
- **How it works:** The SOUL can't move. A shield turns to face one of four directions to block spears coming from
  them. In her fight, a spear swipe shows when the mode changes.
- **Why it works there:** Undyne is a protector, and the player has to stand and *take it*. Blocking reads as courage,
  not evasion.
- **The argument version:** In one phase Nadia doesn't dodge; she **guards** something that sits in the middle of the
  box (Elias's place setting, or Theo's drawing). She turns to face each throw.
  - It mirrors Lena: both women are protecting him, and the phase says so.
  - Best placed after "He called you first", where running away would be the wrong image.
- **Fairness:** Every throw enters from an edge in a straight line, and the four approach lanes are drawn in the box
  from the start.
- **Lena trait:** Caring, both ways: her protection meets Nadia's.
- **Cost:** **M**. A four-way input mode, block geometry, and the pilot.

### 4. The yellow SOUL and the Big Shot
- **Source:** *Undertale* (Mettaton), *Deltarune* (Spamton NEO).
- **How it works:** The SOUL turns yellow and fires shots. In Spamton NEO's fight, shots destroy some bullets, damage
  blue objects and end the turn faster; **holding** the button charges a Big Shot that fires on release.
- **Why it works there:** The player finally answers back inside the box, and the charge is a commitment.
- **The argument version:** Nadia can fire **questions** ("Why?") at some of Lena's things in the box: a tapped shot.
  - A shot on a slip opens it early.
  - A **held** shot is a small Press: it smashes a plate wall, but the plates *crack* on the table and Force goes up.
    This echoes the argument's own hold-to-Press.
  - The dodge teaches the same lesson as the verbs: asking is free, forcing costs.
- **Fairness:** Unchanged: the targets are hazards that already enter from outside.
- **Lena trait:** Curious: questions are her language too (she logs, she wonders).
- **Cost:** **M**. Shots and hold detection in `PressurePhase`; an engine hook for the forced variant.

### 5. Blue-SOUL gravity
- **Source:** *Undertale* (Papyrus, Sans).
- **How it works:** The SOUL falls under gravity and jumps over attacks, like an auto-scrolling platformer.
- **Why it works there:** Papyrus's attacks become a silly obstacle course (that's him); Sans's turn it into a test
  of nerve.
- **The argument version:** Use it **once**, for the painful premise ("He called you first"). The words land and
  Nadia's light gets *heavy*: it sinks to the floor of the box, and she can only jump.
  - It's guilt as weight, the one phase where the rules of her body change.
  - Lena's plates slide along the floor like bowling.
- **Fairness:** Floor hazards slide in from the sides; anything that drops from above casts a shadow first.
- **Lena trait:** None of hers. It's Nadia's feeling, which suits the one moment the argument is about Nadia.
- **Cost:** **M**. Gravity, a jump, floor hazards and the pilot.

### 6. The box changes shape
- **Source:** *Undertale* and *Deltarune*. Many fights resize or reshape the box; in Jevil's last stage the board
  grows until the game window itself is the border.
- **How it works:** The arena's shape is part of the attack: narrower boxes squeeze you, and a box that fills the
  screen says "this is the climax".
- **Why it works there:** It's free drama: the frame itself reacts.
- **The argument version:**
  - The box takes **the shape of what she's talking about**: round, like the attic's star window or a plate, when she
    argues about the house; long and narrow, like the hallway, when she talks about leaving.
  - As heat rises it narrows (she's closing the door).
  - When the painful premise surfaces, it opens to the **whole screen** for one phase. The Force-route rule-break
    (her patterns sliding over the buttons) is already this idea, kept for Night 4.
- **Fairness:** The box changes only *between* hazards, on a visible 0.3 s tween, and never closes on the light
  (the light is pushed inward first).
- **Lena trait:** The house obeys her: rooms rearrange. A box that reshapes is the dream doing what she says.
- **Cost:** **S** for a rectangle that resizes (the clamp and the spawn edges read the size); **M** for a round box
  (a circular clamp, and walls become chords).

### 7. ACT changes the next attack
- **Source:** *Undertale* and *Deltarune*.
  - Undyne's ACTs change her spear speed (Challenge faster, Plead slower).
  - Napstablook's tears form a top hat once you've cheered him (from memory, not confirmed on the web).
  - Jevil's patterns follow how *tired* the party's ACTs have made him.
- **How it works:** What you said in the menu changes what the attack *looks like*, not only how hard it is.
- **Why it works there:** The bullets are the monster's mood, so being kind is visibly answered in the pattern.
- **The argument version:** The current code already scales *intensity* by heat and anger. Borrow the *look*:
  - after a **concession**, Lena's next pattern turns softer and becomes a picture (flour rings settling into the
    shape of a pancake stack, or plates sliding into a set table for four, with the fourth place empty);
  - after a **wrong reason**, her pattern repeats Nadia's mistake back at her;
  - after an **antagonising reply**, it's sharper and messier.
  - So the last thing said is legible in the box.
- **Fairness:** Unchanged hazard rules; only the formations and art differ.
- **Lena trait:** Caring: she answers kindness in kind, and you can see it.
- **Cost:** **S–M**. Pattern presets keyed on the last verb's result (the engine already knows it).

### 8. TP from grazing, and the MERCY meter
- **Source:** *Deltarune*. Getting close to bullets without being hit (grazing) fills TP, the party's shared magic
  meter, which pays for spells and ACTs. Since Chapter 2, a **MERCY %** beside each enemy shows how near they are
  to being spared.
- **How it works:** Risk is rewarded: skimming danger buys your next turn's options.
- **Why it works there:** It ties the dodge to the menu: how you dodged decides what you can do next.
- **The argument version:** Near misses fill Nadia's **attention** (listening closely under pressure). She spends it in
  the argument:
  - on an extra Ask that digs one level deeper;
  - or on **"Think it through"**: one wrong reason card is struck out before she picks.

  Also show a **"Heard"** meter beside Lena (the MERCY %) that fills as premises are heard, so Let it rest has a
  visible goal. **This is the most direct answer to "the dodging feels slight"**: it makes the dodge feed the thinking.
- **Fairness:** No change to hazards. Grazing never forces risk: attention is a bonus, never a requirement for any
  outcome.
- **Lena trait:** Curious: noticing what passes close.
- **Cost:** **M**. A graze radius in `Tick`, a meter in the engine, and the spend in two verbs.

---

## Undertale Yellow

### 9. Axis: blocking fills a bar, then the big orb goes back
- **Source:** *Undertale Yellow*. Confirmed on its wiki.
  - Clover blocks Axis's attacks with a trash-can lid, which fills the Tick O'Bar.
  - When it's full, Axis fires a big energy orb, and Clover **reflects it back** at him.
  - A hit drains his battery and advances the phase; a miss costs nothing and the attack repeats next turn.
  - In later phases you aim the orb at generators instead.
- **Why it works there:** Defence becomes offence, on the enemy's own material, and failure is cheap.
- **The argument version:** Surviving a phase fills a small bar. When it's full, Lena's **claim** itself ("He stays.
  This is his home.") is thrown as the big orb, and Nadia sends it back into one premise of her choosing.
  - It's a *test*, not a break: Lena has to say how that premise holds the claim up, which reveals it if it was
    hidden.
  - A miss costs nothing.
- **Fairness:** The orb enters from Lena's side of the box after a visible wind-up; returning it is optional.
- **Lena trait:** None strongly. It fits the battle's logic (testing premises) more than her.
- **Cost:** **M**. A reflect hitbox, aiming, and an engine reveal hook.

### 10. Starlo's lasso: a tethered SOUL
- **Source:** *Undertale Yellow*. In Starlo's first phase he ties up the SOUL, limiting the player's movement
  (confirmed on the web).
- **How it works:** The SOUL can only move within reach of a fixed point.
- **Why it works there:** The constraint *is* Starlo: he's roping you in, and it plays like a Western.
- **The argument version:** Nadia's light is tied to **Elias's chair** (or to the fourth chair) by a thread. She
  can't leave the table. The thread lengthens as premises fall and shortens as heat rises.
- **Fairness:** The reach is drawn as a circle, and hazards enter from outside it.
- **Lena trait:** Her flaw: she protects by keeping, and keeps everyone at the table.
- **Cost:** **S**. A radial clamp around a point instead of the box clamp.

---

## Classic shoot 'em ups

### 11. Spell card declarations
- **Source:** *Touhou*.
  - Before each of a boss's set pieces, its **name** is declared on screen ("a spell card").
  - It has its own timer, and a bonus.
  - Clearing it with no life lost and no bomb used **captures** it (confirmed on the web).
- **Why it works there:** Each pattern is a named, authored *statement* by the character, and capturing it is a
  trophy you remember.
- **The argument version:** Each pressure phase is **declared with the premise it comes from**, in her words, in the
  display font: *"He chose this."*
  - The pattern is that sentence's feeling.
  - Surviving it without a hit **captures** the line, and it's written in the transcript as heard, which counts
    toward Let it rest.
  - The dodge stops being a gap between thoughts and becomes *listening to one specific thing she said*.
- **Fairness:** The declaration is itself a 0.6 s telegraph before any hazard spawns.
- **Lena trait:** Plain-spoken: she says exactly what she means, and the phase is that sentence.
- **Cost:** **S**. A title card at phase start, the premise id the engine already knows, and a capture flag.

### 12. Focus: slow down and see the true hitbox
- **Source:** *Touhou*. Holding Shift slows the ship and shows its hitbox as a small coloured dot, a few pixels
  across, much smaller than the sprite (confirmed on the web).
- **Why it works there:** Fine dodging becomes possible and *visible*, and the player chooses when to commit to
  precision.
- **The argument version:** Hold to **focus**: Nadia's light slows to half speed and shrinks to its true point. It
  reads as concentrating on what Lena means.
  - It's also an accessibility win (a precision mode without a setting).
  - It's the natural place to put **Danganronpa's Concentration** (slowing the whole scene) in Story mode.
- **Fairness:** It changes only the player's tools.
- **Lena trait:** Curious: a telescope's focus. The light could tighten into a star point when focused.
- **Cost:** **S**. A held input and a speed multiplier. The current hitbox already equals the drawn light, so the
  draw shrinks to it.

### 13. Polarity: absorb your own colour
- **Source:** *Ikaruga*.
  - The ship switches between white and black.
  - Bullets of its own colour are **absorbed**, and fill a homing-laser gauge; the others kill.
  - Shooting three enemies of one colour in a row makes a chain (confirmed on the web).
- **Why it works there:** Every bullet is either food or death, and switching is the skill. You fly *into* half the
  pattern.
- **The argument version:** Nadia switches between **"I hear you"** and **"I disagree"**.
  - Lena's warm things (her care) are absorbed while listening; her cold things (keeping her out) while disagreeing.
  - Absorbing fills attention (see 8).
  - **Caution:** the old slice's battle split the box into a Listening half and a Pressing half, and the creator
    replaced it. This is per object, not per half, but it's close enough to show them side by side before choosing.
- **Fairness:** Objects enter from outside. Polarity is shown by shape and stroke, not colour alone.
- **Lena trait:** Caring and keeping out, her two sides, made into the two polarities.
- **Cost:** **M**. A switch input, per-object polarity, and absorb logic.

### 14. Parry the flash
- **Source:** *Furi*, and *Bug Fables* for the turn-based form.
  - *Furi*: a flash and a sharp sound before an attack mean it can be parried; a parry heals a little.
  - *Furi*: big bullet patterns and beams have **no** flash and must be dodged; their red or orange glow says so.
  - *Bug Fables*: almost any enemy attack can be blocked with a timed press, which reduces the damage.
- **Why it works there:** The telegraph teaches timing, and the parry rewards attention rather than evasion. *Furi*
  makes clear which attacks are parryable.
- **The argument version:** Some of Lena's throws (a single plate, a batter lob) **flash** before they're thrown.
  - A well-timed press **catches** it instead of dodging. Nadia "takes the point", and her heart rate steps *down* one.
  - Everything else must be dodged.
  - It suits a nurse: steady hands, catching what's thrown.
- **Fairness:** The flash *is* the telegraph. Unflashing things follow the normal rules.
- **Lena trait:** Teasing: she tosses some things *to* Nadia, not at her.
- **Cost:** **S–M**. A flash state on blobs, a timing window, and a heart-rate step down.

### 15. The blank
- **Source:** *Enter the Gungeon*. A blank wipes every projectile on screen and staggers most enemies; you start each
  floor with two (confirmed on the web).
- **Why it works there:** It's a panic button with a real cost, so you hoard it and feel clever when you spend it.
- **The argument version:** **"Breathe."** Once per battle, Nadia uses the box-breathing a nurse teaches patients:
  - the screen exhales;
  - every hazard fades;
  - her heart rate drops one step.

  It costs a moment of honesty: she says "I need a second" in the transcript, and Lena gets a gentle reply ("Take
  it."). Lena, who loves her, lets her breathe.
- **Fairness:** Purely defensive.
- **Lena trait:** Caring: her answer to it.
- **Cost:** **S**. Clear the lists, step the heart rate, add a transcript line.

### 16. The table flip
- **Source:** *Enter the Gungeon*. Tables can be flipped into cover that blocks bullets until it breaks (from memory;
  not confirmed on the web this session).
- **Why it works there:** The environment becomes a tool, and it's funny.
- **The argument version:** It's literally a kitchen table. Once per phase Nadia can **tip a plate up** in front of
  her: a short wall that blocks a few hits and then breaks (a smashed plate, like a forced premise). It's a way to
  hold ground, at a small cost to the table Lena laid.
- **Fairness:** Purely defensive.
- **Lena trait:** Her things, though breaking them is Nadia's choice and should read as a small hurt.
- **Cost:** **S**. A temporary wall segment with hit points.

---

## Rhythm and readability

### 17. The warning line before the beam, and "pink is harm"
- **Source:** *Just Shapes & Beats*.
  - Every hazard is one colour (pink), so the rule is learned at a glance.
  - Attacks are timed to the music, with big drops as the warning.
  - Lasers and walls are drawn faintly before they fire.
  - Its only defence is a short invulnerable dash.
  - (*Danganronpa V3*'s Argument Armament is the same idea inside a debate: break armour made of their arguments by
    pressing on the beat.)
- **Why it works there:** The player is never surprised: they're warned by sight and by sound, so the difficulty is
  speed, not ambush.
- **The argument version:** **Lena sketches the attack first.** Every hazard that would appear *inside* the box (a
  ring, a line, a landing plate) is first drawn as a faint **pencil line** for about 0.5 s, then inked in.
  - It's her pencil and her star log, and it's the house obeying her drawing.
  - It also fixes the unfair flour rings: they're pencilled as a circle (with the gap left out) before they bloom.
  - Patterns can land on the battle loop's beat.
- **Fairness:** This *is* the fairness rule, made into a style the whole game can use.
- **Lena trait:** Curious; the stars: the pencil log, sketching what she sees.
- **Cost:** **S**. A "pending" state per hazard with a delay, drawn faint. The beat sync adds a little more.

### 18. Parry the pink
- **Source:** *Cuphead*. Pink objects (some bullets, some limbs) can be slapped with a second jump in midair. Each
  parry adds to the super meter, and a missed parry still hurts (confirmed on the web).
- **Why it works there:** One rule ("pink is touchable") turns a few hazards into opportunities, and chains of parries
  are a skill ceiling.
- **The argument version:** The slips already work this way: Theo's paper planes are the "pink". Extend it:
  - one plane in a phase is **folded from a page of Lena's star log**;
  - touching it gives a line in her handwriting (a hint about the Thursdays, "Clear tonight. Drove out past the
    lights.");
  - it's a slip that reveals *her* subtext, not Theo's.
- **Fairness:** Slips are harmless; nothing changes.
- **Lena trait:** Curious; the stars (her log).
- **Cost:** **S**. A new slip kind with its own art and line.

### 19. Clear wind-ups on the body
- **Source:** *Hollow Knight* and *Silksong*. Commentators credit Hollow Knight's bosses with clear silhouettes and
  consistent hit cues (Grimm is the usual example). *Silksong*'s developers said they changed how bosses pick
  attacks, from reacting to the player's position to something else.
- **Why it works there:** The body *is* the telegraph: you learn a boss by its poses, and every death is readable.
- **The argument version:** Lena's large sprite is already on the left of the box. Give each pattern family a
  **wind-up pose**:
  - she lifts the notebook to her chest before walls;
  - she dusts flour from her hands before rings;
  - she glances up and away (her "listening to something far off") before a slip.

  The player reads *her*, not just the box.
- **Fairness:** A telegraph for every pattern, at no cost to the box.
- **Lena trait:** Her look in the dream: the notebook held to her chest, looking away.
- **Cost:** **S** for code (an expression per pattern); art for three or four poses.

### 20. Follow the path: the duet
- **Source:** *Sayonara Wild Hearts*.
  - Levels are a song you steer through, collecting hearts along a line and dodging obstacles.
  - Some levels are dance duels and fights with the other bikers.
  - There's no health bar: a crash sends you back to just before it, almost at once.
- **Why it works there:** Following a line feels like dancing with the music and with the rival. It's forgiving, so it
  stays expressive.
- **The argument version:** On the **Heard out** path, once every premise is heard, Lena's last pattern changes. She
  **draws a constellation** across the box, and Nadia follows it, collecting the stars rather than dodging.
  - It's the only phase in the battle that's a duet.
  - Missing a star costs nothing: the line waits.
  - It gives the gentle ending an action beat of its own, and it rewards the caring route with play, not just a
    keepsake.
- **Fairness:** Nothing in it hurts.
- **Lena trait:** Caring; the stars. She shows Nadia how she sees the sky.
- **Cost:** **S–M**. A scripted pickup path and a closing hook in the engine.

---

## Turn-based RPGs

### 21. Locks on a charging attack
- **Source:** *Sea of Stars*.
  - An enemy charging a special shows **locks** above it, each marked with a damage type, and a turn countdown.
  - Hitting it with a matching type before the countdown ends breaks a lock: each break weakens the attack, and
    breaking all of them cancels it (confirmed on the web).
- **Why it works there:** The telegraph becomes a puzzle you solve in the menu, so planning and defence are one thing.
- **The argument version:** The painful premise ("He called you first") is foreshadowed.
  - Two exchanges before it surfaces, Lena's claim card starts to **darken**, with two or three locks on it, shown as
    verb icons (an Ask, a Concede, an evidence style).
  - Each matching move Nadia makes in the argument breaks one, and every lock broken takes something out of the coming
    high phase.
  - If Nadia has already made the deduction about why Lena came, she starts with a lock broken.
  - **The thinking shapes the dodging**, the other way round from 8.
- **Fairness:** The heaviest phase in the battle is announced two exchanges ahead.
- **Lena trait:** None directly. It shows *Nadia* preparing for the truth Lena will say.
- **Cost:** **M**. Lock data on painful premises, matching in the engine, and a spec reduction.

### 22. Emotion states choose the pattern
- **Source:** *OMORI*. Fighters are Neutral, Happy, Sad or Angry, each with stat effects, in a rock-paper-scissors
  (Happy beats Angry, Angry beats Sad, Sad beats Happy). Emotions stack (Happy, then Ecstatic, then Manic).
- **Why it works there:** Feelings are the combat stats, which suits a game about avoiding them.
- **The argument version:** Lena's expression (neutral, angry, rattled, softened, hurt, pleading) already follows her
  last line. Let it **choose the pattern family**:
  - **angry:** fast walls;
  - **hurt:** slow, wide rings;
  - **rattled:** scattered blobs, poorly aimed;
  - **pleading:** hazards that drift *toward* Nadia without reaching her.

  A second step could stack her emotion if the same thing is pushed twice.
- **Fairness:** Unchanged hazards; only the mix.
- **Lena trait:** Her warmth and directness: what she feels is what you dodge.
- **Cost:** **S**. The shape list chosen from the current expression (data the engine already has).

---

## Debate games: Danganronpa, Ace Attorney, and others

### 23. Nonstop Debate: shoot the weak point in moving text
- **Source:** *Danganronpa*.
  - In class trials, statements **scroll across the screen** as text, with **weak points** highlighted, and you
    fire a Truth Bullet (a piece of evidence) at the weak point that contradicts it.
  - **White Noise** (other chatter) drifts around in the way: shooting it gains time, hitting it by accident costs
    time.
  - **Concentration** slows the scene.
  - Later games add "agree" points (shoot to support a true statement) and, in *V3*, Lie Bullets (hold to perjure).
  - *V3*'s **Mass Panic Debate** has several people talking over each other.
  - Its **Debate Scrum** matches a key word in each opposing statement with one of yours.
- **Why it works there:** It's the closest thing to an argument *as* action: the evidence system and the shooter are
  one act, so aiming is reasoning.
- **The argument version:** A **phase made of her words**: Lena's premise lines cross the box as the hazards
  themselves, and one phrase in each glows.
  - Nadia fires the *matching evidence* (picked before the phase, so the thinking stays untimed) at the right phrase.
  - Paper planes are the white noise.
  - Agree points become concessions: a true phrase can be touched to concede it.
  - **Caution:**
    - it's text as hazards, which is costly;
    - it conflicts with "no clock on thinking" unless the choice is made before the phase;
    - judges will recognise Danganronpa.
  - Best as **one** set piece (the last premise, "Then let him have his wish"), not the default phase.
- **Fairness:** Text enters from the edges; the weak point is visible as it enters.
- **Lena trait:** Plain-spoken: her actual sentences are what's flying.
- **Cost:** **L**. Text hazards with per-word hitboxes, shooting, and evidence preselection.

### 24. Rebuttal Showdown: cut through the statement
- **Source:** *Danganronpa 2* and *V3*. A one-on-one where the opponent's lines fly at you and you **slash** through
  them, then cut the weak point with a **Truth Blade**: a piece of evidence used as a sword, with a long cooldown.
  In *V3* the debate's mood sets how far you can aim.
- **Why it works there:** One-on-one arguments feel personal, and slashing is more aggressive than shooting: a duel.
- **The argument version:** For the Force route only (Night 4's rule-break, or a battle ended by Press). The pattern
  is Lena's lines coming at Nadia, and Press becomes a slash.
  - It's the right feel for forcing: violent, satisfying, and a little wrong.
  - Keep it out of the sound path, so the honest path never trains the player to cut.
- **Fairness:** Lines enter from Lena's side, one at a time.
- **Lena trait:** None: it's what Nadia does when she stops listening.
- **Cost:** **L**. The same text hazards as 23, plus a melee arc.

### 25. Logic Dive and Psyche Taxi: answer by choosing the lane
- **Source:** *Danganronpa 2* (Logic Dive), *V3* (Psyche Taxi).
  - In Logic Dive you ride down a course in the hero's mind and answer multiple-choice questions by **taking the right
    branch**; a wrong branch makes you fall off and lose time.
  - Psyche Taxi collects letters on a road, then picks up the passenger with the question.
- **Why it works there:** The answer is a place you steer to, so thinking and driving merge, and the game moves at
  speed.
- **The argument version:** After a sound challenge, the **reasoning replays as lanes**. The three "because..." cards
  become three lanes at the end of a short phase.
  - The choice was already made in the untimed menu; here it's re-enacted (the right lane is lit as it enters), so
    it's a victory lap, not a quiz.
  - A **riskier variant** asks for the choice during the phase. It breaks "no clock on thinking", so it belongs in an
    optional hard mode at most.
- **Fairness:** The lanes and hazards enter from the far edge.
- **Lena trait:** Curious: her road out past the city lights.
- **Cost:** **M**. A scrolling lane layout and labels in the box.

### 26. Hangman's Gambit, as a constellation
- **Source:** *Danganronpa*. You reveal a key word by picking the right letters as they float past. *V3*'s version
  has you shine a light to find letters in the dark. A wrong letter costs you.
- **Why it works there:** It turns "the word on the tip of your tongue" into play.
- **The argument version:** The **deepest hidden premise** (its words are undecided: the prototype's Theo-drawing
  premise is cut as out of character) is revealed today by a paper-plane slip or by asking twice. Add a third way:
  - stars appear one by one in the box;
  - each one Nadia touches, *in order*, draws a line;
  - the finished constellation is the shape of the words.
  - Wrong stars cost nothing but time.
- **Fairness:** Stars are harmless; the phase's hazards still follow the rules.
- **Lena trait:** **The stars**; curious: finding meaning by joining points.
- **Cost:** **M**. An ordered pickup set, line drawing, and a reveal hook (slips already reveal premises).

### 27. Pit two statements against each other
- **Source:** *The Great Ace Attorney*. In Summation Examinations you **pit** two jurors' statements against each
  other, picking one and then the other, when they contradict or bear on each other. The jurors change their minds
  and their statements change.
- **Why it works there:** It finds the inconsistency *between* people (or between remarks), not just against
  evidence: it's how arguments really unravel.
- **The argument version:** Two of Lena's premises that pull against each other enter the box as two different hazards:
  - "He chose this", and
  - a hidden premise that pulls against it (which one is undecided: the prototype's pair used the cut Theo-drawing
    premise).

  Steering them into each other **cancels both** in a burst and highlights the pair on the premise map: a hint that
  they contradict. **This is the brief's "challenging inconsistencies", played in the box.**
- **Fairness:** Both enter from edges. Luring is optional.
- **Lena trait:** Curious: she'd want to know why two things she believes don't fit.
- **Cost:** **M**. Hazards that steer toward the light, collisions between hazards, and a map highlight.

### 28. The discordant emotion
- **Source:** *Ace Attorney: Dual Destinies* (the Mood Matrix) and *Spirit of Justice* (the Divination Séance).
  - The witness's emotions are read as they testify. A feeling that **doesn't match** what they're saying is pointed
    at, with the Probe, to expose what they're hiding.
  - The séance does the same with a victim's last sensations against the stated story.
- **Why it works there:** It's cross-examination of feeling, not fact, which suits characters who are lying to
  themselves.
- **The argument version:** In each pattern, **one object is the wrong feeling**: in a phase of brisk, cheerful
  plates, one plate is cracked and moving slowly. Touching it shows the feeling under the words.
  - It's a slip with a reason to exist: Lena says she's fine, the pattern says she isn't.
  - Variant: in a calm phase, one object is angry.
- **Fairness:** Harmless to touch; it enters with the rest.
- **Lena trait:** She knows she's a memory: the discord is the part of her that knows.
- **Cost:** **S**. A slip with a different look and speed.

### 29. Each argument fights on its own
- **Source:** *Griftlands*.
  - Negotiation is a card battle where each side has a **core argument** with **resolve** (health) and can raise
    **supporting arguments**, which act on their own each turn.
  - You attack their resolve or knock out an argument directly.
  - Composure shields your resolve, and resolve carries between negotiations (confirmed on the web).
- **Why it works there:** The state of the debate is on the board: every argument still standing is a visible thing
  that keeps hurting you.
- **The argument version:** **Each standing premise owns a hazard** in the box:
  - "Out there, Theo is dead" is the slow rings;
  - "He chose this" is the walls;
  - and so on.
  - Breaking a premise **removes its hazard** from later phases; conceding one turns it harmless (it drifts through,
    glowing).
  - So the box *is* the argument's state: a sparse box means you've argued well, without a single number.
  - It extends what heat does today.
- **Fairness:** Each premise's hazard must follow the rule. This is a good moment to retire the unwarned rings, or
  pencil them (17).
- **Lena trait:** It shows her case as hers: each fear has its own shape.
- **Cost:** **S–M**. A shape on each premise, and the spec built from the standing premises rather than only the heat
  level.

### 30. Words as weapons, and what's left when they're gone
- **Source:** *NieR: Automata* (hacking).
  - 9S's hack is a small twin-stick shooter inside the enemy: shoot away the **black blocks** to expose the core, then
    hit the core.
  - Hostile blocks move and shoot; too many hits and the hack fails and 9S takes damage (confirmed).
  - From memory, and *not* confirmed this session: some enemy shots (orange) can be shot down and others can't.
- **Why it works there:** Going *inside* the enemy is the theme, and the shell-and-core layout shows exactly what's
  left.
- **The argument version:** A late phase is **inside her notebook**. Her claim is a core in the middle of the box,
  shelled by tags of the premises still standing.
  - Broken premises' tags are already gone.
  - Nadia (with 4's questions) can only open tags that are *hidden* (a question reveals them), never break standing
    ones.
  - It shows the map in the box, and gives the yellow-SOUL idea a target.
- **Fairness:** The shell is static at the start; the shots from tags follow the rule.
- **Lena trait:** Curious: the notebook, her inner pages.
- **Cost:** **M–L**. A new layout, shooting, and the mapping from premises to tags.

### Looked at, little to borrow
- ***OFF*** (the 2025 remaster): action timers instead of turns, and odd elements (smoke, metal, plastic, meat,
  sugar). No dodging; the strangeness is the lesson, not a mechanic.
- ***Hylics*:** first-person turn-based, with "flesh" and "will". Its look is its strength, not its rules.
- ***LISA: The Painful*:** "Joy", a drug that boosts Brad but changes him for good. It's the same shape as Press
  (power now, a cost forever), which the battle already has.
- ***Your Turn to Die*:** wrong or irrelevant remarks in discussions drop the hero's **clout**, and zero is a game
  over. It's close to the existing heat.
- **Ace Attorney's Press and Present:** the battle's Ask and Challenge already are these (in Ace Attorney, pressing
  is almost never punished; wrong evidence costs a strike).

---

## The 8 best-fitting borrowings, and why
Chosen for three things at once: they make the dodge **part of the argument** (the creator's "slight" note), they keep
or strengthen the **fairness rule**, and they're **true to Lena** (curious, caring, the stars). Cheap ones first
where it's close.

| # | Borrowing | Why it's in the eight | Cost |
|---|---|---|---|
| 1 | **Lena sketches the attack first** (17, from *Just Shapes & Beats*' warning lines) | Fairness becomes her signature: every in-box attack is pencilled before it's inked, in her star-log pencil. It fixes the unwarned flour rings and costs little | S |
| 2 | **Spell-card declarations** (11, *Touhou*) | Each phase is named by the line of hers it comes from, and surviving it "captures" the line as heard. The dodge becomes *listening to that sentence* | S |
| 3 | **Each premise owns a hazard** (29, *Griftlands*) | The box shows the argument's state: break a premise and its hazard leaves for good; concede one and it drifts through harmless. Arguing well visibly empties the box | S–M |
| 4 | **Grazing feeds the thinking** (8, *Deltarune*'s TP, and the MERCY meter) | The most direct answer to "slight": close listening under pressure buys an extra Ask or strikes out a wrong reason. A "Heard" meter gives Let it rest a visible goal | M |
| 5 | **Constellation lines** (2, *Undertale*'s purple SOUL), **with the constellation hangman** (26) as its reveal | The most *Lena* mode on the list: the box is her star chart, Nadia moves along her lines, and the deepest hidden premise is found by joining stars | M |
| 6 | **The duet on the Heard-out path** (20, *Sayonara Wild Hearts*) | Her caring side, played: once everything is heard, she draws a constellation and Nadia follows it. The gentle ending gets its own action beat | S–M |
| 7 | **Hush and Answer-me** (1, *Undertale*'s blue and orange) | One rule that puts the argument's choice (listen or push back) into the dodge. Her "hold still, love" is caring and keeping out at once | S |
| 8 | **Pit two of her lines against each other** (27, *The Great Ace Attorney*) | The brief's "challenging inconsistencies", done in the box: lure "He chose this" into a premise that pulls against it and the map flags the contradiction. It's curious play, not just evasion | M |

**Close runners-up:**
- **Locks before the painful phase** (21, *Sea of Stars*): the strongest *two-way* link between thinking and dodging.
  Pick it if the selection wants one more systems idea instead of a Lena-flavoured one.
- **Wind-up poses on her sprite** (19, *Hollow Knight*): the cheapest extra telegraph, with art the battle already
  half has.
- **The Nonstop Debate set piece** (23, *Danganronpa*): the most famous argument-as-action, but large to build and
  easy for judges to call derivative.

---

## Sources
Web pages checked for this survey (2026-10-12). Fan wikis were used where no official source exists.
- Undertale: [Eight Humans (Undertale Wiki)](https://undertale.fandom.com/wiki/Eight_Humans), [Undertale on Giant Bomb](https://www.giantbomb.com/undertale/3030-50897/), [Papyrus in battle](https://undertale.wiki/w/Papyrus/In_battle), [Mettaton in battle](https://undertale.fandom.com/wiki/Mettaton/In_Battle), [Undyne in battle](https://undertale.wiki/w/Undyne/In_battle), [ACT](https://undertale.wiki/w/ACT)
- Deltarune: [TP](https://deltarune.wiki/w/TP), [Yellow SOUL](https://deltarune.wiki/w/Yellow_SOUL), [Spamton NEO](https://deltarune.wiki/w/Spamton_NEO), [Jevil](https://deltarune.wiki/w/Jevil), [Queen](https://deltarune.fandom.com/wiki/Queen), [Deltarune (Wikipedia)](https://en.wikipedia.org/wiki/Deltarune)
- Undertale Yellow: [Undertale Yellow (Wikipedia)](https://en.wikipedia.org/wiki/Undertale_Yellow), [Axis (UTY wiki)](https://undertaleyellow.wiki.gg/wiki/Axis)
- Touhou: [Perfect Cherry Blossom on Giant Bomb](https://www.giantbomb.com/games/3030-21929/), [Embodiment of Scarlet Devil (Wikipedia)](https://en.wikipedia.org/wiki/Embodiment_of_Scarlet_Devil), [Mountain of Faith gameplay (Touhou Wiki)](https://touhou.fandom.com/wiki/Mountain_of_Faith/Gameplay)
- Ikaruga: [Ikaruga (Wikipedia)](https://en.wikipedia.org/wiki/Ikaruga)
- Furi: [Furi general tips (Gamepressure)](https://www.gamepressure.com/furi/general-tips/zf9033), [Furi wiki mirror](https://breezewiki.discard.no/furigame/wiki/Furi)
- Enter the Gungeon: [Dodge Roll (wiki)](https://enterthegungeon.wiki.gg/wiki/Dodge_Roll_(Move)), [5 tips for beginners (Shacknews)](https://shacknews.com/article/94141/enter-the-gungeon-5-tips-for-beginners)
- Just Shapes & Beats: [GDC 2018 hands-on (Shacknews)](https://shacknews.com/article/104019/just-shapes--beats-hands-on-at-gdc-2018), [review (Cubed3)](https://www.cubed3.com/games/reviews/nintendo-switch/just-shapes-and-beats)
- Cuphead: [Parry (Cuphead wiki)](https://cuphead.wiki.gg/wiki/Parry), [how to parry (Prima)](https://primagames.com/tips/cuphead-parry-controls)
- NieR: Automata: [how to hack (Twinfinite)](https://twinfinite.net/guides/nier-automata-how-to-hack/)
- Hollow Knight and Silksong: [Silksong devs on boss design (Military.com)](https://mst.military.com/off-duty/games/silksong-devs-explain-how-bosses-were-designed-differently-those-hollow-knight.html), [what makes a good boss fight (blog)](https://choostgames.com/blog/what-makes-a-good-boss-fight/)
- Sayonara Wild Hearts: [Wikipedia](https://en.wikipedia.org/wiki/Sayonara_Wild_Hearts), [PlayStation Blog](https://blog.playstation.com/2019/12/26/editors-choice-sayonara-wild-hearts-defies-music-game-boundaries/), [God is a Geek review](https://godisageek.com/reviews/sayonara-wild-hearts-review/)
- Bug Fables: [action commands (Bug Fables Wiki)](https://bugfables.fandom.com/wiki/File:Buttonsequence.jpg), [review (Nintendo World Report)](https://www.nintendoworldreport.com/review/53853)
- Sea of Stars: [locks video (Twinfinite)](https://twinfinite.net/news/sea-of-stars-video-locks/), [beginner's guide (BlueStacks)](https://www.bluestacks.com/blog/game-guides/sea-of-stars/soss-beginners-guide-en.html)
- OMORI, OFF, Hylics, LISA: [Omori (Wikipedia)](https://en.wikipedia.org/wiki/Omori_(video_game)), [Off (Wikipedia)](https://en.wikipedia.org/wiki/Off_(video_game)), [OFF review (Checkpoint)](https://checkpointgaming.net/reviews/2025/08/off-review-escaping-from-your-purpose-is-impossible/), [Hylics (Wikipedia)](https://en.wikipedia.org/wiki/Hylics), [LISA Definitive Edition review (CGM)](https://www.cgmagonline.com/review/game/lisa-definitive-edition-nintendo-switch)
- Your Turn to Die: [Wikipedia](https://en.wikipedia.org/wiki/Your_Turn_to_Die_-Death_Game_by_Majority-)
- Danganronpa: [Class Trials, DR1 (Danganronpa Wiki)](https://danganronpa.fandom.com/wiki/Class_Trials/Danganronpa_1), [Gameplay (StrategyWiki)](https://strategywiki.org/wiki/Danganronpa:_Trigger_Happy_Havoc/Gameplay), [V3 trials (Siliconera)](https://www.siliconera.com/dangaronpa-v3-makes-class-trials-complex-frantic/), [V3 trailer (Gematsu)](https://gematsu.com/2016/10/new-danganronpa-v3-introduction-trailer-nonstop-debate-lies-panic-debate-rebuttal-showdown-detailed), [Hangman's Gambit Ver. 3.0](https://danganronpa.fandom.com/wiki/Hangman%27s_Gambit_Ver._3.0), [V3 (Danganronpa Wiki)](https://danganronpa.fandom.com/wiki/Danganronpa_V3:_Killing_Harmony)
- Ace Attorney: [Justice for All (Wikipedia)](https://en.wikipedia.org/wiki/Phoenix_Wright:_Ace_Attorney_%E2%80%93_Justice_for_All), [The Great Ace Attorney: Adventures (Wikipedia)](https://en.wikipedia.org/wiki/The_Great_Ace_Attorney:_Adventures), [Spirit of Justice (Wikipedia)](https://en.wikipedia.org/wiki/Phoenix_Wright:_Ace_Attorney_%E2%80%93_Spirit_of_Justice), [Phoenix Wright gameplay (StrategyWiki)](https://strategywiki.org/wiki/Phoenix_Wright:_Ace_Attorney_-_Justice_For_All/Gameplay)
- Griftlands: [Wikipedia](https://en.wikipedia.org/wiki/Griftlands), [negotiation guide (GameSkinny)](https://www.gameskinny.com/sqa55/griftlands-guide-what-are-negotiations-and-how-do-they-work)
