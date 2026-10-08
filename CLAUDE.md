# VibeJamKit: rules for agents

A Unity 6 project plus the embedded package `Packages/com.vibejam.jamkit`. It's a pre-built
framework for a 3-day "Vibe Coding GameDev" hackathon. Plan and status: `docs/PLAN.md`.

## Code
- **`Runtime/Core` is pure C# with no UnityEngine** (its asmdef sets `noEngineReferences`). Game
  rules and maths go there; MonoBehaviours in `Runtime/Unity` stay thin.
- **C# 9 only** (Unity 6's language version): block-scoped namespaces, no records, no `init`, no
  global usings, no file-scoped namespaces, no `required`, no nullable annotations.
  `core-tests` enforces this with LangVersion 9.
- **Serialized data** (inspector, JsonUtility, ScriptableObjects): `[Serializable]` classes with
  **public fields**, `List<T>`, enums. No properties for saved state, no dictionaries.
  Angles are in degrees in data and radians inside.
- **Per-frame code doesn't allocate:** no LINQ in Tick/Update; output lists are passed in.
- Vectors in the core use `System.Numerics`; adapters convert to `UnityEngine.Vector2/3`.
  System.Numerics vectors aren't Unity-serializable, so serialized data stores floats.
- **The creator's secrets-guard hook blocks any write that contains a C# auto-property with a
  setter** (it reads the setter's semicolon-then-"set" as a shell command that dumps the
  environment). Use a backing field with explicit accessors, or a public field.
- Angles: 0 = +X, counter-clockwise (so -90 is down). The 3D layer maps 2D pattern output onto XZ.
- Tests are NUnit 3 style (`Assert.That(..., Is....)`). The same files run in Unity's Test
  Framework and in `core-tests`.

## Commands
- Core tests (seconds, no Unity): `dotnet test core-tests/All/All.Tests.csproj --nologo -v q`.
  The per-module projects (`core-tests/<Module>/`) exist so parallel agents don't share `obj/`.
- Unity layer compile check (seconds, no Unity; needs Library/ScriptAssemblies from one Unity run):
  `dotnet build core-tests/UnityLayerEditor/UnityLayerEditor.Check.csproj --nologo -v q` (editor view,
  incl. editor tools and Unity tests) and `core-tests/UnityLayerPlayer/UnityLayerPlayer.Check.csproj` (player view).
- Unity tests and builds: `tools/test.ps1`, `tools/build.ps1 -Target Windows|WebGL|All`,
  `tools/smoke.ps1` (runs the Windows build with `-jamSmoke`), and `tools/verify.ps1` for the whole
  chain (core, Rebuild Shell Scenes, EditMode, PlayMode, Windows build, smoke; `-From <step>`).
- The shell (GameRoot, menus, dialogue, juice) is documented for jam teams in `docs/SHELL.md`.
  - **Unity locks a project while the editor has it open** (`Temp/UnityLockfile`). Batch mode
    then fails, so the scripts refuse. Either the creator closes the editor, or we work through
    the MCP link to the open editor.
  - Unity batch builds are heavy jobs: run them through the session memory cap
    (`uv run python ~/.local/bin/capped-run.py --session VibeJamKit -- powershell -File tools/build.ps1 ...`).
    **WebGL needs `--share 0.6`:** at 0.3–0.4, il2cpp.exe crashes natively (0xC0000005, about 7 s
    into IL2CPP_CodeGen) instead of failing cleanly. A WebGL build takes ~9–12 min, Windows ~2 min.
  - Run long Unity steps in the foreground, one verify step at a time (`tools/verify.ps1 -From X -To X`):
    background shells get reaped when memory runs low while the session is idle.
  - **One GPU job at a time** on this laptop: no Blender renders while the editor is in play mode.

## Git
- Remote: private GitHub `aitizazab/VibeJamKit`. Commits use the repo-local email
  `aitizaz.ab24@gmail.com` (already set in `.git/config`).
- Commit in checkpoints with `core-tests/All` green.
- Scenes have owners: never edit a scene a teammate has open. Prefer prefabs and data assets
  over scene edits; they merge cleanly.

## Content
No copied character designs or other games' assets (it's a public, judged event). Parody,
archetypes and mechanics are fine. Credit every sample and every CC0 sound on the credits screen.
