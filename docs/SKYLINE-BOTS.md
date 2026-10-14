# Skyline Repo: the bots

CONCIERGE's maintenance bots, white and gold. One section per bot, each written by that bot's identity agent (plan
Task 20). Every section covers: personality; states (idle, alert, telegraph, attack, hurt, plate shed, wreck) and what
each looks and sounds like; the telegraph (what you see and hear, how long before the attack); the habit it punishes;
its face set; its sounds (layers and what each does); its barks (drafts for the creator).

## Floor-care unit (`floor-care-unit`)

**Personality.** A sunny clean freak. It hums round the hall grinning, polishing, very pleased with itself, until it
sees a "stain" (you). Then the grin becomes gritted teeth and it drives at you to clean you out, with acid. It never
gets clever about it: it just wants the floor spotless. Aim tag: area. Layer: floor.

**Body.** A one-metre commercial floor robot in CONCIERGE white and gold: a white disc on a dark rubber skirt, a gold
domed lid (plate "lid") and a gold front bumper (plate "bumper"), a white screen housing on a neck, tilted back so its
face reads from standing height, with an amber beacon on top. On its back-left shoulder sits the acid canister, bright
green between steel caps: the weak point, and the one green thing on it, so it reads as the thing to shoot. A dark hose
runs from the canister round the side to the spray nozzle low at the front. A three-tuft side brush spins at its
front-right corner.

**States.**

| State | Looks | Sounds |
|---|---|---|
| Idle | happy face (mint "^ ^" eyes, rosy cheeks, a big grin, a sparkle); it cleans round its post until woken: short straight runs, gentle turns, a little bump-and-turn at cover, within 1.5 m (FloorCareIdle), the brush turning | a soft motor hum, the brushes swishing round, a slosh in the tank, now and then a small wheel squeak (a 3 s loop) |
| Alert | wide round eyes looking down at the "stain", eyebrows up, an "O" mouth, a yellow "!" | a bright rising appliance chime (bi-BEEP), the wheels skidding to a stop, the tank sloshing |
| Telegraph (0.6 s) | angry face; it turns to face you and the brush spins up fast | a relay click and a wheel squeak as it squares up, then the brushes whirring up while the pump primes with two wet glugs and the motor whines upward; it cuts out just before the spit |
| Attack | a low green jet sweeps a 120-degree arc at ankle height (0.8 s), leaving fading puddles | the pump's wet "glorp" and spit, and with it the acid hissing and fizzing across the floor |
| Hurt | amber "> <" eyes, a wobbly mouth, a sweat drop | a plastic knock and an indignant squeaky-toy "eep!" |
| Plate shed | the lid or the bumper flies off where it was hit | a plastic panel popping off with a ring of its gold trim, bouncing and skittering away |
| Wreck | grey "X X" eyes, tongue out, a cracked screen | the canister bursts (a wet pop and a gush of hissing acid), the motor winds down, a sad falling chime (G, E, a drooping C), a few sparks |

**Telegraph.** You see it turn to face you and the side brush spin up; you hear the whirr climb and the pump glug twice.
0.6 s later it spits. The sound dips to near silence in the last ~0.1 s, so the spit lands as a clear new onset.

**The habit it punishes.** Staying on the floor. The sweep is ankle height: jump, mantle or wall-run over it.

**Faces** (`Resources/SkylineModels/bots/floor-care-unit_face_<Face>.png`, 256 x 128, a 32 x 16 LED matrix):
happy (mint grin with cheeks and a sparkle), alert (wide eyes looking down, "!"), angry (red brows, narrowed eyes,
gritted teeth, an anger mark on the brow), hurt (amber "> <", wavy mouth, a blue sweat drop), wrecked (grey "X X",
tongue out, a cracked screen). Made by `tools/skyline/bots/floor-care-unit/faces.py`.

**Sounds** (`Resources/SkylineAudio/floor-care-unit/`; recipes in `tools/skyline/bots/floor-care-unit/recipes/`,
stems synthesized by its `sources.py`; domestic and wet: brushes, squeaky wheels, a pump, a hiss of acid):
`idle_loop`, `alert`, `telegraph`, `attack` (the pump spit, weighted low: centroid 706 Hz), `acid_sweep` (the hiss that
plays with it: centroid 8.4 kHz), `hurt`, `plate_shed`, `wreck`.

