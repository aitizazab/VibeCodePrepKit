# Skyline Repo: the lines agents wrote or changed overnight (2026-10-14), for you to rewrite

This collects every dialogue line, bark, sign, poster and menu word that agents wrote or changed overnight, compared with `docs/SKYLINE-DRAFT-LINES.md` (which I did not touch). Every line here is a draft for you to rewrite; the two lines the spec quotes are marked **FIXED**.

"What it was" means the wording in `SKYLINE-DRAFT-LINES.md`, or "new" if that file had nothing. The current wording is read from the code (`Encounters.cs` `TeachingBeats.M1()`, `SkylineBarks.cs`, `SkylineDamageFeedback.cs`, and the sign atlas script `tools/skyline/look/make-textures.py`, which `SkylineArenaDress.cs` only places).

## Nan (2 lines)

| When it plays | The line now | What it was |
|---|---|---|
| The warm-up starts | Unit's live. Warm up here, then the Galleria's through the far doors. | Unit's live. Warm-up hall first; the Galleria's through the far doors. Try not to dent anything we have to return. (cut shorter so Dot's first lesson isn't held back) |
| You get within 6 m of the practice counter (3.5 m high; moved from the kiosk) | That counter's your practice. Tap jump twice for a second hop, then catch its lip and pull yourself up. | Tap jump twice for a second hop, and reach a ledge's lip to mantle up. Every kiosk in here is built for practice. (played at the warm-up start) |

## Dot (5 lines)

| When it plays | The line now | What it was |
|---|---|---|
| You first see the practice bot | Practice bot! Tag its lens with the Writ: it cracks open a weak spot. Then hit the spot. | Practice bot, dead ahead! Tag its lens with the Writ and it leaves a weak spot. Hit the spot: hurts more than the lens! |
| Your first weak spot appears | That glowing crack? That's the spot. Shoot it! | That glowy bit? That's the spot. Shoot it! (spots are now cracked casing) |
| You get near the kiosk ledge | Breacher kicks both ways. Jump, jump again, then blast the floor. Scrap's up top! | Breacher kicks both ways. Jump, jump again, then shoot your own feet. There's scrap on top of that kiosk. |
| You reach the top of the kiosk | Ha! Doesn't hurt you, either. Shoot again mid-air to climb higher. | Ha! One boost a jump, mind. Land or touch a wall to get it back. (that rule was removed; this replaces the "out of date" note) |
| Your plating first breaks | Strip it! Scrap's armour! **FIXED (spec quote, yours)** | same |

## CONCIERGE (7 lines)

| When it plays | The line now | What it was |
|---|---|---|
| First floor-care unit | Please note: the floor-care unit operates at ankle height. Guests are advised to jump. **FIXED (spec quote, yours)** | same |
| First drone | Please note: security drones anticipate where guests are going. Guests are advised to change their minds. | same (unchanged) |
| First spotlight | Please note: the spotlight confirms its target before firing. Unconfirmed guests turn sharply, or stand behind something. | same (unchanged) |
| First gondola | Please note: window cleaning is in progress on the walls. Guests are advised to keep their feet on the floor. | same (unchanged) |
| The next fight is ready (every fight now starts further in, so "food court entrance" was wrong) | Security is re-deploying further in. Guests are asked to proceed to the marked area. | Security is re-deploying. Guests are asked to return to the food court entrance. |
| The service lift opens (after the third fight; teaches the slide under the shutter) | The service lift is now open. Its shutter is stuck at knee height; guests are asked to slide under it. | new |
| Fight starts (a screen banner, header then body) | CONCIERGE · SECURITY NOTICE / Unscheduled guest activity. Security is responding. | new |

## Bot barks (16 lines; 14 changed, 2 kept)

Security drone

| When it plays | The line now | What it was |
|---|---|---|
| Spots you | Halt! Mall security! | Halt! Mall policy! |
| Hurt | That's a citation! (ties to the printer clack in its shot) | Assault on an officer! |
| Loses you | Suspect fled. Noted. (shorter, so its bubble is narrower) | Suspect has left the premises. |
| Wrecked | Officer... down... | Code... red... |

Floor-care unit

| When it plays | The line now | What it was |
|---|---|---|
| Spots you | Ooh! A stain! | Oh! A stain! |
| Hurt | Rude! I'm cleaning! | Rude! |
| Loses you | Spotless again! | All clean! |
| Wrecked | Out of... order... | same (kept) |

Spotlight

| When it plays | The line now | What it was |
|---|---|---|
| Spots you | Lights on you. Smile. | Target acquired. Smile. |
| Hurt | My lens! My LENS! | Lens damage reported. |
| Loses you | Searching the dark. | Scanning. |
| Wrecked | Lights... out... | Lights... out. (only the ellipsis) |

Window-washer gondola

| When it plays | The line now | What it was |
|---|---|---|
| Spots you | Oi! Off my glass! | Mind the squeegee! |
| Hurt | Watch the bucket! | Window's dirty! |
| Loses you | Back to the panes. | Shift's over. |
| Wrecked | Hanging... up... | same (kept) |

