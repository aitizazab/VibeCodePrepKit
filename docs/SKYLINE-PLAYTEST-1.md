# Skyline Repo: the creator's first playtest (2026-10-14) and the work it set

**Start here in a new session.** M1 (the first floor) was built and judged overnight on 2026-10-13/14 (worklog,
`docs/skyline-judge/`). The creator then played it. Their verdict: integral problems first, balancing later. This page
holds their calls (their words quoted) and the work queue. It overrides older docs where they disagree.

## Standing rules for every session

- **After every build, refresh the restart docs** (this page, `docs/SKYLINE-OPEN-THREADS.md`, `docs/SKYLINE-WORKLOG.md`)
  and commit them, so the creator can restart with fresh context. Overnight runs: once, when the night's work is done.
  (The creator, 2026-10-15.)
- **Never drop a thread silently:** every open thread and its next step lives in `docs/SKYLINE-OPEN-THREADS.md`; re-ask
  unanswered questions in the next summary. (The creator, 2026-10-14: "can we stop randomly dropping things".)

## Tonight first (2026-10-15 night, paused at the creator's call; read before "Where we are")

- **Music approved:** Judgement v2 (danger, the Empress of Light opening) and Sanctuary v2 (quiet, Plankerton part 2); Heaven
  stays the normal layer. **Updraft and Rapture** (the speed stems) are rendered per mood; Rapture's celesta became a saw-synth
  pluck (too close to Judgement's sparkle). Waiting on the creator: the layered page (each mood at still / fast / flying),
  `FableAnimationTests/art/review/2026-10-15/skyline-music-speed-layered.html`.
- **The riser into the pre-drop: not solved.** v4 (5 bars), v5 (note-wise) and v6/v6b (both sides meeting in the middle, about
  4 s to the drop) were judged too long or not a conversion; "the riser bleed-in is bad, made worse by how bad the PopularMMOs
  theme was recreated". **The PopularMMOs intro in our instruments failed twice** (synth/sampled rebuild; a layered analysis-by-
  synthesis replica, stopped, notes in `audio/skyline-music/pmmo-replica/RESUME.md`). **Next: an a cappella rebuild** from the
  creator's own voice: method in `FableAnimationTests/audio/skyline-music/research/ACAPELLA-METHOD.md` (record near final
  pitch; pitch with ffmpeg rubberband + pyworld; the sub synthesised from the hum's pitch; the hidden layers). Then the riser.
- **Perf A/B:** switches `-skyline-no-lineweight`, `-skyline-no-damage-fx`, `-skyline-old-bot-sizes` built and merged to main
  (batch pass `out/skyline/20261015-0349`: 1123 tests pass, lint 0, build OK). Next: the windowed runs (stage D): a baseline,
  then each switch alone, via `pass.ps1 -Only D -Out out/skyline/20261015-0349 -PerfArgs '<switch>'`, copying perf.json away
  between runs. **The per-assembly compile check** is built (`core-tests/SkylineUnity/check-asmdefs.ps1`; all 9 pass).
- **Floor-care redesign:** paused before code; plan and code map in `VibeJamKit-floorcare/RESUME.md` (worktree, branch
  `skyline-floorcare`). The spotlight humanoid not started.
- **Agents:** the creator saw a drop in quality from the Sonnet agents: only short agent tasks (about 1-2% of usage) run to
  completion; longer work waits for the creator.

## Where we are (updated 2026-10-15 after pass perf11; read this first in a fresh session)

- **The plan (what to do next, the slice, and beyond) is at the top of `docs/SKYLINE-OPEN-THREADS.md`.**
- **Latest build:** `out/skyline/20261015-perf11/build/SkylineRepo.exe` (the M1 arena in the comic-ink look, line weight
  on; the Relay from the pause menu). In it: the reworked Writ (no chair or gavel, clean ADS, the curling quill, the
  paper-writing reload, the indigo gleam), halved hip aim assist, the new bot-hit sounds (no bot hurt sounds), the drones'
  lagged aim (they fire where you were 0.2 s ago), rails stopping bot bolts, bigger bots and much bigger weak points
  (drone rotors), damage states on every bot, and floor-1 runtime pieces that are not playable until the floor director
  (X0) exists.
- **Pass perf11:** 1118 EditMode tests pass, 1 balance gate parked (the food court's steady pressure, like the façade's:
  the lagged aim misses strafers by design; a balance question for the creator), lint 0, build OK, sync 5/5.
  **Perf regressed** since perf10: slowest 1% of frames 22.3 ms (perf10: 16.3), worst fight 28.2 ms; in the food-court
  fight both the GPU (15 ms wait) and the CPU (12.8 ms) rose. Suspects: the drone branch (bigger bots = more outline
  pixels, damage-state sparks and smoke) and line weight. Next: A/B with each switched off.
- **Music:** "Heaven" (v3) is approved as the normal layer; the named variants Judgement, Sanctuary, Updraft and Rapture
  come next; the riser into the pre-drop was redone from scratch (perfect and step versions) and waits on the creator.
