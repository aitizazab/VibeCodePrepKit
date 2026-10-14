# Skyline Repo: the art style (the style sheet)

Every piece of Skyline art follows this page: the spaces, the bots, the guns, later the hires, and every concept image.
It replaces the photographic afternoon look in `SKYLINE-LOOK.md` (that page stays as the record of the rig and the
dressing being re-skinned). Decided by the creator on 2026-10-14 after the comparison page
(`FableAnimationTests/art/review/2026-10-14/skyline-art-style-2.html`).

## The creator's call

> "Comic Ink is indeed the closest to what I want, but with important objects or enemies carrying a thicker, hued
> outline, so they're distinct against the background. But I want to keep a strong lighting aspect, like Deep Rock
> Galactic, where a bright light source can completely take over a room. If there's anything to borrow from another
> style, it would be the sheer detail of landscape of the maps from Team Fortress 2. They don't look cluttered, but also
> don't look barren. It's also worth keeping a color-free approach like the Retro Neon style in certain areas, which make
> them feel different. I'll wait for the scenes from the game to judge."

## The five rules

### 1. Comic ink (the base; Roboquest is the main reference)
- **Scenery lines:** thin dark ink (about 1.5 px at 1080p) on silhouettes, creases and colour boundaries, from the
  depth and normal edges. Lines thin and fade with distance, so the far city reads as soft shapes, not wire.
- **Shading in bands:** the sun lights in three bands (lit, half, shade) with crisp but anti-aliased steps. Lit is warm,
  shade takes the sky's cool tint. The deepest shade carries light world-space hatching (Borderlands' trick).