## The lift (all new; the sign pieces are in `make-textures.py`)

| Where | The words | What it was |
|---|---|---|
| Board over the service-lift doors (lit) | SKY TERRACE / Lifts · Observation deck | new (the fascia was a blank yellow panel) |
| Indicator panel above the doors | SKY TERRACE | new (was a blank mint bar) |
| Banner screen | SKY TERRACE / Opening soon | new |
| The lift opening line | see CONCIERGE above | new |

"Sky terrace" is the look agent's name for where the service lift leads; reconcile it with your name for it.

## Shop signs and screens (new since the draft file)

| Where | The words |
|---|---|
| Hall sign over the portal | THE GALLERIA / Halcyon Row · est. 2051 ("est. 2051" is an invented date: confirm or change) |
| Info booth and welcome screen | CONCIERGE / At your service, always |
| Floor-care signs | PLEASE DO NOT RUN / Floor care in progress |
| Closed shops (eight in the warm-up) | REMODEL IN PROGRESS / Thank you for your patience |
| Welcome screen behind the start | WELCOME, VALUED GUEST / Your visit is being recorded |
| Portal sign, kiosk, arrow, entry lane | GALLERIA; ESPRESSO / · KIOSK · (all four sides of the boost kiosk); FOOD COURT / ▲; SECURITY RE-DEPLOYMENT (gold chevrons on the entry plate) |
| Shop names, first 12 | VELLUM BOOKS, ORBIT TOYS, HALCYON OPTICS, NIMBUS TEA, PARCEL & PINE, LUMEN AUDIO, MERIDIAN SHOES, KITE & CO, BLOOM FLORIST, AURA SKIN, NOVA PHONES, SOLSTICE SPORTS |
| Shop names, 7 added overnight | GILT & GRAIN, COBALT DENIM, PIXEL ARCADE, ATLAS TRAVEL, HEARTH HOME, VERDE MARKET, QUILL STATIONERY |
| Banners (fabric, hall walls) | THE GALLERIA / Halcyon Row; AT YOUR SERVICE / CONCIERGE; FOOD COURT / Level 0 · east; SKY TERRACE / Opening soon |

## HUD labels (new)

PLATING / NO PLATING and CHASSIS (the two bars).

## Menus (the four food stalls; all four used to show the same generic menu: Signature bowl 6.00, Spicy special 13.25, Combo deal 11.50, Side of greens, Daily soup, Iced drink)

| Stall | Dishes and prices |
|---|---|
| NOODLE BAR | Beef pho 12.50; Dan dan noodles 11.00; Chilli wontons 8.50; Miso ramen 13.25; Spring rolls 6.00; Jasmine tea 3.50 |
| TACO SOL | Al pastor tacos 9.75; Fish tacos 10.50; Loaded nachos 8.25; Street corn 5.00; Churros 4.75; Horchata 3.75 |
| BAO HAUS | Pork belly bao 7.50; Crispy tofu bao 6.75; Chicken bao 7.25; Sesame fries 4.50; Quick pickles 3.00; Yuzu soda 3.95 |
| JUICE LAB | Green machine 6.50; Sunrise press 6.25; Acai bowl 9.00; Protein shake 7.00; Ginger shot 2.75; Cold brew 4.25 |

## Posters (16; each used to be one word: NEW SEASON, SALE, ...). Brand: headline / line of copy

| Brand | Headline / copy | Brand | Headline / copy |
|---|---|---|---|
| VELLUM BOOKS | READ THE SUMMER / 3 for 2 on paperbacks | ORBIT TOYS | PLAY BIG / the new rocket kits |
| HALCYON OPTICS | SEE CLEARLY / eye tests, free | NIMBUS TEA | SLOW DOWN / first-flush oolong |
| PARCEL & PINE | GIFTS, WRAPPED / free wrapping today | LUMEN AUDIO | HEAR EVERYTHING / noise cancelling, all day |
| MERIDIAN SHOES | STEP OUT / the city runner | KITE & CO | THE LINEN EDIT / light layers for the season |
| BLOOM FLORIST | FRESH TODAY / the peonies are in | AURA SKIN | GLOW / vitamin C serum |
| NOVA PHONES | NOVA 9 / brighter. thinner. yours. | SOLSTICE SPORTS | GO FURTHER / members save 20% |
| THE GALLERIA | MEMBERS' EVENING / Thursday, late opening | CONCIERGE | AT YOUR SERVICE / ask at the info desk |
| GILT & GRAIN | WARM FROM THE OVEN / sourdough, all day | PIXEL ARCADE | HIGH SCORE / new cabinets in store |

To change sign, menu, poster and banner words: edit `tools/skyline/look/make-textures.py` and re-run it (command in `docs/SKYLINE-LOOK.md`). Dialogue and barks go through `Encounters.cs` and `SkylineBarks.cs` as before.
