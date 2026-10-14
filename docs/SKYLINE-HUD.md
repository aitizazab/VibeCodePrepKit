# Skyline Repo: the HUD (two styles) and the ammo economy

Decided with the creator on 2026-10-14. Two HUD styles: **minimal** (what the game needs to function; defined first)
and **complete** (later). Both follow `SKYLINE-STYLE.md` (comic ink). Built with the Food Court floor
(`SKYLINE-FLOOR-REPOSSESS.md`), since its objective items belong to the HUD.

## Ammo (the creator's calls)

- **Primary weapons (the Writ): no reserve.** Infinite reserve; the magazine and the reload are the only limits.
- **Special weapons (the Breacher and later ones; more damage, unique effects): an ammo economy.** Special ammo is
  stripped from wrecks together with plating (design 2.4: aggression heals, now it also feeds):
  - a wreck from a kill **by a primary weapon gives 1 special shot**;
  - kills **by special weapons give 1 shot per 3 kills** (starting values; balanced together in play).
- The creator: "No reserve ammo makes sense for primary weapons, but special weapons like the Breacher which do more
  damage and have unique effects want an ammo economy." "Special Ammo stripping from Wrecks is good, one shot per kill
  made by Primary Weapons, and 1 shot every three by Special Weapons to start with."

## The minimal HUD

| Element | What it is |
|---|---|
| Ammo | The magazine count by the gun's small icon (which gun is out); for a special weapon, its reserve beside it. A primary weapon shows no reserve. |
| Health and shields, one bar | Chassis (health) is the bar; plating (the shield) is a brighter layer over it (Apex Legends' shield-over-health). Plating breaking shatters the layer off; low chassis pulses the bar and tints the screen edges red. |
| Crosshair and hit markers | Kept from M1: white a hit, yellow a weak point, gold a Writ spot, red a kill. |
| Damage direction | Kept from M1 (a wedge on a ring round the crosshair); also flashes for an off-screen attack about to land (the spotlight's lock). |
| The objective | The job sheet's three asset icons along the top edge, ticked when towed; an edge-of-screen direction marker per untaken asset; the tow meter as a ring round the crosshair while in a circle. |
| The wave clock | A small "next wave" countdown. |
| Subtitles | Movie-style: bottom centre, white text with a dark outline, no box, at most two lines, the speaker's name as a coloured prefix ("DOT:"); CONCIERGE's PA lines in italics. Setting: Off / Dialogue / Dialogue + bot barks, and a size. |

**Removed:** the text bubbles above bots (the hued outlines carry enemy clarity now) and the boxed speaker cards.

## Comic lettering on hits and kills (replaces the bubbles, for flavour)

The creator: "the text bubbles above bots should be replaced by comic text that animated writes out around certain
kills or hits on enemies, for flavour." Comic sound-effect lettering, in the ink style, that writes itself out (stroke by
stroke, like the ink tracer) beside the bot and fades: on kills, weak-spot hits, Writ spot breaks and big Breacher
shoves, not on every hit. Each bot type and each gun has its own words (draft set, for the creator to rewrite: the
drone "KZZT!", the floor-care unit "SPLORCH!", a Writ spot break "OBJECTION!" / "SUSTAINED!", the Breacher's shove
"WHUMP!"). Colour from the outline roles (hostile red-magenta lettering with a dark ink keyline); never covering the
crosshair; at most two on screen.

## The complete HUD (later)

Adds the hires' portraits and abilities, run currency (scrap and commission), the floor modifier, CONCIERGE's counter
notices, and a compass.

## Feedback flashes and styling (the creator, 2026-10-14)

- **Plating gained** from stripping a wreck: the screen edges flash a light blue (the plating colour), briefly.
- **Special ammo picked up** while a special weapon is out: the ammo counter flashes green.
- **Wrecks you can strip** are outlined light blue; the outline thins as the strip window closes and flashes out over
  its last 0.4 s (`SKYLINE-STYLE.md`, rule 2).
- **Not generic boxes:** "the HUD health bar and ammo counter should be stylized, not generic rounded boxes and text like
  AI-generated content usually looks like." The health bar is a **layered parallelogram** (plating a slanted layer over
  the slanted chassis bar, ink keylines, hard edges); the ammo counter uses a **display font with character** (a bold
  comic/condensed face that fits the ink style, never the UI default), big magazine digits, a small reserve. The whole
  HUD shares the style sheet's ink keyline and palette.
- **The ammo face is Luckiest Guy** (the creator, 2026-10-14, from `art/review/2026-10-14/skyline-hud-fonts.html`). A later
  "Condensed" UI option may switch to **Anton** with the digits set closer together.
- **No label text** (the creator, 2026-10-14): "That's bad comic lettering, there shouldn't be any text that says Plating."
  The bars, flashes and colours say what they are; no captions, no names spelled out over the bars.
