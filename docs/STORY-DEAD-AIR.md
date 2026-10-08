# *Dead Air*: a grounded story for the bullet-hell demo (draft 2, for the creator to edit)

Replaces the *Ember* draft (`STORY-EMBER.md`), following the creator's critique: commit to what it's about, start
ordinary and get deeper in layers, give death a cost without the villain looking incompetent, use characters
who read as people, make floors 2 and 3 different ideas, have the game argue something, and keep the companion's
fate on a separate axis from losing. Everything here is a proposal; names and lines are the creator's.

## What it's about
**On the surface:** a salvage pilot doing a dangerous job. **Underneath:** what you owe the people in front of
you when your job says they don't count. **At the bottom:** how far you'll go for the one person you came for, and
whether that's love or selfishness. Each layer leaks into the next: the job becomes a rescue, and the rescue turns
out to be personal.

**What the game argues** (the way Undertale argues for mercy): doing right costs you something real. Helping the
people inside makes your runs harder and your pay smaller, and the game never pretends otherwise. But the endings
that leave you proud are only reachable if you pay that cost.

## The story

**Wren Halloway** is nineteen and three weeks into her first real job: drone pilot for **Kestrel Recovery**, a
salvage outfit of exactly two people and one rusting tug. The other person is **Ilse Varga**, sixty-eight,
foul-mouthed and brilliant, who flew rescue drones for thirty years until her hands started to shake. Ilse
doesn't fly any more. She sits behind Wren on the tug's cramped flight deck, eats terrible noodles and tells her
what she's doing wrong.

Their contract is the biggest Kestrel has ever landed. **Meridian**, a habitat station owned by the **Halden
Corporation**, went silent three weeks ago after a "reactor incident". Its automated defence grid now fires on
anything that comes close. Halden is paying salvage crews to fly drones down Meridian's central shaft to the core
and pull out the black box. Drones are cheap; pilots fly them from safely outside. Lose one and you launch
another, and Halden docks your pay. Half a dozen other crews are working the same wreck, including the **Gulls**,
led by a loud, grinning gambler called **Pike**, who runs a betting pool on whose drones die first.

**Floor 1, the Outer Ring,** is just the job. Turrets, sentry drones, slag from the reactor fire. Ilse calls the
patterns ("left turret's on a four-count, don't get cute"), and Halden's automated voice thanks you for your
patience as it tries to shoot you down. The first boss, **the Gate**, is a security platform that apologises
politely before every barrage. It's a machine and nothing more. That's the point: the first layer is an arcade
job, and a player who only wants that gets a satisfying one.

**Floor 2, the Habitat Ring,** is where it turns. The lights are on. There are handprints on the inside of the
windows. A child has drawn a ship on the wall with a marker, and under it the word *HELP*. **Forty people are
alive in there**, sealed in by the defence grid. The reactor incident was real, but it was weeks ago, and it
didn't lock the doors. Halden did. The black box isn't a recording of the accident: it's proof of what Halden was
doing on Meridian, and the people inside are witnesses. The contract never mentioned them because Halden never
meant them to leave.

The second boss is **Teodor Brask**, Meridian's security chief, flying the station's security drones by hand from
inside. He's exhausted, half-starved, and he thinks you're Halden's clean-up crew, because every drone that's come
down the shaft so far has been. He fights like a man defending his family, because he is. Mid-fight he starts to
listen. In his last phase he stops shooting at you and starts shooting the turrets around you. After that, the
game's goal changes: **you're not recovering a box any more. You're getting forty people out.**

**Floor 3, the Core,** is where Halden itself arrives. **Director Ines Calloway** patches into every channel, calm
and reasonable. She explains why the people inside have to stay: the data would sink Halden, thirty thousand jobs
would go with it, the station programme would die, and these forty "made their choices". She's not a monster;
she's worse, she's sensible. She just keeps saying sensible things until you notice none of them are reasons.
Her clean-up protocol takes over the grid, and the last fight is against everything Halden owns. She keeps
talking through it, and she keeps losing the argument.

**The hidden layer, for players who look:**
- Ilse built Meridian's defence grid, thirty years ago. That's why she can read its patterns, and why she has
  codes Halden forgot to revoke.
- Wren's mother, **Ana Halloway**, worked on Meridian. Wren never says so. Her name is on the station's memorial
  wall, and the child's drawing in the habitat is signed *T. Halloway*, Wren's little brother, whom Wren hasn't
  seen since their parents split.
