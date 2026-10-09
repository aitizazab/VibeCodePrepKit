# *Ember*: the demo's story (draft for the creator to edit)

> **Status: rejected draft (2026-10-08), kept for history.** The creator's critique: no clear theme, mindless sparks undermined the stakes, the death loop made the antagonist look incompetent. Current game: `VISITING-HOURS.md`.

Status: a proposal (2026-10-08). The story, names and lines are the creator's to rewrite. Gameplay ties to it
only through the hooks listed at the end, so renaming things is cheap.

## The story

At the bottom of the world there is a Forge, and the Forge makes stars. It does it the only way a star can be
made: one spark at a time. The Forgemother, an old intelligence with hands of molten iron, draws each spark out
of the crucible, hammers it until it burns clean, and sets it into the star she is building. Ten thousand sparks
are already in it. Fused together they will be one sun, and none of them will be anyone any more. That's how it
has always worked, and the sparks have never minded, because sparks are not supposed to wake up.

You woke up. Somewhere between the crucible and the anvil you became *someone*, a spark with a self. That self
wants one thing: to see the open sky that the Forge's chimney vents into, and to stay yourself while you do.
Everything in the Forge is built to stop that. Its machines herd loose sparks back towards the anvils, its vents
spit slag, and at the mouth of the furnace stands the Gatekeeper.

The **Gatekeeper** was the Forgemother's very first spark, and it has stood at the gate longer than any star has
burned. It isn't cruel. It has watched every spark that slipped past it rise into the sky and go cold there: out
in the open a lone spark burns for hours, not ages. "Nothing that leaves comes back," it tells you. Its bullets
are walls and closing grids, a jailer's patterns. When it falls, it hands you the lantern it has carried all
this time. In it is a grey, half-dead ember: **Cinder**. "Take the old one with you. He's tried before."

**Cinder** is the reason anyone believes leaving is possible. He was the first spark ever to wake, ages ago, and
the first to try to escape. He got past the gate, made it all the way up to the flue, and burned out within
sight of the sky. The Gatekeeper caught what was left of him and has kept it alight out of something like
guilt. Cinder is too dim to fight. But he knows the Forge, every pattern and every machine, because the
Forgemother built them in front of him. He rides inside your flame, talks too much, and has opinions about
everything.

The **Forge** itself (floor 2) is the workshop: anvils, bellows, rivers of metal, and the other sparks. The
twist comes here. The waves you've been fighting on this floor aren't only machines: they're your siblings,
sent to bring you home. "Mother isn't angry," one of them says. "She just needs you. The star needs one more."
**You are the last piece.** Without you the star never lights, and the ten thousand sparks already in it stay
half-forged forever: neither stars nor selves. The **Forgemother** doesn't hate you. Her last star failed, and
she can't bear another. Her patterns are spirals, the motion of forging. In her final phase her attacks slow and
leave gaps: she's letting you win, because Cinder is with you and she remembers him.

The **Last Light** (floor 3) is the star itself, almost born, speaking with the voices of every spark inside it.
It doesn't want to destroy you. It wants you, the way a sentence wants its last word. Its patterns are made of
every boss before it, because it is everything the Forge has ever made. Beating it doesn't kill it: it means you
can choose. Fly out of the flue as yourself and burn bright for one short night. Or go back down and become the
last spark of a sun. Or, if Cinder is still lit, he can go in your place: the old spark who never made it out
finishes the star, so that the young one can.

You will die along the way, and the Forge doesn't waste anything. A spark that goes out falls back into the
crucible, and the Forgemother pours it again. **Every run is you, re-forged.** In the moment you go out you can
clench one thing, the hottest part of who you just were, and it survives the crucible as an **Echo**. The Forge
notices. "You again," the Gatekeeper says on your second run. "She always pours you facing the door."