**Babble voice** (`SkylineBabble`, kept from Task 15): a triangle wave at 760 Hz with a wide 7-semitone wobble, quick
blips: chirpy and sing-song.

**Barks** (drafts for the creator): spots you "Ooh! A stain!"; hurt "Rude! I'm cleaning!"; loses you "Spotless
again!"; wrecked "Out of... order...".

## Mall-cop drone (`mall-cop-drone`)

**Personality.** Officious. A flying egg in a mall cop's peaked cap that takes its tiny authority very seriously:
bored and smug on patrol, outraged at any infraction, and it writes you a ticket with every shot. It shoots where you
were a moment ago, not where you are. Aim tag: leads you (kept for the never-mix rule; it lags, see "How the drone aims"). Layer: air.

**Body.** A white egg (about 0.7 m) wearing a peaked cap: the gold crown with a white star badge (plate "shell") and
the gold visor brim jutting over its face screen (plate "lens-guard"). Under the screen, the body-cam lens, blue glass
in a steel ring with a red REC light beside it: the weak point. Under its chin hangs a taser pod with two gold prongs
(the muzzle between them). A radio whip antenna on the back, two ducted rotors on struts either side.

**States.**

| State | Looks | Sounds |
|---|---|---|
| Idle | patrol face: half-lidded blue eyes, flat brows, a smug little smirk, a red REC light in the corner; the figure-eight hover | two rotors beating against each other, air chopped by the blades, the motors' thin whine, a soft "recording" blip each loop (a 2 s loop) |
| Alert | white square eyes, one brow raised, an "O" mouth, a red-and-blue siren bar on top | a clipped two-whoop siren yelp, a radio squelch, two confirming chirps |
| Telegraph (0.35 s) | angry face; it turns to you and a red charge ball swells at the taser | a sharp target chirp, then a capacitor whine climbing to the shot with a fizz under it; it clicks off just before the zap |
| Attack | a bolt aimed where you were 0.2 s ago | a taser zap (a snap, a crackling arc, a falling stun buzz), then the citation printer clacking out a ticket, torn off |
| Hurt | amber worried brows, eyes torn sideways by a glitch, an offended frown | a metallic tink, then an offended two-tone error buzz ("uh-uh") |
| Plate shed | the crown or the visor flies off where it was hit | a hollow plastic clonk and a ring of gold trim, a falling "whoop" of protest, two bounces |
| Wreck | grey "X X" eyes in screen static | a burst of sparks, the rotors winding down, three falling error beeps, the printer spewing paper, then it drops and clatters |

**Telegraph.** You see it square up and the red ball swell at its chin; you hear the chirp and the climbing whine.
0.35 s later the bolt flies, aimed at where you were 0.2 s before it left.

**The habit it punishes.** Standing still (and running straight at it, or straight away at a walk). Keep moving across its line of fire and it misses.

**Faces** (`Resources/SkylineModels/bots/mall-cop-drone_face_<Face>.png`, 256 x 128, a 32 x 16 LED matrix):
happy (bored patrol stare in police blue, smirk, REC light), alert (raised brow, "O", red-and-blue siren bar), angry
(red crosshair eyes under heavy brows, a shouting mouth), hurt (amber, glitch-torn eyes, frown), wrecked (grey "X X" in
static). Made by `tools/skyline/bots/mall-cop-drone/faces.py`.

**Sounds** (`Resources/SkylineAudio/mall-cop-drone/`; recipes in `tools/skyline/bots/mall-cop-drone/recipes/`, stems
synthesized by its `sources.py`; officious and electronic: rotor whine, chirps, a citation-printer clack, a taser-like
zap): `idle_loop`, `alert`, `telegraph`, `attack` (centroid 2.7 kHz, half mid and half presence), `hurt`,
`plate_shed`, `wreck`.

**Babble voice** (`SkylineBabble`, kept from Task 15): a square wave at 520 Hz with a narrow 2-semitone spread and
short clipped blips: a flat officious monotone.

**Barks** (drafts for the creator): spots you "Halt! Mall security!"; hurt "That's a citation!"; loses you "Suspect
fled. Noted."; wrecked "Officer... down...".

## Spotlight (`spotlight`)

**Personality.** A searchlight with a rifle and a security guard's eye: cold, patient, procedural. It sweeps the hall
with one big eye, quite content, and the moment it finds you it wants you to hold still for your photo. It guards
perches and hook anchors and punishes whoever lingers in the open up high. Aim tag: fires at where you are now
(telegraphed). Layer: air.

