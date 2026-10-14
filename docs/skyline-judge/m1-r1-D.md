# Skyline Repo M1, round 1: judge D (enemy readability, motion, encounter fairness, consequence)

Pass: `out/skyline/20261014-0326/`. My extra frames and contact sheets are in `out/skyline/20261014-0326/judge-D/`
(`gal-<t>.jpg`, `ar-<t>.jpg`, `sheet-*.jpg`, `gallery-events.json`). Gallery times below are recording seconds
(recording t = game t - 0.821, from `SKYLINE_GALLERY start t=0.821`). The logged telegraph and attack times line up with
the audio onsets to within 30 ms (drone telegraph at 3.67 s, attack at 4.03 s; spotlight telegraph at 27.54 s, lock pip
at 28.16 s, shot at 28.34 s), so I trust the mapping.

## Defects, most severe first

1. **A wreck is the same generic grey blob for all four bots, and the "error face" is never shown.** `SkylineBot.WreckNow`
   calls `_face.Show(BotFace.Wrecked)` and then `Rig.gameObject.SetActive(false)` in the same frame. The bot's whole
   body and the spotlight's stand disappear, and `SkylineWreck.Create` puts down one squashed sphere, a cube "panel" and
   a cube "strut" in their place. Evidence: `still-SkylineGallery-*.png` (all five stills show the same black lozenge
   for the drone, the floor-care unit, the spotlight and the gondola), `frames/SkylineGallery-023.2s.jpg`,
   `judge-D/gal-48.3.jpg` and `SkylineGallery-059.4s.jpg` (the 2.2 m gondola platform becomes a 1 m grey lozenge on the
   floor, under a bark bubble at an empty rail), and `SkylineGallery-033.6s.jpg` then the wide still (the spotlight
   head and its stand become a lozenge on top of the pillar). Why it matters: the creator's call (spec section 14) is
   "Death: an error face ... the hulk stays as the wreck". Every bot in SKYLINE-BOTS.md has a designed wreck that
   nobody ever sees: grey X X eyes, a cracked screen, the canister bursting, the gondola platform dropping. A player
   can't tell which bot a husk was, so a wreck can't show you which scrap is worth stripping. Owner: all four bots
   (shared wreck code).
2. **Floor-care units pile into each other and interpenetrate in the skylight fight.** Three units merge into one blob
   at the player's feet, one riding up on another's lid, with their faces and canisters overlapping: `judge-D/ar-67.5.jpg`
   (full resolution), `ar-63.jpg`, `ar-64.jpg` and `frames/SkylineArena-065.0s.jpg`. There is no separation between
   units. Why it matters: it breaks the identity of each bot (which face belongs to which?), makes plates and spots
   impossible to target, looks broken (bar 6, interpenetrating meshes), and stacks three acid sweeps on one spot. Owner:
   floor-care unit / arena encounters.
3. **In the arena recording you almost never see a bot do its thing.** About half of the fight frames are the camera
   pressed against cover or a wall (`frames/SkylineArena-085/105/115s`, `judge-D/ar-34, 37, 84, 87, 90, 93, 99, 102,
   114, 117, 119.5`). In the food court, the drones are 25-40 px dots across the hall (`judge-D/sheet-arc.jpg`), with
   faces and telegraphs unreadable at that size. In the skylight fight, no spotlight beam appears in 12 sampled frames
   (`judge-D/sheet-sk.jpg`): CONCIERGE's spotlight notice plays over a screen full of floor-care units. In the facade
   fight, the gondola never appears on screen at all (`judge-D/sheet-fa.jpg`), and its safety notice plays at 79 s over a
   wall. Why it matters: the creator judges from this recording and already said "the drones don't even seem to move".
   This recording will repeat that verdict whatever the bots actually do. Owner: demo player/capture.
4. **The drone's face is hidden under its own cap.** At the gallery's eye-level camera, the gold visor brim covers the
   top of the face screen, so the face reads as a black band with a hint of blue or red (`judge-D/sheet-dronec.jpg`,
   1.2-4.6 s; `frames/SkylineGallery-002.6s.jpg`). The bored smirk and REC light, the raised brow with the "O", and the
   red crosshair eyes are not legible. A player sees drones from below or level, which is the worst angle for this. Owner:
   mall-cop drone (model/face placement).
