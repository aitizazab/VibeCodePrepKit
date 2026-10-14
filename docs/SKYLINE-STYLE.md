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
  and edges catch a little light.
- **The sky** is a painted gradient (zenith to horizon) with a drawn sun and simple cloud shapes, not a photo.
- **Post:** neutral tonemapping (the flat palette must survive), bloom kept for the lights, a slight vignette.

### 2. Thick hued outlines on what matters
- **Bots, objectives, pickups and weak spots** get a **thick outline (about 4 px at 1080p, constant on screen)** in a **hue that
  says what they are**, drawn round their visible silhouette:

| Role | Hue | Notes |
|---|---|---|
| Hostile bots (and their mines, acid, bolts) | hot red-magenta `#FF2E63` | no scenery ever uses this hue |
| Objectives (the tow assets, the lift when it opens) | cyan `#2EE6FF` | |
| Scrap and wrecks you can strip | green `#5CFF8A` | goes when the wreck is stripped |
| Weak spots and other vulnerabilities (the Writ's cracked casing, a drone's lens, whatever a body marks as its weak point) | yellow `#FFD60A` | its own thick outline, drawn over the hostile red-magenta where the two overlap, so a weak point reads at a glance on a hostile body |
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
| `SkylineInkFeature` | A renderer feature (Render Graph) on the shared PC renderer, **inactive unless the `SkylineInk` volume override is on** (Visiting Hours' `VhAtmosphereFeature` pattern), so other templates are untouched. Pass 1 draws outlined objects' mask pass (role hue) against the camera depth; pass 2 is one full-screen composite: thin ink from depth/normal edges with distance fade, the thick hued outline by dilating the mask (outside the silhouette only). Before post-processing. Runs on the viewmodel's overlay camera too. |
| The converter | Editor code making `InkLit` twins of the look library's materials, the bot paint, the acid and the guns, with palette colours from a table in code. |
| The look rig | `SkylineLook` gains the ink rig: lower ambient, the banded sun, the painted sky, neutral tonemapping, the volume override. The afternoon rig stays as the fallback. |
| Build rule | Any keyword a material turns on at run time is saved on a build-included material (the invisible-bots lesson). |
| Budget | Mask pass plus composite at most 0.6 ms at 1080p on the RTX 3050; the arena still holds 120 fps. |

## The look test (what the creator sees first)

The current M1 arena re-shaded in the style, not the new floor: the main hall in sun, the lift lobby taken over by one
strong coloured light (rule 3), a colour-free corner (rule 4), the drone and the floor-care unit in shot with their
outlines, the Writ in view. Stills from the still cameras and a short recording, before and after on one review page.
An adversarial judge reviews them first. The Food Court floor is then built in the style.
