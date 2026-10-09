# VibeJamKit plan

Started 2026-10-08. **Updated 2026-10-10.** The event is the weekend of about 2026-10-16 to 18.

## The event (from the official rulebook)
- **"Vibe Coding GameDev"**, part of an annual festival at a friend's university (not a game jam, and only a
  year or two old, so there are no past themes). Teams of 2–4.
- **The theme comes on day 1**, you build on days 1–2, and on **day 3 you present to a panel of judges.**
  **No playtesting by other participants.**
- **Unity 6 is required.** Pre-made online assets and AI-generated code and assets are allowed. Source code goes
  to Google Classroom before the deadline. **A game that's copied or already available online is
  disqualified.** Whether our own pre-built toolkit is acceptable is being asked by the creator's friend.
- **Judging criteria:** Originality & Creativity, Technical Implementation, Art & Design, Playability & Fun
  Factor, Conformity to Theme.
- **What wins:** what the panel *sees*: gameplay, art, and a confident live demo backed by footage. Story is
  flavour: an Urdu-speaking panel may not engage with dilemmas.

## The team (3, all presenting)
| Role | Who | Owns |
|---|---|---|
| Director | the creator | concept, final calls, working with Claude, art direction, the trailer |
| Builder | the teammate with coding experience | the Unity editor: content in data assets, tuning, builds, bug triage |
| Player / Producer | the other teammate | scheduled critical playtests, levels and short text, asset curation, the pitch |

Roles are flexible: each takes on more as they show they can. Rule for three people in one project: work in
**data assets and prefabs**, one owner per scene.

## Goals, in order
1. **Tools ready:** done. Unity 6.3, the link with Claude, Windows and WebGL builds, tests, smoke runs.
2. **Genre frameworks:** done and verified (table below).
3. **A full practice game,** *Visiting Hours* (`docs/VISITING-HOURS.md`), taken to a point the creator is
   satisfied with, to learn how long that really takes. Then a **mock jam** on a practice theme.

## Decisions
- **One Unity project** (this repo), holding the shell and every template. At the jam: copy it, delete what
  isn't used.
- **Unity 6.3 LTS (6000.3.25f1)**, WebGL + Windows modules, installed with the Unity CLI. Claude ↔ Unity via
  Unity's official Claude Code plugin; batch scripts in `tools/` when the editor is closed.
- **JamKit is an embedded package:** a pure C# 9 core (testable with `dotnet test`, no Unity), thin Unity
  adapters, editor tools. **Content is data.** **Dialogue in ink.**
- **Running Unity on this laptop:** every heavy job goes through `capped-run.py`. **WebGL needs `--share 0.6`**
  (il2cpp crashes below it). Long steps run in the foreground one verify step at a time, because background
  shells get reaped on low memory. Subagents can't run Unity; they hand back commands.

## What's built (all verified in Unity on 2026-10-09/10 unless noted)
| Part | What it is | Docs |
|---|---|---|
| Core | randomness, state machines, combat and feel, bullet patterns, roguelite systems, flow, settings and saves, presenting, movement (in progress) | `CLAUDE.md` |
| Shell | self-starting GameRoot, menus (UI Toolkit), difficulty picker on Play, ink dialogue with portraits, audio, juice, debug menu, saves, `-jamSmoke` build test | `SHELL.md` |
| Bullet-hell roguelite | sprint with stamina and a room budget, instanced bullets (3000+), 3 floors and 3 bosses, reward economy (boss epics, trials, sacrifices, wagers), Forge/Altar/Wager/Trial/Trader rooms, remnants, a passive companion, the balance report | `TEMPLATE-BULLETHELL.md`, `DESIGN-ROGUELITE.md`, `STORY-STRUCTURE-BULLETHELL.md` |
| DOOM-like | levels from text maps, a fast FPS controller, 5 weapons, 8-angle billboard enemies, pixel look, upgrade drafts | `TEMPLATE-DOOMLIKE.md` |
| Story | ink-driven stage, saves and backlog, cross-playthrough memory, point-and-click rooms, story checks | `TEMPLATE-STORY.md` |
| Presenter mode | Ctrl+Shift+P (pad: hold L3+R3): chapter select, invincibility, slow-mo, freeze, hide UI, photo; attract mode; Unity Recorder presets | `PRESENTING.md` |
| *Visiting Hours* slice | the opening, Day 1, the booth, Walt's street, the journal and deductions, the argument battle (in its first, superseded art and opening) | `VISITING-HOURS.md`, `SLICE-NOTES.md` |
| Movement Lab | Titanfall-style movement (double jump, wallrun, slide, momentum) and a test course with a chase. **In progress** | `RESEARCH-MOVEMENT.md`, `MOVEMENT.md` (when built) |

Totals at the last full run: EditMode 456/456, PlayMode 73/73; every template builds for Windows and passes its smoke run.

## Next
1. The movement build (in progress), then the creator feels it in the Movement Lab.
2. Style-E character sheets (in progress), then 3D models for the waking world (Tripo, after the creator approves the credits).
3. Rebuild the opening and the diegetic title in style E (`OPENING-AND-TITLE.md`).
4. The dream street in 3D with style-E billboards, then an escape sequence on the new movement.
5. Walt's argument in the new style; music composed with the creator; polish.
6. A build for the teammates (once it has art, a small story and music); then the mock jam.

## Content rules (public, judged event)
- **No copied character designs**; parody, archetypes and mechanics are fine.
- **No real company names in a negative light** (e.g. the crossfire corporations are fictional).
- Other games' assets are never used directly. Samples and third-party audio are credited; CC0 sounds credited anyway.
- Licence notes for every asset source: `JAM-ASSETS-AND-PRESENTATION.md`.

## Open questions
- Whether the organisers accept a team's own pre-built toolkit (the friend is asking).
- How the judges view games (web or exe): both builds stay ready.
