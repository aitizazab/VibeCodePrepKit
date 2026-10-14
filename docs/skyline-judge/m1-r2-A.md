# Skyline Repo M1, round 2: judge A (lighting and sky, materials, art direction)

Pass: `out/skyline/20261014-0520/`. I looked at all 18 stills and the three build shots. I looked at every recording
frame through contact sheets (`judge-A/sheet-*.jpg`), plus frames 6.1, 115.3 and 125 s at full size, extra arena
frames at 118, 122 and 125 s, and crops (`judge-A/build08-beam-crop.png`, `judge-A/build20-sky-crop.png`). I measured
pixel values and highlight levels with PIL (via uv) and scanned every frame, still and build shot for magenta.
Paths below are relative to the pass folder.

## Round-1 defects

| # | Round 1 | Now | Evidence |
|---|---|---|---|
| 1 | Magenta error-shader beam, bark and fragments in the build | **Partly fixed, still a blocker.** The bark bubbles render properly and the scattered fragments are gone. A solid, untapered hot-magenta bar still runs from the drone to the player in `buildcheck/build-shot-08s.png` (251 magenta pixels at 640x360) and `build-shot-40s.png` (165). The editor still of the same moment (`still-SkylineArena.png`, "That glowing crack?") and all 42 editor frames have none, so it happens only in the player build. `player.log` reports no shader errors, so the cause is probably a line or trail material whose shader variant was stripped from the build (the drone's bolt trail or arc in `SkylineBolt`, or the Writ's tracer). | `judge-A/build08-beam-crop.png` |
| 2 | Towers without facades in the build | **Fixed.** The build's towers have window grids, setbacks and variety. | `build-shot-20s.png`, `judge-A/build20-sky-crop.png` |
| 3 | Primitive husks as wrecks | **Fixed.** Each bot leaves a wreck of its own model (drones, floor-care units and spotlights in stills 06, 07 and 09; the gondola falls and sheds its buckets in gallery 55-60 s). | still 07, `frames/SkylineGallery-059.8s.jpg` |
| 4 | Flat acid, jet and bark labels | **Partly fixed.** The barks now have a styled speech bubble with a pointer. The acid has organic blob outlines and a dashed jet, but it is still an opaque, unshaded lime (hue 85, saturation 0.67) with no wet sheen or depth, and reads as stickers on the terrazzo. | `frames/SkylineArena-115.3s.jpg`, `frames/SkylineGallery-024.4s.jpg` |
| 5 | CONCIERGE white and gold doesn't read | **Fixed.** Walls are warm white (lit wall hue 35-41, value 0.82-0.89), with white beams, marble pilasters, gold cornice lines and gold-framed navy banners. A few blotches remain in the shaded cladding (`build-shot-08s.png`, left wall). | stills 01-05 |
| 6 | Orange turns yellow in the sun | **Fixed.** The Breacher measures hue 28 sunlit in the arena (`frames/SkylineArena-006.1s.jpg`), against hue 44-49 for the floor-care units' gold (`frames/SkylineArena-115.3s.jpg`). | |
| 7 | Skylight glass invisible | **Fixed (modest).** There is a visible sheen on the panes along the beams, plus a pale glaze in the build's skylight. | still 08, `judge-A/build20-sky-crop.png` |
| 8 | Stills capped at 223 with no bloom | **Fixed.** Stills reach 251-255 with p99 220-237, and the downlights bloom (still 09). | |
| 9 | Repetition | **Mostly fixed.** Shop names, interiors, posters and menus all differ now (NOODLE BAR: Beef pho / Dan dan noodles; BAO HAUS: Pork belly bao; TACO SOL: Churros / Street corn; JUICE LAB: Green machine), and shutters come in four colours. The same green product-shelf atlas still fills several shopfronts (GILT & GRAIN and HEARTH HOME in still 09; VERDE MARKET in 07). | stills 05, 08, 09 |
| 10 | Blank warm-up walls | **Fixed.** Marble pilasters, banners, a cornice band and a gold line now break up the walls. The mezzanine pillar in still 06 still stops at the glass balustrade. | stills 01-03 |
| 11 | Stretched sign edge | **Not seen again.** No frame this pass lands close to a sign's frame. | |
| 12 | Placeholder posters | **Fixed.** Real poster designs (GO FURTHER, MEMBERS' EVENING, FRESH TODAY, PLAY BIG). | still 05, `build-shot-20s.png` |
| 13 | Red patch on the hall floor | **Fixed.** Gone from still 07. | |
| 14 | Flat-colour bots and guns | **Partly fixed.** The Writ's grip is detailed now, and the arms are modelled and graded with the scene. The floor-care units and drone are still flat, untextured plastic, and the Breacher is still blocky flat orange and grey. | `frames/SkylineArena-115.3s.jpg`, range frames |
| 15 | Far goal not visible | **Fixed in layout, not in look.** The service lift now sits under the mezzanine and can be seen from the hall (stills 06, 09). It is an untextured placeholder (see new defect 1). | |
| 16 | Range and gallery untextured, and in the build | **Not fixed.** Both are still grey boxes on Unity's default sky. | `still-SkylineFiringRange*.png`, `still-SkylineGallery*.png` |

## New defects, most severe first

1. **The far goal is an untextured placeholder, and the arena recording spends its last 45 s staring at it.** The
   service lift (stills 06 and 09, `judge-A/arena-122.jpg`) is a flat light-grey box with a blank pale-yellow rectangle
   where its sign should be. Its doorway is a flat mint-green plane. It has no doors, call panel, lettering, frame,
   material or trim. From about 125 s to the end at 170 s, the screen is entirely that mint plane with the Writ in
   front (`judge-A/arena-125.jpg`; frames 127.5, 139.6, 151.8 and 163.9 s). The lint lists these five untextured pieces
   (the lift's walls and shutter, and the practice counter). Spec 10 lists "an untextured placeholder in view" as a
   blocker, so being known doesn't make it acceptable. *Player:* the goal they've been walking toward is the one
   unfinished object in the level, and the ending of the creator's recording is a quarter-length solid colour.
   *Owner:* arena look/layout (service lift), demo player/capture (the recording ends inside the lift). **Blocker.**
2. **A magenta bar appears only in the build.** This is round-1 defect 1, narrowed (see the table). *Owner:* build
   shader stripping / the bolt or tracer FX. **Blocker.**
3. **The acid is still flat stickers.** See round-1 defect 4. It is the most-seen effect of the food-court fight.
   *Owner:* floor-care unit.
4. **Kiosk goods turn into flat, low-res cut-outs up close.** `frames/SkylineArena-006.1s.jpg`: at point-blank range
   the espresso kiosk's bottles are a soft, flat atlas with no depth behind the glass. The demo's camera framing makes
   it worse, but the player will brush this kiosk while learning the boost ledge. *Owner:* arena look (kiosk goods).
5. **After the fights the hall is a uniform grey scrapyard.** Still 07 shows about 20 identical "scorched" grey wrecks
   scattered evenly over the white floor. Each one is the right shape, but all of them in one grey makes the hall read
   as junk-strewn and loses the white-and-gold. A fade-out, darkening only partly, or keeping some trim colour would
   help. *Owner:* bots (wreck material). This is a minor note.

## Scores (8 is the bar)

| Category | r1 | r2 | Why |
|---|---|---|---|
| Lighting and sky | 6 | **8** | Crisp sun and skylight stripes, a blue sky with high cloud, towers hazed and varied, glass that reads now, HDR stills with bloom on the downlights and signs, and lit shop interiors. It's bright and high-key (mean L 160-170) without clipping. |
| Materials | 4 | **6** | The arena's dressing now meets the bar in most views: white plaster, marble, terrazzo, wood, varied shops and posters, real wrecks. It's held down by the untextured service lift (the far goal), the build-only magenta bar, the flat acid, flat-colour bots and Breacher, the repeated shelf atlas, and the grey-box range and gallery. |
| Art direction | 5 | **7** | CONCIERGE's white and gold now leads, and the firm's orange is clearly separate. The banners and signage make the route read (GALLERIA over the doorway, the kiosk, the CONCIERGE booth, SKY TERRACE banners), and the far goal is placed. What remains is the placeholder lift at the climax, the sticker acid, and toy-flat bots against the PBR mall. |

## Spec 10 blockers I see: 2 (plus 1 conditional)

1. An untextured placeholder in view: the service lift, its sign plate and its mint shutter (stills 06 and 09,
   arena frames 122-170 s).
2. An error-shader magenta bar in the player build (`build-shot-08s.png`, `build-shot-40s.png`).
3. Conditional: the firing range and gallery are untextured grey boxes, and they are included in the build.

## Not covered by the spec

- **A build-only check for missing-shader magenta.** The editor pass can't catch it. Suggest a magenta-pixel scan of
  the build shots inside the pass (today it finds 251 and 165 pixels).
- **"Known in the lint" versus the spec's blocker list.** The spec doesn't say whether an untextured piece the lint
  already lists still blocks. I've treated it as blocking, per section 10.
- **How the demo recording ends.** About 45 s of it shows the inside of the lift. A rule that every recording ends on
  a composed shot would stop the creator's last impression being a flat colour.
- **Wreck accumulation.** There's no rule for how long wrecks persist or how they look once cold.
- **Liquid and effects look.** There's still no bar for the acid's material (wetness, depth, edge falloff).
- **The look of the test scenes.** Should the range and gallery use the afternoon rig, and should they ship? (Carried
  over from round 1.)
