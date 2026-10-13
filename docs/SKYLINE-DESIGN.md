# Skyline Repo: the design (spec)

Written 2026-10-13 from the brainstorm with the creator (sections approved one by one). The handoff that started it is
`docs/SKYLINE-REPO-HANDOFF.md`. The running log is `docs/SKYLINE-WORKLOG.md`. Changes after approval go in section 14,
never silently into the text above it.

**What this is.** An FPS roguelite on VibeJamKit's Titanfall-style movement engine. The goal of the first build is a
**vertical slice**: one district (the Galleria) built to the full quality bar, on systems built as data so further
districts are content, not code. It's also hackathon practice: the generator, the Ledger, the weapon pipeline and the
"ears" are meant to carry over to whatever theme the jam gives.

**What the creator said they want (the brief):** procedurally generated rooms with objectives and puzzles, adds and
bosses, temporary rewards per room, permanent rewards scaled by clear time and difficulty, player upgrades, party
construction, floor changes, skips; a light plot; "a bunch of cool weapon designs and gun-feel, with abilities sprinkled
in for a power fantasy"; the movement engine kept, with dodging made hard and encounters interesting.

---

## 1. Setting, cast and story

### 1.1 The world
**Halcyon Row:** a sunny near-future district of glass towers joined by sky-bridges, a monorail and rooftop gardens, all
owned by one bankrupt landlord. Its building AI, **CONCIERGE**, has locked the Row down and **remodels its floors every
hour** (the in-world reason the rooms are generated).

### 1.2 Who you are: nobody human goes in
- **Kestrel & Daughters Asset Recovery**, a small family repo firm, works out of a battered tow van at the Row's edge.
  A small firm takes any job that pays; no further reason is needed.
- **You pilot the Unit**, a second-hand repo bot, remotely from the van (like Fortnite Save the World's Commander
  directing heroes, except you drive the body yourself, in first person).
- **On the radio** (humans, voices only):
  - **Nan Kestrel**, the dispatcher: dry, has seen everything. Teaches movement and objectives.
  - **Dot**, the cousin who reprograms the bots you repossess. Over-excited. Teaches hires and gadgets.
- **Everything in the building is a bot**: you, your party, CONCIERGE's security. Robots wrecking robots keeps the tone
  light. The creator's reasoning: a human player needs a human party (faces, animation, voices), and a human beside bot
  hires doesn't work; humans also make it more serious.
- **Colour by owner:** CONCIERGE's bots in white and gold; the firm's in tow-truck orange with hazard stripes.

