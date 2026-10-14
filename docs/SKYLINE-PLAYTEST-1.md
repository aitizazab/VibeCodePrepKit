# Skyline Repo: the creator's first playtest (2026-10-14) and the work it set

**Start here in a new session.** M1 (the first floor) was built and judged overnight on 2026-10-13/14 (worklog,
`docs/skyline-judge/`). The creator then played it. Their verdict: integral problems first, balancing later. This page
holds their calls (their words quoted) and the work queue. It overrides older docs where they disagree.

## How we work now (the creator, 2026-10-14)

- "We'll work less on this today by agents, just you and me balancing the current experience now that it's built. IF
  we do call any agents, make sure they're well-scoped Sonnet agents, meant to implement a plan, or research a specific
  fact, or do a specific thing. The only exception is Weapon Ideas I want in the game immediately."
- Budget: the week's usage hit 20% with the pace in the red after the overnight run (resets Tue 20 Oct 09:00).

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

## Work queue (suggested order)

1. The creator confirms the new build (bots visible, menu, ADS, quiet drone).
2. Level design: a short design (objective types, a more complex arena, timed successive waves) for the creator's
   approval; then build it (main session, Sonnet agents for scoped parts).
3. Bot redesigns per the calls above (sizes, floor-care top screen + molly acid with a 15 s recharge, drone rotors as
   the weak spot, spotlight humanoids with charging railguns), CC0 bases where they fit.
4. The Writ: Flow review of the back extension; then the gavel-smack reload, the quill thwip + weak-spot gavel crack,
   ink tracers.
5. The Breacher: pick from round 2; then the model and its sound-system identity.
6. Tone down the sun and shadows; the enemy-clarity pass (silhouettes, rim, no reliance on text bubbles).
