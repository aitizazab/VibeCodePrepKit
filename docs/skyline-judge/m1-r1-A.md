# Skyline Repo M1, round 1: judge A (lighting and sky, materials, art direction)

Pass: `out/skyline/20261014-0326/`. I looked at all 17 stills, all 36 recording frames, the three build shots, and
extra frames and crops in `judge-A/` (arena 62 s, 63.5 s and 70 s; the hall-wide floor patch; the warm-up roof corner;
the Writ's hand in the range; contact sheets of each recording). I measured pixel values with PIL (via uv). Paths below
are relative to the pass folder.

## Defects, most severe first

1. **The built game shows magenta error-shader objects.** In `buildcheck/build-shot-08s.png` the mall-cop drone's
   aim beam and its bark bubble ("Lights on you…") are solid magenta, Unity's missing-shader pink. In
   `build-shot-40s.png` magenta fragments (sparks, impacts or wreck bits) are scattered across the food court
   (x 80-540, y 380-750). `buildcheck/player.log` explains it: `Universal Render Pipeline/Lit` failed to compile with
   "out of memory while parsing" in BSDF.hlsl, Lighting.hlsl, Shadows.hlsl, GlobalIllumination.hlsl, ImageBasedLighting
   and ACES as well as ProbeVolume. The pack's "known and accepted" note only covers probe-volume variants and says
   "the build renders". It does, but parts of it render as error pink. *Player:* the first thing they see in the
   warm-up is a hot-pink laser and a hot-pink label, which reads as a broken game. *Owner:* perf/build (shader variant
   compile under the memory cap), arena look. **Blocker** (an untextured placeholder in view).
2. **The city's towers lose their facades in the build.** In the editor stills and recordings the towers have
   curtain-wall window grids (`still-SkylineArena-08-skylight.png`). In `build-shot-08s.png` and `build-shot-40s.png`
   the same towers are flat white boxes with no windows, mullions or haze bands, probably the same failed Lit variants.
   *Player:* every look up through the skylight, which is the arena's selling shot, shows grey-box blocks. *Owner:*
   arena look / build. **Blocker** (untextured surface in view).
3. **Every wrecked bot leaves the same untextured primitive husk, and the hall is littered with them.**
   `SkylineWreck.Create` builds a squashed sphere, a cube and a strut in flat colour, which turn "Scorched" dark grey.
   Stills 05, 06 and 07 show about 15 identical black blobs on the white terrazzo (07 most of all). The gallery stills
   labelled spotlight, gondola and drone (`still-SkylineGallery-spotlight/-gondola/-drone.png`) show the same black
   disc in place of each bot. A 2.2 m gondola and a 0.7 m drone both wreck into a 1 m floor-robot-shaped puck. Wreck
   bits (`SkylineBot.cs:1197`) are bare white and grey cubes (gallery frames 12.9 s and 54.2 s). *Player:* after the
   first fight the floor looks like debug markers, and nothing says which bot died where. *Owner:* all four bots
   (wreck visuals). I count this as a blocker under "untextured placeholder in view".
4. **The acid, the acid jet and the bark labels are flat placeholder graphics in a PBR scene.** In
   `judge-A/arena-63.5.jpg` and `frames/SkylineArena-065.0s.jpg` the acid puddles are opaque, uniform-green (pure
   #3c3 range) paper discs with no wetness, specular or edge falloff, overlapping like cut-outs. The jet is a stretched
   green cube, and a bare blue-grey cube floats above the wrecked unit at 65 s. The barks ("Ooh! A stain!", "Out of…
   order…") are plain grey rectangles with default black text, with no frame, CONCIERGE styling or screen look.
   *Player:* the most-seen effect of the first fight looks like a prototype. *Owner:* floor-care unit (acid), HUD and
   presentation (barks).
5. **CONCIERGE's white and gold doesn't read; the mall reads grey and navy.** The catalogue calls the walls
   "Plaster_White … near-white", but on screen they are mottled grey concrete. The lit wall in still 04 measures L 184,
   and shaded walls in 05 measure about 96-116 (RGB 96,104,116, blue-grey). The macro variation makes big dark
   blotches that look like stained concrete (`build-shot-20s.png` most of all, which fills the frame with dark
   blue-grey mottling). The "white beams with a gold underline" read as grey concrete beams. The dominant
   brand-coloured surfaces are the navy sign panels, so gold appears only as hairlines. *Player:* the antagonist's
   pristine white-and-gold building reads as a grey parking structure. *Owner:* arena look (Plaster_White tint, macro
   variation strength).
6. **In the sun, the firm's orange turns into the same yellow as CONCIERGE's gold.** The Breacher measures hue 29-32
   (orange) in the range (`frames/SkylineFiringRange-020.6s.jpg`). Sunlit in the arena (`frames/SkylineArena-005.0s.jpg`
   at 880,540) it measures hue 50, saturation 0.57, value 0.99: lemon yellow, clipped. The floor-care units' "gold"
   measures hue 51-52 (`judge-A/arena-62.jpg`), which is the same yellow. The cause is ACES's hue skew on bright
   saturated orange plus a near-clipped sunlit viewmodel. *Player:* spec 3's colour-by-owner rule (the firm's orange
   against CONCIERGE's white and gold) breaks at a glance, because the player's gun and the enemies share a colour.
   *Owner:* arena look (post and exposure on the viewmodel), Breacher, bots (gold albedo).
7. **The skylight has no visible glass, so it has no glass reflections.** `SK_Glass_Skylight` is alpha 0.08 against
   128 px probes (`SkylineArenaDress.cs:82`). In stills 01, 03, 05, 07 and 08 the roof reads as an open pergola of
   beams and mullions against bare sky. There is no sheen, sun glint, tint or reflection anywhere across it. The
   curtain walls do show a little sky reflection (06, 07). *Player:* spec 10.1's "glass reflections" and "a glass
   mall atrium" aren't sold, and the roof looks unfinished. *Owner:* arena look.
8. **The editor stills never reach white and have no bloom, so they misrepresent the look.** Every arena still tops out
   at L 223 (`still-SkylineArena-01/05/07`: max 223, p99 205-219), even the sun disc in 07 and 08 and the emissive
   signs. The recordings and the build reach 255 (`build-shot-08s.png` max 255, p99 243). The still cameras most likely
   render to a non-HDR target: ACES of a clamped 1.0 is about 223 in sRGB, and bloom's threshold of 1.0 is never
   crossed. *Player:* not a player defect, but the stills the creator and judges look at come out flat and greyish, so
   "bloom on highlights" can't be judged from them. *Owner:* demo player/capture (still camera HDR).
9. **Visible repetition.** The same shelf-of-goods atlas (rainbow product rows) is in every shopfront, upstairs and
   down (still 06: 14 identical shop interiors). NOVA PHONES and SOLSTICE SPORTS appear on both levels of the same wall
   (06). All four food stalls carry word-for-word identical menus (Signature bowl 6.00 / Spicy special 13.25 / Combo
   deal 11.50…; still 05: TACO SOL and NOODLE BAR side by side; `build-shot-40s.png`). Eight identical shutters fill
   the warm-up (01, Arena). The skyline repeats three or four tower models (08: the teal setback tower at least three
   times). *Owner:* arena look. Spec 10.2: "no visible repetition at a glance".
10. **Blank walls and missing detail in the warm-up hall.** The upper two-thirds of every warm-up wall is a featureless
    plaster expanse about 6-8 m tall. There are no pilasters visible at that height, and no lighting fixtures, vents,
    banners or clerestory (`still-SkylineArena.png`, 01, 02, `build-shot-08s.png`). The pillar in still 06 stops at the
    mezzanine's glass balustrade and holds nothing up. The dressing doc promises pilasters and a cornice, but they don't
    read at gameplay distance. *Owner:* arena look.
11. **Stretched texture on sign frames.** `frames/SkylineArena-085.0s.jpg`: the underside or edge of the sign above
    the camera shows long horizontal streaks, which is a UV stretch on the frame's depth face. *Owner:* arena look.
12. **The warm-up posters are placeholder art.** The lightbox posters are flat pastel blocks with a circle and a bar
    and a faint word ("STYLE"; still 05 and `build-shot-20s.png`, where one fills a quarter of the frame). Next to the
    photo-sourced marble and the HDRI sky they read as unfinished. *Owner:* arena look.
13. **A blurry red patch on the hall floor.** In still 07 (crop `judge-A/hall-wide-red-smear.png`) there is a soft red
    rectangle on the terrazzo near the east shops. It is probably the 128 px box-projected probe smearing a red menu
    board onto the floor. It reads as a stain or a bug. *Owner:* arena look (probe resolution or blend).
14. **The bots and guns are flat colour in a textured world.** The floor-care units (`judge-A/arena-62.jpg`) and the
    drone are untextured flat albedo plastic with no normal maps, wear or trim. The Breacher (range frames) is blocky,
    flat orange and grey, with almost no surface detail. The Writ is the exception: brick, stone, wood and brass have
    real texture, but its gold grip block is a flat slab (`still-SkylineArena.png`, `frames/SkylineArena-085.0s.jpg`).
    Silhouettes and faces read clearly against the white floor, which is a strength, but the toy-like shading sits a
    tier below the mall's PBR. *Owner:* bots, Breacher, Writ (grip).
15. **The far goal isn't visible.** The doorway frames the hall well (04: GALLERIA over the portal, CONCIERGE's booth
    centred through it), and the espresso kiosk is a strong landmark (01, 03). But no still or frame shows where the
    hall leads next: no exit, lift, lit gate or objective beacon. The hall is a symmetric box whose only focal point is
    the central booth (07). *Owner:* arena layout/look. Spec 10.4: "the next step and the far goal always visible".
16. **The firing range and the enemy gallery are untextured grey boxes on Unity's default sky, and they ship in the
    build.** See `still-SkylineFiringRange*.png`, `still-SkylineGallery*.png` and all their frames. `SkylineBuild.cs`
    builds `SkylineScenes.All`, so they are in the player. As test rigs that's fine. But the gun and bot recordings the
    creator judges are all lit by the neutral default rig, so they can't show how the guns and bots read in the real
    afternoon light (see defect 6). If the player can reach those scenes, they are a blocker. *Owner:* demo
    player/capture (scene look), build.

## Scores (8 is the bar)

| Category | Score | Why |
|---|---|---|
| Lighting and sky | **6** | Strengths: crisp sun at 41°, skylight shadow stripes that sell the space, a good HDRI with high cloud, readable bounce in the shade, and lit shop interiors behind glass (06). Against it: skylight glass invisible; haze bands weak (far towers barely lifted); the build's towers lose their facades; sunlit oranges clip to yellow; the stills have no white or bloom. |
| Materials | **4** | In the build, magenta error shaders and untextured towers. Wreck husks, acid, jet and barks are flat primitives. Walls read as stained grey concrete, not white plaster. Shelves, menus, shutters and towers repeat. The sign edge stretches. Bots and the Breacher are flat colour. Before these, the floors, marble, shutters, planters and plants are good. |
| Art direction | **5** | The route reads (doorway, kiosk, booth), and the mall is coherent in plan. But CONCIERGE's white and gold doesn't dominate (it reads grey and navy). The firm's orange and the bots' gold collapse into one yellow in the sun. The FX and labels are in a prototype style, toy-flat bots sit inside PBR architecture, and no far goal is visible. The Writ's courthouse look is striking and deliberate. |

## Spec 10 blockers I see: 3 (plus 1 conditional)

1. An untextured placeholder in view in the build: magenta error-shader beam, bark and fragments (`build-shot-08s.png`,
   `build-shot-40s.png`).
2. Untextured surfaces in view in the build: the white facade-less towers (`build-shot-08s.png`, `build-shot-40s.png`).
3. Untextured primitive placeholders in view in the arena: the wreck husks and wreck cubes (stills 05-07, gallery
   stills, frames 62-65 s).
4. Conditional: the firing range and gallery are fully untextured grey boxes, and they are in the build.

## Not covered by the spec (gaps to raise, not to patch)

- **Wreck design.** SKYLINE-BOTS specifies each bot's wreck face and sound, but not what its body becomes. Each bot
  needs a wreck look (a broken version of its own model), or the shared husk needs a real model.
- **An FX and in-world text style standard.** Nothing in spec 10 covers the look of acid, beams, sparks, impacts or
  bark bubbles. Today they are flat primitives and default text.
- **Faction colour under the final grade.** Spec 3 sets colour by owner but not a check that it survives ACES and the
  sun. A rule is needed, for example "the firm's orange stays within hue 20-35 and CONCIERGE gold within 40-48 in the
  arena stills".
- **Still cameras must match the player camera's HDR and post.** Otherwise "bloom" and "exposure" can't be judged
  from the stills.
- **The build's shader compile under the memory cap.** The OOM shader failures go beyond probe volumes and are now
  visible. The pass needs a build-side check, for example a magenta-pixel scan of the build shots.
- **The look of the test scenes.** Should the range and gallery use the afternoon rig, so gun and bot reads are judged
  in the real light? And should they ship in the player build?
- **What "the far goal" is in a single-hall arena.** The spec says it must always be visible, but no brief names the
  object (exit, lift or beacon).
- **Glass visibility.** The spec asks for "glass reflections" without a minimum (for example, a visible sheen or glint
  on the skylight from the floor).
