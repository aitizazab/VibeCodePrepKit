# Work log

A running record of the work and the process, newest night on top, so nothing is lost if a Claude session is
compacted or restarted. Claude adds to it as each agent reports and after each verified step. Per the creator's
request (2026-10-10), the **morning report** is built from this log and must be complete on its own: no
"scroll up to find it".

---

## Night of 2026-10-10 → 11

### State at bedtime (about 06:00, 2026-10-10)
**The creator's decisions tonight** (also in `VISITING-HOURS.md`):
- **Structure:** five nights, of different lengths, and play time varies with each player's choices.
- **Threads hinted, not resolved:** Lena missing, the LeBaget/Larpaca crossfire, Elias's money.
- **Endings:**
  - the **Long Night** stays;
  - **Morning** has two paths: **forced** and **chosen** (the old "His Choice", now shown on screen);
  - **Evening**.
- **Argument battles rebuilt from the ground up:** unique art and arenas, stylised icons, no box of rectangles,
  and arguments that make logical sense.
- **The second corporation is Larpaca** (checked: no real company by that name). The news read uses it.
- **Lena:** blonde with long loose waves (she looked too much like Nadia in the bun).
- **The dream versions of characters shouldn't feel overwhelmingly wrong;** billboards against 3D is eerie enough.
- **The bedroom must be a real stylised 3D room,** not gritty or realistic.
- **Tripo credits approved:** Nadia's model done (80 credits, 550 left).
- **Battery:** the laptop stays plugged in; never worry about brief unplugging.
- **Pace:** the creator found today slow. Gameplay is what players remember, then the late game and endings.

**Done today (verified, committed, pushed unless noted):**
- **Presenter mode and footage capture:** all five templates build and pass smoke runs (EditMode 456/456,
  PlayMode 73/73).
- **The movement research** (`RESEARCH-MOVEMENT.md`): mechanics, Source formulas, controller choice, a blueprint,
  chase design, feel (section 8), and **measured Titanfall 2 numbers** (section 9) transcribed from community
  videos:
  - wallrun goal speed 8.64 m/s (31 km/h), max 1.75 s;
  - wall jump 5.21 m/s off the wall, plus up to 1.91 m/s along it, plus up to 4.37 m/s inward;
  - a wall kick within 83 ms gains about 3.1 m/s;
  - crouch kick +1.78 m/s;
  - slide boost cooldown about 2 s;
  - the "lean-in" hand on the wall is the timing cue.
- **Art:**
  - the style exploration (A–E), with **E chosen**;
  - style-E character sheets for Nadia, Elias, Lena, Theo, Mags and Walt (review page
    `FableAnimationTests/art/review/2026-10-10/visiting-hours-characters.html`).
- **Audio:**
  - the creator's voicemail (the earlier take) is in the game;
  - Nicole reads the news report with "her condolences" and "Larpaca".
- **Docs:**
  - `PLAN.md` rewritten;
  - `VISITING-HOURS.md` (the source of truth);
  - `CHARACTERS.md`;
  - `OPENING-AND-TITLE.md` updated;
  - status headers on superseded docs;
  - a README index of every doc.
- **The Rocket League kick:** Easy Anti-Cheat's download timed out at launch (about 120 KB/s), so it ran with no
  anti-cheat module. A temporary connection problem; the connection tested fine afterwards. ExitLag is a
  possible cause but unproven.
- **The battery:** the 80% cap reset after the battery drained to about 2% on 2026-10-09. The fix is in Legion
  Toolkit.

**Running overnight** (results are added below as they come in):
1. **Movement Lab:** round-1 fixes (respawn test timing, the wall catch at high frame rates, corner normals in
   Unity) being verified in the clean checkout `VibeJamKit-play` at 613473c. If the smoke run passes, the
   playable build goes to `VibeJamKit-play/Builds/Play/MovementLab/VibeJamKit.exe`.
2. **The first escape sequence:** a 60–90 s chase through the 3D dream street. The agent builds in
   `Assets/JamKit/Templates/VisitingHours/Dream/`.
3. **Design docs:** `STORY-BIBLE.md`, `CHARACTER-BIBLE.md`, `NIGHTS-GAMEPLAY.md`, `ARGUMENT-BATTLES.md`.
4. **The opening rebuild** in style E: a 3D bedroom, the shot list, the table title, Nadia's model.
5. **Art:** Lena redone; the opening's concept frames and textures.

**Reminders for the morning (first thing):**
1. **The battery cap:** Lenovo Legion Toolkit → Battery → Conservation on, then set it to start with Windows.
2. **Clean the mouse** with compressed air and WD-40 contact cleaner.

### Overnight results
(added as each piece of work reports)

#### Movement Lab: verified, playable build ready (about 07:00)
- **Run in** the clean checkout `VibeJamKit-play` at commit 613473c.
- **Scenes:** OK.
- **PlayMode:** 78 of 79 pass.
- **The real character controller now does what the simulation did.** The momentum chain keeps its speed:
  sprint 9.8 → slide 12.7 → air 11.3 → wallrun 11.3 m/s. The autopilot finishes the whole course.
- **Round-1 fixes that got there:**
  - wall probes read the wall face's own normal (a rounded corner normal peeled runs off at 45°);
  - the autopilot holds a wall's lane until its wall jump (at high frame rates it steered away just before
    contact);
  - the wall catch forgives a view about 12° off;
  - the tests wait for a respawn to really finish.
- **The one remaining failure:** after falling off the course, you respawn at **2.5 m/s instead of sprint
  speed (over 5)**. Sent back to the movement agent, along with a note that a mantle drops you to 0 m/s
  (Titanfall keeps some momentum).
- **Smoke run:** PASS (9.2 s).
- **Playable build: `C:\Users\User\Desktop\yougotgamesonyophone\VibeJamKit-play\Builds\Play\MovementLab\VibeJamKit.exe`**
  - **The course (about 500 m):** a run-up, a slide ramp, a bar to slide under, 5.5 m and 8.5 m gaps, wallrun
    walls of 8, 14 and 21 m, a momentum chain, a wall-jump chimney, mantle ledges of 1.0, 1.5 and 1.85 m, and a
    chase (a wall of dream-static about 12 m behind, two collapses, checkpoints).
  - **Controls:** WASD, mouse, Space (jump and double jump), Left Ctrl / C (crouch and slide; gamepad B),
    Shift is not needed (sprint is automatic). **F3 toggles the speed readout** (m/s and km/h, state, an 8 s
    speed graph, the chase gap). Esc pauses, with comfort settings (FOV, camera roll, head bob, speed effects,
    speed lines). Ctrl+Shift+P is presenter mode, with chapters: course start, wallruns, momentum chain,
    chimney, chase.
  - **Placeholders:** the arms are capsule-shaped, and all sounds are generated.

#### Movement Lab round 2: all green (about 07:45)
- **The slow respawn wasn't a game bug.** The respawn does launch you at sprint speed, but the test measured
  after the fade with no input held, by which time friction had stopped the player. The test now holds
  forward like a player and records the launch speed.
- **Mantles keep 80% of your run's speed,** like Titanfall's. Before, hitting the ledge face clipped your speed
  first, so you left at the 5 m/s minimum. A short jump at 9.5 m/s rescued by a mantle now leaves at 7.3 m/s
  or more.
- **Results:** PlayMode 79/79, Windows build OK, smoke run PASS (9.2 s). The playable build is refreshed (same
  path: `VibeJamKit-play\Builds\Play\MovementLab\VibeJamKit.exe`).
- **The Movement Lab's generated scene, materials, tuning data and config are now committed** to the main repo
  (they had only existed in the clean checkout).