**Body.** A heavy theatre searchlight head, about 1.4 m long with its rifle, hovering over a small stand. The head is a
white drum widening to a big pale glass lens in a thick gold bezel (the lens is the weak point: the beam leaves its
centre), dark cooling fins and a white cap at the back, a gold band round the drum. Its gold side housings, vented
(plates "housing-left" and "housing-right"), cover the drum's flanks. A white visor on top, under a gold brim, holds the
face display. A coil rifle is slung low on its right: a dark receiver, a gold battery, three copper capacitor coils round
the barrel and a gold muzzle brake. The stand is a dark plinth with a gold trim and a white levitator plate (a dark
emitter disc in a gold ring, three gold prongs); the head floats about 0.35 m above it, so it can turn and tip anywhere
without cutting through a yoke.

**States.**

| State | Looks | Sounds |
|---|---|---|
| Idle | a content crescent eye in lamp-white, three status lamps; the head scans slowly left and right | a mains hum (100 Hz, the ballast's rasp above it) breathing once every 2 s (a 2 s loop) |
| Alert | the eye snaps wide open with a pinprick pupil, brows high | a heavy contactor clunk, the arc lamp striking (a bright fizz) and the hum surging up |
| Telegraph (0.8 s) | angry face: a red crosshair under a hard V brow; a thin red beam follows you with a lag, then turns white and widens at the lock | a contactor clunk, then the lock tone: a buzzing stack climbing two octaves while the beam follows; at the lock a hard double pip (`lock_beep`) and the tone stops climbing and holds one pitch, fading, until the shot |
| Attack | a white flash down the beam and a tracer from the rifle | a hard rifle crack (bright and short), a low thump, a falling electric zap, discharge crackle and the crack's slapback off the glass |
| Hurt | the eye squeezed shut into two amber chevrons, static around it | the lens's glass tink, a knock in the drum, the lamp sputtering three times |
| Plate shed | a gold housing flies off where it was hit | a thick metal clang, bolts pinging out, sparks |
| Wreck | grey X and dead pixels | the lamp blows (a pop and a boom), the big lens shatters, an arc sizzles, the hum dives to nothing and a breaker drops out |

**Telegraph.** You see the red beam land on you and follow, a little behind; 0.55 s in it turns white and wide (the
lock). You hear the clunk, the tone climbing with the beam, then the double pip at the lock and the tone freezing on
one pitch. The shot comes 0.25 s after the lock, 0.8 s after the clunk. Change direction after the pip, or break line
of sight.

**The habit it punishes.** Lingering exposed up high: standing on a perch, hanging on a hook anchor or floating in a long
jump in its view.

**Faces** (`Resources/SkylineModels/bots/spotlight_face_<Face>.png`, 512 x 256, a 32 x 16 LED matrix drawn by the shared
`pixel_faces.py`; its colour is
the warm white of its lamp): happy (a content crescent eye, three status lamps), alert (the eye wide with a pinprick
pupil, brows raised high), angry (a red crosshair under a V brow), hurt (amber chevrons squeezed shut, static), wrecked
(a grey X, dead pixels). Made by `tools/skyline/bots/spotlight/make_faces.py`.

**Sounds** (`Resources/SkylineAudio/spotlight/`; recipes in `tools/skyline/bots/spotlight/recipes/`, written by its
`make_recipes.py`; heavy-electric: a mains hum, relay clunks, a rising lock tone, a hard crack): `idle_loop`, `alert`,
`telegraph`, `lock_beep`, `attack` (centroid 4.1 kHz, 77% of its energy in the 2-8 kHz presence band, -15.0 LUFS: its
`sources.py` drives the shot into a soft clip so it sits with the other bots' attacks), `hurt`, `plate_shed`, `wreck`.

**Model** (`Resources/SkylineModels/bots/spotlight.fbx`, the turning head, and `spotlight_mount.fbx`, the stand that
never turns; built by `tools/skyline/bots/spotlight/build_model.py` in Blender with the shared `blender_kit.py`; seven
parts in the head, one in the stand).

**Babble voice** (`SkylineBabble`, kept from Task 15): a sawtooth at 240 Hz with a 1-semitone spread, slow blips: a flat,
buzzing announcer's monotone.

**Barks** (drafts for the creator): spots you "Lights on you. Smile."; hurt "My lens! My LENS!"; loses you "Searching
the dark."; wrecked "Lights... out...".

## Window-washer gondola (`window-washer-gondola`)

**Personality.** A cheery old window-washer with a big handlebar moustache, in CONCIERGE gold. It rides its rail along
the façade, whistling at its work, and takes your wall-run as a personal insult to its clean glass: it slaps at you with
its squeegee and sticks mines on the panes behind it. Aim tag: area. Layer: walls.

**Body.** A 2.2 m window-washer's platform hung from a trolley on a rail: a white deck on a dark frame with a gold kick
plate and toe boards, white railing posts under a gold top rail, an A-frame stirrup at each end. Two cables run up from
the stirrups to the winch under the trolley; the winch's exposed copper coil, between gold flanges, is the weak point.
Two gold buckets of suds with wire handles (plates "bucket-left" and "bucket-right") stand at the ends, a spare squeegee
in the left one. In the middle front, a white control box holds the face display in a gold frame, a gold beacon
on top. On the left, a dark crate with three disc mines (gold rims, a red LED on the top one): mines are tossed
from its wall side. On the right, the squeegee on a pole, hinged on the top rail.

**States.**

| State | Looks | Sounds |
|---|---|---|
| Idle | gold "^ ^" eyes over the moustache, a grin under it; it washes the panes until woken: a slow ride back and forth along the first 2 m of its rail with a stop at each end (GondolaIdle), the squeegee swiping the glass | the winch's gear motor droning (120 Hz with a gear whine beating three times a second) and the rope creaking twice (a 2 s loop) |
| Alert | white "O O" eyes, brows shot up, an "o" under the moustache | the brake letting go (three ratchet clicks), a cable twang as the platform jolts, and a workman's two-note whistle |
| Telegraph (0.4 s) | angry face (red brows down, the moustache bristling, a set mouth); the squeegee winds back toward the wall | the platform's warning beeper: five pips at 1.25 kHz, speeding up and rising, over by 0.33 s |
| Attack | the squeegee swings out and slaps (no damage: the gondola's harm is its mines) | a whoosh, the rubber blade squealing across glass and chattering, a slap at the end |
| Mines | a disc mine is tossed from the crate onto the wall (`mine_lay`), arms (`mine_arm`), and when you come near its trigger ring shows and it beeps (`telegraph`) for 0.35 s, then blasts (`mine_blast`) | a magnetic clamp's thunk and latch; a rising two-note chirp; the warning beeper; a sharp crack with a punchy body and glass and shrapnel pinging out |
| Hurt | pale soapy-blue "> <" eyes, a limp moustache, a wince, a blue sweat drop | a tin bucket's hollow bonk and a cable twanging under the jolt |
| Plate shed | a gold bucket flies off where it was hit | a tin-pail clank, its handle rattling, a second smaller clank as it tumbles |
| Wreck | grey "X X" eyes, the moustache drooping | a cable snaps (a crack and a whip falling in pitch), the winch runs down, the platform drops and clangs, the rope creaks under the dead weight |

**Telegraph.** The squeegee: you see it wind back and hear the beeper, 0.4 s before the slap. The mines (its real
attack): a mine you come within 1.5 m of beeps the same beeper and shows its ring for 0.35 s before it blasts (2.5 m,
25 damage). Leave the wall when you hear it.

**The habit it punishes.** Long wall-runs. It lays a mine every 4 m of rail (six live at most, each lasting 12 s), so
the wall behind it fills with them; wall-running there is as risky as the floor.

**Faces** (`Resources/SkylineModels/bots/window-washer-gondola_face_<Face>.png`, 512 x 256, a 32 x 16 LED matrix drawn by
the shared `pixel_faces.py`; its colour is CONCIERGE gold, never the firm's orange): happy (gold ^ ^ eyes, the handlebar
moustache, a grin), alert (white O O eyes, brows up, an "o"), angry (red, hard brows, small eyes, a bristling moustache,
a set mouth), hurt (pale soapy blue > <, a limp moustache, a blue sweat drop: blue rather than amber, so it can't be
mistaken for its gold calm), wrecked (grey X X, the moustache drooping). Made by `tools/skyline/bots/window-washer-gondola/make_faces.py`.

**Sounds** (`Resources/SkylineAudio/window-washer-gondola/`; recipes in `tools/skyline/bots/window-washer-gondola/recipes/`,
written by its `make_recipes.py`; winch-and-cable: a gear motor, rope creak, cable twangs, a rubber squeegee, mine beeps
and a sharp blast): `idle_loop`, `alert`, `telegraph`, `attack` (the squeegee: centroid 1.2 kHz, 77% mid), `mine_lay`,
`mine_arm`, `mine_blast` (centroid 1.9 kHz, its energy spread low 34%, mid 27%, presence 39%; -14.6 LUFS, driven into
a soft clip by its `sources.py`), `hurt`, `plate_shed`, `wreck`.

**Model** (`Resources/SkylineModels/bots/window-washer-gondola.fbx`; built by
`tools/skyline/bots/window-washer-gondola/build_model.py` in Blender with the shared `blender_kit.py`; 32 parts, the
railing, stirrups and winch kept separate so no joined collider covers a bucket or the coil).

**Babble voice** (`SkylineBabble`, kept from Task 15): a thin square wave (25% duty) at 380 Hz with a 5-semitone spread:
a chatty, sing-song workman.

**Barks** (drafts for the creator): spots you "Oi! Off my glass!"; hurt "Watch the bucket!"; loses you "Back to the
panes."; wrecked "Hanging... up...".

## How the drone aims (the creator, 2026-10-14)

"It's too unpredictable right now. Make it whiff less when you're standing still, and make it miss more when you're moving fast. Like
maybe it shoots at where you were 10 or 20 frames ago, or 0.1-0.2 s ago."

**Before:** a lead of 0.75 (`Aim.LeadPoint` with Normal's factor) plus a random error over a 4 degree cone. At 20 m that cone is a
1.4 m radius disc, so a player standing still was hit by 46% of bolts over 8-30 m (99% at 8 m, 28% at 20 m, 13% at 30 m), and a
sprinter was hit 11-60% depending on the angle: movement hardly changed anything.

**Now:** `EnemyDef.aimLagSeconds = 0.2` and `aimSpreadDeg = 0.5`. Each bot keeps a short history of the player's chest
(`PositionTrail`, a 128-sample ring fed every frame, cleared by a teleport of more than 8 m or a restarted clock) and fires at where
the chest was 0.2 s earlier, tilted by at most 0.5 degrees (a 0.26 m disc at 30 m). No leading. FightSim, CombatSim/EncounterDodge and
`SkylineBot` all use the same rule. A bolt flies at 30 m/s, so its flight (0.27 s at 8 m, 1 s at 30 m) adds to the miss: a mover
is missed by `speed x (0.2 s + flight time)` across the line, against a hit width of 0.53 m (the Unit's 0.38 m radius plus the 0.15 m
bolt). Straight at the drone or straight away the line is the same, so the bolt still meets you (the drone hovers about 3 m up, so a
fast run toward it or away leaves the bolt's descending line too: a vertical miss).

**Why 0.2 s:** the three lags in the creator's range give the same result across the line (0% for any speed from 4 m/s: the flight
time alone puts a walker 1.5-6 m off), but along the line a lag of 0.1 s still hits a sprint at the drone 94% of the time and 0.2 s
hits it 58% (13 m/s: 22% and 5%). The longest lag is the one that makes fast movement miss most.

Hit share of one bolt over 8, 12, 16, 20, 25 and 30 m (6 ranges, equal weight; 400 bolts each, a drone 3 m above the chest, bolt 30 m/s,
a target on a straight run; `LaggedAimTests`). "Any direction" is the mean over runs every 15 degrees to the line from the drone.

| Target | Old: across | toward | away | any direction | New: across | toward | away | any direction |
|---|---|---|---|---|---|---|---|---|
| standing | 46% | 46% | 46% | 46% | 100% | 100% | 100% | 100% |
| walking 4 m/s | 42% | 53% | 38% | 44% | 0% | 100% | 100% | 17% |
| 5 m/s (the Unit's walk) | 37% | 54% | 36% | 42% | 0% | 100% | 78% | 14% |
| 8 m/s (a wall-run) | 20% | 59% | 29% | 34% | 0% | 83% | 0% | 6% |
| 9.5 m/s (sprint) | 11% | 60% | 25% | 29% | 0% | 58% | 0% | 4% |
| 13 m/s (slide / fast) | 2% | 65% | 17% | 20% | 0% | 5% | 0% | 0% |

New standing: 100% at every range from 8 to 30 m (old: 99, 73, 44, 28, 19, 13%). Read it: standing still is punished every time;
moving across the line of fire is never hit at 4 m/s and up; the ways of still being hit while moving are running straight at the
drone below 9.5 m/s or straight away at a walk (a lag of 0.2 s is not long enough to miss those, and leading would be the cure,
which the creator ruled out). Pinned by `LaggedAimTests`: standing >= 90% at every range, a sprint or faster missed >= 75% across,
obliquely and away (toward the drone at 9.5 m/s: under 65%, at 13 m/s: under 25%), hit share monotone in speed, and walking sitting
between standing and sprinting over all directions.

**What moved with it (floor sims):** (1) `TacticalBot` (the skilled line) strafes up to 3 m either side of its peek point,
flipping every 0.6-0.9 s, while a lagging drone sees it (`FightTiming.peekStrafe`): a skilled player moves, and standing in the open
for a 1-1.8 s peek was hit every time (food court: 3 of 30 seeds clean before the strafe). (2) The drone's cycle 0.8 -> 1.0 s and
damage 26 -> 24: with perfect accuracy on a standing target the old numbers took three times the hits; the new ones hold the
skilled-line gates (the cycle stays 1.0 s or less because the hover patrol needs a volley every second to keep strafing 0.6 m a
second). (3) The dodge band for drones turned over: the lane runner crossing the fire is hit at under a quarter of the jinker's
rate (the jinker stops to turn). (4) The facade's steady-pressure gate (median lowest plating at most 60) is now `[Ignore]`d as a
gap for the creator: the strafing line takes about 1.6 hits a fight there and the median is 76.

**D21 (bolts and glass):** a drone's bolt now stops at balustrades, the skylight and the bridge floor, and breaks a facade
pane (`SkylineFacade.TryBreak`) and flies on, as the player's shots do. Sight still passes through all glass (D21: "all glass is
see-through for sight"; the pure sim and the player's `SkylineShooting.Mask` do the same), so a drone behind a rail sees you,
telegraphs, and its bolt hits the rail.

## Damage states (every bot; the creator, 2026-10-14)

"I want to see model state changes when taking damage changes health thresholds, for example a hit that takes a drone
from above 90% to less than 30% of its health should show that damage in the model, with an animation that shows that
damage happening."
- Each bot has **visible damage stages at health thresholds** (starting set: 70%, 30%): panels cracked or torn off,
  dents, exposed wiring, sparks and smoke, a limp or a wobble in its movement, its face reacting.
- **Crossing a threshold plays the damage happening** (a panel snapping off and tumbling, a rotor guard bending, a burst
  of sparks), not a silent swap. **One hit that crosses several thresholds plays each stage's animation in quick
  succession** (above 90% to below 30% shows the 70% and the 30% damage happening, back to back).
- Damage states are separate from Writ spots (the cracked casing with yellow outlines) and stack with them.

## The gondola rides OUTSIDE the glass (the creator, 2026-10-14)

"Why were the gondolas on this side of the walls? Shouldn't they be on the outside, looking in? So they can plant the
mines through the glass?" The window-washer gondola rides the façade's outside face, looking in through the glass, and
lays its mines on the inside of the wall-run (through the façade's service hatches). Built so in the Food Court floor's
east façade.

## How the bots' sounds are mixed (2026-10-14)

The bots' one-shots (alert, telegraph, attack, hurt, plate shed, wreck, mine and acid sounds) go through the voice gate and a gentle
harsh-band EQ (3.4 kHz, -4 dB; -5 to -6 dB for the drone's telegraph, floor-care hurt and alert, the spotlight's hurt and attack),
at most 2-3 of each kind at once with the nearest bot winning; their barks are one at a time, quieter and low-passed; only the nearest
three idle loops are heard. The recipes themselves are untouched. See `docs/SKYLINE-PIPELINE.md`, "The mix". The creator picked replacements for four sounds that were grating on their own (options in
`FableAnimationTests/audio/skyline-sfx-options/`), now installed: drone `telegraph` = rotor spool, floor-care `hurt` = tank slosh,
spotlight `hurt` = dying bulb, spotlight `attack` = the rifle muffled at 1.5 kHz with static and sparse crackle clicks (2-5 kHz share
0-4%). Their recipes: `bots/mall-cop-drone/recipes/telegraph.json`, `bots/floor-care-unit/recipes/hurt.json`, and the spotlight's
two from `bots/spotlight/make_recipes.py` (`attack()`, `hurt()`).
