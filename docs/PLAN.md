# VibeJamKit plan

Written 2026-10-08, with the hackathon about 9 days away (around 2026-10-17).

## The event (what we know)
- "Vibe Coding GameDev": a 3-day hackathon. The theme is revealed on day 1 and games are presented on day 3. AI use is expected.
- **Unity only** (per the event sheet). No rule booklet: we assume pre-made code is allowed.
- Judging:
  - a panel scores **completeness, bug-freeness, originality and scope**;
  - a fellow participant gives a **blind playthrough** rating.
  - Each participant plays 3 games in at most 4 hours, so **target 10–20 minutes** to finish.
- How games are played (browser or download) is unknown, so **both a Windows and a WebGL build work from day 1**.
- There should be internet at the venue (Claude, generation tools).

## Goals, in order
1. **Tools ready.** Day 1 of the event is spent on the game, not on setting up Unity, MCP or builds.
2. **Genre frameworks finished:** a shared shell plus three templates (below).
3. **If time is left:** a compressed practice run on a random theme, with a teammate as the blind tester.

## Decisions
- **One Unity project** (this repo) holding the shell and all three templates. At the jam we copy it and delete the templates we don't use.
- **Unity 6.3 LTS (6000.3.25f1)** with the WebGL and Windows IL2CPP modules. It was installed through
  the **Unity CLI** (`%LOCALAPPDATA%\Unity\bin\unity.exe`, 1.0.0-beta.13), so the Hub isn't required.
- **Claude ↔ Unity link:** Unity's official Claude Code plugin (`unity@unity-agent-plugin`, enabled
  in `.claude/settings.json`, project scope). Its CLI drives the open editor through the
  `com.unity.pipeline` package (`unity pipeline install`). It can enter Play mode, run C#, and edit
  scenes and assets. Batch mode (`tools/*.ps1`) is for when the editor is closed.
- **URP** with two renderers in one asset: Universal (3D) and Renderer2D, chosen per camera.
- **The new Input System:** keyboard and mouse plus gamepad.
- **JamKit is an embedded package** (`Packages/com.vibejam.jamkit`):
  - `Runtime/Core`: **pure C# 9, no UnityEngine** (`noEngineReferences`). Game rules, maths and data, unit-tested outside Unity with `dotnet test` (`core-tests/`). Bugs get caught in seconds, before Unity is even open.
  - `Runtime/Unity` (next): thin MonoBehaviour and ScriptableObject adapters over the core.
  - `Editor`: the build script, import presets, validators.
- **Content is data:** one asset per enemy, upgrade, pattern, weapon or room. Teammates make content by duplicating an asset and changing numbers, with no code.
- **Dialogue in ink** (inkle, MIT). It's plain-text scripts that a writer can play in the Inky editor on its own.
- **Gameplay animation** comes from libraries (Mixamo etc.) or code. The sketcher is the fallback only where those miss.
- **Retro presentation as a consistency tool:** low-res render plus a palette lock, so generated assets look like one set.

