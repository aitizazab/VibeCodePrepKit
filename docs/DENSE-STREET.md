# Dense street: a denser Larch Street for Elias's Saturday

The creator's note (STORY-PROSE:51): the demo's neighbourhood is barren, and the street is meant to be dense enough for
a stealth section later. If the Saturday truly repeats on one street at golden hour, the street itself has to become
interesting, rather than linking to a second, denser place (which the creator dislikes). The model is the Distillery
District in *Dishonored*: "we don't need anything nearly as dense, but the safe traversal of the terrain was interesting
enough to warrant copying. That space was balanced around intermittent Blinks, but ours should be around our own
movement set." They also liked a stealth intro where Theo plays hide-and-seek with Nadia, like Dishonored's prologue,
with memories to find and a sign of how many are left.

This page covers the space; the story side is in `docs/STORY-TALK.md` section 2. Research and layouts only: nothing in
the game was changed. Written 2026-10-11.

**Labels.** **[Doc]** = a developer (a talk, a dev commentary, an interview) or the game's own code and numbers.
**[Comm]** = a wiki, a guide or a player. **[Op]** = my proposal.

**The maps** (SVG, drawn to scale, in `docs/art/dense-street/`; regenerate with
`uv run python docs/art/dense-street/make_maps.py`):

| Map | What it shows |
|---|---|
| `distillery-schematic.svg` | The Distillery District as three stacked layers, with the ways into each goal (not to scale) |
| `plan-a-deep-block.svg` | **A. The deep block** (recommended): plan and section |
| `plan-b-hill.svg` | **B. The hill**: plan and section |
| `plan-c-close.svg` | **C. The close**: plan and section |
| `plan-d-link.svg` | **D.** The disliked option (a link to a second place), for comparison |

The review page that shows them all is `FableAnimationTests/art/review/2026-10-11/dense-street.html`.

---

## 1. How the Distillery District works as a space

