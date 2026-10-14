# Skyline Repo M1, round 3 (last): judge A (lighting and sky, materials, art direction)

Pass: `out/skyline/20261014-0625/`. I looked at:
- all 18 stills and the three build shots (now full resolution at the PC quality level);
- every recording frame, through contact sheets (`judge-A/sheet-SkylineArena.jpg`, `judge-A/sheet-SkylineGallery.jpg`),
  plus arena frames 160.7 s and 173.6 s at full size;
- crops in `judge-A/`: `build08-counter.png`, `lift-portal.png`, `acid-gallery.png`.

I also scanned every frame, still and build shot for magenta pixels, measured the highlight levels with PIL (via uv),
and read the lint in `editor-batch.json`, which reports 0 problems. Paths below are relative to the pass folder.

## Round-1 defects

| # | Round 1 | Now |
|---|---|---|
| 1 | Magenta error-shader objects in the build | **Fixed.** No magenta in any build shot. In round 2 the bar was 251 px in `build-shot-08s.png`; at the same warm-up moment it is now 0. The only magenta left anywhere is the purple shop posters (stills 06, 09). |
| 2 | Towers without facades in the build | **Fixed.** |
| 3 | Primitive husks as wrecks | **Fixed.** Each bot leaves a wreck of its own model (still 07; gallery 55-60 s). |
| 4 | Flat acid and bark labels | **Mostly fixed.** The barks are styled bubbles. The acid now has a rippled surface and a dark wet rim (`judge-A/acid-gallery.png`), but it is still a saturated lime that reads closer to turf or moss than to a corrosive liquid. In the arena it looks flatter, and the jet is a bright lime streak (`frames/SkylineArena-160.7s.jpg`). |
| 5 | CONCIERGE's white and gold doesn't read | **Fixed.** |
| 6 | Orange turns yellow in the sun | **Fixed.** The Breacher is orange and the bots are gold (`frames/SkylineArena-160.7s.jpg`). |
| 7 | Skylight glass invisible | **Fixed (modest).** |
| 8 | Stills capped at 223 | **Fixed.** Stills reach 251-255 with p99 220-237. |
| 9 | Repetition | **Mostly fixed.** One green product-shelf atlas still shows in several shopfronts (still 09: HEARTH HOME and the shop beside COBALT DENIM). |
| 10 | Blank warm-up walls | **Fixed.** |
| 11 | Stretched sign edge | **Not seen again.** |
| 12 | Placeholder posters | **Fixed.** |
| 13 | Red patch on the floor | **Fixed.** |
| 14 | Flat-colour bots and Breacher | **Not fixed.** The floor-care units and drone are still untextured flat plastic, and the Breacher is still a blocky flat orange-and-grey box, next to a fully textured Writ and a PBR mall (`frames/SkylineArena-160.7s.jpg`). |
| 15 | Far goal not visible | **Fixed.** The service lift sits under the mezzanine with brushed-steel doors, a gold seam, light strips and a SKY TERRACE / "Lifts · Observation deck" sign, and the recording now ends on a vista (`frames/SkylineArena-173.6s.jpg`) instead of 45 s of solid mint. Two blank panels remain (new defect 1). |
| 16 | Range and gallery untextured, and in the build | **Not fixed.** They are still grey boxes on Unity's default sky. |

## New or remaining defects, most severe first

1. **The lift still carries two blank flat-colour panels.** Above the portal there is a large pale-yellow rectangle with
   nothing on it, and above the doors a small mint-green bar, also blank (`judge-A/lift-portal.png`, still 09,
   `frames/SkylineArena-173.6s.jpg`, where the yellow one fills the left of the frame). The lint passes them, probably
   because they are emissive, but on screen they read as the placeholders of round 2 painted over. The fix is small:
   put the lift's lettering (SKY TERRACE, an up arrow, a floor indicator) on them. *Player:* it's the goal they walk to
   at the end of the floor. *Owner:* arena look (service lift). This is a borderline blocker; see below.
2. **The practice counter reads as a teal shipping crate.** In still 01 and `buildcheck/build-shot-08s.png` (crop
   `judge-A/build08-counter.png`) it is a tall plain teal box wrapped in horizontal roller-shutter slats, with a thin
   gold lip on top. It has no counter top, no trim and no shop identity, and nothing says "climb me" or "counter". It is
   in the first frame of the game, beside the kiosk landmark. *Owner:* arena look (practice counter / lockers).
3. **Toy-flat bots and the Breacher next to PBR.** Carried over from round-1 defect 14.
4. **The acid is still turf-green.** Carried over from round-1 defect 4.
5. **The test scenes are grey boxes.** Carried over from round-1 defect 16. This only matters if players can reach
   those scenes.

**What a player notices in the first five minutes:** the teal crate counter (the first second, in the warm-up), the
toy-flat floor-care units and their lime acid (the food-court fight, around 1-2 min), the flat Breacher whenever it is
drawn, and the blank yellow panel over the lift (the end of the floor, around 3 min). The lighting, sky, glass, white
and gold, signage and shops all hold up in that window.

## Scores (8 is the bar)

| Category | r1 | r2 | r3 | Why |
|---|---|---|---|---|
| Lighting and sky | 6 | 8 | **8** | Crisp sun and skylight stripes, cloud sky, varied hazed towers, visible glass, HDR with bloom, lit shop interiors. The build matches the stills now, at full resolution. |
| Materials | 4 | 6 | **7** | The magenta and the untextured lift walls are gone, the acid is wetter, and the lift is dressed. What keeps it below 8: two blank lift panels, the shutter-wrapped crate counter, flat-colour bots and Breacher, one repeated shelf atlas, and grey-box test scenes. |
| Art direction | 5 | 7 | **7** | The faction colours hold, the route reads from the doorway to the far goal, and the signage system is coherent. Held back by the incoherent teal counter at the start, toy-flat bots against a PBR mall, and the lime acid. |

## Spec 10 blockers I see: 0 confirmed, 1 borderline, 1 conditional

- **Borderline:** the lift's two blank flat-colour panels. The lint passes them, but the spec's test is "an untextured
  placeholder in view", and they read as one. I'd fix them before the creator plays, not ship them.
- **Conditional:** the untextured firing range and gallery, if the player can reach them in the build.

## Not covered by the spec

- **Whether a flat emissive panel counts as "textured".** The lint passes blank lightboxes, and the spec's blocker
  wording doesn't say either way.
- **A bar for prop identity.** A gameplay prop such as the practice counter should read as its real-world object
  (counter, lockers) at a glance, not only be textured.
- **A material bar for bots and viewmodels.** Spec 10.2 says "PBR with normal maps everywhere", but the bots and the
  Breacher were never held to it, only the arena.
- **The look of the test scenes, and whether they ship.** Carried over from rounds 1 and 2.