## Architecture
| Layer | Holds | Status |
|---|---|---|
| Core / Shared | `Rng` (seeded PCG32, forks), `StateMachine<T>` | done, 12 tests |
| Core / Combat | Health and damage, i-frames, Cooldown, RepeatingTimer, HitStop (capped), ScreenShake (trauma), WeaponDef and WeaponState (frame-rate independent, magazine-less mode) | done, 59 tests |
| Core / Patterns | PatternDef, PatternSequence, Emitter (frame-rate independent), BulletMotion (closed form), 7 presets | done, 30 tests |
| Core / Roguelite | StatBlock and modifiers, UpgradeDef, inventory, draft (luck, reroll, fallback), RunGenerator/RunPlan/RunState | done, 22 tests |
| Core / Flow | GameFlow states, Settings and difficulty assists, SaveData with migration | done, 11 tests |
| **Core total** | `core-tests/All` | **302/302 passing** (2026-10-08, after the first playtest round) |
| Unity / Shell | GameRoot (self-creating), Boot/Title/Sandbox scenes from SceneBuilder, UI Toolkit menus (title, settings, pause, game over, victory, credits, how-to-play), HUD, scene loader with fade, audio (crossfade, pooled SFX, generated placeholder sounds), juice (hit stop, shake, flash, sprite flash, floating text), debug menu, ink dialogue with layered portraits, `-jamSmoke` build smoke test. How-to: `docs/SHELL.md` | written 2026-10-08; see the latest verify run |
| Template: bullet-hell roguelite (2D) | Player with Sprint on Shift (stamina bar inside a per-room sprint budget; classic Focus kept behind a flag), struct-array bullets drawn with GPU instancing (3000+), pattern-driven enemies and waves, rooms to 3 floors + boss with phases, choose-1-of-3 upgrade draft, shop, rest, ink events, graze, bombs, assists; 11 enemies, 13 waves, 14 rooms, 3 bosses (8 phases), 18 upgrades. How-to: `docs/TEMPLATE-BULLETHELL.md` | written 2026-10-08; core tests green, Unity run pending |
| Template: DOOM-like (3D) | Levels from ASCII text maps (built at runtime: merged meshes, colliders, doors, NavMesh), validator with key-order reachability, fast FPS controller with click-to-play pointer lock, 5 weapons on shared ammo pools (fist, pistol, shotgun, rapid gun, launcher), 8-angle billboard enemies (generated placeholder sheets; Blender renders drop in) with core-StateMachine AI and telegraphed attacks, ambush closets, secrets, low-res palette render (Settings toggle), tally, 1-of-3 upgrade draft between levels; 6 enemies (1 boss), 3 levels, 11 upgrades. How-to: `docs/TEMPLATE-DOOMLIKE.md` | written 2026-10-08; core tests green, Unity run pending |
| Template: story (visual novel / point-and-click) | ink drives everything through tags: backgrounds and CGs with transitions, characters with layered expressions, places and slides, shake/flash/fade/zoom/blur/colour filters/glitch, music and ambience, text box or full-screen text, chapter cards, inline markup (bold, palette colours, pauses, typing speed); backlog, auto, skip-read, 3 slots + quick + autosave; cross-playthrough memory visible to ink (playthroughs, endings seen, remember/recall, quits, reloads and rewinds), endings gallery, title screen changes and fake system moments (#meta); point-and-click rooms from data (rect/polygon hotspots, conditions, keyboard/pad selection) with an inventory used on hotspots; story check (unknown tags, missing ids, broken hotspots, unreachable knots) on save, in tests and before builds; hot reload of edited ink in play mode. Sample "Last Train Home": ~10-12 min, 4 endings (1 only on a second playthrough), a 7-hotspot office with 3 items. How-to: `docs/TEMPLATE-STORY.md` | written 2026-10-08; core tests green (incl. the sample played headless), Unity run pending |
| Playtest round 1 (2026-10-08) | From the creator's first bullet-hell playtest: Play opens a Story / Normal / Hard picker (shell, all action templates); Shift is Sprint (was Focus) with a first-run hint, a two-layer SPRINT meter and a HUD legend; story hooks from ink (boss intro/phase/defeat, death-screen line, Rest companion line, run-start line by run count) with placeholder lines only; the story smoke run logs its choices, rooms and ending. Story method: `docs/STORY-STRUCTURE-BULLETHELL.md` | code and core tests green; Unity run pending |
| Tools | `tools/build.ps1` (Windows/WebGL, batch mode), `tools/test.ps1` (core plus Unity EditMode/PlayMode, results read from the XML), `tools/setup-unity-merge.ps1`, itch.io upload | scripts written; checked after the install |

## Schedule (the creator has 3–4 h/day; Claude works in between)
| Dates | Work | Creator |
|---|---|---|
| Oct 8–9 | Repo, core modules, scripts; Unity install (CLI), project creation, Claude ↔ Unity link, both builds, test itch.io upload | Create a Unity ID, run `unity auth login`, approve the licence (~15 min) |
| Oct 9–11 | Unity shell and the story template | Click through the menus and dialogue |
| Oct 11–13 | Bullet-hell roguelite | Feel test |
| Oct 13–15 | DOOM-like | Feel test |
| Oct 15–16 | Buffer, the jam runbook (`docs/JAM-RUNBOOK.md`), teammate onboarding, optional mini practice | Bring a blind tester |

**If time slips, cut in this order:** first the DOOM-like's extras (keep the controller, one weapon, one enemy and one level), then the story template's hotspots. The bullet hell and the shell stay complete.

## Team
- **Creator:** decisions, feel tests, writing, the final call on art.
- **Teammates:** art review (review grids at play size), content as data (duplicate an asset, tune it), references and scene descriptions, progression ideas, and **one stays blind** to the practice game.
- **Claude:** all code, tools, builds, tests and integration.

## Content rules (public, judged event)
- **No copied character designs.** Archetypes, mechanics and parody premises are fine; a renamed copy of a design is not.
- Assets from other games (models, sounds, maps' art) are never used directly.
- Samples and third-party audio are credited on the credits screen, or replaced with original material.
- Sound effects: CC0 sources (Freesound etc.), credited anyway.

## Open questions
- How the judges play (web or exe): ask the organisers if possible.
- Whether the plugin's live-editor link needs a Unity Cloud project connection (check after sign-in).
  If it does and that's blocked, the fallback is the community "MCP for Unity" bridge.
