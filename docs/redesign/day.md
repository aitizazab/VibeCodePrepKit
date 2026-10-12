# The days, redesigned (Day agent, 2026-10-12)

The creator on the old days: *"a joke. The space is small, uninteresting to explore, and even interacting with everything
takes less than a minute ... There's no purpose to this interaction, it doesn't change anything in the story ... it's just
text, with nothing changing on screen."* This is the redesign and how it's built. The bar is REDESIGN.md section 3.

## 1. What a day is for

**The day is where Nadia prepares for the night.** It's her late shift on Ward 4 West (3 PM to 11 PM, the sun low in
the west). Every find gives the night something: a journal entry the night's battle can use as evidence, or a
**topic** she can bring to Elias's bedside. What she tells him at the bedside **changes the next night** (the dream hears
it). The duty round is the day's small gameplay: real nursing decisions with a right answer and a slip she catches,
and the slips wear her down (the Long Night's slope).

## 2. The floor (Ward 4 West, 48 x 23 m)

![The floor plan](day-floor-plan.png)

| Area | What's there | Why it earns its place |
|---|---|---|
| **Lift lobby** (spawn) | the lifts, the floor's directory, the WARD 4 WEST sign, a bench | arrival: the corridor and its sunlit end window are the first view (the landmark) |
| **Corridor** (48 m) | rooms 11 to 16, stairwell B, handrails, a noticeboard, a linen trolley, a wheelchair; **the tall west window** pouring the afternoon sun down it | the spine; the window is the vista ("you can see all of Larch Hill from here") |
| **Nurses' station** | the counter and Rafael (Day 1), the **round board** (Rafael's handwriting), the police envelope, monitors, the clock | the handover starts the round |
| **Room 14** | Elias: the bed, the live monitor, the drip, the chart, the photo, the lilies, the effects bag, **the chair** | the finds of the first day; the bedside |
| **Room 12** | Mrs. Okafor (Larch Street, number 9; "the boy who fixed my gate with a bicycle chain") | a round patient (meds on Day 1; obs and the penicillin catch on Day 2) |
| **Room 16** | Mr. Brandt (asleep on Day 1; awake on Day 2) | Day 2's band check; "too stubborn to die" |
| **Staff room** | Nadia's locker (**the creek photo**), the rota (seven lates in a row), her phone ("Not now."), the kettle, Rafael on his break (Day 2) | the creek topic; "Did Lena get out?" |
| **Quiet room** | candles, **the remembrance book** (Theo's team: "We kept your shirt. Number 4.") | Theo's topic |
| **Family room** | the muted TV ("ROUTE 9: SIX WEEKS ON, NO ARRESTS"), the vending machine, **June Brandt** knitting (Day 1), **the man from LeBaget** (Day 2) | the patients topic; the visitor |
| **Stairwell B** | down to the booth at 11 PM (only after the round) | the exit |

**Minutes:** Day 1 about 7 to 10 minutes played normally (the handover, a three-task round, about twelve finds, the
bedside); Day 2 about 6 to 9 (a five-task round, Rafael, Brandt, the visitor, the bedside).

## 3. The duty round (the gameplay)

The handover (Rafael, Day 1; the day's opening on Day 2) puts the **round sheet** on screen (top left; each task ticks
off as it's done). Each task is a decision in her hands, with one right answer and one or two tired shortcuts she catches
herself taking (a **slip**). Day 1's round is the tutorial, three tasks; Day 2's is five (the creator: "a bit shorter").

| Day | Task | The decision | The slip |
|---|---|---|---|
| 1 | Room 14: wristband against the chart | they match | "skip it, it's been checked twice" |
| 1 | Room 14: observations | chart them as they are | "copy last night's line" |
| 1 | Room 12: Mrs. Okafor's six o'clock | two 500 mg tablets with water | the 1 g infusion bag; codeine |
| 2 | Room 16: Mr. Brandt's band against his drug chart | the numbers don't match: hold his tablets, ring pharmacy | "close enough" |
| 2 | Room 12: Mrs. Okafor's obs (38.4, pulse 96) | call the doctor now | "recheck in an hour" |
| 2 | Room 12: her antibiotic | hold it: the red band, penicillin | give it, it's prescribed |
| 2 | Room 14: the drip | sodium chloride 0.9% | glucose 5% (same bag, same size) |
| 2 | Room 14: observations | chart them | "copy yesterday's line" |

**Consequences:** the round must be done before the bedside and the stairs. A slip is always caught (no one is hurt),
but it counts: Mags sees it in the booth ("your hands are shaking"), and three slips across the run are
one step down the Long Night's slope (`Chain.DespairTally`).

## 4. The bedside (shown, and consequential)

The chair by his bed, after the round. In first person: she sits (the view glides to the chair and lowers, turns to him;
the room softens behind his hand), **her hand comes into frame and takes his**, and she talks. She can tell him **two
things a day** (then "His heart's up. That's enough for one day."), or just sit. His body answers on screen: **his
fingers move against hers**, or **the monitor climbs** (the numbers and the beeping quicken), or, when she just sits,
**it settles**. When she's done, "E  Stand up": she lets go and rises.

What she can tell him depends on what she found. **What she tells him changes the next night:**

| Day | Topic | Opened by | His body | The night it changes (`DreamChanges` id) |
|---|---|---|---|---|
| 1 | Walt's shop | his effects bag (the keyring, Walt's card) | fingers | Night 1: **Walt's ledger lies open on his workbench** at the page of 41 identical lines (`n1_walt_ledger`) |
| 1 | The creek, and the bikes | the photo in her locker | heart | Night 1: **the old plank is back across the creek**, a short cut at the foot of the hill (`n1_creek_plank`) |
| 1 | His patients ask after him | June Brandt in the family room | heart | Night 1: **the hospital at the top of the hill has its windows lit** (`n1_hospital_lit`) |
| 1 | The phone | the police envelope | fingers | Night 2: **she's already said sorry out loud**: Lena's "He called you first" lands on an admission that's open from the start (`n2_lena_sorry`) |
| 1 | Just sit with him | always | calm | Night 1: **Walt greets her warmly, by name** (`n1_walt_warm`) |
| 2 | The lake, last summer | the photo on his locker (Day 1) | heart | Night 2: **the lake photo is up on the hall's photo wall**, lit, and counts as found (`n2_photo_wall`) |
| 2 | Theo's team | the remembrance book | fingers | Night 2: **Theo's room is open from the start**; Theo trusts her sooner (`n2_theo_room_open`) |
| 2 | Lena | Rafael: "Did Lena get out?" | heart | Night 2: **Lena is waiting at the window**; her anger starts a step higher (`n2_lena_wary`) |
| 2 | Just sit with him | always | calm | Night 2: **the house is gentle**; Lena's anger starts a step lower (`n2_lena_softer`) |

There's always more to tell than time to tell it (four topics for two places on Day 1, three on Day 2), so it's a
choice. What she told him is written in her journal ("I told him about Walt's shop... If he heard me, it'll be in there
tonight."), and Mags says what the monitor saw in the booth ("His heart did something this evening... Whatever you told
him, it's in there now. Watch for it.").

### The API the nights read (World and House agents)

In the core (`Packages/com.vibejam.jamkit/Runtime/Core/VisitingHours/`): `DayContent.cs` and `Chain.cs`.

```csharp
var save = GameRoot.Save.Data;
if (Chain.HasDreamChange(save, DreamChanges.WaltLedger)) { /* put the ledger out on the bench, open */ }
foreach (var id in Chain.DreamChangesFor(save, ChainSegment.Night2)) { /* every Night 2 change */ }
Chain.HasTold(save, "theo");        // the topic itself, if a line wants to know
DreamChanges.Find(id).what          // the change in words
```

Ask when the night builds. The ids are stable; an id a night doesn't know is ignored. A new game clears them
(`Chain.Reset`). The battles can also read the "told" journal entries (`told_<topic>`, in `Chain.Known`).

**Wired by me (data the game already reads):** the journal entries, the booth's reaction, the run's Despair and the
monitor's day-to-day state. **To wire in the nights (owners):** the street's ledger, plank, lit hospital and Walt's
greeting (World); the photo wall, Theo's room, Lena at the window (House); Lena's starting anger and the sorry admission
(Battle: `Chain.HasDreamChange(save, DreamChanges.LenaWary / LenaSofter / LenaSorry)`).

### Forced out of the dream (the House agent's hook)

```csharp
Chain.ForceOut("the house tipped her out");   // instead of Chain.Done() when a night throws her out
```

The night ends as with `Done`, and it costs: the forced exit is kept (`Chain.WasForcedOut`, `ForcedOutCount`,
`ForcedOutReason`); the next day opens on the crash call ("They called a crash on him at four this morning."), his
monitor runs higher (74 resting), Mags says "five nights, now. Not six." (`Chain.NightsLeft`); and each forced exit is
one step of Despair (`Chain.Strain` in `DespairTally`). The runner (`VhChainRunner.ForceOutNow` for tests) handles it.

## 5. The look

Every surface is a PBR material (URP/Lit: albedo, OpenGL normal map, a metallic/occlusion/smoothness mask) from CC0
ambientCG textures, tinted per surface (`VhWardLibrary`, built by *JamKit > Visiting Hours > Build the Ward Materials*
and by every *Rebuild Shell Scenes*; licences in `Templates/VisitingHours/Ward/LICENSES.md`). Signs, notices, the rota,
the round board, the TV, the remembrance book and the vending machine are printed/handwritten pictures (Inter and
Caveat, OFL). The windows show a CC0 HDRI sky (Poly Haven). The light (`VhWardLight`): a late-afternoon sun 24 degrees
up from the west-south-west, pouring down the corridor from the end window and through the south windows; cool
fluorescent panels; trilight ambient; six realtime box-projected reflection probes; ACES, bloom, warm white balance, a
light vignette; a shallow focus at the bedside. The booth at 11 PM gets the night version. When the Look agent's
hospital-day rig lands (docs/redesign/look.md), the ward can swap to it.

**People** (`VhWardPeople`): the People agent's models are placed by id from `Resources/VhPeople/<id>` (rafael, mags,
june_brandt, okafor, brandt, lebaget_visitor, waiting_man, elias); the prefab gets `SendMessage("SetPose", "Standing" |
"Sitting" | "InBed" | "LyingInBed")`. Until they land, stand-ins in their clothes (PBR fabric and skin), standing,
sitting or in bed. Elias's near hand is a real hand with fingers (`VhHandRig`) so it can answer her.

## 6. The lines

Every line of the days is in `Assets/JamKit/Templates/VisitingHours/Dialogue/VisitingHours.ink`. The creator's Mags
scene (STORY-PROSE 83-92) is kept word for word; everything else on the days is mine, in the voice of their lines
(short, dry, tired, proud; Mags brisk and warm). Rafael: the handover, Day 2's "Did Lena get out?". Mrs. Okafor, June
and Harold Brandt, the man from LeBaget (Daniel Arce, "Community Partnerships", "Has he said anything?", no transfer
clock). The round's decisions and slips. The nine bedside topics. Mags's booth reactions.

## 7. Captures for the judge (main runs them)

- **Stills:** `VisitingHours.exe -ward-shot "<folder>" -screen-width 1920 -screen-height 1080 -screen-fullscreen 0`
  (or the editor with the same arguments): 18 framed stills of every area on both days and the booth (UI hidden),
  9 bedside frames (sitting, her hand coming in, the topics, his fingers, the second topic, his heart on the monitor,
  "E  Stand up", rising, standing), then one still per examinable on each day. `shots.txt` lists them.
- **A recorded day:** `VisitingHours.exe -day-run "<folder>" [-day-run-day 2] -screen-width 1920 -screen-height 1080
  -screen-fullscreen 0`: the autopilot plays the whole day at real speed on the real controller (walking the ward,
  every find, the round, the bedside, the stairs, the booth to the dive), a JPG frame every 0.5 s and `run.txt`.

## 8. Built in

`Scripts/World/VhWakingWard.cs` (the kit, the booth), `Scripts/World/VhWardFloor.cs` (the floor), `Scripts/Waking/`
(`VhWardLibrary`, `VhWardLight`, `VhWardPeople`, `VhHandRig`, `VhPatientMonitor`, `VhBedside`),
`Scripts/VisitingHoursGame.Day.cs` (the round, the bedside, the ink's `vh_state` and `vh_topic`), `Scripts/Chain/`
(`VhWardShot`, `VhDayRun`, `VhChainRunner`: forced out, sound hand-offs), the core's `DayContent.cs` and `Chain.cs`, and
the shell's title sound fix (`GameRoot`: the ambience and a battle's restored music no longer carry into the title).
