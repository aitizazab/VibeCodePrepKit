# Skyline Repo: the handoff (a new game, started 2026-10-12)

> **Latest (2026-10-14): read `docs/SKYLINE-PLAYTEST-1.md` first.** M1 is built and judged; the creator's first playtest set
> the current work (level design and the objective, bot redesigns, the Writ and the Breacher, fidelity). It overrides
> older pages where they disagree.

You're starting a new game prototype in the VibeJamKit Unity repo
(`C:\Users\User\Desktop\yougotgamesonyophone\VibeJamKit`, branch `main`, Unity 6000.3, URP 17.3). Another session is
working on Visiting Hours in the same repo at the same time. Read this whole page first.

## Who and why

- **The creator** (they/them) is practising for a 3-day Unity hackathon ("Vibe Coding GameDev", around 2026-10-16 to
  18, a team of three, games judged without the judges playtesting). They have 3-4 hours a day.
- **How they want to work (2026-10-12):** "if I have to write out prose for every single scene's dialogue, come up with
  and describe the visuals and audio, and create interesting level design and describe it to you on my own, we're going
  to be here for months." So **you design and write everything** (levels, visuals, audio, lines). They give direction
  and play. **Nothing reaches them until an adversarial judge agent has reviewed real captures** (stills, recorded runs,
  automated checks) against a written bar; green tests are never evidence of quality. Visiting Hours' bar is in
  `docs/REDESIGN.md` section 3; adapt it.
- Start this session in `C:\Users\User\Desktop\yougotgamesonyophone\FableAnimationTests` (like the Visiting Hours
  session), so it loads the same memory of the creator's preferences, and work in VibeJamKit from there.

## The creator's brief, in their words

> "I want the basis of an FPS roguelite, where you move through rooms/areas procedurally generated, completing
> objectives and puzzles, and clearing out adds or fighting bosses, temporary rewards on the end of each room for the
> run, and permanent rewards based on your time taken for full completion and difficulty level, that let your next runs
> progress further, easier, or faster. Player upgrades, party construction, floor changes, skipping certain mechanics,
> and so on. This one will have a lighter story, to not get bogged down in what matters the most. It just needs a
> setting, a light plot to follow (basically a reason to run through the floors), and a bunch of cool weapon designs and
> gun-feel, with abilities sprinkled in for a power fantasy. It will keep our movement engine, but either the engine or
> the game needs to be rebalanced so avoiding enemy fire isn't criminally easy, and encounters are interesting to engage
> with."

## The pitch they chose: Skyline Repo (working title)

**Setting.** Halcyon Row: a sunny near-future district of glass towers joined by sky-bridges, a monorail and rooftop
gardens, all owned by one bankrupt landlord. Its building AI, **CONCIERGE**, has locked the district down to keep the
bailiffs out, and remodels its floors every hour (the in-world reason the rooms are procedurally generated).

**Plot (light).** You work for a scrappy family repossession firm. The court hires you to cross the Row and repossess
CONCIERGE's core in the last tower before the lockdown becomes permanent. Each district (the mall lobby, open-plan
offices, R&D labs, hydroponic gardens, the casino, the penthouses) ends in a boss: a department head's security mech or
an executive in a powered suit. CONCIERGE talks over the PA the whole way, polite and passive-aggressive ("Your visit
has been noted. Please enjoy our complimentary turrets."), carrying the comedy and the plot with no cutscenes. At the
end it turns out CONCIERGE was protecting the last tenants: repossess the core, or leave it running (two endings).

**How their systems map:** temporary rewards = the mods and gadgets you "repossess" at the end of each room; permanent
rewards = the firm's commission, scaled by clear time and difficulty (a better van, starting kit, new hires); party
construction = hiring subcontractors with abilities; floor changes = CONCIERGE's remodels (low gravity, flooded floors,
lights out); skipping = express-elevator keys bought for later runs; weapons = bailiff-issue guns upgraded with the
corporate prototypes you find.

## The creator's fix to the pitch: across the skyline, with verticality as the obstacle

The first pitch was one 100-floor tower climbed upward. The creator: "It's almost fully vertical, but our movement
engine thrives in horizontally expedient spaces the most. Verticality is great, but as a novelty, because the way the
movement engine works, an enemy far above you has a great advantage on you compared to the enemy next to you or below
you." Then, clarifying: "Verticality as an obstacle is good, but the entire game built around scaling upwards was not.
So our design should still involve moving up during combat as well, perhaps once every 5 to 10 floors? And the game
should heavily emphasize verticality as an obstacle too, and we can just add in some tools the player can use, like a
naive grappling hook, a true grappling hook, a glider or a jumping upgrade."

