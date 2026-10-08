# Roguelite design rules

These come from the creator's first bullet-hell playtest and their notes on Inscryption (2026-10-08).
They're the spec for the template's next round, and the checklist for any roguelite made at the jam.

## 1. Where power comes from
The best upgrades have to feel special to get. If every battle can hand them out, none of them do.

| Source | What it gives |
|---|---|
| Normal battle | 1 of 3 commons or rares; a **small** chance that one is epic |
| **Boss** | **A guaranteed choice of epics** (1 of 3) |
| **Trial** | An epic for meeting a condition: finish a room without being hit, own 3 upgrades of one kind, clear a room without sprinting, beat a timer... |
| **Sacrifice** | An epic for giving something up: an upgrade, a life, your bombs, max stamina |
| **Risky encounter** | A chosen wager: take a curse or lose a life now, for an epic or a rare later |

Rule of thumb: epics mostly come from bosses and from encounters with conditions you have to plan for.

## 2. Every epic must be useful
"Useful" means it changes how you play in at least one or two real situations. From the playtest:
- **Homing twin gun: good.** It changed how the player played: dodge freely, and the damage still lands.
  Check it against the rest; it may be stronger than an epic should be.
- **Rear twin gun: useless.** Enemies never get behind you, the player sits low on the screen (where it's
  safest), and it only matches the front guns' damage. **Rule: an upgrade that depends on position has to
  match where players actually are.** Replace it: shots that bounce back up off the bottom edge, a trail
  that damages enemies diving through it, or side guns that sweep.
- **Bombs: rarely used, and odd as free kit.** Take them out of the starting kit and make them an epic:
  **Bomb Bay**, N bombs that recharge over time, with stacking upgrades.

The epic rubric (every epic has to pass all four):
1. It changes a **verb** (how you move, aim, dodge or take risks), not just a number.
2. It's useful in **at least two** situations: bosses and waves, or open and crowded rooms.
3. It **fits where the player actually is** (see the rear gun).
4. It has an **in-universe name and idea** from the setting, which is why epics get designed after the theme.

**Checking it (planned tool):** a headless balance report. A scripted bot plays N rooms with each upgrade
and reports its share of damage, hits taken and time to clear. Anything near zero (like the rear gun) is flagged.

## 3. Encounters between battles: risk the player chooses, reward for planning ahead
Inscryption's campfire (buff a card again and again, at a growing risk of losing it) and its sacrifice
altar are the model. Ours could be:
- **Forge:** improve an upgrade; each extra push risks breaking it.
- **Altar:** sacrifice upgrade A to strengthen upgrade B, or for an epic.
- **Wager:** lose a life now; win an epic if you clear the next room unhit.
- **Trial board:** take a condition for the next room; meet it and get an epic.
- **Trader:** swap two commons for a rare.

## 4. The room outside the map
Inscryption lets you stand up from the table between battles and explore the cabin. Its puzzles and the
card game feed each other, and you need both to progress. Players explore without being told, because
it's presented as a break.

Ours: a **hub room between floors**, built with the Story template's hotspot layer and inventory. Its
puzzles unlock things inside runs (an altar, a starting upgrade, a shortcut), and items found in runs open
its puzzles. It doubles as the place the story and the companion live.

## 5. Dying
- Dying is part of the story and **never limited**: die as often as you like.
- There's a floor most first-time players will hit: the first run is expected to end in death.
- **Something carries forward**: Inscryption turns your death into a custom card made from your run.
  Ours: a **remnant** upgrade built from the dead run (for example the run's best upgrade, weakened, or a
  stat from how you died), which can appear in later drafts.
- Each run should **stay fresh**: different drafts, encounters and remnants, so the second run isn't a repeat.

## 6. Companions fight too
A companion shouldn't only exist in the rooms between battles. In battle it **does something**: shoots
with you, takes a bullet on a cooldown, or calls out the boss's next pattern, and it reacts with short
lines. Its conversations between battles deepen what it does in them.

## 7. Movement
- **Shift = sprint** (being built): faster while held, with a stamina bar, and a **per-room sprint budget**
  so dragging fights out doesn't pay. Focus (slow, precise movement) stays as an option, off by default.
- Difficulty is chosen when a run starts (being built).

## 8. Coming up with enough epics
That depends on the setting, which is why it's a day-1 task once the spine is written:
1. Brainstorm 12+ epic ideas from the setting's own objects, powers and characters.
2. Filter them through the rubric in section 2.
3. Run the balance report.
4. Keep the best 6–8.

Each boss's guaranteed choice of 3 epics draws from these.