- **Every open thread and its next step:** `docs/SKYLINE-OPEN-THREADS.md`. **History:** `docs/SKYLINE-WORKLOG.md`.
  **Music:** `docs/SKYLINE-MUSIC.md`. **The Writ:** `docs/SKYLINE-GUN-WRIT.md`. **Bots, the drone aim table, the planned
  Valet:** `docs/SKYLINE-BOTS.md`. **Floor 1 plan and task map:** `docs/SKYLINE-PLAN-FLOOR1.md`. **Relay:** `docs/SKYLINE-RELAY.md`.
  **Style (line weight on; the other options off):** `docs/SKYLINE-STYLE.md`.
- **Usage:** the week is in the red: well-scoped Sonnet agents only (Opus agents only if the creator forces it).
- **Disk:** keep it clear: after each pass delete the previous pass folder's build/, *.mp4 and stills (literal paths).

## How we work now (the creator, 2026-10-14)

- "We'll work less on this today by agents, just you and me balancing the current experience now that it's built. IF
  we do call any agents, make sure they're well-scoped Sonnet agents, meant to implement a plan, or research a specific
  fact, or do a specific thing. The only exception is Weapon Ideas I want in the game immediately."
- Budget: the week's usage hit 20% with the pace in the red after the overnight run (resets Tue 20 Oct 09:00).

## Later on 2026-10-14: the level is decided; the art style comes first

- **The level design is approved and specced:** `docs/SKYLINE-FLOOR-REPOSSESS.md` (repossess three listed assets in
  tow circles; waves every 30 s from flanking bearings, at most about 10 bots alive; a pit + ring + shops + galleries
  arena with a bridge, escalators, a service corridor and a façade wall-run). The creator: "A as the objective is fine,
  and starting numbers are fine." **Not built until the art style is settled.**
- **Art style before anything else is built or generated.** The creator: "the game doesn't look fun to play, and the
  vision I had for the art seems unrealistic with how generated images look like. The weapons, the models for enemies
  and later friendly AI, and other important stuff stays, but what about the space we're actually in? It's
  prohibitively expensive to detail it all out like we do for the guns and enemies, but it absolutely needs something
  better than what we're currently doing. So we need to look at other 3D first-person indie games, to see the kind of
  aesthetic they push onto their backgrounds and scenery."
- **Concept art must sit inside a defined style:** "the art looks too AI-Generated, and it's annoying me. It's not
  coming up with cool visuals, and it's not within a defined art style we have either, which is a massive mistake.
  Once we have the background, the basic enemy models and the gun models defined, the rest of the art should be based
  in the same art style." (Cause on our side: every Breacher prompt shared one template, "a premium weapon-skin
  reveal, three-quarter angle, moody studio light, dark backdrop", so all twelve read alike.)
- **The Breacher, round 2:** "B. Take 1 looks the best, but only by a bit, and none of them are particularly
  impressive. They all are very same-y, even if things are changing." The club subwoofer (B) leads; redo it inside the
  chosen style.
