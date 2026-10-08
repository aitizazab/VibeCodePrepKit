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
| **Core total** | `core-tests/All` | **131/131 passing** (2026-10-08) |
| Unity / Shell | Boot, title, settings, pause, game over, victory, credits, how-to-play cards, scene loader, audio manager (crossfade), juice (hit-stop, Cinemachine shake, flash, particles), pooling, debug menu, ink dialogue UI with layered portraits | after the Unity install |
| Template: bullet-hell roguelite (2D) | Focus-mode player, pooled bullets (thousands), pattern-driven enemies and waves, rooms → 3 floors + boss with phases, choose-1-of-3 upgrade draft, graze, assists | after the shell |
| Template: DOOM-like (3D) | Fast first-person controller, hitscan, projectile and melee weapons, billboard enemies (8-angle sprites rendered from 3D in Blender), NavMesh AI, keys and doors, a ProBuilder modular kit, low-res palette render, optional roguelite layer | after the bullet hell |
| Template: story | ink scenes, backgrounds, layered characters, choices, flags, multiple endings, clickable hotspots (escape-room structure), CGs | after the shell |
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