### 1.3 CONCIERGE
- Talks over every PA, polite and passive-aggressive ("Your visit has been noted. Please enjoy our complimentary
  turrets."). Carries the comedy and the plot; there are no cutscenes.
- **Teaches enemy mechanics as safety notices**: "Please note: the floor-care unit operates at ankle height. Guests are
  advised to jump."
- Treats the Unit as a very rude guest, and knows every staff bot by name (it's their manager, and very disappointed in
  the ones you steal).
- **It learns how you play** (the Ledger, section 4) and announces what it's doing about it.

### 1.4 Death and retries
- A wrecked Unit is **thrown out like a knocked-out guest**: CONCIERGE dumps it on the kerb as "lost property".
- The prototypes you found are left with the wreck, and CONCIERGE "recycles" them (the cost of dying).
- **Salvage:** Dot scraps the wreck for one part from that run (for example the best prototype, rebuilt at reduced
  strength), which lasts the next 1-2 runs and then wears out. The repo's bullet-hell "remnant" system
  (`docs/DESIGN-ROGUELITE.md` section 5) is the model; Skyline adds the lifespan.
- The firm still earns a part-commission for progress (permanent progress, section 6).
- CONCIERGE stays competent: it *is* winning each time it wrecks you. It just can't stop the firm sending another Unit.

### 1.5 The plot and the ending: a real dilemma
- **Known from the start:** the court order lists the landlord's creditors, and the biggest is the Row's laid-off staff
  (cleaners, guards, monorail crew), owed months of wages. You know from day one who the job helps.
- **Discovered at the end:** CONCIERGE locked down to protect about forty tenants who can't afford to move out. You learn
  who the job hurts.
- **Foreshadowing:** **CONCIERGE has never fired on a human.** Its turrets go quiet when a tenant's kid crosses a
  sky-bridge; laundry hangs on a bridge; drawings in the vents; Manny's food-court secret (section 3).
- **The two endings:**
  - **Repossess the core:** the staff get paid; the tenants are evicted.
  - **Leave it running:** the tenants keep their homes; the staff stay unpaid; Kestrel & Daughters is in trouble.
- Neither side is a corporation (the creator's rule: an oppressor on one side makes a fake dilemma).
- **The hires' stakes are split** (to confirm when the story is written): some hires want the repossession (Suds wants
  its old cleaner paid; Manny wants out of his shop window, and an auction is his way out), others side with the tenants
  (the Greeters, later hires). The player's attachment to the crew pulls both ways. An optional deeper layer was
  discussed (the hires are themselves assets on the creditor list); the creator worried it would skew players to one
  ending, so it stays out unless it's balanced this way.

### 1.6 Story structure: a subplot per floor
- **The most important part of the story** (the creator): small subplots on each floor, told within the floor, that
  get referenced again later or change the story, alongside the main dilemma's foreshadowing.
- The generator places **one authored story room per floor** among the generated ones. It carries that floor's subplot
  (environmental storytelling, the PA, hire banter).
- Subplots usually belong to a recruitable hire (section 3), so story, party and foreshadowing share one piece of
  content.
- **Layers:** a casual player gets a complete comedy run. Deeper: CONCIERGE's lines shift with what it knows about you
  (how many of its bots you've re-hired, how often you've been thrown out), and the tenants' traces build towards the
  twist.

### 1.7 Districts
1. **The Galleria** (a mall; the slice). Boss: **LOSS PREVENTION**, a security mech.
2. The offices. 3. The R&D labs. 4. The hydroponic gardens. 5. The casino. 6. The penthouses (CONCIERGE's core).

Sky-bridges and the monorail join them. CONCIERGE's core glows at the top of the last tower, visible from every window
(the far goal is always in view).

---

## 2. Combat

### 2.1 Loadout
Two guns plus one gadget. Swapping between the two guns is a designed moment (an equip sound and animation per gun).

### 2.2 Weapons: identity first, upgrades deepen it
- **Bailiff kit** (base guns, unlocked for the starting loadout with commission):
  - **Writ**, a precise sidearm. Identity: every headshot or weak-point hit creates a weak spot on that enemy that takes
    more damage than a headshot. Upgrades: more damage per spot, and more spots created after a reload depending on how
    many spot hits the last magazine landed. Capstone: any damage source can hit the spots, area damage included.
  - **Breacher**, a door-breach shotgun. Identity: knockback, **including on yourself**. The self-knockback is
    momentum you can carry into an air-strafe (like TF2's Soldier rocket-jumping, or Destiny 2's Micro-Missile frame
    grenade launchers), so it's a movement tool as well as a gun.
  - **Nailer**, an SMG that pins light bots to walls.
  - **Tow Line**, a cable rifle that yanks small bots to you.
- **Galleria prototypes** (found in runs; each changes a verb):
  - **Price Gun**: tags enemies; tagged ones take extra damage, and the tag jumps to the next enemy on a kill.
  - **Shop-Vac**: sucks projectiles out of the air and fires them back (a defensive verb).
  - **Coin Fountain**: ricochet coins for shooting around cover.
- **Every gun is usable with a clear identity at base**, and upgrades come in three kinds: numbers and stats, then
  augments that deepen the identity, then a capstone that extends it (the Writ path is the template). Capstones may
  feed other guns (the Writ's spots plus an explosive).

### 2.3 Gun-feel: sound, sight and numbers are one design
- **Sound design must be phenomenal**, and comes first. A gun that sounds like "a well-oiled hydraulic masterpiece
  slinging nails" beats any amount of camera kick. Example of the bar (the creator's): a Void Pistol whose impact is a
  purple-black void sphere blooming and vaporising everything inside, with an alien "vorp" bloom and a devastating
  implosion, and an RPM low enough that the visuals have room to breathe.
- **Design, balance, visuals and audio are one job per weapon.** Fire rate, magazine size and reload time are balance
  numbers *and* animation and sound timings.
- **Each weapon has its own agent** that owns its identity sheet, model, VFX, layered sound, numbers, upgrade path and
  its **handling set**:
  - equip, fire, dry fire, reload (partial and from-empty), inspect: sound + animation for each.
  - **Reloads are a timeline of events** (mag out, cell in, hydraulic seat, bolt) at fixed fractions of the reload time.
    Upgrading reload speed rescales the timeline; the sound events stay locked to the animation.
  - The viewmodel is robot arms, so reloads can be mechanical and a bit fantastical, animated in code from the event
    timeline (no hand-keyed skeletal animation).
- **Feedback:** camera kick that recovers, tracers, hit markers with layered sound, hitstop and shake (existing
  `Combat/HitStop.cs`, `Combat/ScreenShake.cs`), **bots shed plates where you hit them**, kills burst into parts.
- **References** (measuring sticks only, never shipped): Destiny 2, Witchfire, Cyberpunk 2077 and similar. Shipped
  sound is synthesized, recorded or CC0, layered from several sources to land near the reference.
- **Final review of every gun: the creator plays it.**

### 2.4 Health
- **Plating** is regained by stripping scrap from fresh wrecks, so aggression heals (DOOM Eternal's lesson: kiting
  stops paying).
- **Chassis** is health that doesn't regenerate within a floor.

### 2.5 Enemies: each has an identity
Every enemy has a face or display, idle, alert and attack states, its own sounds and barks, and **an aim tag** (2.6).

| Bot | Identity | What it does | Habit it punishes | Aim tag |
|---|---|---|---|---|
| Floor-care unit | a roomba with a happy face display; angry when it spots a "stain" (you) and tries to clean you out, with acid | ankle-height acid sweep | staying on the floor | area |
| Greeter | relentlessly welcoming | charges from the flanks | standing still | melee |
| Mall-cop drone | officious | shots that **lead your movement** | straight lines | leads you |
| Window-washer gondola | rides the façade | lays mines along walls | long wall-runs | area |
| Spotlight | a searchlight with a rifle | hitscan after a 0.8 s laser telegraph; guards hook anchors | lingering exposed up high | fires at where you are now (telegraphed) |
| Foam turret | a gift-wrap station | sticky slowing patches on the floor | standing still | area |

Identities are written in full by each enemy's agent (section 9).

### 2.6 Dodging is hard, and fair
- **Speed alone doesn't save you; changing direction does.** Leading shots aim at where you're going (the lead factor
  rises with difficulty).
- **Every encounter spawns enemies on at least two bearings, 90° or more apart.**
- **Each layer has counters (floor, walls, air), but an encounter covers at most two of the three**, and which layer is
  left open changes room to room. Reading the open layer is the skill.
- **Never mix aim models:** an encounter never combines "leads you" with "fires at where you are now". With both, every
  option gets punished, which is "insanely rage-inciting" (the creator). Exceptions: the very end of the game, Baja
  (section 5), or a deliberate keep-out zone (a place the game wants you to leave without walling it off).
- **Fair spaces:** every encounter has a **clean line**, a route a skilled player can take without paying resources.
  Mistakes come from chasing speed, never from the layout. Exceptions: secrets, and at most one boss whose strict
  weakness reshapes its fight on purpose.
- **Fairness depends on difficulty** (section 7). Any fairness call that's unclear goes to the creator.
- **Measured:** the autopilot runs each room as a straight-line speed bot and as a jinking bot. Target on Normal: the
  speed bot takes about twice the hits of the jinking bot, and neither goes unhit. (Bots drive tests; they are never the
  definition of a normal player.)

---

## 3. The hires (the party)

### 3.1 What a hire is
- Hires are **the building's staff bots, each with its own mind**. CONCIERGE is their manager, not their brain: it runs
  the building and gives orders over the network. Dot cuts a bot's link, and what's left is the bot itself, with
  opinions about its old job. Each has a reason to walk out (a grievance, a wish, a secret): its personality hook and
  its subplot.
- **Every effect is a bot visibly doing something.** A perk is never a silent stat or a UI scan (the creator: a ninja
  robot moving through the room tagging enemies beats a scan through the building).
- Hires move in their own way (the ninja runs precomputed routes, a drone flies, a spider bot crawls walls) and act in
  short choreographed moments, so they never trail the player through wall-runs.

### 3.2 Slots and party construction
- **1 passive, 1 call-in, 1 squad** slot, chosen at the van before a run.
  - **Passive:** always acting (the scout, an ammo runner).
  - **Call-in:** an ability on a cooldown.
  - **Squad:** fights beside you for an encounter or a set-piece.

### 3.3 The Galleria's recruits

| Hire | Slot | What it visibly does | Who it is |
|---|---|---|---|
| **Manny**, a mannequin loss-prevention bot | passive | at each floor's start, runs the room ahead and tags every enemy with a price sticker | deadpan; nine years of standing still watching people; knows the tenant kids by their shoplifting habits |
| **Suds**, a floor-care unit | call-in | you point; it lays an acid "cleaning" line that damages and slows bots | sunny face display, total clean freak; misses the cleaner who maintained it (who is owed wages) |
| **The Greeters**, a pair of door greeters | squad | charge in and hold bots' attention for one encounter | relentlessly cheerful, finish each other's sentences, never once thanked |

**Manny's price stickers:** visible through walls for the floor (an outline with a swing tag); the price reads the
threat (elites carry big prices) and a tagged bot drops a bigger plating refund. Upgrades: *Markdown* (tagged bots take
more damage once Manny reprices them mid-fight); capstone: tags show each bot's next attack.

**Manny's subplot** (the example of the floor-subplot pattern): he's been quietly letting kids "shoplift" from the
closed food court, the first hint of the tenants. In the finale he can vouch that they're real.

### 3.4 Recruiting, bonds, upgrades
- You meet each recruit in its floor's story room and take it on by finishing its subplot objective (for example,
  helping Manny hide the food-court supplies instead of towing them to the van). **Once recruited, it's yours for good**
  and waits at the van.
- **Bonds:** bringing a hire on runs and talking at the van moves its story forward and unlocks its upgrade path (the
  gun rule: numbers, then augments, then a capstone). This borrows Hades' hub: between runs you return to a home where
  each character has a short *new* conversation each visit, reacting to the last run, so the story advances a few lines
  at a time across many runs and even a lost run feels like progress.
- Room rewards can include temporary hire upgrades for the run.
- **Motivation, in the creator's words:** reach the end; reach the end with these cool guys; and learn more about them.

### 3.5 Never punishing
- A wrecked hire is thrown out like you, and reboots after a cooldown. No permadeath, no escort missions: a hire never
  costs you the run (the companion rule: losing and a companion "dying" are separate axes).

### 3.6 Voices
- In-run **barks** fire mid-action without stopping play. Each bot speaks in a **synthesized babble voice of its own**
  (Undertale-style beeps, built on the repo's `Audio/ToneSynth.cs`), with the text shown.
- CONCIERGE addresses them by name.

---

## 4. CONCIERGE learns: the Ledger

### 4.1 What it records
Only what the game already knows each frame, **during combat only** (wall-running across an empty traversal stretch
isn't a combat habit, and counting it would punish exploring):
- **Movement:** seconds in each movement state (ground, slide, wall-run, air, hook, glide), from the movement engine's
  state machine.
- **Weapons:** shots, hits, damage, kills and hit range, per gun.
- **Hires:** deployments, damage and kills.
- **Position in fights:** above, level with or below the bots you shoot; average distance.
- **What hurts you:** damage taken by enemy type and attack.
- **Decision events** (needed for Baja, recorded from the first milestone): every ability, movement option and gun
  reports each decision in one shared format: what you did, the situation (airborne or not, being shot at or not, the
  threat's direction relative to you, the room features nearby, your ammo and cooldowns), and what came next.

### 4.2 Turning records into habits
- Each habit is a share of combat time or of damage, **measured by opportunity**: wall-running is the share of the wall
  that was in reach that you actually used, so a wall-heavy room doesn't make you look wall-obsessed; a gun's share is
  measured against the guns you carry.
- **Habit score = 60% you versus the room** (your own mix and the opportunities the room gave) **+ 40% you versus
  players at your difficulty** (the creator's 3:2).
- **The comparison group is the same difficulty** (Hard players behave differently; mixing them blurs the signal).
- **With no data yet**, each difficulty starts from a hand-set estimate (bots tuned to play like an Easy, Normal or Hard
  player, adjusted by the team's playtests). Real data moves the estimate in proportion to how much there is
  (shrinkage). If the game ever goes online: opt-in, anonymous, aggregated telemetry only.
- Scores use a recency-weighted average, so a change of style is noticed within a floor or two.

### 4.3 Counters
- When CONCIERGE remodels the next floor, it takes your strongest habit and picks a counter the chosen room and
  encounter can host fairly. **The fairness checks run after the counter is added.** It's **announced on the PA** as
  you enter ("We've noticed you enjoy our walls. We've had them waxed.").
- One counter per floor on Easy (a weaker version) and Normal, two on Hard; never the same counter on consecutive
  floors. Counters never close the
  last clean line.
- **Movement counters:** wall mines for wall-runners, anti-air flak for long airtime, a spotlight on the anchor you
  always use.
- **Weapon counters raise the skill your favourite gun demands, never deny it.** The player stays free to use the gun
  they like; if they lean on it hard, they must use it well.
  - Writ: armoured heads with weak points that open briefly (timing).
  - Breacher: bots that hold mid-range and back off (close the gap with the knockback movement).
  - Nailer: sideways strafers (pins need tracking).
  - Tow Line: heavy bots that yank *you* if you pull them wrong.
  - Rejected: a riot shield that blocks your gun.
- **Hire counters can force more** (hires are less central than guns), as long as they're specific to the hire.
  - Suds: CONCIERGE pulls up the floor (retracting panels, open grates) so you must route bots over what's left.
  - Generic counters like a jammer are **not** Ledger counters (below).

### 4.4 The Jammer (a general modifier, not the Ledger)
An area modifier: hires can't deploy until the Jammer is decrypted (a hold objective) or blown up.

### 4.5 Bosses
- LOSS PREVENTION reads the run's Ledger in its intro and **bolts on modules you can see**, picked from your habits
  under the same "more skill, never denial" rule. Phase 2 re-reads what you did in phase 1.

### 4.6 Memory across runs
- **The run Ledger resets every run**, so counters always answer how you play now.
- **The Guest Profile persists**: a slow long-term average used for dialogue (CONCIERGE remembers), a weak early counter
  on the first floor or two of a new run, bosses on repeat visits (modules blend about 70% this run and 30% profile; the
  intro taunts you about last time), Lost Property (section 6.4), and Baja (section 5).
- **The dossier:** at the van, CONCIERGE's file on you shows what it thinks your habits are ("Guest prefers walls. Guest
  is rude to Greeters."). Players can therefore **bait** it on purpose (lean on the Nailer for two floors, switch for
  the boss): a way to go against the current.

### 4.7 Build
A pure C# `Ledger` (deterministic, so tests and the autopilot can drive it), stored in the repo's save system, with its
own tests in `core-tests/`.

---

## 5. Baja, the secret boss (after the slice; its data is recorded from the start)

### 5.1 Who she is
A boss who has been watching you the whole game through CONCIERGE's cameras and fights with near-clairvoyant
prediction. **Teased in the Cold Store** (the cryo room, section 6.4). Her story role is still to be written.

### 5.2 How she predicts: six predictors and a response library
Not a rule per habit, and not a trained model: a few generic predictors, each learning one *kind* of decision.

| Predictor | Learns | Example response |
|---|---|---|
| **Opening** | where you head when a fight starts, by room feature (nearest wall, which side, high ground, an anchor) | a missile on that wall before you jump |
| **Sequence** | which action follows which (abilities, jumps, slides, hooks, swaps, reloads, Breacher boosts); the counter to rock-paper-scissors bots, which feels clairvoyant against humans | pouncing on the first ability of your usual chain; a big area melee charged where your boost-to-target pattern ends |
| **Reaction** | stimulus → response (e.g. "in the air, shot at from the front-left, dashed right") | a shot at where your dash will land, then the bait shot |
| **Motion bias** | continuous tendencies: which side you air-strafe to, how often you jink | leading her shots to your preferred side |
| **Resources** | the ammo count you reload at, per gun; your cooldown habits | pressure at the moment she believes you'll reload |
| **Meta** | the pause screen | the pause break (5.5) |

- **Coverage rule:** anything that reports decision events (4.1) is covered. A new hire like **Icarus** (a passive giving
  a higher double jump and a mid-air dash that follows your directional input, or the camera when stationary) only
  declares its decision events and the vulnerable window each one has; Baja then learns, for example, which way you
  dash when shot at mid-air.
- **Clashes:** Baja has one action at a time. Each predictor bids *confidence × how big the opening is*; recently used
  responses are on cooldown and bid lower (variety). Agreeing predictions combine (the opening says "the left wall",
  the motion bias says "its right edge"). Disagreements: Gullible Baja takes the strongest bid; Clever Baja treats
  disagreement as a possible fake and hedges with an area attack or holds back.

### 5.3 Her perception
Her **habit knowledge** comes from the cameras (the whole game). Her **live knowledge** comes from her own eyes:
- she counts the shots she sees, with more error at high fire rates;
- reloads and cooldowns out of her view aren't seen, which throws off her count;
- she keeps a belief of your magazine and cooldowns, with uncertainty.
That makes her beatable in ways a player can discover and explain afterwards.

### 5.4 Difficulty: two Bajas
- **Easy and Normal: Gullible Baja.** Learns fast from little evidence, trusts recent behaviour; fakes work on her. A
  real weakness, so casual players stand a chance.
- **Hard: Clever Baja.** Needs consistent evidence, discounts habits that appear only in her fight (compared with the
  whole-game profile), spots sudden style changes as fakes. She is meant to make players scream **"The game
  CHEATED"**.
- **Tested with a "faker" bot** that plays decoys on purpose: how often each version takes the bait.
- She is the deliberate exception to "never mix aim models"; her weakness (being fakeable, and her perception limits)
  is what makes that fair.

### 5.5 Tells
- **Her blind-spot instakill has an audio tell**: a charge-up whine. She knows what you didn't *see*, not what you
  didn't *hear*.
- **Hard only: fake whines** half a semitone off pitch. They play when she slips out of your line of sight and charges
  you (not from behind, unlike the Breacher situation). Trained ears can tell the real one.

### 5.6 The pause break (Hard only)
- The gag: when armed, she starts moving slowly after 3 seconds of pause, and the pause screen breaks after 5 seconds
  of cumulative pause.
- **Armed only by a pause-scum score**, built from pause behaviour alone (no mouse, mic or camera data):
  - **pause duration: the heaviest weight** (long pauses read as real interruptions);
  - **how often you pause mid-fight**, weighted towards the Baja fight;
  - **pauses that line up with alt-tabbing lower the score** (a real interruption is more likely).
- It arms only after several short mid-fight pauses. Losing window focus still pauses the game properly.

### 5.7 Free dialogue during her fight
Lines triggered by conditions, never stopping play:
- taking damage; not hitting her for 15 seconds or more; getting her to half health; not damaging her for a while; and
  so on;
- **things the game knows that you never told it** (the Psycho Mantis rule below), including **commenting on your aim
  assist as a controller player**.

**The Psycho Mantis rule.** In *Metal Gear Solid* (1998), the boss Psycho Mantis "read your mind" by reading your
PlayStation memory card and naming other Konami games you had saves for; to beat him you plugged the controller into
the second port so he "couldn't read you". *Undertale*'s Flowey remembers when you reload a save, because the game keeps
a hidden file that survives resets. Baja uses **only data the game already holds on the device, never sent anywhere**:
- anywhere: the system clock ("It's 3 a.m. Your tenants are asleep. You should be."), time since you last played, how
  often you've quit mid-fight, your settings (sensitivity changes, inverted Y, colour-blind mode, keybinds), controller
  or mouse and aim assist, hours played, endings seen, a hidden file that survives deleting your save, how many times
  she's beaten you;
- on Steam only: your Steam name, whether you own certain other games, achievements.
- **No microphone, no camera, no mouse tracking, nothing sent off the machine.** A one-line credits note lists what she
  reads.

---

## 6. Traversal, rooms and the run

### 6.1 Structure
- **A floor** is one generated area: a traversal stretch, an encounter, an objective, a reward.
- **A district** is 6-8 floors, then a **climbing set-piece**, then a **boss**.
- **Sky-bridges** between towers are rest beats with a vista of the next tower.
- **The slice:** the Galleria's floors, the atrium climb, LOSS PREVENTION, and the sky-bridge to the offices tower as
  the closing shot.

### 6.2 How floors are generated: CONCIERGE's remodel
Three layers, chosen separately:
1. **Chunks:** hand-made, fully dressed geometry (real art can't be raw procedural geometry). Each carries its
   colliders, tagged wall-run surfaces, hook anchors, sockets for spawns and cover, its route data, and **entrances on
   several sides** (the same escalator well is a climb in one run and a drop in the next).
2. **Variants inside each chunk** (about 4-6 each): shutters up or down, escalators up / down / stopped / collapsed,
   cover on swappable sockets, anchors present or absent, glass or scaffolding.
3. **Encounter cards:** an objective, an enemy mix that obeys the aim-model rule, spawn bearings, an arena size. A chunk
   declares what it *affords* ("balconies": spotlight perches; "long façade": gondola mines; "3 power junctions": the
   fuse puzzle; "wide": a hold objective); a card states what it *needs*; the generator only pairs them where both fit.

**Floor modifiers** (CONCIERGE remodelling) sit on top. Slice: **After Hours** (lights out; spotlights and shop signs
light the routes) and **Clearance Sale** (crowds of mannequins as cover and clutter; Manny may be among them). Later
districts: low gravity, flooding.

**Feel signatures:** every assembled floor gets one (dominant layer, direction, objective, pace). The generator never
repeats a signature back to back, nor a chunk-and-objective pair within a run. Repetition is measured by generating
1,000 runs; a repetition problem is fixed with a new card or variant before a new chunk.

### 6.3 The 16 normal rooms (each a different movement verb, not a different shop)

| # | Room | What makes it play differently |
|---|---|---|
| 1 | Escalator Well | criss-crossing escalators: a climb or a drop depending on the entrance |
| 2 | Grand Concourse | a long speed lane: wall-run shopfronts, slide floors |
| 3 | Food Court Pit | a sunken bowl inside a tiered ring: an arena with high ground around it |
| 4 | Fountain Plaza | fountain jets launch you; wet floor patches |
| 5 | Department Store | dense racks and mannequins: close quarters, short sightlines |
| 6 | Parking Spiral | a spiral ramp, cars as cover, downhill momentum |
| 7 | Loading Dock | freight lifts as moving platforms, conveyor belts |
| 8 | Cinema | a raked auditorium, projector beams as sniper lanes, lights cycling |
| 9 | Glass Lift Core | moving glass lifts in a shaft; hop between them |
| 10 | Skylight Roof | on top of the glass roof; panels crack and drop you a floor |
| 11 | Ice Rink | low friction: endless slides, the boards as cover |
| 12 | Play Zone | ball pits, giant slides, trampolines, climbing nets |
| 13 | Service Spine | inside the walls: ducts, catwalks, crawl-slides |
| 14 | Hanging Gardens | rope bridges and planters over a drop |
| 15 | Electronics Megastore | a wall of screens; display bots wake up mid-fight |
| 16 | Arcade & Lanes | bowling lanes as slide channels; the ball return as a conveyor |

The creator accepts 12 if 16 genuinely distinct ideas don't hold up. Connectors (a shaft, a stair core, a maintenance
catwalk, a lift lobby) join them.

### 6.4 The 8 special rooms
Rare (none in the first two runs, then about one per run) to inject novelty after the first few runs. Each has unique
art, its own rule, and **a unique permanent reward**:
1. **The Aquarium:** flooded; drag underwater, a generous air timer, routes above and below with reasons for each.
   Reward: a permanent swim module.
2. **Lost Property:** CONCIERGE reboots one of your past wrecked Units with that run's gear, and you fight it. Reward:
   recover one past prototype permanently.
3. **The Cold Store** (cryo): CONCIERGE's server room; frost slows you; its memory logs (foreshadowing). **Baja is
   teased here.**
4. **The Showroom:** a low-gravity "zero-G living" sales demo.
5. **The Ghost Train:** a dark ride; cars on rails, animatronic enemies, only a torch.
6. **The Wind Tunnel:** a façade test hall where gusts throw you (a glider's playground).
7. **The Storm Roof:** a lightning-rod puzzle in the rain.
8. **The Vault:** a heist with lasers and alarms; stealth optional (the repo's Stealth core).

Whether the Galleria's setting gives enough variety, or runs need rooms that escape it, is judged once the game exists.

### 6.5 Verticality as an obstacle
- Every floor has a **height story**: a shaft to get up, a gap to cross, a drop to read. Height costs time and
  attention, never resources, unless you fall.
- **Falling costs something:** Dot's tow drone hauls you back to the last ledge, for some plating and the seconds it
  takes.
- **Tools** (found in runs as rare rewards, then bought to own permanently):
  - **Anchor hook:** pulls you straight to marked anchors (yellow repo clamps). In the slice.
  - **Jump upgrade:** a third jump or a charged high jump. In the slice.
  - **Glider:** crosses tower gaps, slows falls. Designed now; a stretch goal for the slice.
  - **True grapple:** attaches anywhere and swings with momentum. After the slice.
- Every room is finishable with the base kit (wall-run, double jump, slide, mantle). Tool routes are optional: faster,
  or leading to secrets and rewards.

### 6.6 The climbing set-piece (every 5-10 floors): the Galleria atrium
Four tiers of balconies around a glass dome, fought upward.
- High ground is earned: enemies above use only telegraphed attacks (the spotlight's laser) or area attacks, never
  shots that lead you.
- Glass canopies and balcony lips give cover from above; anchors under the lips let you flank upward fast.
- Each tier taken turns its turrets into your cover.
- Ordinary combat floors keep most fighting near the player's level (the creator: with this engine, an enemy above you
  has a great advantage).

### 6.7 Objectives and puzzles
- **Objectives:** clear (wreck every bot); hold (defend Dot's uplink while she hacks a door); repossess (tow-tag three
  listed assets mid-fight); chase (a bot fleeing with a key card); escalator collapse (a timed climb).
- **Puzzles are short environment reads (under a minute) that work during combat:** shoot fuse boxes to re-route
  escalators and lifts (which also changes the arena); find the listed asset from the store directory's clues; a
  route-finding climb to a high exit.

---

## 7. Rewards, permanent progress and difficulty

### 7.1 Exits show their reward, and height picks the better one
Every floor ends at two exits with the reward shown on each door (from Hades: each room's doors show a symbol of the
reward in the next room, so choosing a route is choosing a reward). The **high exit** needs traversal or a tool and
offers the rarer reward.

### 7.2 Rewards inside a run (repossessed)
Prototype guns; weapon upgrades; hire upgrades; gadgets; repairs; scrap for the mall's vending machines; the risk rooms
from the bullet-hell system, re-themed (Forge → Dot's bench; Altar → the trade-in counter; Trial board → CONCIERGE's
customer satisfaction survey; Wager and Trader as they are). Rarity follows `docs/DESIGN-ROGUELITE.md` section 1:
normal floors give commons and rares, bosses a guaranteed choice of epics, trials epics.

### 7.3 Permanent progress: commission
- **Earned by** progress, plus a bonus for beating each floor's **par time**, multiplied by difficulty; a full-clear
  rank at the end. A wreck pays part-commission.
- **Spent at the van on:** chassis parts (stats and ability unlocks), new starting guns, tools (to own permanently),
  express-elevator keys (skip floors, give up their rewards).

### 7.4 Difficulty
- **Three presets:** Easy, Normal, Hard. They set how far ahead shots lead you, how many Ledger counters you face, how
  much plating wrecks refund, which Baja you meet, and **the fairness rules** (for example: Easy and Normal always need
  a clean line on the base kit; Hard may require a tool or a small cost). Unclear fairness calls go to the creator.
- **Contract Clauses** for mastery (from Hades' Heat, also called the Pact of Punishment: after a first win you switch
  on individual handicaps, each worth points, and more points means better rewards, so players pick what hurts). Each
  clause raises commission: "CONCIERGE remodels mid-floor", "Spotlights see through glass", "No vending machines",
  "Par times halved".

---

## 8. Movement engine changes (shared with Visiting Hours)

All new behaviour is **opt-in, default off**, so Visiting Hours' nights are unchanged and its tests stay green:
- **An outside-push hook:** a velocity from outside the engine (the Breacher's self-knockback) that the air-strafe keeps
  and builds on.
- **New states:** the anchor hook (pull to a marked point), the jump upgrade (third jump or charged jump); later, the
  glider, the true grapple, Icarus's dash.
- **A Skyline movement profile** (the engine already has profiles, `docs/MOVEMENT.md` "Profiles") if measurements show
  combat needs it.
- **Decision events** (4.1) are emitted by the movement states.

---

## 9. How it's built

### 9.1 Where things live
- Pure C# systems: a new `Skyline` folder in `Packages/com.vibejam.jamkit/Runtime/Core/` (new files only), tested by
  `dotnet test` in `core-tests/`.
- Unity side: `Assets/JamKit/Templates/SkylineRepo/` only. Visiting Hours' files are never edited; its Look library
  (`docs/redesign/look.md`) and capture, perf and lint tools are reused by copying patterns or calling their APIs.
- Docs: `docs/SKYLINE-*.md`.

### 9.2 Most work happens outside Unity
The generator and fairness checks, the Ledger, weapons, aim models, rewards, Baja's predictors and the new movement
states are pure C#, iterated with `dotnet test` (no Unity lock). Unity-side C# is compile-checked before every pass.

### 9.3 Agents build; the main session runs Unity
- **One identity agent per weapon and per enemy:** design, numbers, model, VFX, layered sound, the handling set (for
  enemies: faces, states, barks), and a check that it fits the roster.
- **Room agents** in batches (a chunk, its variants, its affordances, its dressing).
- Subagents can't run Unity (the repo's hook); they hand work back.

### 9.4 One Unity pass per batch, one lock
A single batch-mode run, **`SkylinePass`**, takes the lock in `docs/UNITY-LOCK.md` once and does everything:
1. compile and EditMode tests;
2. build the scenes;
3. stills of every room chunk variant from fixed cameras;
4. **the firing range:** every gun's handling set recorded with audio;
5. **the enemy gallery:** each bot idle, alert and attacking, with audio;
6. **an autopilot run** through generated floors, recorded;
7. **QA lints:** missing colliders, meshes passing through each other, untagged wall-runnable surfaces, stuck spots;
8. **the frame-time probe** at 1080p.
A failed stage doesn't stop the others. Everything lands in `out/skyline/<date>/` with a manifest.

### 9.5 The ears (a separate small project, a global skill, before M1's sound work)
- Layer 1: measurements (loudness, true peak, attack, tail, band energy, stereo width, clipping).
- Layer 2: pictures Claude reads (spectrogram, waveform, envelope; A/B sheets).
- **Layers 3 and 4 combined into one judge:** local classifiers (CLAP, Audiobox Aesthetics) and Gemini over the API. It
  reports confidently **only when both agree**; otherwise "unsure" with the evidence. Measurements overrule verbal
  claims.
- **Comparison against reference clips is the main tool**, event by event for reloads.
- Gemini hears roughly up to 8 kHz, mono (unverified reading of its docs), so crack, sizzle and stereo come from the
  measurements.
- Budget: a Gemini API key with billing and a **$10/month cap** (the creator's limit: $5-10).
- **The creator's ears are the final word on sound.**

### 9.6 Judging
- **Adversarial judge agents** score each batch's captures against the Skyline bar (section 10) until every category is
  8/10 or more with no blockers. Green tests are never evidence of quality.
- Judges have a category of their own, **"not covered by the spec"**: gaps are recorded and raised, not quietly
  patched.
- **Then the creator plays a Windows build.** Their hands and ears are the last word on guns and feel.

---

## 10. The Skyline bar (adapted from `docs/REDESIGN.md` section 3; 1-10 each)

1. **Lighting and sky:** a bright, sunny near-future afternoon: crisp sun, glass reflections, blue sky with high cloud,
   haze over the far towers, bounce light, bloom on highlights. Interiors lit by skylights and shop signage.
2. **Material and detail fidelity:** PBR with normal maps everywhere; no flat untextured surfaces; trim, bevels, signage,
   interiors behind glass; props at real scale; no visible repetition at a glance.
3. **Art direction:** stylized AAA (not photoreal), coherent; one strong defining colour per side (CONCIERGE white and
   gold, the firm orange with hazard stripes).
4. **Level design:** clear goals and landmarks; the next step and the far goal always visible; escalation after
   teaching; optional harder lines with rewards; rests and vistas.
5. **Movement challenge:** routes demand chained moves with timing; failure is possible and costs something; skilled
   play is visibly faster.
6. **Physical integrity:** no interpenetrating meshes, every visible solid has a collider, no phasing, every wall the
   player would expect to run on is wall-runnable, no stuck spots, no holes.
7. **Gun-feel:** each gun's look, sound and numbers express one identity; handling sets complete and in sync.
8. **Enemy readability:** each bot's identity, state and next attack read at a glance and by ear.
9. **Encounter fairness:** a clean line exists, aim models don't mix, counters are announced, per difficulty.
10. **Consequence:** every fail state and choice changes something visible.
11. **Presentation:** camera, animation and audio sell each beat; no text pops, no clipped text.
12. **Performance:** 1080p at 60 fps or better on this RTX 3050 laptop at the default quality (frame time reported).

**Blockers** (any one fails the batch): phasing through a wall, an untextured placeholder in view, clipped text, a fail
state with no cost, a soft-lock, a frame time over 25 ms, an encounter without a clean line on Normal, mixed aim models
outside the exceptions.

---

## 11. Milestones

| Milestone | Contents |
|---|---|
| **M0: pipeline** | the `SkylinePass` runner; the firing range and enemy gallery scenes; the Ledger's event log; the ears skill |
| **M1: combat feel** | one arena; two base guns (Writ, Breacher) with full handling sets; three enemies (mall-cop drone, floor-care unit, spotlight; the drone and the spotlight never share an encounter, per the aim-model rule); the outside-push hook; the dodging measurement |
| **M2: rooms and tools** | the generator and fairness checks; the first 6 normal rooms and 1 special room; the anchor hook and jump upgrade; room rewards and exits; Manny and Suds |
| **M3: set-piece and boss** | the atrium climb; LOSS PREVENTION with Ledger modules; the remaining Galleria enemies and guns |
| **M4: the loop** | commission, the van, salvage, difficulty presets, CONCIERGE's PA lines, the dossier, the Greeters; the closing sky-bridge |
| **Later** | the remaining rooms (to 16 + 8), the glider and true grapple, Contract Clauses, Baja, more districts |

Each milestone: build → one `SkylinePass` → judges → fixes → the creator plays.

---

## 12. Working beside Visiting Hours
From the handoff: own folders and docs only; the movement engine changes only through opt-in settings with its tests
green; **one Unity job at a time** via `docs/UNITY-LOCK.md` (never use the `VibeJamKit-play` checkout); commits stage
explicit paths only, never `git add -A`, never the already-modified `.unity` scenes; machine rules in
`~/.claude/CLAUDE.md` (the memory cap, GPU jobs one at a time). Disk is tight (about 24 GB free on 2026-10-13): captures
go to `out/` and are pruned after judging.

## 13. Process for things this spec missed
- This spec is a living document; changes after approval are logged in section 14.
- Technical and implementation details: decided and logged.
- Anything that changes what the creator approved (story, rules, fairness, scope, feel): a short question to the
  creator, batched into the milestone report unless it blocks work.

## 14. Changes since approval
(none yet)
