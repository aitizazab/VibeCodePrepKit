# Story structure for the bullet-hell roguelite

The template already has a solid core: dodge, shoot, draft upgrades, three floors, three bosses. What's
missing is a reason to fight and something to do between battles. These two depend on each other: the
story gives the in-between rooms something to say, and the in-between rooms are where the story is told.
So write the story first, then make each in-between room deliver one piece of it.

## What the well-loved games in this space do

| Game | How the story arrives | What happens between fights | What to take |
|---|---|---|---|
| **Hades** | In short conversations, a few lines at a time, mostly back at home after you die | You go home, talk to the household, give gifts, buy permanent upgrades | **Dying is part of the story**: characters react to how you died and the plot moves on. Small amounts, often |
| **Enter the Gungeon** | Very little text: an NPC hub, item descriptions, each character's "Past" behind a hidden final room | Shops, NPCs you rescue who then open shops at the hub, secret rooms | **Rescue or unlock something and it shows up later.** Lore lives in item and upgrade descriptions |
| **Undertale** | Through the fights themselves: each enemy's attacks show its personality, and you can spare it instead of killing it | Talking, puzzles, choices that change who survives | **The bullet pattern is the dialogue.** A boss's phases are its emotional beats |
| **Furi** | A short exchange before each boss, plus a companion who talks during walking stretches between them | Walking: a breather that also carries the story | **Every boss is a character with a reason to stop you.** The quiet stretch between fights is where meaning lands |
| **Touhou** | A few lines of banter before each boss | Almost nothing: stages flow straight on | **Pre-boss dialogue is enough to make a boss memorable** |
| **Inscryption** | Through the game itself: the opponent narrates the card game, and the cabin around the table holds the mystery | Map encounters that make you choose a risk (campfire buffs, sacrifice altar), and getting up from the table to solve puzzles in the cabin. The two feed each other | **Dying is expected but never limited, and leaves something behind** (your death becomes a card). The best cards come from bosses and hard conditions. See `DESIGN-ROGUELITE.md` |
| *Save the Date* (your scope reference) | Through repeat attempts: the game remembers what you did before | — | **The second attempt can be different on purpose.** A blind tester who dies once should meet a changed game |

## The skeleton (fits 3 floors, 10–15 minutes, a stranger who will probably die once)

### 1. The spine: one sentence before anything else
> **You are** ___, **who wants** ___, **but** ___ **stands in the way**, **and getting there will cost** ___.

Every room, boss and upgrade should touch one part of that sentence. Leave the cost blank and the ending
has nothing to land on.

### 2. Three acts on three floors
| Floor | Act | Job | The boss is... |
|---|---|---|---|
| 1 | Setup | Who you are and what you want; the world says no | **The obstacle:** someone guarding the way, with a reasonable reason |
| 2 | Complication | What you thought was true turns out not to be (the enemies, your goal, or you) | **The turn:** beating them reveals the twist, in their last words or phase |
| 3 | Climax | You now know the cost; decide whether to pay it | **The question:** the fight *is* the choice, and the ending depends on how you got here |

### 3. What each room type says
The template's room types already match the beats. Give each one a story job:

| Room | Gameplay | Story job |
|---|---|---|
| Combat / Elite | Waves | Show the world: enemy designs and their bullet styles *are* the setting. Keep text to a line on entry, if any |
| Event (ink) | A choice with a gameplay effect | **The main storytelling room.** One short scene: a character, a dilemma, a choice. It sets a flag the ending reads |
| Rest | Heal or train | **The companion's room.** A quiet conversation, Furi- or Hades-style: what the hero is thinking. 2–4 lines |
| Shop | Spend coins | A merchant with an opinion; the upgrade descriptions carry lore (Gungeon-style) |
| Treasure | Draft 1 of 3 | Names and descriptions can tell the story: "Forgemother's Patience: +20% fire rate" |
| Boss | Fight | A line before the fight, a line at each phase change, a line when they fall. Patterns express the character |

### 4. Dying is a story beat
A stranger will most likely die on the first run. Make that death **part of the story**, not a reset:
- the death screen shows a line from the companion or the boss who killed you;
- the second run opens with something new: a character who remembers, a skipped tutorial line, or a
  choice that wasn't there before;
