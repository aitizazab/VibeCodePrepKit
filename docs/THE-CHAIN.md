# The waking days and the chain

The game from the title to the end card (plan: `docs/NIGHTS-1-2.md`). Built 2026-10-12 by the opening agent.

```
opening → title → Day 1 → 11 PM booth → Night 1 (DreamStreet) → Day 2 → booth → Night 2 (DreamStreet, then DreamHouse) → end card
```

## What plays where

| Segment | Scene | Code |
|---|---|---|
| Day 1, its booth | `VisitingHours` | `VisitingHoursGame` (`BeginDay`, `GoToBooth`), the ward `Scripts/World/VhWakingWard.cs` |
| Night 1 | `DreamStreet` (the movement agent's hill street) | `DreamStreetGame`; its `NightEnded` hands back |
| Day 2, its booth | `VisitingHours` | `BeginDay2`, `SliceArea.HospitalDay2` / `BoothDay2` |
| Night 2 | `DreamStreet` told it's Night 2 (Rowan Street, outside the house), then `DreamHouse` (the house agent's) when she reaches the gate | `DreamStreetGame` (`NextNight = 2`) loads the house itself; `DreamHouseGame`'s `Escaped` hands back |
| The end card | `VisitingHours` | `ChainEnd` (a placeholder card: Walt, Lena, Force, Despair, the journal); thrown out of the house first: the booth plays the house's lines (`booth_thrown_out`) |

`Scripts/Chain/VhChainRunner.cs` (on the GameRoot, added by `VhBoot`) loads each night and brings the waking scene
back. A night's scene is the first of `Chain.Night1Scenes` / `Night2Scenes` in Build Settings; with none, Day 1's booth
dives into the slice's own street and Day 2's booth goes to the end card. Any other scene can end its segment with
`Chain.Done()` (or `Chain.ForceOut(reason)`: the night ends with its cost kept).

**The hill plays both nights** (World's request, 2026-10-13): `Chain.Night2Scenes` is `{ "DreamStreet", "DreamHouse" }`.
Whenever the runner loads the hill (the dive, or a presenter jump) it says which night first: `DreamStreetGame.NextNight`
is 1 for Night 1 and 2 for Night 2 (`Chain.NightOnTheHill`; used once). On Night 2 she starts outside the house and
reaching the gate loads DreamHouse (the street does it: nothing listens to `Night2AtTheDoor`); the house's `Escaped`
(or `Chain.ForceOut`) ends the night. Without the hill in the build, Night 2 is the house alone.

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
- **Lena's keepsake** (Heard out): her star-shaped watch on Nadia's right wrist. `Chain.HasLenaWatch(save)`: the flag
  `vh_lena_watch`, the keepsake's journal entry, or Lena's battle in the ledger ending with a keepsake. The ward shows it on
  Nadia's arm at the bedside (`VhBedside`, People's `VhKeepsakeWatch`); the hill can ask the same question
  (`DreamStreetGame.SetKeepsakeOwned`).
- **A new game** (Day 1 from the title or a presenter jump) clears all of it: the journal, the tallies, the ledger,
  Despair, Lena's condemning, Theo's answer, Night 1's done flag, the thrown-out flag (`vh_n2_thrown_out`) and Lena's
  watch (`vh_lena_watch`). (The title's quit piece keeps showing the last run's
  Despair until then.)

## Debug jumps

Presenter panel: "Chain: Night 1 (the street)", "Chain: Day 2", "Chain: Night 2 (the house)", "Chain: the end card"
(any time; the journal as the save has it). In the waking scene, the debug menu's Next scene steps Day 2 → its booth →
the dive, as it does Day 1.

## The days

Redesigned 2026-10-12 (the Day agent): **docs/redesign/day.md** has the design (the bigger floor, Ward 4 West; the duty
round as small gameplay; the finds; the animated bedside; what she tells him and how it changes the next night), the API
the nights read (`Chain.HasDreamChange`, `Chain.DreamChangesFor`, `Chain.HasTold`), the forced-out hook
(`Chain.ForceOut`), the look and the captures (`-ward-shot`, `-day-run`).

- **Day 1:** Rafael's handover starts the round (three tasks); room 14 (the chart, the effects bag, the photo, the
  lilies), the police envelope, her locker (the creek photo), her phone ("Not now."), June Brandt, the remembrance book;
  the bedside (two things, or just sitting); the stairs (only after the round); the booth with Mags, the creator's
  scene word for word, then Mags on what his monitor did, the journal and the dive.
- **Day 2:** the round (five tasks), Rafael's "Did Lena get out?" in the staff room, Mr. Brandt, the woman from LeBaget
  (Corinne Vasseur) in the family room, the bedside, the stairs, the booth. If Night 1 forced her out, the day opens on the
  crash call.
- **Thrown out of Night 2** (the house's `vh_n2_thrown_out`): before the end card she comes to in the booth's chair, Mags
  beside her, and the house's lines play word for word (`DreamHouseLines.ThrownOutBooth` in the ink's
  `booth_thrown_out`, checked by `VisitingHoursInkTests`), with the costs on screen. The cost itself is
  `Chain.ForceOut`'s (Despair a step, one fewer of Mags's nights).

**Sound at the hand-offs:** the runner stops the ambience and the music when a night loads and when the ward comes back;
the title stops the ambience and asserts its own music when it loads (a battle torn down on quit used to hand the world's
sound back into the title).
