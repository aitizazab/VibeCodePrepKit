# VibeJamKit

A pre-built Unity 6 framework for a 3-day game hackathon. It has a shared game shell (menus,
settings, pause, game over, ending, credits, dialogue, game feel) and three genre templates:
a **2D bullet-hell roguelite**, a **DOOM-like 3D shooter** and a **story game**.

- The plan, status and schedule are in [docs/PLAN.md](docs/PLAN.md).
- Rules for AI agents working here are in [CLAUDE.md](CLAUDE.md).

## Layout
| Path | What |
|---|---|
| `Packages/com.vibejam.jamkit/Runtime/Core` | Game rules in pure C#: randomness, state machines, combat, bullet patterns, roguelite systems, flow, settings |
| `Packages/com.vibejam.jamkit/Tests/Core` | Their unit tests; these run in Unity and outside it |
| `core-tests/` | dotnet projects that run those tests without Unity |
| `tools/` | Build, test and merge-driver scripts |

## Quick start
```powershell
dotnet test core-tests/All/All.Tests.csproj --nologo -v q   # core tests, no Unity needed
powershell -File tools/build.ps1 -Target All                 # Windows + WebGL builds into Builds/
```
