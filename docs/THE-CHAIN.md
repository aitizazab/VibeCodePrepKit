# The waking days and the chain

The game from the title to the end card (plan: `docs/NIGHTS-1-2.md`). Built 2026-10-12 by the opening agent.

```
opening → title → Day 1 → 11 PM booth → Night 1 (DreamStreet) → Day 2 → booth → Night 2 (DreamHouse) → end card
```

## What plays where

| Segment | Scene | Code |
|---|---|---|
| Day 1, its booth | `VisitingHours` | `VisitingHoursGame` (`BeginDay`, `GoToBooth`), the ward `Scripts/World/VhWakingWard.cs` |
| Night 1 | `DreamStreet` (the movement agent's hill street) | `DreamStreetGame`; its `NightEnded` hands back |
| Day 2, its booth | `VisitingHours` | `BeginDay2`, `SliceArea.HospitalDay2` / `BoothDay2` |
| Night 2 | `DreamHouse` (the house agent's) | `DreamHouseGame`; its `Escaped` hands back |
| The end card | `VisitingHours` | `ChainEnd` (a placeholder card: Walt, Lena, Force, Despair, the journal) |

`Scripts/Chain/VhChainRunner.cs` (on the GameRoot, added by `VhBoot`) loads each night and brings the waking scene
back. A night's scene is the first of `Chain.Night1Scenes` / `Night2Scenes` in Build Settings; with none, Day 1's booth
dives into the slice's own street and Day 2's booth goes to the end card. Any other scene can end its segment with
`Chain.Done()`.

**Off in batch runs** (tests, the default 20 s smoke) unless asked for, so the slice's tests stay as they were:
`VhChainRunner.ForceEnabled` (tests) or a smoke budget of 60 s or more (`-jamSmoke 90`), which walks the whole chain
(each night's scene loads, it's ended as if she got out, Day 2 is skipped to its booth, the end card shows; the log
lines start `JAMKIT_CHAIN`).

## What's carried (the save; `Packages/.../Core/VisitingHours/Chain.cs`)

- **The journal:** `vh_dream_journal` (the house's `DreamJournal.Key`), one comma list. The waking days write their facts
  there at the dive; the nights add their memories and deductions; Day 2 reads it back.
- **Force and Despair:** `Chain.ForceTally` / `Chain.DespairTally` (0..3) count every battle in the argument ledger
  (`vh_arguments`; a battle played again replaces its share), plus the slice's own Force; the dream's Despair flag
  (`vh_despair`: into the rain without the umbrella) is Despair's maximum. `Chain.BattleContext(save, evidence)` is what
  a night's battle starts with; `DreamBattles.Context` reads the tallies.
- **Outcomes:** Walt's in `vh_walt_night1` (the street writes it), Lena's in the ledger (`Chain.BattleResult`).
- **A new game** (Day 1 from the title or a presenter jump) clears all of it: the journal, the tallies, the ledger,
  Despair, Lena's condemning, Theo's answer, Night 1's done flag. (The title's quit piece keeps showing the last run's
  Despair until then.)

## Debug jumps

Presenter panel: "Chain: Night 1 (the street)", "Chain: Day 2", "Chain: Night 2 (the house)", "Chain: the end card"
(any time; the journal as the save has it). In the waking scene, the debug menu's Next scene steps Day 2 → its booth →
the dive, as it does Day 1.

## The days

- **Day 1** (style E: the corridor, the nurses' station, room 14): Rafael's handover; the tutorial duty round is two
  checks (his wristband, his vitals), a bit shorter than the later rounds (the creator); the chart, the effects bag, the
  photo, LeBaget's lilies (a new fact); the police envelope (3:03 Nadia, not answered, as on her phone; 3:06 Lena, 2 minutes; no
  comment); her phone ("Not now."); the booth with Mags, the creator's scene word for word (`STORY-PROSE.md` 83-92; the
  phone-charge ritual dropped), then the journal and the dive.
- **Day 2** (short): Rafael's "Did Lena get out?" (a fact), the LeBaget "Community Partnerships" visitor ("Has he said
  anything?", a fact, no transfer clock), one round of three checks (wristband, the drip, vitals), the chair by his bed
  (talk to him, or just sit), the stairwell, the booth, the dive.

Every line the creator didn't write is marked `PLACEHOLDER` in `Dialogue/VisitingHours.ink` and `SliceContent.cs`.
People are style-E stand-ins until they have models; the ward uses Kenney's furniture where the kit has the piece
(desk chairs, the sink, the visitors' chair, the locker, the booth's lamp, radio, coat stand and boxes).
