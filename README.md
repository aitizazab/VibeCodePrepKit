# VibeJamKit

A pre-built Unity 6 framework for a 3-day game hackathon, plus the practice game being made with it.
It has a shared game shell (menus, settings, pause, game over, ending, credits, dialogue, game feel,
presenter mode), four genre templates (**2D bullet-hell roguelite**, **DOOM-like 3D shooter**, **story game**,
**sandbox**), and **Visiting Hours** (the practice game).

- **The plan, status and what's next:** [docs/PLAN.md](docs/PLAN.md)
- **Rules for AI agents working here:** [CLAUDE.md](CLAUDE.md)

## Docs
| Doc | What |
|---|---|
| [PLAN.md](docs/PLAN.md) | The event's rules, the team, decisions, what's built, what's next |
| [VISITING-HOURS.md](docs/VISITING-HOURS.md) | **The practice game's current state**: premise, art, audio, movement, status, open questions |
| [CHARACTERS.md](docs/CHARACTERS.md) | Visiting Hours' characters and their looks |
| [OPENING-AND-TITLE.md](docs/OPENING-AND-TITLE.md) | Visiting Hours' opening shot list and diegetic title screen |
| [STORY-VISITING-HOURS.md](docs/STORY-VISITING-HOURS.md), [SLICE-VISITING-HOURS.md](docs/SLICE-VISITING-HOURS.md), [SLICE-NOTES.md](docs/SLICE-NOTES.md) | Visiting Hours' story design, the slice spec, notes on the first slice (partly superseded; see their headers) |
| [RESEARCH-MOVEMENT.md](docs/RESEARCH-MOVEMENT.md) | Titanfall-style movement: mechanics, measured numbers, formulas, Unity approach, feel, chase design |
| [SHELL.md](docs/SHELL.md) | How to build a game on the shell |
| [TEMPLATE-BULLETHELL.md](docs/TEMPLATE-BULLETHELL.md), [DESIGN-ROGUELITE.md](docs/DESIGN-ROGUELITE.md), [STORY-STRUCTURE-BULLETHELL.md](docs/STORY-STRUCTURE-BULLETHELL.md) | The bullet-hell template, its roguelite design rules, story structure for it |
| [TEMPLATE-DOOMLIKE.md](docs/TEMPLATE-DOOMLIKE.md) | The DOOM-like template |
| [TEMPLATE-STORY.md](docs/TEMPLATE-STORY.md) | The story template (tags, ink cheat sheet) |
| [PRESENTING.md](docs/PRESENTING.md) | Presenter mode, demo routes, the 4-minute pitch, capturing footage |
| [JAM-ASSETS-AND-PRESENTATION.md](docs/JAM-ASSETS-AND-PRESENTATION.md) | Free asset sources with licences, AI tool terms, capture tools, pitch advice, practice themes |
| [STORY-EMBER.md](docs/STORY-EMBER.md), [STORY-DEAD-AIR.md](docs/STORY-DEAD-AIR.md) | Rejected story drafts (history) |

## Layout
| Path | What |
|---|---|
| `Packages/com.vibejam.jamkit/Runtime/Core` | Game rules in pure C#, testable without Unity |
| `Packages/com.vibejam.jamkit/Runtime/Unity`, `Editor` | The Unity side: shell, presenter mode, builders, build script |
| `Assets/JamKit/Templates/*` | The templates and Visiting Hours |
| `core-tests/` | dotnet projects that run the core tests (and compile checks) without Unity |
| `tools/` | Build, test, smoke, verify, play-builds, recording, art and audio scripts |
| `script/` | The creator's editable scripts |

## Quick start
```powershell
dotnet test core-tests/All/All.Tests.csproj --nologo -v q   # core tests, no Unity needed
powershell -File tools/verify.ps1                            # the whole chain (run steps singly with -From X -To X)
powershell -File tools/build.ps1 -Target All                 # Windows + WebGL builds into Builds/
```