- **Painted flat colour:** no photographic textures. Each surface is a flat palette colour with soft, large-scale
  painted variation: walls darken towards the floor and lighten towards the top (Team Fortress 2's vertical gradient),
  and edges catch a little light. **Walls are a cool blue-white** (plaster about `#E9EDF2`, its tints cooler and lighter
  with them) **and floors a warm cream** (about `#CDBB9C`), so the lit and shaded halves read as warm sun against cool sky.
- **The sky** is a painted gradient (zenith to horizon) with a drawn sun and simple cloud shapes, not a photo.
- **Post:** neutral tonemapping (the flat palette must survive), bloom kept for the lights, a slight vignette.

### 2. Thick hued outlines on what matters
- **Bots, objectives, pickups and weak spots** get a **thick outline (about 4 px at 1080p, constant on screen)** in a **hue that
  says what they are**, drawn round their visible silhouette:

| Role | Hue | Notes |
|---|---|---|
| Hostile bots (and their mines, acid, bolts) | hot red-magenta `#FF2E63` | no scenery ever uses this hue |
| Objectives (the tow assets, the lift when it opens) | violet `#B36BFF` (was cyan; moved 2026-10-14 so it can't be confused with the light-blue wrecks) | |
| Wrecks you can strip (plating and special ammo) | light blue `#8FD3FF` (the creator, 2026-10-14; blue = plating) | **thins as the strip window runs out and flashes out over its last 0.4 s**; gone once stripped |
| Weak spots and other vulnerabilities (the Writ's cracked casing and spots) | yellow `#FFD60A` | its own thick outline, drawn over the hostile red-magenta where the two overlap, so a weak point reads at a glance on a hostile body |
| A bot's own weak-point parts (the drone's rotors and lens, whatever each body marks as its weak point) | the hostile red-magenta tinted towards yellow, `lerp(#FF2E63, #FFD60A, t)` with t pulsing between 0.35 and 0.6 at about 1.2 Hz | a slight pulse, so the part reads as the thing to shoot; priority where outlines overlap: the Writ's yellow over this over the hostile hue |
| The Unit's guns (the viewmodel) | the ink colour, 2.5 px | heavier than scenery, not hued |

- Hues are tested in the stills against every area's palette, the colour-free zones included; the creator judges them.

### 3. Light takes over (Deep Rock Galactic)
- **Low ambient, strong local lights.** A room's look is decided by its brightest light: a shop's neon, the strip
  lights of the service corridor, a warning beacon, the tow drone's beam, a muzzle flash. Local lights are banded like
  the sun, with a hard-edged pool, so a light paints its colour across the room instead of a soft glow.
- **The sunlit hall stays the bright, warm hero space**; interiors are dim until their own light takes them over.
- Lights are gameplay-readable: a bot's own light announces it, an objective's beam marks it.

### 4. Colour-free zones (from the retro-neon games)
- Some areas drop their colour: the surfaces go to greys and **only the lights carry hue**. First use: the **service
  corridor** and the **walk-in shop interiors** of the Food Court floor (`SKYLINE-FLOOR-REPOSSESS.md`), and later the
  After Hours modifier. Stepping into one should feel like entering a different place.
- The hued outlines keep their colours inside these zones, which is where they read best.

### 5. Team Fortress 2's level of detail
- **Not cluttered, not barren:** big readable shapes first; detail goes on the edges and the skyline (rooflines,
  cornices, trims, signs, awnings, props clustered against walls); floors and routes stay clean for movement.
- **Layered depth:** near dressing, a mid layer, far silhouettes, each further layer lighter, lower in contrast and with
  fewer lines.
- **Readability order:** scenery is lowest in contrast and saturation; gameplay objects are highest.

## Concept art from now on
- A **style block** written from this page (comic ink, cel-shaded 3D game render, flat painted colour, the outline rule,
  the palette) opens every prompt, and **an in-game still in the style is attached as the reference** once it exists.
- Framing, angle and lighting **differ per idea**: no shared "premium skin reveal" template.
- The Breacher's club subwoofer (round 2's B, take 1) is redone inside the style.

## How it's built (Unity, URP 17 Forward+)

| Part | What |
|---|---|
| `Skyline/InkLit` shader | Hand-written URP shader: banded main and additional lights (Forward+ light loop), cascaded sun shadows, the vertical gradient, deep-shade hatching, a rim; passes ForwardLit, ShadowCaster, DepthOnly, DepthNormals and an outline-mask pass. Properties: colour, optional painted map, gradient strength, desaturation (the colour-free zones), outline role. |
| `SkylineInkFeature` | A renderer feature (Render Graph) on the shared PC renderer, **inactive unless the `SkylineInk` volume override is on** (Visiting Hours' `VhAtmosphereFeature` pattern), so other templates are untouched. Before post-processing, four graph passes: (A) the mask: outlined objects' mask pass (role hue) against the camera depth; (B) the occupancy: a quarter-resolution map of where the mask is, grown by the outline radius, in two tiny passes; (C) one full-screen composite: thin ink from depth/crease edges with distance fade, the thick hued outline by a 24-tap circle search of the mask (outside the silhouette only) that runs only where the occupancy says the mask is within reach. The composite **blends over the camera colour** (`Blend One SrcAlpha`, rgb premultiplied by the ink and outline, alpha = what is left of the scene), so it never reads or copies the colour. Runs on the viewmodel's overlay camera too. |
| The converter | Editor code making `InkLit` twins of the look library's materials, the bot paint, the acid and the guns, with palette colours from a table in code. |
| The look rig | `SkylineLook` gains the ink rig: lower ambient, the banded sun, the painted sky, neutral tonemapping, the volume override. The afternoon rig stays as the fallback. |
| Build rule | Any keyword a material turns on at run time is saved on a build-included material (the invisible-bots lesson). |
| Budget | Mask pass plus composite at most 0.6 ms at 1080p on the RTX 3050; the arena still holds 120 fps. The cost analysis below is why the pass was rebuilt on 2026-10-15 after a food-court fight with 15 bots measured 10-25 ms of GPU for it. |

## The look test (what the creator sees first)

The current M1 arena re-shaded in the style, not the new floor: the main hall in sun, the lift lobby taken over by one
strong coloured light (rule 3), a colour-free corner (rule 4), the drone and the floor-care unit in shot with their
outlines, the Writ in view. Stills from the still cameras and a short recording, before and after on one review page.
An adversarial judge reviews them first. The Food Court floor is then built in the style.

### Cost analysis of the ink pass (2026-10-15)

**The problem.** The same automated playthrough, 1080p, RTX 3050 laptop: ink on averaged 6.6 ms (1% low 30.4 ms, worst fight 44 ms,
152 frames over 25 ms), ink off 4.6 ms (1% low 16.8 ms, worst fight 19 ms, 14 frames over 25 ms). In the food-court fight (15 bots
on screen) the ink passes cost 10-25 ms of GPU, against the 0.6 ms budget. This was found by reading the pass, not by profiling it
(Unity was not run for this work): **the per-pass figures below are arithmetic estimates for a 1920x1080 RGBA16F camera on this
GPU, to be replaced by the A/B in "What the Unity run must check".** `-skyline-no-ink` removes the whole feature, which also removes
the normals prepass it asks for, so the 10-25 ms is the sum of every row, not only the visible passes.

| Pass (per camera: the base and the viewmodel overlay) | Before | Why it was costly | After | Est. saving |
|---|---|---|---|---|
| Normals prepass (`ConfigureInput(Normal)`: URP redraws every opaque object with `DepthNormals` into a second target) | the whole scene's geometry again plus a normals target write; on the CPU every draw call twice; the overlay camera again for the guns | the largest single item and the only one that grows with scene and bot count (15 bots x their parts, each its own draw because the per-bot material blocks SRP batching) | **kept by default**; `creasesFromDepth` (or `-skyline-ink-depth-creases`) drops it, leaving only a depth requirement | est. 1-3 ms GPU and the doubled draw submission, when the option is on |
| Mask (A) | 5 renderer lists drawn into an 8 MB target with a full clear | draws every outlined part once more (bots, weak spots, mines, bolts); depth-tested, cheap per pixel, scales with the draw count | unchanged | 0 (est. 0.2-0.5 ms stays) |
| Colour copy | a full-screen blit of the camera colour (RGBA16F: 16 MB read, 16 MB write, and a pass boundary that stops pass merging), on both cameras | the composite could not read and write the same target | **gone**: the composite blends over the camera colour (`Blend One SrcAlpha`; the shader returns the ink premultiplied and alpha = what is left of the scene). The maths is identical to the two `lerp`s it replaces | est. 0.3-0.6 ms (two cameras) |
| Composite thick-outline search | 24 point taps of the mask for **every pixel** (50 M taps a frame), each with a six-way class branch, on both cameras | the search ran everywhere although the mask covers a small part of the screen; it did not depend on the bots, so it was a flat tax that a GPU already behind turned into dropped frames | the search runs only where a **quarter-resolution occupancy map**, grown by `ceil(radius / 4)` blocks, says some mask lies within the radius. The map is 4 bilinear taps per 4x4 block, then a 3x3 window of loads at 1080p (radius 4 px): about 1.2 M + 1.2 M reads against 50 M. Every other pixel pays one load. Exactly the same pixels draw, because a 0 in the map guarantees no mask within reach (a unit-tested bound) | est. 1-3 ms (two cameras) |
| Composite thin-ink kernel | 1 + 4 depth taps, up to 5 normal taps | cheap, but ran at full resolution on both cameras | unchanged; with depth creases the normal taps go too (two arithmetic second differences instead) | est. 0.1-0.3 ms when depth creases are on |
| Overlay camera (the viewmodel) | the whole feature again: prepass, mask, copy, composite | its depth holds only the guns, the rest is sky | the occupancy skip makes the empty screen nearly free; no copy | included above |
| Render Graph churn | 3 transient full-size targets (mask, copy, normals) | pooled by the graph, so not a per-frame allocation, but 40 MB+ of bandwidth | the mask plus two 1/16-size R8 maps (130 KB each) | included above |

Not changed, and why: the mask stays full resolution, because the outline's edge is read from it at pixel precision and a half-resolution
mask would blur the 4 px hued rim the creator approved; the thick-outline taps themselves (24) stay, because reducing them changes the rim.
If the A/B shows the mask pass itself is the fight's cost (its draw count scales with the bots), the next step is to fold the bots' parts into
fewer renderers, or to draw the mask only for renderers within the outline's view distance.

**Why the fight spiked.** The flat per-pixel costs (copy, search) do not depend on the bots, but a GPU already near its frame budget turns any
fixed cost into a queue: the CPU then waits for the GPU (the 20 ms median wait measured). The fight adds draw calls (every bot part is drawn
by the prepass and the mask pass as well as the colour pass), so it is both the heaviest frame and the one with the least headroom.

**Pure maths under test:** `SkylineInkMath` (occupancy size, dilation blocks for a radius and height, load counts, the pixel angle, the wobble
amplitude) is covered by `SkylineInkPerfTests` (EditMode), together with the shader's pass list and the switch parser.

### Optional stylization (2026-10-15; all off by default)

The creator asked for the comic look to be "even more stylized if possible". These are options on the `SkylineInk` volume, each 0 (off)
to 1 (full), **0 by default so nothing changes until the creator picks** from stills. None of them touches the hued gameplay outlines (the
roles table above): line weight, wobble and grain live only in the scenery ink and the scene colour; halftone and posterize live in the lit
shader's shading. Costs are estimates at 1080p on the RTX 3050; the A/B measures them.

| Option (volume field) | Switch name | What it does | Est. cost |
|---|---|---|---|
| Line-weight variation (`lineWeightVariation`) | `lineweight` | Scenery lines up to 90% heavier on near objects, a steeper taper to thin with distance, and creases inside a surface 45% lighter than silhouettes (so the outlines of shapes stand out from the detail). | about 0 (a few ALU ops) |
| Halftone dots (`halftoneDots`) | `halftone` | Ben-Day dots (a 45 degree grid, 5 a metre, each covering about a third of its cell) in the deepest shade band, on the plane the surface faces most; the hatching there gives way in proportion (full strength = dots instead of hatching, half = both). World-space like the hatching, so it does not swim; fades out between 14 and 26 m and where the dots would be under about 3 px. | about 0.05 ms (a dot-grid evaluation in the shade branch of the lit shader) |
| Hand-drawn wobble (`handWobble`) | `wobble` | A smooth two-octave screen-space noise offset on where the scenery ink is looked up, up to about 1.2 px at 1080p, re-seeded 6 times a second so the lines boil; the thick hued outlines keep their true position. | about 0.05 ms (a value-noise evaluation per pixel) |
| Fewer, crisper bands (`posterize`) | `posterize` | The sun's three bands become two (shade and lit, one crisp step at 0.30) and each local light's two steps become one; blended in by the strength. | about 0 (replaces the band maths) |
| Paper grain (`paperGrain`) | `grain` | Sparse dark flecks (up to 14% darker) at two scales over the scene colour, screen-fixed like paper; never over a hued outline. | about 0.1 ms (the composite then writes every pixel instead of only the inked ones) |

**Capturing them.** `-skyline-ink-style <name>[,<name>...]` on the stills or the player raises the named options to full strength
(`halftone=0.5` for a strength, `all` for every option, `none`). Names: `lineweight`, `halftone`, `wobble`, `posterize`, `grain`. The switch never
writes to the volume: it raises the strengths the pass uses (the larger of the volume's and the switch's wins), and the pass logs its
state (`SKYLINE_INK ... style ...`) for the first three cameras. For the stills page, one run per option into its own folder:

    powershell -NoProfile -File tools/skyline/pass.ps1 -Only B -Out <folder> -CaptureArgs '-skyline-ink-style halftone'

(and the same with `lineweight`, `wobble`, `posterize`, `grain`, `all`, plus one with no switch as the reference). Posterize and halftone
reach the lit shader as the global vector `_SkInkLitStyle`, set by the feature for each camera (not a keyword, so there is nothing to lose in
a build: the invisible-bots lesson).

**Depth-curvature creases (`creasesFromDepth`, off by default).** The thin ink's crease lines (the surface turns with no depth jump) normally
come from comparing neighbouring normals, which needs the normals prepass. With this on they come from the depth buffer instead: 1 / eye
depth is affine in screen space on a flat surface, so its second difference across a pixel vanishes on a plane and jumps where two planes
meet; it is measured in units of one pixel's slope change (`creaseSlope`, default 0.5) so it does not depend on distance or grazing angle.
The lines come out a little different (centred on the crease instead of on the far side of it), so the creator should compare stills
first; `-skyline-ink-depth-creases` / `-skyline-ink-normal-creases` force either for the A/B without editing the volume.

**What the Unity run must check.** (1) Compile and the EditMode tests (the shader's three passes, the new defaults). (2) Perf A/B, three
runs of `tools/skyline/pass.ps1 -Only D -Out <existing folder>`, copying `perf.json` away between runs: with no extra args (the new pass), with
`-PerfArgs '-skyline-ink-depth-creases'`, and with `-PerfArgs '-skyline-no-ink'`; compare with the 2026-10-15 baseline (6.6 / 30.4 / 44 ms).
(3) Stills of the default look against the previous ones (the hued outlines and the thin ink must be unchanged), then one stills run per
stylization option.