5. **Attacks and debris are bare primitives that contradict their identity sheets.** The drone's "taser bolt" is a lit
   orange capsule (`SkylineBolt.cs`, `Slug` colour, `PrimitiveType.Capsule`) that reads as a floating sausage
   (`judge-D/arc-46.jpg`). It has no arc and no taser look. The floor-care "low green jet" is a lit cube, a solid
   laser-straight green bar (`frames/SkylineGallery-018.1s.jpg`, `judge-D/ar-67.5.jpg`), and its puddles are perfect flat
   discs. Wreck debris is plain white and grey cubes (`frames/SkylineGallery-012.9s.jpg`, a blue cube floating at
   `frames/SkylineArena-065.0s.jpg`). Why it matters: you can't read "taser" or "acid spray" from these shapes, and flat
   untextured primitives in view are on the spec 10 blocker list. Owner: mall-cop drone, floor-care unit, shared wreck
   code.
6. **Several idle behaviours are invisible in the evidence.** (a) The spotlight's "slow scan left and right": its head
   faces the camera unchanged in `judge-D/sheet-spotc.jpg` across 25.1-26.9 s, though the code has an idle scan
   (`SkylineBot.cs:1241`). (b) The gondola's 2 m ride with a stop at each end never shows: the gallery gives every bot
   `IdleSeconds = 2f` (`SkylineGalleryDirector.cs:44`), too short for the ride, and the gondola sits still at the rail's
   end through 37.3-38.6 s (`judge-D/sheet-gond.jpg`). (c) The floor-care unit mostly turns in place on its mat
   (13.3-14.7 s, `judge-D/sheet-floor.jpg`) instead of making its short runs. Verified working: the drone's hover patrol
   (it drifts about 30-45 px at 1080p between 1.2, 2.2 and 3.4 s and banks into a strafe at 4.15 s, `sheet-dronec.jpg`),
   the gondola riding once alert (39.6-40.3 s), and the floor-care unit driving at the dummy (15.4 s). Owner: the
   spotlight, the gondola, the floor-care unit, and the gallery director.
7. **The boost ledge's scrap cache always gives +0.** `SkylineScrapCache` only refills missing plating
   (`room = PlatingMax - Plating`), and nothing in the warm-up hurts you, so anyone who follows Dot's lesson gets nothing
   and the cache is used up: `stage-b-capture.log` reads `SKYLINE_ARENA scrap-cache plating +0`. Dot promises "There's
   scrap on top of that kiosk". The optional harder line's reward (bar 4) is invisible. Per the creator's call, excess
   should bank as recovered scrap, but the cache never passes the excess on. Owner: arena layout/encounters.
8. **Damage taken reads weakly.** `SkylinePlayer.TakeAttack` only updates a direction wedge and the plating flash. There
   is no player-side hurt sound, camera kick or vignette (a grep finds no vignette or hurt sound). The two HUD bars
   (bottom-left, about 300 px) are the only steady read; the plating bar is almost empty in `judge-D/ar-67.5.jpg` while
   nothing else on screen says you are in danger. Owner: HUD/presentation, audio.
9. **There is no damage wear on a live bot.** The creator's call (14): "the chassis shows wear ... smoke at low
   chassis". None appears in any gallery frame between the plate sheds and the wreck (`SkylineGallery-007.7s`, `023.2s`,
   `033.6s`). Plates do shed well (see the strengths below). Owner: all bots.
10. **The skylight fight's spotlights sit in one forward arc.** From `HallEntry` (0, -18), the spotlights at (±12.5,
    12.3) are at bearings of about ±22°, only 45° apart (`Encounters.cs`, `Skylight()`). The 90° rule is met only
    through the floor-care units at ±64°. The fight's signature threat therefore always comes from the front. This is a
    note, not a rule break. Also, the food court and the skylight both leave the walls open, so the open layer does
    not change between consecutive fights (2.6). Owner: arena layout/encounters.
11. **Gallery capture faults.** The five gallery stills are taken after the run, so they show only wrecks, never a live
    bot. The last 13 s of `rec-SkylineGallery.mp4` (49-62 s) show a husk with no audio events after 48.56 s
    (`judge-D/gallery-events.json`). The "wall-run" dummy is an upright capsule floating flat against the glass
    (`frames/SkylineGallery-043.9s.jpg`). The drone segment's camera puts a pillar in front of the drone's wreck
    (`still-SkylineGallery-drone.png`). Owner: demo player/capture.
