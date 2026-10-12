# One Unity job at a time (every session on this PC)

The PC has 15.2 GB of RAM and one RTX 3050. Two Unity batch runs at once starve each other (and the creator's own
programs), so every session that launches Unity (batch mode, tests, builds, or a built player for captures) takes this
lock first. It's shared between the Visiting Hours session and the Skyline Repo session (2026-10-12).

**The lock:** the folder `C:\Users\User\Desktop\yougotgamesonyophone\.unity-lock\` (creating a folder is atomic).

1. **Take it:** create the folder. If that fails, someone holds it: read `owner.txt` inside (`<pid> <who> <time>`).
   If that process no longer exists, the lock is stale: delete the folder and try again. Otherwise wait (main session
   only: a light background wait, 30 s between tries; subagents never wait, they hand the run back to their main).
2. **Mark it:** write `owner.txt`: your process id, who you are (e.g. `skyline-repo`), the time.
3. **Hold it for one job** (a build, a test run, a capture), not a whole night's queue, so the other session gets turns.
4. **Release it:** delete the folder when the job ends, also when it fails (try/finally).

Also check before taking it the first time: no `Unity.exe` should already be running against the project you're about
to open (Unity's own project lock refuses a second editor on the same project anyway).

The Visiting Hours runner (`detached-verify.ps1` in the Visiting Hours session's scratchpad) takes the lock around each
stage. Memory still goes through `~/.local/bin/capped-run.py` (30% of RAM per session), and GPU-only jobs (Blender,
CUDA) follow the GPU rules in `~/.claude/CLAUDE.md`.