- Ilse knew all along. She took the contract *because* of it.

Nothing forces the player to find any of this. It's in the memorial names, a signature, and a line Ilse only says
if you ask the right question at the third rest stop.

**Dying, and why you can keep trying:** that's literally the job. When a drone dies, the tug's fabricator prints
another and you send it down. Nobody is being incompetent: the grid destroys drones automatically, and Halden
sells you new ones. **But it isn't free.** Every lost drone is another day of air for the people inside, and the
station's air reserve is shown between runs. The first couple of losses are covered by the reserve, so a
first-time player isn't punished. After that, each loss shrinks how many people the Rescue ending can carry out.
You can die as often as you like and still finish. You just can't save everyone that way.

## The companion: Ilse is a **mentor**, on a separate axis from losing
- **She's never in danger from your flying.** She's on the tug, behind you. Your drone exploding costs a drone,
  not Ilse.
- **She's beyond you in what only she can do:**
  - **Foresight:** she built the grid, so a moment before a machine boss's pattern fires she marks the gaps.
    *Not* for Brask: he flies by hand, and she can't predict a person. The second boss feels different because of it.
  - **Know-how:** she reads the fabricator's misprint odds exactly, knows when Pike's bet is rigged, and can talk
    a trader down.
  - **Override (E):** her old builder codes freeze the grid for 3 seconds and clear the shots around you.
- **Her cost is on its own axis: Exposure.** Every Override is logged in Halden's system. At a set Exposure,
  Halden finds her codes and **cuts her off the channel**: Wren flies the rest of the run alone, and the endings
  that need Ilse's codes close. Losing a drone never touches this, and using Ilse never ends a run. Two separate
  axes.
- The game is beatable without a single Override. Using her makes you safer now and costs you later.

## The mechanics in the world
| Mechanic | In the world |
|---|---|
| Your ship | A salvage drone, flown remotely |
| Bullets | Turrets, sentry drones, reactor slag; later, Brask's hand-flown drones and Halden's clean-up swarm |
| Sprint (with a per-room budget) | Afterburner on a small fuel cell, topped up at each docking point |
| Normal upgrades | Parts salvaged from the machines you destroy |
| **Epics (bosses)** | Each boss's own hardware, salvaged when it falls (the Gate's shield emitter, Brask's targeting rig...) |
| **Forge** | The tug's **fabricator**: Ilse reprints a part stronger, and each extra pass risks a misprint that ruins it (she tells you the exact odds) |
| **Altar** | The habitat's **memorial wall**, where the residents have been leaving things. Leaving a part there sends it to the people inside: your drone gets weaker, they get to fix an air pump. They send something back, and a hidden **Trust** score rises |
| **Wager** | **Pike and the Gulls**, betting on your runs over an open channel. Win enough of his bets and he respects you, and the Rescue ending needs his ship |
| **Trial board** | Halden's open contract board and other crews' side jobs ("clear this vent without a scratch") |
| **Trader** | Other crews' drones meeting in the shaft to swap parts: a whole small economy inside a dead station |
| **Rest** | Docking points where the drone recharges; conversations with Ilse, and later with the residents over a crackling line |
| **Death** | A lost drone: a new one is printed, and the residents lose a day of air |
| **The Echo** | As your drone breaks up you get one second to **eject a module** (the death screen: pick one of the run's upgrades). The next drone finds it in the wreck, damaged (weakened). **Chosen, not random** |
| Run count | Pike's odds get longer, Brask recognises your drone's markings, the residents start counting your drones |

## Endings (from the flags you set)
- **Paid:** deliver the black box to Halden. The residents stay. The easiest ending, and Calloway thanks you.
- **Rescue:** open the grid, and Pike's ship takes them off. Needs Pike's respect and enough Trust. How many get
  out depends on the air reserve, which means how many drones you lost.
- **Broadcast:** use Ilse's codes to send the black box to every channel at once. Halden falls, and it costs Ilse
  everything she has. Needs Ilse still on the channel, which means Exposure kept under the limit.
- **Hidden:** if you found the Halloway names, Wren gets one more line at the end, whoever she saved.

## Why this structure works for the jam
- Characters are people with faces: portraits in the dialogue box (the Story template's portrait system), so
  motives read clearly.
- The layers come in on a schedule the template already has: floor 1 is the job, floor 2's boss is the turn,
  floor 3's boss is the argument.
- Every in-between room says something about the world: it isn't empty, and helping costs you.