- persistent flags (the shell's `set_flag`/`has_flag` are saved across runs) are all it takes.

### 5. Bosses are characters
For each boss, write four things:
1. **What they want.** One line: not "kill you" but *why*.
2. **Pattern language.** How their bullets show that want: walls for a guard, spirals for a maker,
   aimed shots for a hunter, gaps that close for someone who's giving up.
3. **Phase beats.** Phase 1 = who they are; phase 2 = they get serious or crack; phase 3 = the truth.
4. **Last words.** What they say when they fall, and what it changes.

### 6. A second-run layer (optional, very effective for judges)
If the tester replays, give them one thing that only exists the second time: a character who says
"back again?", an extra choice at the first event, a secret ending. Two flags and three ink lines can do it.

## Things to do between battles, each tied to the story
- **Talk:** an event scene with a choice (a flag plus a gameplay effect: coins, a life, a curse or a blessing).
- **Spare or strike:** an event where an enemy asks for mercy, Undertale-style; sparing changes a later scene.
- **Trade:** give up an upgrade for story progress, or the other way round (a sacrifice that costs power).
- **Remember:** a lore pickup or companion line that only appears if you made an earlier choice.
- **Gamble:** a shrine: take a curse for a rare upgrade.
- **Breathe:** a no-combat room with one line and something pretty: pacing matters as much as content.

## Worked example: *Ember* (uses the template's three bosses as they're named)

**Spine:** You are **the last spark of a dying star-forge**, who wants **to reach the open sky**, but **the
forge's guardians** stand in the way, and getting there will cost **everyone who made you**.

- **Floor 1, the Gate (setup).** Enemies are cinders and drones. Event: a cooling spark begs you to take it
  along (yes: +1 life and flag `carried_friend`). Rest: your companion, an old moth drawn to your light,
  asks why you're leaving.
  - **Boss: the Gatekeeper.** *Want:* nothing that leaves ever comes back, so nothing leaves. *Patterns:*
    walls and closing grids. *Phase 2:* "Every spark before you said the same." *Last words:* "Then go.
    I'll keep the door warm."
- **Floor 2, the Forge (complication).** The waves are other sparks, and in an event one says: "Mother
  sent us to bring you home." **Twist:** the enemies are your siblings. Event choice: spare one (flag
  `spared_sibling`) or take its fire (rare upgrade).
  - **Boss: the Forgemother.** *Want:* to remake the star, and she needs every spark, you included.
    *Patterns:* spirals, the forging motion. *Phase 3:* her patterns slow and leave gaps: she's letting you
    win. *Last words:* "Burn bright, then."
- **Floor 3, Last Light (climax).** The sky is close, and every spark you passed is dimming.
  - **Boss: Last Light**, your own reflection: the spark you'd become if you never stopped running. *Want:*
    to make you choose. *Patterns:* your own upgrades turned against you.
  - **Ending** reads the flags:
    - leave alone: *Free*;
    - carried a friend and spared a sibling: *Return*, you go back and relight the forge;
    - second run only: *Both*, the Gatekeeper remembers you and opens the door before the fight.
- **Death screen lines:** the moth: "Again? Fine. I'm not going anywhere." On the second run, the
  Gatekeeper's first line changes: "Back so soon?"

That's about **6 event scenes, 3 rest conversations, 12 boss lines and 3 endings**, roughly 1,500 words of
ink: a one-day writing job, which fits day 2 of the jam.

## Day-1 worksheet (fill in when the theme drops)
1. Spine: You are ___, who wants ___, but ___ stands in the way, and getting there will cost ___.
2. Floor 1 boss: wants ___ / patterns ___ / last words ___
3. Floor 2 twist: what turns out to be untrue? ___ Who reveals it? ___
4. Floor 2 boss: wants ___ / patterns ___ / last words ___
5. Floor 3 question: what must the hero decide? ___
6. Final boss: wants ___ / patterns ___ / last words ___
7. Endings (2–3) and the flags that pick them: ___
8. Companion: who, and their one-line personality: ___
9. What the death screen says, and what changes on run 2: ___
10. Event scenes (one choice each, with a gameplay effect and a flag): ___

## Template changes this calls for (done 2026-10-08, Unity run pending)
- Choose difficulty when starting a run, not only in Settings (creator's feedback, 2026-10-08).
  **Done:** Play opens a Story / Normal / Hard picker (docs/SHELL.md).
- ~~Make Focus (Shift) unmistakable~~. The creator then chose to make Shift a **Sprint** (what players
  expect): stamina bar inside a per-room sprint budget, a first-run hint and a HUD legend; Focus stays
  behind a flag (docs/TEMPLATE-BULLETHELL.md, Controls).
- Boss dialogue hooks: an ink knot before the fight, at each phase change and on defeat. **Done.**
- A death screen line chosen from ink (by killer, run number or flags). **Done.**
- A companion line on Rest rooms and run-start lines that change with run count. **Done.**

The knot names and the ink functions they can use are in docs/TEMPLATE-BULLETHELL.md, "Story hooks".
The template ships one placeholder line per hook; the story is still yours to write.