12. **Bark bubbles are skewed and cut off.** "Out of... order..." is drawn tilted in world space, and a second bubble is
    cut off at the frame edge ("Ou..."): `frames/SkylineArena-065.0s.jpg`, `judge-D/ar-66.jpg`. Owner: HUD/presentation.

## What works (checked, not assumed)

- **The four bots are told apart at a glance up close.** Each has a distinct silhouette in CONCIERGE white and gold
  (`judge-D/sheet-drone`, `sheet-floor`, `sheet-spot`, `sheet-gond`): a flying egg in a peaked cap, a disc with a tilted
  screen and a green canister, a searchlight drum on a levitating stand, and a railed platform with buckets.
- **The faces change state on cue.** The floor-care unit goes happy, then alert, then angry (13.3, 15.4, 16.4 s), and
  the spotlight goes from crescent to wide eye to red crosshair (`sheet-spotc`).
- **Telegraph timings match the identity sheets exactly** (from the `SKYLINE_GALLERY` telegraph and attack times):
  drone 0.35 s, floor-care 0.6 s, spotlight 0.8 s, gondola squeegee 0.4 s. The spotlight's beam goes red and thin, then
  white and wide at the lock (27.75, 28.0 and 28.2 s), with the lock pip on time.
- **Plates fly off where hit, and read well:** the drone's crown and visor (7.7 s), the floor-care lid and bumper
  (23.2 s), the spotlight's housings (33.6 s), the gondola's buckets (46.0 s).
- **Aim models never mix.** The food court is drones (leads you) plus floor-care (area), the skylight is spotlights
  (where you are now) plus floor-care, and the facade is gondolas plus drones (`Encounters.cs`). No encounter combines
  "leads you" with "where you are now".
- **Each bot's first appearance gets CONCIERGE's safety notice** (`SKYLINE_LINE concierge-drone`, `-floor-care`,
  `-spotlight`, `-gondola`), and Dot's "Strip it! Scrap's armour!" plays on the first plating break.

## Scores (8 is the bar)

| Category | Score | Why |
|---|---|---|
| Enemy readability | 4 | Strong bodies, faces and telegraphs up close, but the wrecks have no identity, the error face is never shown, the drone's face is hidden, the attacks are primitives, and in the arena the bots are tiny or piled up |
| Motion | 4 | The drone's hover and the alert behaviours work; the spotlight's scan and the gondola's idle ride are invisible, and the floor-care units interpenetrate |
| Encounter fairness | 6 | Aim rules kept and telegraphs timed to spec, but the floor-care units collapse onto the player in the skylight fight and 12 drones' 0.35 s red-dot telegraphs can't be read at food-court range. Counters are M3, so not judged |
| Consequence | 5 | Plates fly, strips give plating and Dot speaks, but the cache gives +0, taking damage barely registers, no wear, and the wrecks all look alike |

## Spec 10 blockers I see

1. **An untextured placeholder in view (probable):** the wreck husk (a flat grey sphere and cubes), the white and grey
   debris cubes, the orange capsule bolt and the green cube acid jet are flat-colour primitives in view during play
   (`frames/SkylineGallery-012.9s.jpg`, `frames/SkylineArena-065.0s.jpg`, `judge-D/arc-46.jpg`).

Not blockers, but bar 6 failures: the floor-care units interpenetrating each other (defect 2).

Blocker count: 1.

## Not covered by the spec (gaps to raise)

- There is no rule that bots of one kind keep apart (separation or steering) or a cap on how many melee or area bots
  can converge on one point.
- There is no minimum on-screen size or legibility distance for faces and telegraphs, so a 0.35 s telegraph on a bot
  25 m away passes every written check.
- There is no rule for what a wreck must keep of the bot's silhouette (the identity sheets each describe a wreck; the
  code replaces them all).
- There is no player-side "you were hit" audio or visual spec beyond the direction wedge.
- There is no rule on what an optional reward does when plating is already full (the cache gives +0).
- There are no rules for the gallery capture: how long each state is held (2 s idle can't show a 2 m ride) and when the
  stills are taken (they show wrecks).
- The demo player's camera behaviour: nothing requires it to look at the threat it is fighting, so the recordings
  can't serve as evidence of enemy readability.