### Sources
- **The Dishonored wiki** (fandom): the mission pages *High Overseer Campbell* and *House of Pleasure*, and the pages for
  the *Distillery District*, *Streets of Dunwall*, *Dunwall Whiskey Distillery* and *Blink* (read 2026-10-11 through the
  wiki's API). [Comm]
- **Arkane's own commentary.** *The Creators Play Dishonored*, transcribed with Whisper (timestamps below are into
  each video). [Doc]
  - Part 1, the prologue: Raphaël Colantonio and Harvey Smith
    ([video](https://www.youtube.com/watch?v=ZVq0af9DwPU)).
  - Part 2, *High Overseer Campbell*, the first visit to the district: with Ricardo Bare, Rich Wilson and Seth Shain
    ([video](https://www.youtube.com/watch?v=YJrU-l0UDb4)).
  - Part 3, *House of Pleasure*, the second visit: with level designer Joackim Daviaud, lead level designer Christophe
    Carrier, level architect Manu Petit and producer Julien Roby ([video](https://www.youtube.com/watch?v=8huHNkW92Q4)).
- **Steve Lee**, a senior level designer on *Dishonored 2*, playing and critiquing the Campbell mission, the
  district's first visit ([video](https://www.youtube.com/watch?v=pVQDWX3lv_w)). Transcribed. [Doc for craft; he
  didn't make this level]
- What I **couldn't** see: no published top-down map of the district beyond the wiki's gallery images, and no GDC talk
  on this level. The layout below is reconstructed from the wiki's route descriptions, so the schematic is topology,
  **not to scale**.

### 1.1 What it is
One district by the river (the Wrenhaven), played in two missions: *High Overseer Campbell* (you cross it to Holger
Square) and *House of Pleasure* (you cross it to the Golden Cat). [Comm: wiki]
- **John Clavering Boulevard** is the main street: City Watch checkpoints and two **Walls of Light**. The offices of
  Dr. Galvani and the art dealer's apartment face each other on it, and it forks to Holger Square and the Golden Cat.
- **Bottle Street** "begins in the alley opposite Granny Rags' apartment, runs through the Dunwall Whiskey Distillery
  yard and then **runs underneath John Clavering Boulevard**". The Bottle Street Gang holds it.
- **Endoria Street** is the waterfront (Granny Rags' apartment, where you land). **Bloodox Way** is a dead end with
  Griff's shop. **Gaff Street** is an alley off Bottle Street's lower half that walks you round the second Wall of
  Light.
- **Contents:** the Distillery (Slackjaw), Granny Rags, Griff, Galvani, the art dealer, the Golden Cat, and the Office
  of the High Overseer beyond. Carrier's summary in Part 3 (56:17): "We have Slackjaw, we have Granny Rags, we have the
  Art Dealer, we have Galvani. It was a thick mission." [Doc]

### 1.2 The routes are stacked on each other
Three layers run along the same corridor (see `distillery-schematic.svg`):
1. **The boulevard**: the watched street, with its walls of light and guards.
2. **Bottle Street's lower half and Gaff Street**: a street *under* the boulevard, then an alley round it.
3. **The roofs and balconies**: "the easiest strategy is to climb to the rooftops of nearby buildings and use Blink to
   travel from one to the other" (wiki, *High Overseer Campbell*) [Comm].

Plus a fourth layer for possession: sewers, rat tunnels and fish in the water. In Part 2 (24:17) the designers call
this "an unsung hero of the game ... a level opening here and they can put the exit anywhere in it" [Doc].

Arkane's word for this is **circulation**: "being able to go out windows ... the circulation of the building ... not
having to go out the door is such a key to the games that we make ... being able to go vertical, being able to go
through an alternate entrance" (Part 1, 1:17:13) [Doc].

### 1.3 How many ways each goal is reached

| Goal | Ways in | Source |
|---|---|---|
| Past the Walls of Light | 3: roofs (Blink), Bottle Street under the boulevard then Gaff Street, or rewire the wall | wiki [Comm] |
| The Office of the High Overseer | **4**: "high above, underground, through the front, or through the back" (high window; sewer into the kennels; the front door past a reception; the back yard) | wiki [Comm] |
| The Golden Cat | **5**: the front door; the roof via the Captain's Chair Hotel, then **left** (sign, gazebo, beams, a second-floor window) or **right** (balcony, a hole in the ceiling, a vent, then a rat tunnel or a ledge to a window); possessing a fish in the pond; the VIP exit out | wiki [Comm]; Part 3 18:00-18:28 [Doc] |
| Finding the Pendletons | 3: eavesdrop at the window over the entrance, the guest ledger (2 copies), the courtesans in the washroom | wiki [Comm] |
| The art dealer's safe | 2: the back door (key from a side quest) or the second-floor balcony | wiki [Comm] |
| The distillery's still | over the yard: barrels, then overhead planks (Blink), a roof, then pipes into the building | wiki [Comm] |

**The information is placed at every entrance.** Daviaud (Part 3, 28:20): "we have overheard the position of all the
twins for any entrance, because we do not want the player looking for the guys but to plan his way." [Doc]

### 1.4 How it rewards Blink and "safe traversal"
- **Blink** moves you up to **10 m** (tier I) or **15 m** (tier II), costs 20 % mana, and refills enough for one more
  ([wiki](https://dishonored.fandom.com/wiki/Blink)) [Comm].
  - The refill was designed: "a mix of a cooldown and still using mana ... the lower part regenerates just enough that
    you can do one more blink if you wait a little bit, so that you're not stuck, but at the same time you can't abuse
    it ... It does slow you down and make you listen to some of the conversations and observe some of the guards"
    (Part 1, 1:01:18) [Doc].
  - That is the creator's "intermittent Blinks": **hop, perch, look, hop**.
- **Blink was made the core late.** It started as an optional purchase; "very late in the project ... what if we always
  had Blink? ... it changed the whole complexion of the game" (Part 1, 1:00:48; Part 2, 09:51) [Doc].
- **Speed was a goal.** Colantonio, quoting Doug Church (*Thief*): "if we had Thief to do over again ... don't reward
  people for going super slow. And so part of blinking and all of that came from not wanting to force the player to move
  at a snail's pace" (Part 2, 56:47) [Doc].
- **What "safe traversal" means here** [Op, my reading of the creator's phrase]: crossing hostile ground by moving
  between **perches**: roofs, balconies, ledges, pipes, the lower street. Each perch is out of reach and out of sight,
  and each hop is short.
  - **The perch height.** Steve Lee: "this is the kind of height where you can look down on people and they feel close
    to you ... but you're also high enough that they can't see you ... I'm guessing that's about five meters ... I would
    always be looking for opportunities to create rooms where you have ... five or six meters above the floor to look
    down on enemies from" (13:28-14:03) [Doc].
  - **A vantage at the start.** Lee calls the climb at the start "the perfectly framed, high-perspective tactical
    overview of the whole area" (04:55) [Doc].
- **Players had to be told they could climb.** Carrier (Part 3, 05:29-07:11): at first climbing was purely systemic,
  with no icons; "during the playtest we realized that nobody was climbing ... we planned that there was all these paths
  along the balconies, on the roof ... and nobody was going there". Players said "because there's no icon", so they added
  one [Doc].
  - Colantonio is "not a yellow paint guy", but he grants that "there are some games where the point is not to go slow
    and pick your way around, so I get it" (Part 1, 1:07:43) [Doc].
  - Our game is one of those. That supports the dream escape's single gold "go" colour.
- **Verticality should still feel organic.** "You can't really feel the blocks, like 'oh, this height over there is
  exactly that'" (Part 2, 20:27) [Doc].

### 1.5 Sight lines
- **Vision.** Guards see by line of sight and facing; a light gem was dropped because soft shadows were ambiguous
  ("hiding in shadows ... never worked", Part 1, 22:49) [Doc].
- **Squashed cones.** View cones were squashed vertically, because being seen up in the rafters felt unfair. In a
  prototype a guard's ambient neck-crack spotted the testers on a wall, since the cone was "slave to the look
  direction" (Part 2, 41:37) [Doc].
- **What a revisit adds.** On the second visit the district adds a watchtower by the river, patrols on balconies, and
  Daud's assassins on the roofs; the roofs become contested too (wiki) [Comm].
- **Barks** ("the whistling, the throat clearing") tell the player where the dangers are (Part 3, 36:17) [Doc].
- **Alarms are contained.** The Golden Cat's floors were stacked, so a mess on one floor pulled in the whole building.
  They added audio "airlocks" so that a failure "does not break the entire Golden Cat" (Part 3, 41:42) [Doc]. Lee:
  "you generally want the player to be able to recover from little mistakes" (11:22) [Doc].

### 1.6 Where side content sits
- **On the alternative routes, early.** Granny Rags sits right at the start, down a side alley. Lee: it "makes it very
  clear that there's quite juicy stuff to find in the optional side paths right at the start of the level" (02:43)
  [Doc]. Griff is at the dead end of Bloodox Way; Galvani and the art dealer are off the boulevard (wiki) [Comm].
- **Side content pays out in routes.**
  - Slackjaw's errand to Galvani's gives the key to the Captain's Chair Hotel, the rooftop way into the Golden Cat.
  - Saving a woman on Gaff Street gives a back-door key to the art dealer's.
  - Slackjaw's second errand is the non-lethal way to finish the mission (wiki) [Comm].
  - The designers kept keys as hard gates: no lockpicks, "which really helped us gating" (Part 3, 22:18) [Doc].
- **Events are sparse; the space is dense.** Lee: the level "is quite spacious and kind of not too dense with events
  relative to the size of the layout ... however the player chooses to play, you don't want different scenes kind of
  clashing" (08:34) [Doc].
- **Nobody sees it all.** "In one playthrough there's no way a player can see a hundred percent of the content"; players
  compared notes about Granny Rags (Part 2, 1:04:28) [Doc].

### 1.7 The same place, changed on a revisit
This is our "repeating Saturday" problem, solved in miniature.
- **What changes between the two visits** (wiki) [Comm]:
  - a Wall of Light becomes a barricade, and a watchtower is installed;
  - the docks' gate is shut, and Granny's door is locked;
  - weepers prowl the lower street, and assassins walk the roofs;
  - the gang turns neutral;
  - the distillery's cell lock is changed, and the traps are removed.
- **The designers' reason.** Carrier (Part 3, 14:41-16:16): "the context changed ... the Wall of Light is not there
  anymore, it's been barricaded ... to make the world evolve when we're back ... it was part of the challenge to have
  some variation. Even if you know the place, you are still surprised and there are still new challenges. That's why we
  block some paths, we introduce some new mechanisms like the watchtower or the Wall of Light" [Doc].
- **Within a visit, positions vary.** The twins have "two position pairs", picked at random; the madame "is on a
  schedule" (Part 3, 33:12) [Doc].
- **A third visit was planned**, to build up Granny Rags, Slackjaw and the gangs, and was cut (Part 3, 56:46; wiki)
  [Doc].
- **Golden hour was chosen on purpose.** "We assumed every mission would be at night ... a goal of our direction to
  say no, let's use golden hour, let's use daylight" (Part 2, 03:34). Later a "redeemability pass" changed the lighting
  because playtesters found the world too dark to care about (Part 2, 1:00:01) [Doc]. A golden-hour stealth space is
  proven.

### 1.8 What we take, and what we don't

| Distillery feature | Take? | How it becomes ours |
|---|---|---|
| Three stacked layers along one corridor (watched street, a street under it, roofs over it) | **Yes** | The street, the back gardens and the lane (or the creek bed), the roofs. Cross-links every 20-25 m |
| 3-5 ways into every goal | **Yes** | Each goal (Walt's shop, house 3's back door, the hill gate, each memory cluster) gets one way per layer |
| Hop, perch, look, hop (intermittent Blink) | **Yes, rebuilt** | Our moves are unlimited, so the rhythm comes from **perches spaced one move apart**: ≤ 6 m single jump, ≤ 10 m double, ≤ 15 m wallrun. Perches are unseen and see the street |
| The ~5 m stalking height | **Yes** | House eaves at 5.2 m, garage roofs 2.7, the treehouse 3, the scaffold 2/4/6 |
| Side content on side paths, paying out in routes | **Yes** | Memories and favours sit off the street. A favour opens a gate or lowers the scaffold ladder (a "switch", below) |
| Information at every entrance | **Yes** | Each entrance to the hide-and-seek garden has its own way to learn where Theo is (he counts aloud; a window; a reflection) |
| Revisits changed by blocking paths and adding watchers | **Yes, as the heart of the repeat** | Per-night switches (gates, ladders, sheets), the watchers' positions, the static. No new geometry |
| Icons or paint for climbables | **Yes (already)** | The dream's single gold "go" colour, used sparingly so verticality stays organic |
| Squashed vertical vision; contained alarms | **Yes** | Theo and the watchers don't look up onto roofs (vertical cone ±15°); a mistake brings only nearby seekers |
| Light and shadow as stealth (Thief's light gem) | **No** (Arkane dropped it) | But golden hour gives **fixed, hard shadows**: the sun never moves on a repeating day. Use shadow as a modifier only, if the stealth kit keeps one |
| Combat, gangs, a desaturated grim palette | **No** | Ours is a pastel dream with no killing. Arkane's warning still applies: "for a stealth game information clarity is key", and they kept colour for what matters (Part 1, 07:16) |
| Possession tunnels, fish, rats | **No** | Our equivalents are the crawl spaces (decks, the culvert) and the slide gaps |
| A whole district | **No** | One block, about 120 x 110 m |

---

## 2. Our kit of parts: what each size is for

From `docs/MOVEMENT.md` and `MoveDef` (`Packages/com.vibejam.jamkit/Runtime/Core/Movement/MoveDef.cs`), checked against
`CourseReach` [Doc: our code]. Heights are above the ground you start from.

| Element | Size | The move it asks for | Why |
|---|---|---|---|
| Picket fence (front gardens) | **0.85 m** | a hop (jump 1.25 m; hops need ≤ 1.0) | The street layer stays fast |
| Low wall, hedge, pallets | **1.0-1.2 m** | a hop or a vault; a 1.2 m hedge hides a crouched Nadia (capsule 1.0 m) | Crouch cover |
| Garden fence | **1.8 m** | a **mantle from the ground** (0.4-1.9 m) | Every fence is passable but costs a beat; you can't hop it at speed |
| Shed, lean-to | **2.2-2.4 m** | jump + mantle | Steps up |
| Garage roof (flat) | **2.7 m** | jump + mantle (reach ≈ 3.15 m = jump 1.25 + mantle 1.9) | The first perch; also the lane's wall-kick walls |
| Porch roof | **2.9 m** | jump + mantle | Street to roof |
| House eaves | **5.2 m** | from a garage or porch roof: jump + mantle (5.2 - 2.7 = 2.5 ≤ 3.15) | Lee's ~5 m stalking height |
| Pitched roof | ridge **8 m**, 35° | walkable (slope limit 50°); slide down for speed | The back pitch is hidden from the street |
| Side passage between houses | **4.5 m** | wall to wall (chain 4-6 m); house side walls are 12 m (one wallrun 8-15 m) | Street to back, fast |
| Narrow passage | **3.5 m** | the chimney (~1.2 m up per kick) | Climb without a ladder |
| Gap between roofs | **4.5 m** at the eaves | a single jump (6.1 m at sprint) with ≥ 4 m of run-up | Roof to roof |
| Long gap | **8-9 m** | a double jump (9.9 m at sprint) | The empty lot, the creek's wide reach |
| The creek | **5.5 m** wide, 2 m deep | a single jump (Nadia's bike jump); a miss lands in the creek bed, the low route | A miss is a route, not a death |
| Slide gaps | **1.15 m** clear | a slide (capsule 1.0 + skin + margin) | Walt's half-open door, the culvert, under decks |
| Runnable walls | **≥ 3 m tall**, 8-15 m long | a wallrun (needs 0.6 m of air and a chest-high wall) | Fences are too low to wallrun; use houses, garages, the lane wall, the extension |
| Run-up | **6-10 m** before a lateral move | (MOVEMENT "Sizing a level") | Roofs and the lane give it |

**Speeds the block is sized for.** Walk 5, sprint 9.5, a slide boost to 13, cap 22 m/s. A 120 m street takes about
13 s at sprint, so the block must reward *not* sprinting down the street: everything worth having is off it. [Op]

**Stealth numbers.** The stealth kit another agent is building now (`Packages/com.vibejam.jamkit/Runtime/Core/Stealth/`,
uncommitted, values may change):
- **Sight:** range 18 m, cone ±40°, peripheral ±75°.
- **Noise radii:** sprinting 7 m, a wallrun 9 m, a slide 5 m, a hard landing 12 m.
- **Seeker:** counts for 10 s.
- **Its test yard** is a fenced 48 x 36 m garden, the same scale as the hide-and-seek gardens below. [Doc: draft code]

What these mean for the layout [Op]:
- A seeker on the trampoline can't see the whole 42 x 29 m zone at once.
- A wallrun is heard from one garden away.
- The roofs are quiet only if you walk them.

---

## 3. What the story fixes, and how a repeating day still changes

**Fixed** (STORY-BIBLE, NIGHTS-GAMEPLAY, DREAM-ESCAPE):
- Larch Street at golden hour, about 120 m long.
- Walt's shop at the bottom, five houses, the creek, and the chained gate of the hill house at the top.
- Mrs. Lindqvist at her roses, Mr. Okafor at the grill, Biscuit the dog, a barbecue.
- Elias at the party, "always just too far away to reach".
- The escape runs along it.

STORY-TALK 2A ("one street, remembered dense") needs no story change: a child's memory of a neighbourhood is bigger and
more tangled than the real one.

**The day is the same; the dream's response to Nadia isn't.** Nothing about Saturday moves, but the dream notices her
more each night. So the place changes the way the Distillery did between visits, with no new geometry [Op]:

| Lever | Example | Cost |
|---|---|---|
| **Switches**: things that are open or shut tonight | A garden gate, a garage door between garden and lane, house 3's back door, the scaffold ladder up or down, a plank over the creek, the lane's top gate, the culvert grate, Walt's door half open or shut | Each is one prop with two states |
| **Watchers**: who stands where, looking where | Lindqvist at the front roses or the back; Okafor at the grill; Elias at the party; the dog; from Night 2 the neighbours who know her name, turning to watch (as the cut-outs already do in the escape) | Positions and facing per night |
| **Cover that moves** | Sheets on the washing lines (they block sight); grill smoke; a sprinkler's spray and hiss (noise cover); the party's marquee and bunting | Props with a schedule |
| **The static** | A patch of the dream's static closes one layer tonight (the lane, or the creek bed), forcing the others | A volume per night |
| **The schedule within the day** | One loop of Saturday events, Majora's Mask style (my comparison): sprinklers on every 90 s, the ice-cream van's chime at minute 3, Okafor flipping burgers. The Distillery's madame was "on a schedule" too (Part 3) | Timed events, same every night |
| **Where things are** | Theo's hiding or seeking spots and the memory slots shuffle among fixed sockets (the Pendletons' "two position pairs") | Data |

Deathloop (also Arkane) is the modern precedent: four districts that "will be different depending on the time of day you
visit, and some locations may be open or closed off" ([PlayStation Blog](https://blog.playstation.com/2021/05/19/deathloop-combat-exploration-and-ps5-features-detailed/),
[GameWatcher](https://www.gamewatcher.com/deathloop-structure-freeform)) [Comm]. Ours is the opposite axis: one time of
day, a different *night*.

**The sun never moves** [Op]. At golden hour it hangs low at the bottom of the street (west, behind Walt's shop), so
every building throws a long shadow uphill. At 6° of sun height an 8 m house throws a shadow about 75 m long. On a
repeating day those shadows are fixed level design: the east sides of houses and fences are always dark, and the west
faces of the roofs always glow. Use this for readability (gold surfaces lit) and, if the stealth kit keeps its shadow
modifier, for cover.

---

## 4. Proposal A: the deep block (recommended)
`plan-a-deep-block.svg`. About 124 x 110 m.

**Idea.** Keep the 120 m street exactly where it is, and build the block *backwards and upwards*:

| Layer | What it is | Who sees it |
|---|---|---|
| **Front: the street** | Pavements, porches, front lawns, the block party on the road, everyone at the party | Watched by everyone |
| **Back: the gardens and the lane** | The five houses' back gardens (fences, sheds, decks, a trampoline, a treehouse, washing lines), a 5 m rear lane lined with garages, and the half-built extension on house 5 | Seen only from neighbouring gardens and back windows |
| **Above: the roofs** | Porch roofs (2.9) → eaves (5.2) → the back pitches; garage roofs (2.7) along the lane; the extension's scaffold (2 / 4 / 6) | The back pitch is hidden from the street by the ridge |
| **Under: the creek and the culvert** | Behind the south houses the creek runs in a 2 m gully. It turns under the street through a 1.15 m culvert (slide) into the empty lot by the hill path. This is Bottle Street under the boulevard | Nobody watches it; it's slow and wet |

**Cross-links** (switch layers every 20-25 m):
- side passages A and B between the north houses (4.5 m: wall to wall);
- the snicket between houses 2 and 4 (4 m);
- the lane's two ends;
- porches (street to roof) and garages (lane to roof);
- the culvert (creek to street to empty lot).

**Main routes**, from Walt's shop to the hill gate:

| Route | Line | Moves | Length, rough time | Exposure |
|---|---|---|---|---|
| Street | the pavement, through the party, up the hill path | sprint, hop the sawhorses | ~120 m, ~13 s | everyone |
| Back | side passage A, the gardens of house 1 (two 1.8 m fences), out through the garage, up the lane, the lane's top gate | mantle, slide, sprint the lane (it slopes: slide back down it fast) | ~140 m, ~20-25 s | gardens only |
| Roof | house 1's porch → eaves → along the back pitches, jumping passages A and B (4.5 m) → down onto the scaffold → **wallrun** the extension's 14 m block wall → double jump to the skip → the hill path | jump + mantle twice, two single jumps, a wallrun, a double jump | ~110 m, ~18 s | the front pitches only |
| Under | Walt's back door → the south gardens → the creek bed → the culvert slide → the pond → the hill path | drop, run, slide, mantle out | ~160 m, ~25 s | none |

**Every goal has 3-4 ways in** (the Office of the High Overseer's four):
- **Walt's shop (the ledger):**
  - slide under the half-open door from the street;
  - the back door, with the key from house 3's hook (the existing puzzle);
  - the skylight from the shop's flat roof (4.0 m: lean-to 2.4, then a mantle of 1.6);
  - or force the lock.
- **House 3's back door (the key hook):**
  - through side passage A or B from the street;
  - over the garden fences;
  - from the lane through the garage (switch S1);
  - down from the back pitch onto the deck.
- **The hill gate:** the street; the lane's top gate (S6); the roof line; the culvert and pond.
- **Elias at the party:** he stays out of reach, which suits a party you can reach three ways:
  - from the watched street;
  - down off house 5's porch roof onto the marquee roof (2.6 m);
  - up the snicket from behind.

**Hide-and-seek** (dashed teal on the map): the gardens of houses 1 and 3 plus the lane's west half, 42 x 29 m.
- **The base.** Theo counts on the trampoline. *At the top of each bounce he can see over the fences*, which teaches
  timing in one prop.
- **8 hiding places** of different kinds:
  - the crawl space under house 1's deck, the treehouse (3 m), the greenhouse;
  - under house 3's deck, the shed, behind the bins in side passage A;
  - on a garage roof (above his sight);
  - behind a garage in the lane.
- **6 memories in the zone**, one near each kind of place, so collecting means moving while he searches (section 9).

**The later stealth section:** the whole block, from a night when the neighbours know her name (STORY-TALK 2):
- **The street is a no-go.** Lindqvist, Okafor and Elias watch it.
- **The gardens are cover with gaps.** Sheets and smoke open and close the gaps.
- **The roofs are the stalking height.** They're silent if walked, loud if wallrun (9 m).
- **The creek is slow and unseen.**
- **Contained alarms.** Being seen in one garden alerts that garden's watcher only, the Golden Cat's airlock rule.

**Memories** (the slots; the story agent owns what each one is):
- **On the street:** the porch newspaper, Walt's sign, the party, the culvert's mouth. These are the slice's four.
- **In the gardens:** the six hide-and-seek finds.
- **One per other layer** (my suggestions):
  - the lane: a bike left against a garage;
  - the roofs: a kite caught on house 3's ridge, Nadia's;
  - the extension: something Elias was building and never finished;
  - the creek: the bike jump's launch, with a skid in the grass;
  - the hill path: the gate.

**Across nights** (switches S1-S8 on the map; examples, [Op]):

| Night | What's different |
|---|---|
| 1 | Everything open; nobody hunts. The street layer is safe; the gardens and roofs are found by curiosity, and Walt's skylight rewards it |
| 2 | Hide-and-seek (if Theo is on the street that night) |
| 3 | The neighbours turn to watch. The lane's top gate (S6) is shut, the scaffold ladder is down (S4), the sheets are up: the roof and the culvert are the ways through |
| Later | The static closes one layer per night. The back route dies first, then the roof, so the last visit is the street in plain sight |

**Costs and risks:**
- **Art.** About 3x the slice's street: five houses in real 3D with backs and roofs, six garages, sheds and props. But
  they're one kit, built once and reused.
- **Readability.** Many fences need the gold rule kept strict (only usable things gold), or the gardens become a maze.
- **The escape.** The current escape (DREAM-ESCAPE) runs the street's front; it can stay as is, with the back layer as
  its fast line later.

---

## 5. Proposal B: the hill
`plan-b-hill.svg`.

**Idea.** Larch Street climbs 12 m from Walt's shop (at the creek, bottom west) to the hill house (top east), and the
block is built into the slope. **Height replaces the bands.**
- **Above the street:** dug-in garages whose roofs are the front terraces (+2.7); the houses on their terrace (+4.5;
  walls of 1.8 m are mantles); back gardens (+6.3); and the **Top Path** (+9), a ridge footpath that sees the whole
  street.
- **Below the street:** the south houses are entered from the street at their upper floor across short bridges; their
  gardens fall 3 m to **the creek bed**.
- **Through it all:** the creek comes down a wooded ravine on the west edge and under the street through a culvert.
  **The Steps**, a public stair with a landing every 1.5 m, climbs between houses 3 and 5; its ramp edge is a slide.
  The snicket steps down to the creek.

**Main routes:**

| Route | Description |
|---|---|
| Street | Uphill, watched from above and from the party (on the street's only flat landing) |
| Terraces | Mantle up 1.8 m walls house by house |
| Top Path | Fast and long-sighted |
| Creek bed | Low and hidden, from the culvert to the hill house's lower garden |

The Steps and the snicket are the cross-links.

**Hide-and-seek:** on the Steps and the terraces of houses 3 and 5 (the dashed box), with lots of levels. Theo counts
on a landing halfway up.

**Stealth:**
- From above, the Top Path is Lee's stalking height made literal.
- From below, the retaining walls block the street's view of the terraces.

**Across nights:** the switches are the Steps' gate, the culvert grate, the snicket gate and the hill house's lower
gate.

**Why not first:**
- Sloped terrain is harder to author and to build collision for.
- The whole street is visible from above, so street stealth is hard.
- The escape runs uphill or downhill, which changes its feel.
- It's also less like the slice's flat street.
- **Its best idea, the creek bed running under the street, is already in A.**

---

## 6. Proposal C: the close
`plan-c-close.svg`. About 100 x 100 m.

**Idea.** Larch Street ends in a turning circle:
- **The hub:** the block party fills the island in the middle (marquee, tables, the oak), and everyone at the party
  watches the ring road.
- **The houses:** Walt's shop and the five houses sit around the ring.
- **The Back Way:** behind them, a 4 m ring alley between the garage backs (2.7) and an outer wall (3.2) loops behind
  every house. You kick across it.
- **The hill house** stands above the head of the close.
- **The stem road** leads out south to the bus stop; that's where the escape runs.

**Routes:**
- **The ring road:** a panopticon, the most watched street of the three proposals.
- **The Back Way:** a hidden ring.
- **The roof ring:** houses 4 m apart around the circle.
- Radial side passages (4 m, wall to wall) cross between them.

**Hide-and-seek** on the north arc of the Back Way. A loop is the oldest hide-and-seek trick: Theo circles, you circle
the other way.

**Across nights:** gates on the Back Way (S1-S3) cut the ring into arcs.

**Why not first:**
- It changes the story's street (a straight street of childhood becomes a close), and the escape moves to the stem.
- Loops can disorient at speed.
- It's the densest per square metre, though, and the cheapest to build (one ring of houses).

---

## 7. D (the option the creator dislikes): link to a second, denser place
`plan-d-link.svg`. The street stays as the slice has it; Walt's back door opens onto a dense second place, for example
the yards behind the high street, built as the stealth level.

**Why not:**
- It's two spaces to build.
- The dense one has no reason to be in Elias's Saturday (STORY-TALK 2C).
- It leaves the street, where the story is, as empty as now: the creator's own objection, and it holds.

**Its only advantage:** the street stays cheap. A tells the same story with the density where the memories are.

---

## 8. Comparison

| | A. Deep block | B. Hill | C. Close | D. Link |
|---|---|---|---|---|
| Story changes | none | the street climbs | the street becomes a close | a new place to justify |
| Stacked routes | 4 (street, back, roof, creek) | 4 by height | 3 rings | 1 + the other place |
| Ways into each goal | 3-4 | 3-4 | 3 | 1-2 on the street |
| Hide-and-seek fit | gardens with a trampoline base | terraces and the Steps | a loop | elsewhere |
| Later stealth | good (watched front, hidden back) | hard on the street, great from above | the hub sees everything | good, elsewhere |
| Night variety without geometry | 8 switches + watchers + static | 4 switches + watchers | 3 gates cut the ring | — |
| Fits the current escape | yes, unchanged | rebuilt on a slope | moves to the stem | yes |
| Build cost | medium-high (one kit, reused) | high (terrain) | medium | two spaces |

**Recommendation** [Op]: **A, the deep block**. It keeps the street, the escape and the story. It gives the
Distillery's three stacked layers plus an under-route. It sizes every piece to our moves, and it changes per night
with switches, watchers and the static rather than new geometry. Two pieces can be borrowed from the others:
- B's **Top Path idea**, as the hill path's last terraces above the lane;
- C's **loop**: the lane and the street already make a loop round the north block.

---

## 9. Hide-and-seek, the stealth intro (all proposals)

**What Dishonored's does.** In the prologue Emily asks Corvo to play. She counts to ten and seeks, Corvo hides, and
leaving the area fails it. It's optional, "a very basic tutorial on stealth", and it introduces cover
([GamerGuides](https://www.gamerguides.com/dishonored/main-game/walkthrough/prologue)) [Comm].
- Harvey Smith calls it "a great way to bond with her ... a low stakes way to [teach] ... it's kind of like a double win"
  (Part 1, 23:45) [Doc].
- Theo is ours: STORY-BIBLE already says he "hides when things get loud (literally: hide-and-seek is how he copes)".

**Who seeks.** As the creator describes it (STORY-PROSE:125), **Theo seeks and Nadia hides**, and the memories are
scattered for her to find meanwhile. So the tension is moving between hiding places to collect without being found.

**Rules that come from the research** [Op, each tagged]:
1. **Show how many are left.** The creator asks for a count, or at least a line when none are left ("I'm not sure if
   there's anything left to find. I could let Theo find me here..."). The stealth kit's `CollectibleSet` already fires a
   one-time "all collected" event for that line. The Distillery's equivalent is the end-of-mission tally Arkane used
   (Part 1, 50:43).
2. **Every hiding place sees a memory or a route.** It's information at every entrance (Part 3, 28:20).
3. **Theo doesn't look up.** He's a child, and a vertical cone of about ±15° fits him. Squashed cones and no
   head-turn ambushes: Arkane learned this the hard way (Part 2, 41:37). Roofs over 2.5 m are safe from him, which
   teaches the perches.
4. **The trampoline is his one look-over.** At the top of each bounce he sees over the fences, which teaches timing.
5. **A mistake is recoverable.** Being found by Theo is a laugh and a "found you!", then a recount. Never a fail
   screen; stealth "makes some people feel stupid ... feel judged" (Part 1, 49:47).
6. **Not slow.** The zone rewards fast moves out of sight (a slide under the deck, a mantle onto the garage), not
   crawling: Doug Church's "don't reward going super slow" (Part 2, 56:47).
7. **It's optional and short:** 3-6 minutes (NIGHTS-GAMEPLAY), in 42 x 29 m. That's the same scale as the stealth
   kit's 48 x 36 m test yard.

---

## 10. What's unknown
- **The district's real dimensions.** No published plan exists; the schematic is topology only. Measuring needs the
  game (noclip) or annotated screenshots.
- **The stealth kit's final numbers** (sight, noise, shadows). The sizes above use its draft values; re-check the
  hide-and-seek zone and the roof noise when it lands (`docs/STEALTH.md`, being written).
- **Whether the story puts Theo on the street at all.** NIGHTS-GAMEPLAY has his hide-and-seek in the house (Night 2).
  The zone works in either a garden of the deep block or the house's yard.
- **Art cost.** "Three times the slice's street" is a guess; a blockout of one plot (house, garage, fences, shed) would
  price the kit.
- **The exact heights.** All of them are starting points from the movement numbers. Like the Gauntlet, the block
  should be checked by `CourseReach` and an autopilot run once it's blocked out.
