# Roguelite design rules

These come from the creator's first bullet-hell playtest and their notes on Inscryption (2026-10-08).
They're the spec for the template's next round, and the checklist for any roguelite made at the jam.

**Status (round 2, 2026-10-08):** sections 1, 2, 3, 5 and 6 are built in the bullet-hell template
(how to change them: `docs/TEMPLATE-BULLETHELL.md`); section 4 (the hub room) is a later round. Core
tests green; the Unity run is pending. Each section below ends with what was built.

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

> **Built:** a reward table per source in `BulletHellConfig > Rewards` (rarity weights, a small epic
> chance, cards, luck). Normal battles now give a draft (commons/rares, 5% chance of one epic); bosses a
> guaranteed choice of 3 epics; trials, sacrifices (the Altar) and wagers give epics. Validated: those
> four must offer only epics, battles only get epics through the small chance.

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

**Checking it:** a headless balance report. A scripted bot plays N rooms with each upgrade
and reports its share of damage, hits taken and time to clear. Anything near zero (like the rear gun) is flagged.

> **Built:** JamKit > Bullet Hell > Balance Report (and a batch method) writes `out/balance-report.md`.
> First results on the built-in test rooms (8 seeds, companion off): the **rear gun is flagged** (0% of
> the damage, x1.00 DPS: identical runs), and so is a deliberately useless upgrade; **Seeker Rounds was
> x1.80 DPS**, far above every other epic, so its homing streams went from 0.6 to 0.35 damage (now
> about x1.55, still the strongest damage epic, "keep it fun"). The epics that change a verb all pass:
> Backdraft x1.26 DPS (23% of the damage), Bomb Bay about 40% fewer hits, Ghost Step about 75% fewer
> hits, Ember Hunger about x1.1 DPS for more hits (the trade is the point), Red Line x1.5 on the last
> life, Time Dilation about a third fewer hits. Four ideas failed and were dropped or reworked: a damaging
> wake behind the ship (divers die to the main guns before they reach it: 0%), "Still Point" (more
> damage while holding still: players never hold still), a sprint-only Time Dilation (0%: now a bubble
> around the ship), and a decoy on sprint. Light Frame and Piercing Shot (a common and a rare) sit near
> the threshold: worth a look once the theme's rooms exist.
>
> **Epic balance pass (2026-10-08, the creator's notes).** On the real rooms, 8 seeds, before -> after:
>
> | Epic | Change | Before | After |
> |---|---|---|---|
> | Ghost Step | phases only in a sprint's first 0.5 s, 5 s apart, 3 a room (pips) | 77% fewer hits | 11% fewer hits |
> | Red Line -> **Last Gambit** | last life: +75% damage, hits ricochet, kills blast (the only area damage besides bombs) | x1.43 DPS | x1.68 DPS, 70% fewer hits |
> | **Still Point** (back) | holding still ramps to double damage in 0.6 s; any move resets it | flagged (bot never still) | x1.13 DPS (bot as a burst mover) |
> | **Scatter** (new) | a shotgun cone instead of the guns, about 3x point-blank | x0.82 at 2x point-blank | x1.28 DPS, more hits (aggressive bot) |
> | **Blade** (new) | an arc slash instead of the guns, 3.5x DPS, 1.7 range | x0.85 at 2.6x and 1.35 | x1.25 DPS, most hits taken |
> | Backdraft | demoted to rare, toned down: it rewards the spot that's already best | x1.19 | x1.14 |
> | Seeker Rounds | homing streams every third volley, 70% speed, 70% damage, turn rate 7 -> 4 | x1.59 | x1.37 |
> | Guardian Angel | spends every stored bomb, then 10 s to recover (shown) | 56% fewer hits | 47% fewer hits |
> | Light Frame (common) | a 20% smaller hitbox and +10% stamina regen (more move speed reads as more hits for the bot) | flagged | 16% |
> | Hot Barrels / Quick Fuse | +12% damage / +70% recharge | flagged 3% / 14% | 15% / 17% |
> | Time Dilation | checked, unchanged: a legendary under Bomb Bay and Guardian Angel | 28% fewer hits | 28% |
>
> **Epics now:** Seeker Rounds (aim), Bomb Bay (bombs left the starting kit; Spare Bomb, Quick Fuse, Big
> Bang and Guardian Angel stack on it), Guardian Angel (risk), Ghost Step (dodge), Ember Hunger (risk:
> grazes heat your guns), Still Point (move: burst between gaps, then plant), Scatter and Blade (risk:
> close range, one or the other); legendaries Time Dilation (dodge: bullets near you at half speed) and
> Last Gambit (risk: your last life). Backdraft (the rear gun's replacement) and Extra Life are rares.
> Every epic carries its rubric (verb, situations, where the player is, idea) and design notes with its
> balance numbers; Validate Content and the EditMode tests check them. The companion is unchanged until
> the story decides its role.

## 3. Encounters between battles: risk the player chooses, reward for planning ahead
Inscryption's campfire (buff a card again and again, at a growing risk of losing it) and its sacrifice
altar are the model. Ours could be:
- **Forge:** improve an upgrade; each extra push risks breaking it.
- **Altar:** sacrifice upgrade A to strengthen upgrade B, or for an epic.
- **Wager:** lose a life now; win an epic if you clear the next room unhit.
- **Trial board:** take a condition for the next room; meet it and get an epic.
- **Trader:** swap two commons for a rare.

> **Built:** all five, as room types the run generator rolls (weights in `Run`; a floor keeps about 2-3
> fights). Each plays an ink line first (`forge`, `altar`, `wager`, `trial_board`, `trader`, placeholder
> text), then a choice screen. The Forge shows the odds of each push (safe, then 25%, 50%...); the Altar
> takes one offering (an upgrade, a life or max stamina for an epic, or feeds one upgrade to another);
> wagers stake a life, coins or a curse; the trial board offers no-hit, no-sprint, timer, "own 3 gun
> upgrades", graze and no-bomb conditions; the Trader swaps two commons for a rare or buys a stack.
> Trials and wagers are data lists in the config.

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

> **Built:** the remnant. On death the run's best upgrade (the rarest with a lasting effect) becomes
> "Echo of ...": its stats at half strength, its effects kept, plus +6% of a stat chosen by how you died
> (a boss: damage, an elite: fire rate, a battle: move speed). The save keeps the newest 3; the next
> run's first draft shows one (then 40% per draft), marked REMNANT, and taking it uses it up.

## 6. Companions fight too
A companion shouldn't only exist in the rooms between battles. In battle it **does something**: shoots
with you, takes a bullet on a cooldown, or calls out the boss's next pattern, and it reacts with short
lines. Its conversations between battles deepen what it does in them.

> **Built:** all three, data-driven (`Data/Companion.asset`, off with one tick in the config). It orbits
> and shoots the nearest enemy at 40% of a ship shot, takes one bullet meant for you every 18 s (a GUARD
> meter and a ring show it), and barks a line from ink a moment before each boss pattern
> (`companion_bark_<boss>_<shape>`, `companion_bark_<shape>`), when it guards and on your last life,
> without stopping the game. The Rest room's line reads `companion_guards()`, so the conversation
> between battles can mention what it did in them. Two rare upgrades target it.

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
