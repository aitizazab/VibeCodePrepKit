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