So:
- **The run crosses the Row, tower to tower, and rises as it goes.** Most floors are wide horizontal spaces built for
  momentum (office floors, mall concourses, sky-bridges, rooftop gardens, monorail platforms: wall-runs along glass
  façades, slides down concourses).
- **Verticality is a heavy, constant obstacle in traversal:** gaps to cross, walls and shafts to get up, drops to read,
  routes that need the right tool. Getting up and across is a puzzle and a skill test in itself.
- **Combat that climbs, every 5-10 floors:** a set-piece floor where you fight your way upward (an atrium, an elevator
  shaft, a façade under construction). Since enemies above have the advantage with this engine, those floors are built
  so the high ground is earned and fair: enemies above telegraph their fire, there's cover from above, and the tools give
  ways to reach and flank them fast. Ordinary combat floors keep most fighting near the player's level.
- **Traversal tools** (upgrades found or bought, some possibly permanent unlocks):
  - **a naive grappling hook:** pulls you straight to marked anchor points;
  - **a true grappling hook:** attaches anywhere it hits and swings, with momentum;
  - **a glider:** crosses the gaps between towers and slows falls;
  - **a jump upgrade:** a higher or extra jump.
  Design rooms so each tool opens shortcuts and optional routes, and so a run without one is still finishable.
- **Enemies punish lazy movement:** flankers, chargers, area denial on the floor, projectiles that lead the player's
  movement, and enemies that punish one habit (a sweeper that forces a jump, a mine-layer that punishes long
  wall-runs, a sniper who covers a grapple point).

## Open questions for your brainstorm with the creator

They asked these and want them settled with you:
- **Who is the repo agent?** "A cute little bot, a murder machine, or a human agent working for a corporation?"
- **Is the crew human or robotic too?**

Also worth settling early: the weapon roster's identity, the ability set, how the party works in first person (AI
companions, call-in abilities, or loadout perks), what "puzzles" means in an FPS room, the difficulty levels, and how
dodging enemy fire is made hard enough with this movement engine.

## What already exists to build on

- **Core systems** (`Packages/com.vibejam.jamkit/Runtime/Core/`): Roguelite (runs, rewards, meta-progression), Combat,
  Patterns and BulletHell (projectile patterns, fairness rules), Doomlike, Movement (the Titanfall-like movement: wall-run,
  slide, double jump, mantle), Stealth, Presenting, settings and saves. Pure C# with dotnet test suites in `core-tests/`.
- **Templates** (`Assets/JamKit/Templates/`): Doomlike (the creator never played it), MovementLab, BulletHell,
  DeepBlock, StealthLab.
- **Docs:** `docs/DESIGN-ROGUELITE.md`, `docs/TEMPLATE-DOOMLIKE.md`, `docs/MOVEMENT.md`, `docs/SPEED-FEEL.md`,
  `docs/RESEARCH-MOVEMENT.md`, `docs/JAM-ASSETS-AND-PRESENTATION.md` (free asset sources and licences).
- The Visiting Hours session's look library (golden-hour sky, lighting rig, post, a CC0 PBR material library) is in
  `Assets/JamKit/Templates/VisitingHours/Look/` with an API in `docs/redesign/look.md`; reuse it rather than redoing it.

## Rules for working beside Visiting Hours

- **Your own folder:** `Assets/JamKit/Templates/SkylineRepo/` and your own docs (`docs/SKYLINE-*.md`, a
  `docs/SKYLINE-WORKLOG.md` you update after every result). Don't edit Visiting Hours' files.
- **The movement engine is shared.** Visiting Hours' nights depend on it. Rebalance in your game's own settings and
  enemies (its tuning is data); if the engine itself must change, add it as an opt-in setting whose default keeps today's
  behaviour, and keep its tests green.
- **Unity: one job at a time across both sessions.** Take the lock in `docs/UNITY-LOCK.md` around every Unity run.
  Visiting Hours runs its Unity jobs in a second checkout, `VibeJamKit-play`; don't use that folder. Disk is tight, so
  don't make another full checkout without asking the creator.
- **Git:** both sessions commit to `main`. Stage and commit only explicit paths at commit time (twice on 2026-10-12 one
  agent's staged deletions were swept into another's commit and broke main); never `git add -A`; never commit the
  already-modified .unity scene files in the tree (noise). End messages with the Co-Authored-By line the session gives.
- **Machine rules** are in `~/.claude/CLAUDE.md` (the memory cap, GPU jobs one at a time, keeping the PC awake). The
  subagent hook blocks Unity runs and waiting inside subagents: subagents build, and the main session runs Unity.
- Plain words with the creator, not internal ids; anything they must read or act on goes in your final message.
