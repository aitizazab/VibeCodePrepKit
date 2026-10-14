# Skyline Repo: every line of dialogue in M1, for you to rewrite

Edit this file freely (it's yours; no builder touches it). When you're done, tell Claude, and it copies your lines
into the code (`Encounters.cs` for the teaching lines, `SkylineBarks.cs` for the barks). Lines marked **(yours)**
are already your writing; the rest are Claude's drafts.

## Teaching lines (shown as subtitles, spoken in babble)

| When it plays | Who | Line |
|---|---|---|
| The warm-up starts | Nan | Unit's live. Warm-up hall first; the Galleria's through the far doors. Try not to dent anything we have to return. |
| The warm-up starts (after the first) | Nan | Tap jump twice for a second hop, and reach a ledge's lip to mantle up. Every kiosk in here is built for practice. |
| You first see the practice bot | Dot | Practice bot, dead ahead! Tag its lens with the Writ and it leaves a weak spot. Hit the spot: hurts more than the lens! |
| Your first weak spot appears | Dot | That glowy bit? That's the spot. Shoot it! |
| You get near the kiosk ledge | Dot | Breacher kicks both ways. Jump, jump again, then shoot your own feet. There's scrap on top of that kiosk. |
| You reach the top of the kiosk | Dot | Ha! One boost a jump, mind. Land or touch a wall to get it back. **(Out of date: the boost is now limited only by fire rate, and the ground and walls soak it up. Suggested: "Ha! Every shot in the air kicks. On the ground or a wall, it just soaks in.")** |
| Your plating first breaks | Dot | Strip it! Scrap's armour! **(yours)** |
| First floor-care unit | CONCIERGE | Please note: the floor-care unit operates at ankle height. Guests are advised to jump. **(yours)** |
| First drone | CONCIERGE | Please note: security drones anticipate where guests are going. Guests are advised to change their minds. |
| First spotlight | CONCIERGE | Please note: the spotlight confirms its target before firing. Unconfirmed guests turn sharply, or stand behind something. |
| First gondola | CONCIERGE | Please note: window cleaning is in progress on the walls. Guests are advised to keep their feet on the floor. |
| The next fight is ready (at the hall entrance) | CONCIERGE | Security is re-deploying. Guests are asked to return to the food court entrance. |

## Enemy barks (a small speech bubble, spoken in the bot's babble)

Each bot has four: when it spots you, when it's hurt, when it loses you, when it's wrecked.

| Bot | Spots you | Hurt | Loses you | Wrecked |
|---|---|---|---|---|
| Security drone | Halt! Mall policy! | Assault on an officer! | Suspect has left the premises. | Code... red... |
| Floor-care unit | Oh! A stain! | Rude! | All clean! | Out of... order... |
| Spotlight | Target acquired. Smile. | Lens damage reported. | Scanning. | Lights... out. |
| Window-washer gondola | Mind the squeegee! | Window's dirty! | Shift's over. | Hanging... up... |

## Floor 1 lobby (the Halcyon Grand) and the Food Court: teaching lines

All drafts for you to rewrite, except the lines marked **(yours)**: those the specs quote. They live in `FloorBeats.cs`
(`FloorBeats.Floor1()`). The lessons from the warm-up hall moved into the lobby with their words unchanged.

| When it plays | Who | Line |
|---|---|---|
| The Unit arrives in the staff hallway | Nan | Unit's live. This is the Halcyon Grand. Warm up in the lobby, then the lift takes you down to the Galleria. |
| You come within 8 m of the front desk, in sight | CONCIERGE | Welcome to the Halcyon Grand. Guests are reminded that nothing in this lobby is for sale, and nothing in it is yours. |
| You first see the practice bot (the bellhop demo) | Dot | Practice bot! Tag its lens with the Writ: it cracks open a weak spot. Then hit the spot. |
| Your first weak spot appears | Dot | That glowing crack? That's the spot. Shoot it! |
| You get near the practice counter | Nan | That counter's your practice. Tap jump twice for a second hop, then catch its lip and pull yourself up. |
| You get near the luggage stack (the boost ledge) | Dot | Breacher kicks both ways. Jump, jump again, then blast the floor. Scrap's up top! |
| You reach the top of the stack | Dot | Ha! Doesn't hurt you, either. Shoot again mid-air to climb higher. |
| The lift ride starts (the riser) | Nan | Job sheet: three assets in the food court. Stand in a tow circle for ten seconds to take one. The clock's running, love. (the phrase "the clock's running, love" is **yours**) |
| The drop: the doors open, the first wave arrives | CONCIERGE | Security has been informed of your arrival. Security would like a word. Security is on its way. |
| That first meter sounds the alarm | CONCIERGE | Attention: an item is leaving the premises without a receipt. Security has been invited to take an interest. |
| The first asset is towed (the tow ends; moved from the meter starting, the creator, 2026-10-14) | Dot | Clamped! Get Moving! **(yours, 2026-10-14)** |
| The first time the Unit leaves an asset's circle while its tow is under way (once) | Dot | Don't go too far, you need to stay nearby to transmat! **(yours, 2026-10-14)** |
| Each later asset is towed (the second and third) | Dot | Got it! Up and away. Next one, go go go. |
| A wave arrives (said on every third wave: 3, 6, 9...) | CONCIERGE | Reinforcements are arriving. Guests are advised that this is not an invitation to wait. |
| The third asset is towed | CONCIERGE | Asset transfer... acknowledged. Under protest. **(yours)** |
| First floor-care unit | CONCIERGE | Please note: the floor-care unit operates at ankle height. Guests are advised to jump. **(yours)** |
| First drone | CONCIERGE | Please note: security drones anticipate where guests are going. Guests are advised to change their minds. |
| First gondola (outside the glass; it lays mines, it never shoots) | CONCIERGE | Please note: window cleaning is in progress outside the glass. Guests are advised that the squeegee leaves things behind. |
| Your plating first breaks | Dot | Strip it! Scrap's armour! **(yours)** |
| You first pick up special ammo | Dot | Special rounds! Kill with the Writ and the wreck drops one. The Breacher runs dry; the Writ never does. |

The spotlight has no notice here: it is not on this floor.

## Comic lettering (hit and kill words)

Drafts for you to rewrite. They live in `ComicLettering.Words` (`Runtime/Skyline/ComicLettering.cs`) as plain string arrays. The
core picks one word of the set (never the same word twice in a row for one bot type or gun), and one of your two fonts, Luckiest
Guy or Bangers, per word (never the same font on both of two words showing at once). Lettering is hostile red-magenta with a
dark ink keyline, at most two on screen, at least 0.35 s apart, only for the events below.

| When it plays | Words |
|---|---|
| A security drone is wrecked | KZZT! / BZZT-CRK! / ZAP! |
| A floor-care unit is wrecked | SPLORCH! / GLORP! / SPLAT! |
| A window-washer gondola is wrecked | CLANG! / KA-SPLASH! |
| A spotlight is wrecked (new draft; the spotlight is not on floor 1) | KLAK! / FZZT-POP! |
| A weak-point hit that is not a kill (any known bot) | CRACK! / TINK! |
| A Writ spot breaks | OBJECTION! / SUSTAINED! / OVERRULED! |
| A big Breacher shove (knockback of 8 m/s or more) | WHUMP! / BWOOM! |
| Any other (unknown) bot is wrecked (new draft, a fallback) | POW! / BAM! / KRAK! |
| A weak-point hit on an unknown bot (new draft, a fallback) | PING! / TICK! |
| A spot breaks from an unknown gun (new draft, a fallback) | SNAP! / CRACK-OPEN! |
| A shove from an unknown gun (new draft, a fallback) | THUD! / WHAM! |