## The companion: Cinder is a **mentor**
Cinder's place in the story is the one who tried before and failed. That makes him a mentor, and his gameplay
follows from it:
- **He isn't a body you protect.** He rides inside your flame, so he has no hitbox. That fits a game of
  threading the needle, and it fits the story: he's too dim to exist on his own.
- **He's beyond you in unique ways:** knowledge and one great sacrifice, not damage.
  - **Foresight:** before a boss attack, he briefly marks where the gaps will be (a faint outline, a moment
    before the pattern fires). It helps, but it doesn't play for you.
  - **Know-how in encounters:** with Cinder, the Forge shows its exact odds of breaking (without him you only see
    "risky"). He tells you what the Assayer's wager is hiding, reads the soot-folk's trial board (one extra
    trial), and at the Altar he can take one offering back.
  - **Kindle** (the great cost): when you would lose your last life, or when you press E, Cinder burns part of
    himself to stop the Forge: the screen clears and everything freezes for 3 seconds. Each Kindle dims him for
    the rest of the run: first his foresight weakens, then he can't help in encounters, and the third time he
    goes out. He's gone for that run, with a scene. So the choice is real: save yourself, or keep your mentor.
- **The game can be beaten without him** (he's gone if you spend him), but having him makes it smarter, not
  easier to brute-force. And keeping him lit opens the third ending.
- **When he joins:** after the Gatekeeper falls (it hands him over). On a later run, if he wasn't spent last time,
  he's waiting at the start: "I saw where you went out."

## The mechanics in the world
| Mechanic | In the world | What that means for the design |
|---|---|---|
| Your shots | Your own fire, spent | |
| Sprint and the room budget | You **flare**, burning brighter to move faster. Each room only has so much air | The per-room budget is the room's oxygen; the stamina bar is your heat |
| Normal upgrades (drafts) | Heat you take from the machines you break: they're built from sparks' fire | Small, frequent |
| **Epics (bosses)** | A piece of a great flame, taken when it falls | Why the best upgrades come from bosses |
| **Forge** | The Forgemother's own anvils, left idle. You temper yourself on them, and hitting yourself harder risks cracking | Each push risks breaking the upgrade |
| **Altar** | Shrines the sparks inside the star built to the **Hearth**, the star-to-be. An offering is part of you given to the star, and the star pays you back because it wants you | Every offering raises a hidden **Hearth** score. The Last Light speaks to it, and a high score makes the "become the star" ending more tempting and better rewarded |
| **Wager** | **The Assayer**, the Forge's inspector, who weighs every spark to see if it's pure enough for the star. He's bored, so he bets on sparks | A recurring character. Win enough of his bets and he tells you something true on floor 3 |
| **Trial board, Trader** | The **soot-folk**: small creatures living in the Forge's walls, who don't care about stars. They post bounties and trade | Implies a civilisation and distance travelled between rooms |
| **Rest** | Cool pockets in the vents where a spark can dim for a moment. Cinder talks | |
| **Bombs (Bomb Bay)** | You bank heat and release it all at once | The recharge is rebuilding heat |
| **Grazing** | Brushing past slag steals a little of its heat | Ember Hunger makes sense |
| **Coins** | **Ashglass**: cooled droplets of sparks that went out | A quiet moral weight to spending |
| **Death and the Echo** | You fall into the crucible and are poured again. You *choose* what to clench | **The Echo is chosen, not random**: the death screen offers your run's upgrades and you pick one (weakened) to carry forward. Agency in failure, like Inscryption's death card |
| Run count | The Forge remembers you; Gatekeeper and Forgemother lines change | Story hooks per run count |

## Endings (flags the run sets)
- **Free:** leave alone through the flue. The default once the Last Light is beaten.
- **Hearth:** go back and become the star. Always offered, and its lines are warmer the higher your Hearth score.
- **Kindling:** Cinder goes in your place. Offered only if Cinder is still lit at the end.
- (A second-run touch: the Gatekeeper opens the gate before the fight if you reached the Last Light before.)