- **The style is decided: `docs/SKYLINE-STYLE.md`** (comic ink, thick hued outlines on bots/objectives/scrap, light
  that takes over a room like Deep Rock Galactic, colour-free zones, Team Fortress 2's level of detail). Look test plan:
  `docs/SKYLINE-PLAN-STYLE.md` (the current arena re-shaded; the creator judges stills from the game: "I'll wait for the
  scenes from the game to judge").
- The style comparison page: `FableAnimationTests/art/review/2026-10-14/skyline-art-style-2.html` (Steam screenshots of
  first-person games, grouped by approach).

## Fixed on 2026-10-14 (commit 96a7338, build `out/skyline-play/2026-10-14-m1b/SkylineRepo.exe`)

- **Invisible bots in the build.** Their painted parts rendered see-through (shadows only). Cause: the bot paint
  material enabled `_NORMALMAP` at run time; the build strips shader variants no saved material uses, so the depth
  prepass variant was missing and the surfaces failed the depth test. Fix: `Resources/SkylineFx/SK_Bot_Paint.mat` and
  `SK_Fx_Acid.mat` carry `_NORMALMAP` (and a normal texture) so the build keeps the variants. **Rule: any keyword a
  material turns on at run time must be saved on some build-included material.** `-skyline-render-diag` logs every bot
  and viewmodel renderer's material, shader, queue and keywords in a build.
- **The magenta** was the drone's alert light (a design choice), not a broken shader.
- **A pause menu** (Escape / Start): resume; Master, Music, Effects, Voices volume; rebinding for pad and keyboard
  (crouch/slide defaults to R3, B/Circle free); look sensitivity, invert Y, aim assist; quit. Saved in PlayerPrefs.
- **Aim down sights** (right mouse / left trigger): the Writ looks through its round window to the obelisk; the
  Breacher shoulders. FOV, look, move and spread scale per gun (`SkylineGuns` aim fields).
- **The practice drone** no longer whines and beeps forever (soft hum; alert once; quiet unless hit).
- **Not yet confirmed by the creator in play:** all of the above. The build-time screenshot check caught a terminal,
  not the game; the render diagnostic says the drone's parts render with the right materials.

## The creator's calls, by area

### Level design and the objective (the biggest one)
- What's wrong: "It basically just forced jiggle peeking from behind a wall to pick off the drones one by one, with no
  other viable strategy. That's not fun, that's boring."
- Patching symptoms is out: "Drone projectiles are already slow enough that you can dodge them when looking right at
  them, and plating from stripping wrecks within the timer is already the only healing source, but it doesn't matter
  because playing cover means you take no damage anyways."
- The ask: "The Arena itself changing is what I meant, **the objective needs to move from clearing out all the enemies
  to something else**, and **the arena needs to be more complex than just a room with some walls randomly in the middle
  and a catwalk up high**. And **waves need to spawn in successively, not when the player feels like it**, so taking
  your time isn't an option."
- Next step: propose objective types (e.g. escort/hold/carry/reach/disable) and a more complex arena layout in a short
  design for the creator before building; then timed successive waves.

### Visual fidelity
- "The sunlight and shadows are honestly more annoying than cool fidelity, because everything else is so undetailed."
  Tone the hard sun and shadows down until the rest has the detail to carry them.
- **Enemy clarity is a principle:** "enemies need to be clear in the background (the way I tracked them in the first
  playtest was because of the text bubbles above them, which isn't a good sign for enemy clarity)". The gondola's mines
  must follow it too.

### The bots (redesigns; the creator will judge them properly once the level design exists)
- All three main bots are a bit **larger**. The main enemy (the drone) was "tiny. I could barely see it at the ranges I
  was fighting it."
- **Floor-care unit:** no protrusion for the face; the face display sits on the **top face of the roomba**. The only
  protrusion is the **acid canister**. When it reaches you it **winds up, then deploys the acid like a molly** where it
  burst. It must be shot (the canister) or it **recharges in 15 seconds** and can go again.
- **Mall-cop drone:** larger, with **much larger rotors, which are the new weak spot**. The faceplate is **not a separate
  screen** from its face's surface.
- **Spotlight:** the look is fine, but they become **larger humanoid robots with the spotlight as their head**, who
  **charge up electric railguns before locking on and firing**.
- **Gondola:** not seen yet (the creator never got onto the walls); notes later. Its mines must read clearly at range.
- Art route: the creator wants CC0 first (Tripo credits are limited; "I'm not averse to buying more explicitly, I'd
  like to avoid spending money where we can"). The CC0 candidates are on `FableAnimationTests/art/review/2026-10-14/cc0-bots.html`
  (downloads in `out/skyline-refs/cc0-bots/`); the as-built models on `.../skyline-bots-as-built.html`.

### The Writ (a weapon idea: full treatment allowed)
- "The quality of the Writ is pretty low."
- **Reload:** "a flop, it's just an implied swap of the magazine in the handle, instead of something like a smack of
  the gavel." The reload is a gavel smack.
- **Sound:** "quite grating". **The gavel crack only when a weak spot is shot; otherwise the shot is a small thwip, like
  shooting a small quill.**
- **Tracers:** they look like Chamber's Headhunter; make them unique: **ink from a quill**.
- **The back of the gun** (above the handle, facing the player) "is just a boring box. It should extend the unique
  design backwards more." The creator will review this in Flow first.

### The Breacher (a weapon idea)
- Of round 1 (`art/review/2026-10-14/skyline-breacher-concepts.html`) only the **Sonic Boom subwoofer** pitch was
  interesting, "but the image you made with Flow is highly uninteresting. Try again, with better colors and a more
  unique look, staying on the electronic sound system theme." Round 2 (six sound-system takes: boombox, club subwoofer,
  turntable, tube amp, modular synth, line array) prompts are in `FableAnimationTests/.work/breacher-prompts/v2/`; its
  page is `art/review/2026-10-14/skyline-breacher-sound.html` once generated.
- Its current in-game model is a plain orange tube ("isn't textured at all"); the worn-paint look now applies in the
  build but the redesign replaces the model.

### Still open from before
- Overnight draft lines to rewrite: `docs/SKYLINE-DRAFT-LINES-OVERNIGHT.md`; plus `SKYLINE-RULINGS.md` and
  `SKYLINE-DRAFT-LINES.md`.
- The Breacher reload (heavier than the Witchfire Echo pick) and the Writ's "hushed court" loop: the creator's calls.
- The Subway Surfers floor theme: not started (compose together with the creator).

## Work queue (superseded: see `docs/SKYLINE-OPEN-THREADS.md`)

1. The creator confirms the new build (bots visible, menu, ADS, quiet drone).
2. Level design: a short design (objective types, a more complex arena, timed successive waves) for the creator's
   approval; then build it (main session, Sonnet agents for scoped parts).
3. Bot redesigns per the calls above (sizes, floor-care top screen + molly acid with a 15 s recharge, drone rotors as
   the weak spot, spotlight humanoids with charging railguns), CC0 bases where they fit.
4. The Writ: Flow review of the back extension; then the gavel-smack reload, the quill thwip + weak-spot gavel crack,
   ink tracers.
5. The Breacher: pick from round 2; then the model and its sound-system identity.
6. Tone down the sun and shadows; the enemy-clarity pass (silhouettes, rim, no reliance on text bubbles).
