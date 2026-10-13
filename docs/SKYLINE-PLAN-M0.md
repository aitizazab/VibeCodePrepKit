# Skyline Repo M0 (the pipeline) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build Skyline Repo's pipeline (M0 in the spec): its own C# assembly with the decision-event log and the
Ledger's recording side, a headless combat sim that measures dodging, the sound forge, the "ears" skill (measurements,
pictures, reference comparison), and the `SkylinePass` runner that captures everything from Unity under the shared lock.

**Architecture:** Pure C# systems live in a new `JamKit.Skyline` assembly (no engine references) tested with
`dotnet test` outside Unity. Python tools (the sound forge in the repo, the ears as a global Claude skill) run through
`uv`. A PowerShell runner queues four short Unity jobs (an editor batch job, a graphics editor recording job, a Windows
build, the built player's frame-time probe), each holding the shared Unity lock for one job, then checks the recorded
audio with the ears.

**Tech Stack:** C# 9 (Unity 6000.3's language version), NUnit 3 (Unity runs 3.5: use only `Assert.That`, `Is.*`,
`Assert.Throws`), .NET 10 SDK for `core-tests`, Unity 6000.3 + URP 17.3 + Unity Recorder 5, Python 3.12 via `uv`
(numpy, scipy, soundfile, pyloudnorm, matplotlib), PowerShell 5.1, ffmpeg 9.

**Spec:** `docs/SKYLINE-DESIGN.md` (sections 4.1, 4.7, 8, 9, 11.1). Read sections 9 and 11.1 before starting.

## Global Constraints

- C# 9 only: no file-scoped namespaces, no `global using`, no records, no nullable annotations, no implicit usings.
- Tests use NUnit 3 APIs that exist in NUnit 3.5: `Assert.That(x, Is.EqualTo(y).Within(z))`, `Is.True`,
  `Is.GreaterThan`, `Assert.Throws<T>(...)`. Nothing from NUnit 3.10+ (e.g. no `Assert.Multiple`).
- All Skyline runtime C# goes in `Packages/com.vibejam.jamkit/Runtime/Skyline/` (assembly `JamKit.Skyline`, references
  only `JamKit.Core`, `noEngineReferences: true`). It must never be added to `core-tests/All` (spec 9.1: a Skyline
  compile error must not break Visiting Hours).
- Unity-side code goes only in `Assets/JamKit/Templates/SkylineRepo/`. Never edit Visiting Hours' files.
- No engine (movement) changes in M0. `core-tests/Movement` must stay green after every task.
- **Never hand-write Unity `.meta` files.** Unity creates them on its next import (the main session's first pass);
  they're committed then.
- Python runs through `uv` (bare `python` fails on this machine). Heavy jobs run through
  `uv run python ~/.local/bin/capped-run.py --session skyline-repo -- <command>`.
- Unity runs only in the main session, one job per lock hold (`docs/UNITY-LOCK.md`). Subagents write code and hand
  back; they never launch Unity and never wait or poll.
- Git: stage explicit paths only (`git add -- <paths>`), never `git add -A`, never the already-modified `.unity`
  scenes outside SkylineRepo. Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Save slots (when used) start with `skyline-`.
- The secrets guard hook blocks some text patterns in files and commands; if a write is blocked, rename the variable or
  phrase rather than working around the hook.

## Review Focus

1. **A Unity stage hangs or crashes** (a modal dialog in the graphics editor, a crash): the runner must time the stage
   out, kill Unity, release the lock and mark the stage failed, then continue. Test: Task 9's `-SelfTest`.
2. **A stale lock from a crashed run** (owner pid gone): taken over automatically; a live owner is waited on. Test:
   Task 7's lock self-test.
3. **A recording with silent audio** (listener muted, wrong scene): the audio check must fail loudly, not pass on
   silence. Test: Task 9 stage E (the 440 Hz tone must be found) and Task 6's silent-clip test.
4. **A broken sound recipe** (a missing sample file, an unknown layer type): a clear error naming the layer, not a
   numpy traceback. Test: Task 5.
5. **Degenerate inputs to the measuring code** (a clip shorter than 0.4 s, total silence, a target standing on the
   shooter): no crash, no NaN; the result says what couldn't be measured. Tests: Task 3 (lead point), Task 6
   (short and silent clips).

**Parallelism:** Tasks 1→2→3→4→8(Step 1-3) are one C# chain. Task 5 and Task 6 (Python) are independent of everything.
Tasks 7→8(Step 4)→9 (Unity and the runner) need Task 1's assembly. Task 10 is the main session's.

---

### Task 1: The Skyline assembly, its test projects, and decision events

**Files:**
- Create: `Packages/com.vibejam.jamkit/Runtime/Skyline/JamKit.Skyline.asmdef`
- Create: `Packages/com.vibejam.jamkit/Runtime/Skyline/Decisions.cs`
- Create: `Packages/com.vibejam.jamkit/Tests/Skyline/JamKit.Skyline.Tests.asmdef`
- Create: `Packages/com.vibejam.jamkit/Tests/Skyline/DecisionTests.cs`
- Create: `core-tests/Skyline/Skyline.Tests.csproj`
- Create: `tools/skyline/test.ps1`

**Interfaces:**
- Consumes: `JamKit.Movement.MoveEvents` (`Runtime/Core/Movement/MoveTypes.cs`), `System.Numerics.Vector3`.
- Produces: `DecisionKind`, `DecisionContext`, `DecisionEvent`, `DecisionLog` (`Add(in DecisionEvent)`, `Count`,
  `Capacity`, `Total`, indexer, `Clear()`), `MoveDecisions.Append(DecisionLog, in MoveEvents, float time, Vector3
  position, DecisionContext, float threatBearing) : int`.

- [ ] **Step 1: Create the assembly definitions and the test project**

`Packages/com.vibejam.jamkit/Runtime/Skyline/JamKit.Skyline.asmdef`:
```json
{
    "name": "JamKit.Skyline",
    "rootNamespace": "JamKit.Skyline",
    "references": [
        "JamKit.Core"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": true
}
```

`Packages/com.vibejam.jamkit/Tests/Skyline/JamKit.Skyline.Tests.asmdef` (it references `JamKit.Core.Tests` for
`BoxWorld`):
```json
{
    "name": "JamKit.Skyline.Tests",
    "rootNamespace": "JamKit.Tests.Skyline",
    "references": [
        "JamKit.Skyline",
        "JamKit.Core",
        "JamKit.Core.Tests",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

`core-tests/Skyline/Skyline.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <!-- Shared + Movement + Skyline (Skyline Repo's pure C# systems, docs/SKYLINE-DESIGN.md). BoxWorld comes from the
       Movement tests. Skyline is its own assembly in Unity (JamKit.Skyline), so it is deliberately not in All. -->
  <ItemGroup>
    <Compile Include="$(PkgRoot)Runtime\Core\Movement\**\*.cs" />
    <Compile Include="$(PkgRoot)Tests\Core\Movement\BoxWorld.cs" />
    <Compile Include="$(PkgRoot)Runtime\Skyline\**\*.cs" />
    <Compile Include="$(PkgRoot)Tests\Skyline\**\*.cs" />
  </ItemGroup>
</Project>
```

`tools/skyline/test.ps1`:
```powershell
# Skyline Repo's pure C# tests, plus the Movement suite as the engine gate (docs/SKYLINE-DESIGN.md section 8).
#   powershell -File tools/skyline/test.ps1
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$bad = @()
foreach ($p in 'Skyline', 'Movement') {
    & dotnet test (Join-Path $root "core-tests\$p\$p.Tests.csproj") --nologo -v q
    if ($LASTEXITCODE -ne 0) { $bad += $p }
}
if ($bad.Count -gt 0) { Write-Host "Failed: $($bad -join ', ')"; exit 1 }
Write-Host 'Skyline and Movement tests passed.'
```

- [ ] **Step 2: Write the failing tests**

`Packages/com.vibejam.jamkit/Tests/Skyline/DecisionTests.cs`:
```csharp
using System;
using System.Numerics;
using JamKit.Movement;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    public class DecisionTests
    {
        static DecisionEvent At(float t) => new DecisionEvent { time = t, kind = DecisionKind.Jump, subject = -1, ammo = -1 };

        [Test]
        public void KeepsTheNewestWhenFull()
        {
            var log = new DecisionLog(4);
            for (int i = 0; i < 6; i++) log.Add(At(i));
            Assert.That(log.Count, Is.EqualTo(4));
            Assert.That(log.Total, Is.EqualTo(6));
            Assert.That(log[0].time, Is.EqualTo(2f));
            Assert.That(log[3].time, Is.EqualTo(5f));
        }

        [Test]
        public void RejectsANonPositiveCapacity()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DecisionLog(0));
        }

        [Test]
        public void IndexOutsideTheLogThrows()
        {
            var log = new DecisionLog(4);
            log.Add(At(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => { var _ = log[1]; });
        }

        [Test]
        public void JumpAndWallrunBecomeDecisions()
        {
            var log = new DecisionLog(16);
            var e = new MoveEvents { jumped = true, wallrunStarted = true, wallSide = -1 };
            int n = MoveDecisions.Append(log, e, 3.5f, new Vector3(1f, 0f, 2f), DecisionContext.InCombat, 90f);
            Assert.That(n, Is.EqualTo(2));
            Assert.That(log[0].kind, Is.EqualTo(DecisionKind.Jump));
            Assert.That(log[1].kind, Is.EqualTo(DecisionKind.WallrunStart));
            Assert.That(log[1].side, Is.EqualTo(-1));
            Assert.That(log[1].time, Is.EqualTo(3.5f));
            Assert.That(log[1].threatBearing, Is.EqualTo(90f));
            Assert.That(log[1].subject, Is.EqualTo(-1));
            Assert.That(log[1].ammo, Is.EqualTo(-1));
        }

        [Test]
        public void LandingIsNotADecision()
        {
            var log = new DecisionLog(16);
            var e = new MoveEvents { landed = true, landImpact = 8f, leftGround = true };
            Assert.That(MoveDecisions.Append(log, e, 0f, Vector3.Zero, DecisionContext.None, float.NaN), Is.EqualTo(0));
            Assert.That(log.Count, Is.EqualTo(0));
        }
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test core-tests/Skyline/Skyline.Tests.csproj --nologo -v q`
Expected: build FAILS with `CS0246: The type or namespace name 'DecisionLog' could not be found`.

- [ ] **Step 4: Write the implementation**

`Packages/com.vibejam.jamkit/Runtime/Skyline/Decisions.cs`:
```csharp
using System;
using System.Numerics;
using JamKit.Movement;

namespace JamKit.Skyline
{
    /// <summary>A choice the player made (docs/SKYLINE-DESIGN.md 4.1). The Ledger and, later, Baja's predictors read these.</summary>
    public enum DecisionKind { Jump, DoubleJump, WallJump, WallrunStart, SlideStart, MantleStart, Fire, Reload, Swap, Ability, Boost }

    /// <summary>The situation a decision was made in.</summary>
    [Flags]
    public enum DecisionContext { None = 0, Airborne = 1, UnderFire = 2, Wallrunning = 4, Sliding = 8, InCombat = 16 }

    /// <summary>One decision in the shared format every gun, ability and movement option reports.</summary>
    public struct DecisionEvent
    {
        public float time;
        public DecisionKind kind;
        /// <summary>The gun, ability or hire this is about; -1 for movement.</summary>
        public int subject;
        /// <summary>+1 right, -1 left, 0 none (a wall's side, a dash's side).</summary>
        public int side;
        public DecisionContext context;
        /// <summary>Degrees clockwise from the player's facing to the nearest threat; NaN when nothing is shooting.</summary>
        public float threatBearing;
        /// <summary>Rounds in the magazine at the moment of the decision; -1 when it isn't a gun decision.</summary>
        public int ammo;
        public Vector3 position;
    }

    /// <summary>The newest decisions, in order, in a fixed-size ring (oldest dropped first).</summary>
    public sealed class DecisionLog
    {
        readonly DecisionEvent[] _items;
        int _start, _count;

        public DecisionLog(int capacity = 4096)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _items = new DecisionEvent[capacity];
        }

        public int Count => _count;
        public int Capacity => _items.Length;
        /// <summary>Every decision ever added, including the ones the ring has dropped.</summary>
        public long Total { get; private set; }

        /// <summary>0 is the oldest kept decision, Count - 1 the newest.</summary>
        public DecisionEvent this[int i]
        {
            get
            {
                if ((uint)i >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(i));
                return _items[(_start + i) % _items.Length];
            }
        }

        public void Add(in DecisionEvent e)
        {
            if (_count < _items.Length)
            {
                _items[(_start + _count) % _items.Length] = e;
                _count++;
            }
            else
            {
                _items[_start] = e;
                _start = (_start + 1) % _items.Length;
            }
            Total++;
        }

        public void Clear()
        {
            _start = 0;
            _count = 0;
        }
    }

    /// <summary>Turns the movement engine's events into decisions (the engine itself knows nothing about threats).</summary>
    public static class MoveDecisions
    {
        /// <summary>Adds one decision per movement choice in <paramref name="e"/> (one step's or one frame's merged
        /// events) and returns how many it added. Landing and leaving the ground are outcomes, not choices.</summary>
        public static int Append(DecisionLog log, in MoveEvents e, float time, Vector3 position, DecisionContext context, float threatBearing)
        {
            int n = 0;
            if (e.jumped) n += Add(log, DecisionKind.Jump, 0, time, position, context, threatBearing);
            if (e.doubleJumped) n += Add(log, DecisionKind.DoubleJump, 0, time, position, context, threatBearing);
            if (e.wallJumped) n += Add(log, DecisionKind.WallJump, 0, time, position, context, threatBearing);
            if (e.wallrunStarted) n += Add(log, DecisionKind.WallrunStart, e.wallSide, time, position, context, threatBearing);
            if (e.slideStarted) n += Add(log, DecisionKind.SlideStart, 0, time, position, context, threatBearing);
            if (e.mantleStarted) n += Add(log, DecisionKind.MantleStart, 0, time, position, context, threatBearing);
            return n;
        }

        static int Add(DecisionLog log, DecisionKind kind, int side, float time, Vector3 position, DecisionContext context, float threatBearing)
        {
            log.Add(new DecisionEvent
            {
                time = time, kind = kind, subject = -1, side = side, context = context,
                threatBearing = threatBearing, ammo = -1, position = position,
            });
            return 1;
        }
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass, with the engine gate**

Run: `powershell -NoProfile -File tools/skyline/test.ps1`
Expected: `Skyline and Movement tests passed.` (5 Skyline tests; Movement's count unchanged).

- [ ] **Step 6: Commit**

```bash
git add -- Packages/com.vibejam.jamkit/Runtime/Skyline/JamKit.Skyline.asmdef Packages/com.vibejam.jamkit/Runtime/Skyline/Decisions.cs Packages/com.vibejam.jamkit/Tests/Skyline/JamKit.Skyline.Tests.asmdef Packages/com.vibejam.jamkit/Tests/Skyline/DecisionTests.cs core-tests/Skyline/Skyline.Tests.csproj tools/skyline/test.ps1
git commit -m "Skyline: its own assembly and test project, and the decision-event log (spec 4.1)"
```

---

### Task 2: The Ledger's recording side and "wall in reach"

**Files:**
- Create: `Packages/com.vibejam.jamkit/Runtime/Skyline/LedgerRun.cs`
- Create: `Packages/com.vibejam.jamkit/Runtime/Skyline/WallReach.cs`
- Create: `Packages/com.vibejam.jamkit/Tests/Skyline/LedgerRunTests.cs`

**Interfaces:**
- Consumes: `JamKit.Movement.MoveMode`, `IMoveWorld.CastWall(Vector3 origin, Vector3 direction, float radius, float
  maxDistance, out SurfaceHit hit)`, `SurfaceHit.runnable`; tests use `JamKit.Tests.Movement.BoxWorld`
  (`Floor(x0, x1, z0, z1, top)`, `Add(center, size, runnable)`).
- Produces: `LedgerRun` (`BeginCombat()`, `EndCombat()`, `InCombat`, `Tick(float dt, MoveMode mode, bool wallInReach)`,
  `CombatSeconds`, `SecondsIn(MoveMode)`, `ShareOf(MoveMode)`, `WallReachSeconds`, `WallUse`,
  `RecordShot(int weapon, bool hit, float damage, float range, bool kill)`, `Weapon(int) : WeaponTally`,
  `DamageShare(int) : float`, `Reset()`), `WeaponTally`, `WallReach.InReach(IMoveWorld, Vector3 feet, float
  chestHeight, bool airborneOrSprinting) : bool`, `WallReach.Reach = 3f`.

- [ ] **Step 1: Write the failing tests**

`Packages/com.vibejam.jamkit/Tests/Skyline/LedgerRunTests.cs`:
```csharp
using System.Numerics;
using JamKit.Movement;
using JamKit.Skyline;
using JamKit.Tests.Movement;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    public class LedgerRunTests
    {
        [Test]
        public void OnlyCombatTimeCounts()
        {
            var l = new LedgerRun();
            l.Tick(1f, MoveMode.Wallrun, true);
            Assert.That(l.CombatSeconds, Is.EqualTo(0f));
            l.BeginCombat();
            l.Tick(2f, MoveMode.Wallrun, true);
            l.Tick(2f, MoveMode.Sprint, false);
            l.EndCombat();
            l.Tick(5f, MoveMode.Air, false);
            Assert.That(l.CombatSeconds, Is.EqualTo(4f));
            Assert.That(l.SecondsIn(MoveMode.Wallrun), Is.EqualTo(2f));
            Assert.That(l.ShareOf(MoveMode.Sprint), Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void WallUseIsMeasuredByOpportunity()
        {
            var l = new LedgerRun();
            l.BeginCombat();
            l.Tick(1f, MoveMode.Wallrun, true);   // on a wall: in reach by definition
            l.Tick(3f, MoveMode.Air, true);       // a wall was in reach, not used
            l.Tick(6f, MoveMode.Sprint, false);   // no wall nearby: doesn't count against you
            Assert.That(l.WallReachSeconds, Is.EqualTo(4f));
            Assert.That(l.WallUse, Is.EqualTo(0.25f).Within(1e-5f));
        }

        [Test]
        public void NoWallInReachMeansNoWallUse()
        {
            var l = new LedgerRun();
            l.BeginCombat();
            l.Tick(3f, MoveMode.Sprint, false);
            Assert.That(l.WallUse, Is.EqualTo(0f));
        }

        [Test]
        public void DamageShareIsPerWeapon()
        {
            var l = new LedgerRun();
            l.BeginCombat();
            l.RecordShot(0, true, 30f, 12f, false);
            l.RecordShot(0, false, 0f, 0f, false);
            l.RecordShot(1, true, 10f, 4f, true);
            Assert.That(l.DamageShare(0), Is.EqualTo(0.75f).Within(1e-5f));
            Assert.That(l.Weapon(0).shots, Is.EqualTo(2));
            Assert.That(l.Weapon(0).hits, Is.EqualTo(1));
            Assert.That(l.Weapon(1).kills, Is.EqualTo(1));
            Assert.That(l.Weapon(0).MeanHitRange, Is.EqualTo(12f));
            Assert.That(l.DamageShare(7), Is.EqualTo(0f));
        }

        [Test]
        public void ShotsOutsideCombatAreIgnoredAndResetClears()
        {
            var l = new LedgerRun();
            l.RecordShot(0, true, 30f, 12f, false);
            Assert.That(l.Weapon(0).shots, Is.EqualTo(0));
            l.BeginCombat();
            l.Tick(1f, MoveMode.Air, true);
            l.RecordShot(0, true, 30f, 12f, false);
            l.Reset();
            Assert.That(l.CombatSeconds, Is.EqualTo(0f));
            Assert.That(l.Weapon(0).shots, Is.EqualTo(0));
            Assert.That(l.InCombat, Is.False);
        }

        static BoxWorld Room(float wallX, bool runnable)
        {
            var w = new BoxWorld();
            w.Floor(-20f, 20f, -20f, 20f, 0f);
            w.Add(new Vector3(wallX + 0.25f, 2f, 0f), new Vector3(0.5f, 4f, 10f), runnable);
            return w;
        }

        [Test]
        public void ARunnableWallWithinThreeMetresIsInReachWhileAirborne()
        {
            Assert.That(WallReach.InReach(Room(2.2f, true), Vector3.Zero, 1.2f, true), Is.True);
        }

        [Test]
        public void WallReachNeedsAirOrSprintAndARunnableWallInRange()
        {
            Assert.That(WallReach.InReach(Room(2.2f, true), Vector3.Zero, 1.2f, false), Is.False);
            Assert.That(WallReach.InReach(Room(4.5f, true), Vector3.Zero, 1.2f, true), Is.False);
            Assert.That(WallReach.InReach(Room(2.2f, false), Vector3.Zero, 1.2f, true), Is.False);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test core-tests/Skyline/Skyline.Tests.csproj --nologo -v q`
Expected: build FAILS with `CS0246: The type or namespace name 'LedgerRun' could not be found`.

- [ ] **Step 3: Write the implementation**

`Packages/com.vibejam.jamkit/Runtime/Skyline/LedgerRun.cs`:
```csharp
using System;
using System.Collections.Generic;
using JamKit.Movement;

namespace JamKit.Skyline
{
    /// <summary>One gun's record for the run.</summary>
    public struct WeaponTally
    {
        public int shots, hits, kills;
        public float damage, rangeSum;
        public float MeanHitRange => hits > 0 ? rangeSum / hits : 0f;
    }

    /// <summary>
    /// What CONCIERGE saw this run (docs/SKYLINE-DESIGN.md 4.1-4.2): seconds per movement state and per-gun records,
    /// counted during combat only. Resets every run. Scoring and counters are built on top later (M2-M3).
    /// </summary>
    public sealed class LedgerRun
    {
        readonly float[] _modeSeconds = new float[Enum.GetValues(typeof(MoveMode)).Length];
        readonly Dictionary<int, WeaponTally> _weapons = new Dictionary<int, WeaponTally>();
        float _totalDamage;

        public bool InCombat { get; private set; }
        public float CombatSeconds { get; private set; }
        /// <summary>Combat seconds when a runnable wall was in reach (or the player was on one).</summary>
        public float WallReachSeconds { get; private set; }

        public void BeginCombat() => InCombat = true;
        public void EndCombat() => InCombat = false;

        /// <summary>Call once per simulation step. <paramref name="wallInReach"/> comes from <see cref="WallReach"/>.</summary>
        public void Tick(float dt, MoveMode mode, bool wallInReach)
        {
            if (!InCombat || dt <= 0f) return;
            CombatSeconds += dt;
            _modeSeconds[(int)mode] += dt;
            if (wallInReach || mode == MoveMode.Wallrun) WallReachSeconds += dt;
        }

        public float SecondsIn(MoveMode mode) => _modeSeconds[(int)mode];
        public float ShareOf(MoveMode mode) => CombatSeconds > 0f ? _modeSeconds[(int)mode] / CombatSeconds : 0f;

        /// <summary>The share of the wall time on offer that was used (4.2: measured by opportunity).</summary>
        public float WallUse => WallReachSeconds > 0f ? SecondsIn(MoveMode.Wallrun) / WallReachSeconds : 0f;

        public void RecordShot(int weapon, bool hit, float damage, float range, bool kill)
        {
            if (!InCombat) return;
            _weapons.TryGetValue(weapon, out var t);
            t.shots++;
            if (hit)
            {
                t.hits++;
                t.damage += damage;
                t.rangeSum += range;
                _totalDamage += damage;
            }
            if (kill) t.kills++;
            _weapons[weapon] = t;
        }

        public WeaponTally Weapon(int weapon) => _weapons.TryGetValue(weapon, out var t) ? t : default;

        /// <summary>This gun's share of all the damage dealt this run (0 when nothing has been hit).</summary>
        public float DamageShare(int weapon) => _totalDamage > 0f ? Weapon(weapon).damage / _totalDamage : 0f;

        public void Reset()
        {
            Array.Clear(_modeSeconds, 0, _modeSeconds.Length);
            _weapons.Clear();
            _totalDamage = 0f;
            CombatSeconds = 0f;
            WallReachSeconds = 0f;
            InCombat = false;
        }
    }
}
```

`Packages/com.vibejam.jamkit/Runtime/Skyline/WallReach.cs`:
```csharp
using System;
using System.Numerics;
using JamKit.Movement;

namespace JamKit.Skyline
{
    /// <summary>"A wall is in reach" (docs/SKYLINE-DESIGN.md 4.1): a tagged wall-runnable surface within 3 m of the
    /// player while airborne or sprinting. Probed in 8 horizontal directions at chest height.</summary>
    public static class WallReach
    {
        public const float Reach = 3f;
        static readonly Vector3[] Directions = MakeDirections();

        static Vector3[] MakeDirections()
        {
            var d = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                float a = i * MathF.PI / 4f;
                d[i] = new Vector3(MathF.Sin(a), 0f, MathF.Cos(a));
            }
            return d;
        }

        public static bool InReach(IMoveWorld world, Vector3 feet, float chestHeight, bool airborneOrSprinting)
        {
            if (!airborneOrSprinting) return false;
            var origin = feet + new Vector3(0f, chestHeight, 0f);
            foreach (var dir in Directions)
                if (world.CastWall(origin, dir, 0.1f, Reach, out var hit) && hit.runnable) return true;
            return false;
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `powershell -NoProfile -File tools/skyline/test.ps1`
Expected: `Skyline and Movement tests passed.` If a `WallReach` test fails, read `BoxWorld.CastWall` in
`Tests/Core/Movement/BoxWorld.cs` for how it measures distance (from the origin to the surface, or to the sphere's
centre) and fix the probe, not the test's 3 m rule.

- [ ] **Step 5: Commit**

```bash
git add -- Packages/com.vibejam.jamkit/Runtime/Skyline/LedgerRun.cs Packages/com.vibejam.jamkit/Runtime/Skyline/WallReach.cs Packages/com.vibejam.jamkit/Tests/Skyline/LedgerRunTests.cs
git commit -m "Skyline: the Ledger's recording side (combat-only, wall use by opportunity, per-gun shares; spec 4.1-4.2)"
```

---

### Task 3: Aim models, the telegraphed shot, and the aim-mixing rule

**Files:**
- Create: `Packages/com.vibejam.jamkit/Runtime/Skyline/Aim.cs`
- Create: `Packages/com.vibejam.jamkit/Tests/Skyline/AimTests.cs`

**Interfaces:**
- Consumes: `System.Numerics.Vector3`.
- Produces: `AimModel { LeadsYou, FiresAtWhereYouAre, Area, Melee }`, `Aim.LeadPoint(Vector3 shooter, Vector3 target,
  Vector3 targetVel, float speed, float leadFactor) : Vector3`, `AimRules.CanShareEncounter(params AimModel[]) : bool`,
  `TelegraphedShot` (fields `telegraph = 0.8f`, `lockBefore = 0.25f`, `followRate = 6f`, `hitRadius = 0.5f`;
  `Start(Vector3)`, `Step(float dt, Vector3 target, out bool hit) : bool`, `Active`, `Locked`, `AimPoint`).

- [ ] **Step 1: Write the failing tests**

`Packages/com.vibejam.jamkit/Tests/Skyline/AimTests.cs`:
```csharp
using System.Numerics;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    public class AimTests
    {
        [Test]
        public void AStillTargetIsShotWhereItStands()
        {
            var p = Aim.LeadPoint(Vector3.Zero, new Vector3(10f, 0f, 0f), Vector3.Zero, 30f, 1f);
            Assert.That(Vector3.Distance(p, new Vector3(10f, 0f, 0f)), Is.LessThan(1e-4f));
        }

        [Test]
        public void AFullLeadMeetsTheTarget()
        {
            var shooter = Vector3.Zero;
            var target = new Vector3(0f, 0f, 20f);
            var vel = new Vector3(8f, 0f, 0f);
            const float speed = 30f;
            var p = Aim.LeadPoint(shooter, target, vel, speed, 1f);
            float t = Vector3.Distance(shooter, p) / speed;          // when the shot arrives at p
            var targetThen = target + vel * t;                        // where the target is then
            Assert.That(Vector3.Distance(p, targetThen), Is.LessThan(0.01f));
        }

        [Test]
        public void NoLeadMeansAtTheTargetNow()
        {
            var target = new Vector3(0f, 0f, 20f);
            var p = Aim.LeadPoint(Vector3.Zero, target, new Vector3(8f, 0f, 0f), 30f, 0f);
            Assert.That(Vector3.Distance(p, target), Is.LessThan(1e-4f));
        }

        [Test]
        public void NoInterceptFallsBackToTheTarget()
        {
            // the target flees faster than the shot: no solution
            var target = new Vector3(0f, 0f, 20f);
            var p = Aim.LeadPoint(Vector3.Zero, target, new Vector3(0f, 0f, 50f), 30f, 1f);
            Assert.That(Vector3.Distance(p, target), Is.LessThan(1e-4f));
        }

        [Test]
        public void ATargetOnTheShooterGivesNoNaN()
        {
            var p = Aim.LeadPoint(Vector3.Zero, Vector3.Zero, new Vector3(5f, 0f, 0f), 30f, 1f);
            Assert.That(float.IsNaN(p.X) || float.IsNaN(p.Y) || float.IsNaN(p.Z), Is.False);
        }

        [Test]
        public void LeadingAndWhereYouAreNeverShareAnEncounter()
        {
            Assert.That(AimRules.CanShareEncounter(AimModel.LeadsYou, AimModel.FiresAtWhereYouAre), Is.False);
            Assert.That(AimRules.CanShareEncounter(AimModel.LeadsYou, AimModel.Area, AimModel.Melee), Is.True);
            Assert.That(AimRules.CanShareEncounter(AimModel.FiresAtWhereYouAre, AimModel.Area), Is.True);
        }

        static bool RunShot(TelegraphedShot s, System.Func<float, Vector3> targetAt)
        {
            const float h = 1f / 120f;
            float t = 0f;
            s.Start(targetAt(0f));
            while (true)
            {
                t += h;
                if (s.Step(h, targetAt(t), out bool hit)) return hit;
                if (t > 5f) Assert.Fail("the shot never fired");
            }
        }

        [Test]
        public void TheSpotlightHitsAStillTarget()
        {
            Assert.That(RunShot(new TelegraphedShot(), t => new Vector3(0f, 0f, 20f)), Is.True);
        }

        [Test]
        public void ChangingDirectionAfterTheLockDodgesTheSpotlight()
        {
            // still until 0.6 s (after the 0.55 s lock), then sidestep at 7 m/s
            Assert.That(RunShot(new TelegraphedShot(), t => new Vector3(t < 0.6f ? 0f : (t - 0.6f) * 7f, 0f, 20f)), Is.False);
        }

        [Test]
        public void TheSpotlightLocksBeforeItFires()
        {
            var s = new TelegraphedShot();
            s.Start(Vector3.Zero);
            s.Step(0.5f, Vector3.Zero, out _);
            Assert.That(s.Locked, Is.False);
            s.Step(0.1f, Vector3.Zero, out _);
            Assert.That(s.Locked, Is.True);
            Assert.That(s.Active, Is.True);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test core-tests/Skyline/Skyline.Tests.csproj --nologo -v q`
Expected: build FAILS with `CS0103: The name 'Aim' does not exist in the current context`.

- [ ] **Step 3: Write the implementation**

`Packages/com.vibejam.jamkit/Runtime/Skyline/Aim.cs`:
```csharp
using System;
using System.Numerics;

namespace JamKit.Skyline
{
    /// <summary>How an enemy aims (docs/SKYLINE-DESIGN.md 2.5-2.6). Every enemy carries one.</summary>
    public enum AimModel { LeadsYou, FiresAtWhereYouAre, Area, Melee }

    public static class Aim
    {
        /// <summary>Where a shot at <paramref name="speed"/> m/s must go to meet a target moving at
        /// <paramref name="targetVel"/>, with the velocity scaled by <paramref name="leadFactor"/> (0 = at the target now,
        /// 1 = a perfect intercept). Falls back to the target's position when no intercept exists.</summary>
        public static Vector3 LeadPoint(Vector3 shooter, Vector3 target, Vector3 targetVel, float speed, float leadFactor)
        {
            if (speed <= 0f || leadFactor <= 0f) return target;
            Vector3 v = targetVel * leadFactor;
            Vector3 d = target - shooter;
            float a = Vector3.Dot(v, v) - speed * speed;
            float b = 2f * Vector3.Dot(d, v);
            float c = Vector3.Dot(d, d);
            float t;
            if (MathF.Abs(a) < 1e-6f)
            {
                if (MathF.Abs(b) < 1e-6f) return target;
                t = -c / b;
            }
            else
            {
                float disc = b * b - 4f * a * c;
                if (disc < 0f) return target;
                float s = MathF.Sqrt(disc);
                float t1 = (-b - s) / (2f * a), t2 = (-b + s) / (2f * a);
                t = t1 > 0f && t2 > 0f ? MathF.Min(t1, t2) : MathF.Max(t1, t2);
            }
            if (!(t > 0f) || float.IsInfinity(t)) return target;
            return target + v * t;
        }
    }

    /// <summary>The never-mix rule (2.6): an encounter never combines shots that lead you with shots at where you are now
    /// (both continuing and changing direction would be punished). Exceptions (Baja, the end of the game) bypass this.</summary>
    public static class AimRules
    {
        public static bool CanShareEncounter(params AimModel[] models)
        {
            bool leads = false, now = false;
            foreach (var m in models)
            {
                if (m == AimModel.LeadsYou) leads = true;
                if (m == AimModel.FiresAtWhereYouAre) now = true;
            }
            return !(leads && now);
        }
    }

    /// <summary>The spotlight's shot (2.5): a telegraph that follows the target with a lag, locks
    /// <see cref="lockBefore"/> seconds before the shot, then a hitscan at the locked point. Changing direction in the
    /// last <see cref="lockBefore"/> seconds, or breaking line of sight, dodges it.</summary>
    public sealed class TelegraphedShot
    {
        public float telegraph = 0.8f;
        public float lockBefore = 0.25f;
        /// <summary>How fast the laser catches up with the target (1/s, exponential).</summary>
        public float followRate = 6f;
        /// <summary>How far from the locked point still counts as a hit (m).</summary>
        public float hitRadius = 0.5f;

        float _t;
        Vector3 _aim;

        public bool Active { get; private set; }
        public bool Locked { get; private set; }
        public Vector3 AimPoint => _aim;

        public void Start(Vector3 targetNow)
        {
            Active = true;
            Locked = false;
            _t = 0f;
            _aim = targetNow;
        }

        /// <summary>Advances the telegraph. Returns true on the step the shot fires; <paramref name="hit"/> says whether it landed.</summary>
        public bool Step(float dt, Vector3 target, out bool hit)
        {
            hit = false;
            if (!Active) return false;
            _t += dt;
            if (!Locked)
            {
                float k = 1f - MathF.Exp(-followRate * dt);
                _aim += (target - _aim) * k;
                if (_t >= telegraph - lockBefore) Locked = true;
            }
            if (_t >= telegraph)
            {
                Active = false;
                hit = Vector3.Distance(target, _aim) <= hitRadius;
                return true;
            }
            return false;
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `powershell -NoProfile -File tools/skyline/test.ps1`
Expected: `Skyline and Movement tests passed.`

- [ ] **Step 5: Commit**

```bash
git add -- Packages/com.vibejam.jamkit/Runtime/Skyline/Aim.cs Packages/com.vibejam.jamkit/Tests/Skyline/AimTests.cs
git commit -m "Skyline: aim models, the spotlight's telegraphed shot, and the never-mix rule (spec 2.5-2.6)"
```

---

### Task 4: The headless combat sim and the dodge report

**Files:**
- Create: `Packages/com.vibejam.jamkit/Runtime/Skyline/CombatSim.cs`
- Create: `Packages/com.vibejam.jamkit/Runtime/Skyline/CombatBots.cs`
- Create: `Packages/com.vibejam.jamkit/Tests/Skyline/CombatSimTests.cs`

**Interfaces:**
- Consumes: `MoveSim(IMoveWorld, MoveDef)`, `MoveSim.Step(in MoveInput, float h)`, `MoveSim.Teleport(Vector3 feet,
  float yaw, Vector3 velocity)`, `MoveSim.Position`, `.Velocity`, `.Height`, `.def.radius`, `.def.tickRate`;
  `MoveInput { move, yaw, sprintHeld }`; `JamKit.Rng(ulong seed)` with `Range(float, float)`; `Aim`, `AimModel`,
  `TelegraphedShot` (Task 3).
- Produces: `IPlayerBot.Next(MoveSim sim, float time, float h) : MoveInput`, `Shooter` (fields `position`, `aim`,
  `projectileSpeed`, `fireInterval`, `leadFactor`, `projectileRadius`, `firstShotAt`), `CombatSim(MoveSim, IPlayerBot,
  IEnumerable<Shooter>)` with `Run(float seconds)`, `Hits`, `ShotsFired`, `Time`; `StandStillBot`,
  `StraightLineBot(float laneHalfLength = 15f)`, `JinkingBot(ulong seed, float laneHalfLength = 15f)`;
  `DodgeReport.Run(Func<IMoveWorld> makeWorld, Func<List<Shooter>> makeShooters, float seconds, ulong seed) :
  DodgeResult` (`straightHits`, `jinkHits`, `shots`, `Ratio`).

- [ ] **Step 1: Write the failing tests**

`Packages/com.vibejam.jamkit/Tests/Skyline/CombatSimTests.cs`:
```csharp
using System.Collections.Generic;
using System.Numerics;
using JamKit.Movement;
using JamKit.Skyline;
using JamKit.Tests.Movement;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    public class CombatSimTests
    {
        static BoxWorld Arena()
        {
            var w = new BoxWorld();
            w.Floor(-25f, 25f, -25f, 25f, 0f);
            return w;
        }

        static List<Shooter> OneShooter(AimModel aim, float lead) => new List<Shooter>
        {
            new Shooter { position = new Vector3(20f, 1.5f, 0f), aim = aim, projectileSpeed = 30f, fireInterval = 1.2f, leadFactor = lead },
        };

        static CombatSim Sim(IPlayerBot bot, List<Shooter> shooters)
        {
            var sim = new MoveSim(Arena(), new MoveDef());
            sim.Teleport(Vector3.Zero, 0f, Vector3.Zero);
            return new CombatSim(sim, bot, shooters);
        }

        [Test]
        public void AStandingTargetIsHitByNearlyEveryShot()
        {
            var c = Sim(new StandStillBot(), OneShooter(AimModel.FiresAtWhereYouAre, 0f));
            c.Run(30f);
            Assert.That(c.ShotsFired, Is.GreaterThan(20));
            Assert.That(c.Hits, Is.GreaterThanOrEqualTo(c.ShotsFired - 2));   // the last shots may still be in the air
        }

        [Test]
        public void LeadingCatchesAStraightLineRunnerMoreThanNotLeading()
        {
            var lead = Sim(new StraightLineBot(), OneShooter(AimModel.LeadsYou, 1f));
            var none = Sim(new StraightLineBot(), OneShooter(AimModel.LeadsYou, 0f));
            lead.Run(60f);
            none.Run(60f);
            Assert.That(lead.Hits, Is.GreaterThan(none.Hits));
        }

        [Test]
        public void TheSpotlightFiresTelegraphedHitscan()
        {
            var shooters = new List<Shooter>
            {
                new Shooter { position = new Vector3(20f, 6f, 0f), aim = AimModel.FiresAtWhereYouAre, projectileSpeed = 0f, fireInterval = 2f },
            };
            var c = Sim(new StandStillBot(), shooters);
            c.Run(10f);
            Assert.That(c.ShotsFired, Is.GreaterThanOrEqualTo(4));
            Assert.That(c.Hits, Is.EqualTo(c.ShotsFired));
        }

        [Test]
        public void TheDodgeReportIsDeterministicAndPrintsItsNumbers()
        {
            var a = DodgeReport.Run(Arena, () => OneShooter(AimModel.LeadsYou, 0.9f), 60f, 7UL);
            var b = DodgeReport.Run(Arena, () => OneShooter(AimModel.LeadsYou, 0.9f), 60f, 7UL);
            Assert.That(a.straightHits, Is.EqualTo(b.straightHits));
            Assert.That(a.jinkHits, Is.EqualTo(b.jinkHits));
            TestContext.WriteLine($"Dodge report (one leading drone, lead 0.9, 60 s): straight-line bot {a.straightHits} hits, jinking bot {a.jinkHits} hits, ratio {a.Ratio:0.00} (M1 target: about 2, neither 0)");
            Assert.That(a.shots, Is.GreaterThan(0));
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test core-tests/Skyline/Skyline.Tests.csproj --nologo -v q`
Expected: build FAILS with `CS0246: The type or namespace name 'Shooter' could not be found`.

- [ ] **Step 3: Write the implementation**

`Packages/com.vibejam.jamkit/Runtime/Skyline/CombatSim.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Movement;

namespace JamKit.Skyline
{
    /// <summary>Drives the player's body in the headless sim, like a player's hands.</summary>
    public interface IPlayerBot
    {
        MoveInput Next(MoveSim sim, float time, float h);
    }

    /// <summary>A stationary enemy that fires at the player (docs/SKYLINE-DESIGN.md 2.5).</summary>
    public sealed class Shooter
    {
        public Vector3 position;
        public AimModel aim = AimModel.LeadsYou;
        /// <summary>m/s; 0 means a telegraphed hitscan (the spotlight).</summary>
        public float projectileSpeed = 30f;
        public float fireInterval = 1.2f;
        /// <summary>0..1, how much of the player's velocity a leading shot accounts for (rises with difficulty).</summary>
        public float leadFactor = 0.9f;
        public float projectileRadius = 0.15f;
        /// <summary>Seconds into the fight before the first shot.</summary>
        public float firstShotAt = 0.5f;

        internal float nextFire = -1f;
        internal TelegraphedShot telegraphShot;
    }

    /// <summary>
    /// A headless fight: the real movement engine moving the player, stationary shooters, straight projectiles and the
    /// spotlight's telegraphed hitscan. It measures how often a way of moving gets hit (the dodging measurement, 2.6).
    /// </summary>
    public sealed class CombatSim
    {
        struct Projectile
        {
            public Vector3 pos, vel;
            public float radius, age;
        }

        const float MaxProjectileAge = 4f;

        readonly MoveSim _player;
        readonly IPlayerBot _bot;
        readonly List<Shooter> _shooters;
        readonly List<Projectile> _shots = new List<Projectile>();

        public int Hits { get; private set; }
        public int ShotsFired { get; private set; }
        public float Time { get; private set; }

        public CombatSim(MoveSim player, IPlayerBot bot, IEnumerable<Shooter> shooters)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _bot = bot ?? throw new ArgumentNullException(nameof(bot));
            _shooters = new List<Shooter>(shooters);
            foreach (var s in _shooters) s.nextFire = s.firstShotAt;
        }

        public void Run(float seconds)
        {
            float h = 1f / _player.def.tickRate;
            int steps = (int)MathF.Ceiling(seconds / h);
            for (int i = 0; i < steps; i++) Step(h);
        }

        Vector3 Chest => _player.Position + new Vector3(0f, _player.Height * 0.5f, 0f);

        void Step(float h)
        {
            var input = _bot.Next(_player, Time, h);
            _player.Step(in input, h);
            Time += h;

            foreach (var s in _shooters)
            {
                if (s.telegraphShot != null && s.telegraphShot.Active)
                {
                    if (s.telegraphShot.Step(h, Chest, out bool hit) && hit) Hits++;
                    continue;
                }
                if (Time < s.nextFire) continue;
                s.nextFire = Time + s.fireInterval;
                ShotsFired++;
                if (s.projectileSpeed <= 0f)
                {
                    if (s.telegraphShot == null) s.telegraphShot = new TelegraphedShot();
                    s.telegraphShot.Start(Chest);
                    continue;
                }
                Vector3 aimAt = s.aim == AimModel.LeadsYou
                    ? Aim.LeadPoint(s.position, Chest, _player.Velocity, s.projectileSpeed, s.leadFactor)
                    : Chest;
                Vector3 dir = aimAt - s.position;
                float len = dir.Length();
                if (len < 1e-4f) continue;
                _shots.Add(new Projectile { pos = s.position, vel = dir / len * s.projectileSpeed, radius = s.projectileRadius });
            }

            for (int i = _shots.Count - 1; i >= 0; i--)
            {
                var p = _shots[i];
                p.pos += p.vel * h;
                p.age += h;
                if (HitsPlayer(p.pos, p.radius)) { Hits++; _shots.RemoveAt(i); continue; }
                if (p.age > MaxProjectileAge) { _shots.RemoveAt(i); continue; }
                _shots[i] = p;
            }
        }

        /// <summary>The player's capsule: a vertical segment from feet+radius to feet+height-radius, thickened by the radius.</summary>
        bool HitsPlayer(Vector3 point, float radius)
        {
            float r = _player.def.radius;
            Vector3 a = _player.Position + new Vector3(0f, r, 0f);
            Vector3 b = _player.Position + new Vector3(0f, MathF.Max(r, _player.Height - r), 0f);
            Vector3 ab = b - a;
            float t = Vector3.Dot(point - a, ab) / MathF.Max(1e-6f, Vector3.Dot(ab, ab));
            t = MathF.Max(0f, MathF.Min(1f, t));
            return Vector3.Distance(point, a + ab * t) <= r + radius;
        }
    }

    public struct DodgeResult
    {
        public int straightHits, jinkHits, shots;
        public float seconds;
        public float Ratio => jinkHits > 0 ? (float)straightHits / jinkHits : float.PositiveInfinity;
    }

    /// <summary>The dodging measurement (2.6): the same fight run by a straight-line bot and a jinking bot. Target on
    /// Normal: the straight-line bot takes about twice the hits, and neither goes unhit.</summary>
    public static class DodgeReport
    {
        public static DodgeResult Run(Func<IMoveWorld> makeWorld, Func<List<Shooter>> makeShooters, float seconds, ulong seed)
        {
            var straight = Fight(makeWorld(), new StraightLineBot(), makeShooters(), seconds);
            var jink = Fight(makeWorld(), new JinkingBot(seed), makeShooters(), seconds);
            return new DodgeResult { straightHits = straight.Hits, jinkHits = jink.Hits, shots = straight.ShotsFired, seconds = seconds };
        }

        static CombatSim Fight(IMoveWorld world, IPlayerBot bot, List<Shooter> shooters, float seconds)
        {
            var sim = new MoveSim(world, new MoveDef());
            sim.Teleport(Vector3.Zero, 0f, Vector3.Zero);
            var c = new CombatSim(sim, bot, shooters);
            c.Run(seconds);
            return c;
        }
    }
}
```

`Packages/com.vibejam.jamkit/Runtime/Skyline/CombatBots.cs`:
```csharp
using System;
using System.Numerics;
using JamKit.Movement;

namespace JamKit.Skyline
{
    /// <summary>Stands still: the baseline every shot should hit.</summary>
    public sealed class StandStillBot : IPlayerBot
    {
        public MoveInput Next(MoveSim sim, float time, float h) => new MoveInput { yaw = 0f };
    }

    /// <summary>Sprints straight lanes along Z at full speed, turning at the ends: the habit leading shots punish.</summary>
    public sealed class StraightLineBot : IPlayerBot
    {
        readonly float _half;
        float _yaw;

        public StraightLineBot(float laneHalfLength = 15f) { _half = laneHalfLength; }

        public MoveInput Next(MoveSim sim, float time, float h)
        {
            if (sim.Position.Z > _half) _yaw = 180f;
            else if (sim.Position.Z < -_half) _yaw = 0f;
            return new MoveInput { move = new Vector2(0f, 1f), yaw = _yaw, sprintHeld = true };
        }
    }

    /// <summary>Runs the same lanes but flips its strafe every 0.3-0.7 s (seeded): the skill the design rewards.</summary>
    public sealed class JinkingBot : IPlayerBot
    {
        readonly float _half;
        readonly Rng _rng;
        float _yaw, _strafe = 1f, _nextSwitch;

        public JinkingBot(ulong seed, float laneHalfLength = 15f)
        {
            _rng = new Rng(seed);
            _half = laneHalfLength;
        }

        public MoveInput Next(MoveSim sim, float time, float h)
        {
            if (sim.Position.Z > _half) _yaw = 180f;
            else if (sim.Position.Z < -_half) _yaw = 0f;
            if (time >= _nextSwitch)
            {
                _strafe = -_strafe;
                _nextSwitch = time + _rng.Range(0.3f, 0.7f);
            }
            // keep inside the arena: when far from the centre line, strafe back towards it
            float facing = _yaw < 90f ? 1f : -1f;            // facing +Z, right is +X; facing -Z, right is -X
            if (MathF.Abs(sim.Position.X) > 12f) _strafe = -MathF.Sign(sim.Position.X) * facing;
            return new MoveInput { move = Vector2.Normalize(new Vector2(_strafe, 0.6f)), yaw = _yaw, sprintHeld = true };
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `powershell -NoProfile -File tools/skyline/test.ps1`
Expected: `Skyline and Movement tests passed.` Run `dotnet test core-tests/Skyline/Skyline.Tests.csproj --logger
"console;verbosity=detailed" --filter TheDodgeReport` to see the dodge report line, and copy it into the hand-back.
**Don't tune the bots or the shooter to make the ratio look good**; M0 only builds the measurement. If
`LeadingCatchesAStraightLineRunnerMoreThanNotLeading` fails, report the numbers instead of loosening the test.

- [ ] **Step 5: Commit**

```bash
git add -- Packages/com.vibejam.jamkit/Runtime/Skyline/CombatSim.cs Packages/com.vibejam.jamkit/Runtime/Skyline/CombatBots.cs Packages/com.vibejam.jamkit/Tests/Skyline/CombatSimTests.cs
git commit -m "Skyline: the headless combat sim, straight-line and jinking bots, and the dodge report (spec 2.6)"
```

---

### Task 5: The sound forge

**Files:**
- Create: `tools/skyline/soundforge/pyproject.toml`
- Create: `tools/skyline/soundforge/soundforge.py`
- Create: `tools/skyline/soundforge/tests/conftest.py`
- Create: `tools/skyline/soundforge/tests/test_soundforge.py`
- Create: `tools/skyline/soundforge/recipes/test_tone.json`
- Create: `tools/skyline/soundforge/README.md`

**Interfaces:**
- Produces (Python): `render_recipe(recipe: dict, base_dir: Path, sr: int | None = None) -> np.ndarray` (float32,
  shape `(n, 2)`), `render_timeline(timeline: dict, base_dir: Path, duration: float | None = None) -> np.ndarray`,
  `write_wav(path, audio, sr)`, `RecipeError(ValueError)`; CLI `uv run python soundforge.py render <recipe.json> -o
  <out.wav>` and `uv run python soundforge.py timeline <timeline.json> [--duration S] -o <out.wav>`.
- Recipe format (JSON): `{"sample_rate": 48000, "length": 1.0, "seed": 1, "layers": [ ... ], "master": {"limit_db":
  -1.0}}`. Each layer: `type` (`sine` | `noise` | `sample`), `start` (s), `attack` (s, linear), `decay` (s to reach
  -60 dB), `gain_db`, `pan` (-1 left .. 1 right), optional `highpass` / `lowpass` (Hz). `sine`: `freq`, optional
  `freq_end` (exponential sweep). `noise`: `color` (`white` | `pink`). `sample`: `path` (relative to the recipe),
  optional `pitch_semitones`.
- Timeline format: `{"duration": 2.1, "events": [{"at": 0.1, "recipe": "mag_out.json", "gain_db": 0}]}`; `at` is a
  fraction of the duration (spec 2.3: events sit at fixed fractions of the reload time, so changing the duration
  rescales them).

- [ ] **Step 1: Create the project, the import shim and the test tone recipe**

`tools/skyline/soundforge/pyproject.toml`:
```toml
[project]
name = "skyline-soundforge"
version = "0.1.0"
requires-python = ">=3.12"
dependencies = ["numpy>=2.0", "scipy>=1.13", "soundfile>=0.12"]

[dependency-groups]
dev = ["pytest>=8"]

[tool.uv]
package = false
```

`tools/skyline/soundforge/tests/conftest.py`:
```python
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
```

`tools/skyline/soundforge/recipes/test_tone.json` (the 440 Hz beep the pipeline uses to prove audio is captured):
```json
{
  "sample_rate": 48000,
  "length": 0.5,
  "layers": [
    {"type": "sine", "freq": 440, "start": 0.0, "attack": 0.005, "decay": 0.45, "gain_db": -6, "pan": 0}
  ],
  "master": {"limit_db": -1.0}
}
```

- [ ] **Step 2: Write the failing tests**

`tools/skyline/soundforge/tests/test_soundforge.py`:
```python
import json
from pathlib import Path

import numpy as np
import pytest

import soundforge as sf

SR = 48000


def peak_freq(mono, sr=SR):
    spec = np.abs(np.fft.rfft(mono * np.hanning(len(mono))))
    return np.fft.rfftfreq(len(mono), 1 / sr)[np.argmax(spec)]


def onset(mono, sr=SR, frac=0.1):
    level = np.abs(mono)
    return np.argmax(level >= level.max() * frac) / sr


def test_sine_has_its_frequency(tmp_path):
    a = sf.render_recipe({"length": 1.0, "layers": [{"type": "sine", "freq": 440, "attack": 0.01, "decay": 5.0}]}, tmp_path)
    assert a.shape == (SR, 2)
    assert abs(peak_freq(a[:, 0]) - 440) < 2


def test_decay_reaches_minus_60_db(tmp_path):
    a = sf.render_recipe({"length": 1.0, "layers": [{"type": "sine", "freq": 1000, "attack": 0.001, "decay": 0.3}]}, tmp_path)
    m = a[:, 0]
    peak = np.abs(m).max()
    late = np.abs(m[int(0.31 * SR):int(0.33 * SR)]).max()
    assert 20 * np.log10(late / peak) < -55


def test_gain_and_pan(tmp_path):
    a = sf.render_recipe({"length": 0.5, "master": {"limit_db": 0},
                          "layers": [{"type": "sine", "freq": 200, "attack": 0.001, "decay": 50, "gain_db": -6, "pan": -1}]}, tmp_path)
    assert abs(np.abs(a[:, 0]).max() - 10 ** (-6 / 20)) < 0.02
    assert np.abs(a[:, 1]).max() < 1e-6


def test_limiter_caps_the_peak(tmp_path):
    loud = {"type": "sine", "freq": 100, "attack": 0.001, "decay": 50, "gain_db": 0}
    a = sf.render_recipe({"length": 0.3, "layers": [loud, dict(loud)], "master": {"limit_db": -1.0}}, tmp_path)
    assert np.abs(a).max() <= 10 ** (-1 / 20) + 1e-4


def test_timeline_events_sit_at_fractions_and_rescale(tmp_path):
    click = {"length": 0.05, "layers": [{"type": "noise", "attack": 0.0005, "decay": 0.03}]}
    (tmp_path / "click.json").write_text(json.dumps(click))
    tl = {"duration": 2.0, "events": [{"at": 0.5, "recipe": "click.json"}]}
    a = sf.render_timeline(tl, tmp_path)
    assert abs(onset(a[:, 0]) - 1.0) < 0.005
    b = sf.render_timeline(tl, tmp_path, duration=1.0)
    assert abs(onset(b[:, 0]) - 0.5) < 0.005


def test_sample_layer_pitch_shift(tmp_path):
    t = np.arange(SR) / SR
    sf.write_wav(tmp_path / "a440.wav", np.stack([np.sin(2 * np.pi * 440 * t)] * 2, axis=1).astype(np.float32), SR)
    a = sf.render_recipe({"length": 1.0, "master": {"limit_db": 0},
                          "layers": [{"type": "sample", "path": "a440.wav", "pitch_semitones": 12, "attack": 0.001, "decay": 50}]}, tmp_path)
    assert abs(peak_freq(a[: SR // 2, 0]) - 880) < 4


def test_missing_sample_names_the_layer(tmp_path):
    with pytest.raises(sf.RecipeError, match=r"layer 1 .*nope\.wav"):
        sf.render_recipe({"length": 0.2, "layers": [{"type": "sine", "freq": 100}, {"type": "sample", "path": "nope.wav"}]}, tmp_path)


def test_unknown_layer_type_names_the_layer(tmp_path):
    with pytest.raises(sf.RecipeError, match=r"layer 0.*'saw'"):
        sf.render_recipe({"length": 0.2, "layers": [{"type": "saw"}]}, tmp_path)


def test_test_tone_recipe_renders():
    here = Path(__file__).resolve().parent.parent / "recipes"
    a = sf.render_recipe(json.loads((here / "test_tone.json").read_text()), here)
    assert abs(peak_freq(a[:, 0]) - 440) < 3
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `cd tools/skyline/soundforge; uv run --group dev pytest -q`
Expected: FAIL with `ModuleNotFoundError: No module named 'soundforge'`.

- [ ] **Step 4: Write the implementation**

`tools/skyline/soundforge/soundforge.py`:
```python
"""The sound forge: renders layered sounds from JSON recipes, and handling sets from event timelines.

docs/SKYLINE-DESIGN.md 2.3 and 9.4. Recipes stack layers (sines with sweeps, noise, recorded or CC0 samples), each with
an envelope, gain, pan and filters. Timelines place recipes at fractions of a duration, so a faster reload rescales its
sound events with its animation.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

import numpy as np
import soundfile
from scipy import signal

DEFAULT_SR = 48000
LN1000 = np.log(1000.0)  # -60 dB


class RecipeError(ValueError):
    """A recipe that can't be rendered; the message names the layer."""


def write_wav(path, audio: np.ndarray, sr: int = DEFAULT_SR) -> None:
    soundfile.write(str(path), np.asarray(audio, dtype=np.float32), sr, subtype="FLOAT")


def _shape(n: int, sr: int, attack: float, decay: float) -> np.ndarray:
    """Linear attack, then an exponential decay that reaches -60 dB `decay` seconds after the attack."""
    t = np.arange(n) / sr
    gain = np.ones(n, dtype=np.float64)
    if attack > 0:
        gain = np.minimum(1.0, t / attack)
    after = np.maximum(0.0, t - attack)
    if decay > 0:
        gain = gain * np.exp(-after * LN1000 / decay)
    return gain


def _filter(x: np.ndarray, sr: int, layer: dict) -> np.ndarray:
    if layer.get("highpass"):
        x = signal.sosfilt(signal.butter(2, layer["highpass"], "highpass", fs=sr, output="sos"), x)
    if layer.get("lowpass"):
        x = signal.sosfilt(signal.butter(2, layer["lowpass"], "lowpass", fs=sr, output="sos"), x)
    return x


def _pink(n: int, rng: np.random.Generator) -> np.ndarray:
    white = rng.standard_normal(n)
    spec = np.fft.rfft(white)
    f = np.arange(len(spec), dtype=np.float64)
    f[0] = 1.0
    pink = np.fft.irfft(spec / np.sqrt(f), n)
    return pink / (np.abs(pink).max() or 1.0)


def _load_sample(path: Path, sr: int, semitones: float) -> np.ndarray:
    data, file_sr = soundfile.read(str(path), dtype="float64", always_2d=True)
    mono = data.mean(axis=1)
    step = (file_sr / sr) * 2.0 ** (semitones / 12.0)  # source samples consumed per output sample
    n = int(len(mono) / step)
    return np.interp(np.arange(n) * step, np.arange(len(mono)), mono)


def _pan_gains(pan: float) -> tuple[float, float]:
    """Hard left/right at the ends, full level on both sides at the centre, a constant-power curve between."""
    pan = float(np.clip(pan, -1.0, 1.0))
    if pan == 0.0:
        return 1.0, 1.0
    if pan <= -1.0:
        return 1.0, 0.0
    if pan >= 1.0:
        return 0.0, 1.0
    angle = (pan + 1.0) * np.pi / 4.0
    return float(np.cos(angle) * np.sqrt(2)), float(np.sin(angle) * np.sqrt(2))


def _layer(layer: dict, index: int, n: int, sr: int, base_dir: Path, rng: np.random.Generator) -> np.ndarray:
    kind = layer.get("type")
    start = int(round(float(layer.get("start", 0.0)) * sr))
    if start >= n:
        return np.zeros((n, 2))
    m = n - start
    if kind == "sine":
        t = np.arange(m) / sr
        f0 = float(layer.get("freq", 440.0))
        f1 = float(layer.get("freq_end", f0))
        if f1 == f0:
            phase = 2 * np.pi * f0 * t
        else:  # exponential sweep from f0 to f1 over the layer
            k = np.log(f1 / f0) / (m / sr)
            phase = 2 * np.pi * f0 * (np.exp(k * t) - 1) / k
        x = np.sin(phase)
    elif kind == "noise":
        x = _pink(m, rng) if layer.get("color") == "pink" else rng.uniform(-1.0, 1.0, m)
    elif kind == "sample":
        path = base_dir / str(layer.get("path", ""))
        if not path.is_file():
            raise RecipeError(f"layer {index} (sample): file not found: {path}")
        x = _load_sample(path, sr, float(layer.get("pitch_semitones", 0.0)))[:m]
        x = np.pad(x, (0, m - len(x)))
    else:
        raise RecipeError(f"layer {index}: unknown type {kind!r} (use 'sine', 'noise' or 'sample')")
    x = _filter(x, sr, layer)
    x = x * _shape(m, sr, float(layer.get("attack", 0.002)), float(layer.get("decay", 0.25)))
    x = x * 10 ** (float(layer.get("gain_db", 0.0)) / 20.0)
    left, right = _pan_gains(layer.get("pan", 0.0))
    out = np.zeros((n, 2))
    out[start:, 0] = x * left
    out[start:, 1] = x * right
    return out


def _master(audio: np.ndarray, master: dict) -> np.ndarray:
    limit = 10 ** (float(master.get("limit_db", -1.0)) / 20.0)
    peak = np.abs(audio).max() if audio.size else 0.0
    if peak > limit:
        audio = audio * (limit / peak)
    return audio


def render_recipe(recipe: dict, base_dir: Path, sr: int | None = None) -> np.ndarray:
    sr = int(sr or recipe.get("sample_rate", DEFAULT_SR))
    n = int(round(float(recipe.get("length", 1.0)) * sr))
    rng = np.random.default_rng(int(recipe.get("seed", 1)))
    out = np.zeros((n, 2))
    for i, layer in enumerate(recipe.get("layers", [])):
        out += _layer(layer, i, n, sr, Path(base_dir), rng)
    return _master(out, recipe.get("master", {})).astype(np.float32)


def render_timeline(timeline: dict, base_dir: Path, duration: float | None = None) -> np.ndarray:
    base_dir = Path(base_dir)
    sr = int(timeline.get("sample_rate", DEFAULT_SR))
    dur = float(duration if duration is not None else timeline["duration"])
    parts = []
    for i, ev in enumerate(timeline.get("events", [])):
        path = base_dir / ev["recipe"]
        if not path.is_file():
            raise RecipeError(f"event {i}: recipe not found: {path}")
        part = render_recipe(json.loads(path.read_text()), path.parent, sr) * 10 ** (float(ev.get("gain_db", 0.0)) / 20.0)
        parts.append((int(round(float(ev["at"]) * dur * sr)), part))
    n = max([int(round(dur * sr))] + [s + len(p) for s, p in parts])
    out = np.zeros((n, 2), dtype=np.float64)
    for s, p in parts:
        out[s:s + len(p)] += p
    return _master(out, timeline.get("master", {})).astype(np.float32)


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    sub = ap.add_subparsers(dest="cmd", required=True)
    r = sub.add_parser("render", help="render one recipe")
    r.add_argument("recipe")
    r.add_argument("-o", "--out", required=True)
    t = sub.add_parser("timeline", help="render a handling-set timeline")
    t.add_argument("timeline")
    t.add_argument("--duration", type=float)
    t.add_argument("-o", "--out", required=True)
    a = ap.parse_args(argv)
    path = Path(a.recipe if a.cmd == "render" else a.timeline)
    spec = json.loads(path.read_text())
    try:
        audio = render_recipe(spec, path.parent) if a.cmd == "render" else render_timeline(spec, path.parent, a.duration)
    except RecipeError as e:
        print(f"soundforge: {e}", file=sys.stderr)
        return 2
    sr = int(spec.get("sample_rate", DEFAULT_SR))
    write_wav(a.out, audio, sr)
    print(f"wrote {a.out} ({len(audio) / sr:.3f} s)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
```

`tools/skyline/soundforge/README.md`:
```markdown
# The sound forge (Skyline Repo)

Renders layered sounds from JSON recipes and handling sets (equip, fire, reload...) from event timelines.
Spec: docs/SKYLINE-DESIGN.md 2.3 and 9.4. Shipped sound is synthesized, recorded or CC0; reference clips from other
games are measuring sticks only and never go in this folder.

    uv run python soundforge.py render recipes/test_tone.json -o out.wav
    uv run python soundforge.py timeline <timeline.json> --duration 1.8 -o reload.wav
    uv run --group dev pytest -q

Judge the result with the ears skill (`~/.claude/skills/ears`), against the weapon's reference clips.
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `cd tools/skyline/soundforge; uv run --group dev pytest -q`
Expected: `9 passed`.

- [ ] **Step 6: Commit**

```bash
git add -- tools/skyline/soundforge/pyproject.toml tools/skyline/soundforge/uv.lock tools/skyline/soundforge/soundforge.py tools/skyline/soundforge/tests/conftest.py tools/skyline/soundforge/tests/test_soundforge.py tools/skyline/soundforge/recipes/test_tone.json tools/skyline/soundforge/README.md
git commit -m "Skyline: the sound forge (layered recipes, handling-set timelines that rescale; spec 2.3)"
```

---

### Task 6: The ears skill (measurements, pictures, reference comparison)

A global skill, outside the repo: `C:\Users\User\.claude\skills\ears\`. Follow superpowers:writing-skills for the
SKILL.md. Layers 3-4 (classifiers and Gemini) are **not** in M0 (spec 9.5).

**Files:**
- Create: `C:\Users\User\.claude\skills\ears\SKILL.md`
- Create: `C:\Users\User\.claude\skills\ears\pyproject.toml`
- Create: `C:\Users\User\.claude\skills\ears\listen.py`
- Create: `C:\Users\User\.claude\skills\ears\tests\conftest.py`
- Create: `C:\Users\User\.claude\skills\ears\tests\test_listen.py`

**Interfaces:**
- Produces (Python): `load(path) -> (np.ndarray (n, ch) float64, sr)` (mp4/mkv/webm/mov go through ffmpeg),
  `measure(audio, sr) -> dict`, `onsets(audio, sr) -> list[float]`, `compare(cand, ref, sr_c, sr_r) -> dict` (with
  `diffs` and plain-English `lines`), `picture(audio, sr, png_path, title)`; CLI `uv run --project <skill> python
  listen.py measure <file> [--png out.png]`, `compare <candidate> <reference> [--png out.png]`, `events <file>`,
  printing JSON.
- `measure` keys: `seconds`, `channels`, `silent`, `notes`, `lufs` (None when the clip is under 0.4 s or silent),
  `sample_peak_dbfs`, `true_peak_dbtp`, `crest_db`, `attack_ms`, `tail_ms`, `centroid_hz`, `rolloff_hz`, `flatness`,
  `band_share` (`sub` <60, `low` 60-250, `mid` 250-2k, `presence` 2k-8k, `air` >8k Hz), `stereo` (`width`,
  `correlation`), `dc_offset`, `clipped_samples`.

- [ ] **Step 1: Create the project**

`C:\Users\User\.claude\skills\ears\pyproject.toml`:
```toml
[project]
name = "ears"
version = "0.1.0"
requires-python = ">=3.12"
dependencies = ["numpy>=2.0", "scipy>=1.13", "soundfile>=0.12", "pyloudnorm>=0.1.1", "matplotlib>=3.9"]

[dependency-groups]
dev = ["pytest>=8"]

[tool.uv]
package = false
```

`C:\Users\User\.claude\skills\ears\tests\conftest.py`:
```python
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
```

- [ ] **Step 2: Write the failing tests**

`C:\Users\User\.claude\skills\ears\tests\test_listen.py`:
```python
import numpy as np
import soundfile

import listen

SR = 48000


def sine(freq, secs, db=0.0, sr=SR):
    t = np.arange(int(secs * sr)) / sr
    return (np.sin(2 * np.pi * freq * t) * 10 ** (db / 20)).reshape(-1, 1)


def test_peaks_of_a_minus_6_db_sine():
    m = listen.measure(sine(1000, 1.0, -6), SR)
    assert abs(m["sample_peak_dbfs"] + 6) < 0.1
    assert abs(m["true_peak_dbtp"] + 6) < 0.3
    assert m["lufs"] is not None


def test_attack_and_tail_of_a_decaying_hit():
    t = np.arange(SR) / SR
    rng = np.random.default_rng(3)
    x = (rng.uniform(-1, 1, SR) * np.exp(-t / 0.05)).reshape(-1, 1)
    m = listen.measure(x, SR)
    assert m["attack_ms"] < 3
    expected = 0.05 * np.log(1000) * 1000  # about 345 ms to -60 dB
    assert abs(m["tail_ms"] - expected) / expected < 0.15


def test_a_low_sine_sits_in_the_low_band():
    m = listen.measure(sine(100, 1.0, -6), SR)
    assert m["band_share"]["low"] > 0.9


def test_mono_in_stereo_has_no_width():
    s = sine(500, 1.0, -6)
    m = listen.measure(np.hstack([s, s]), SR)
    assert m["stereo"]["width"] < 0.01
    assert m["stereo"]["correlation"] > 0.99


def test_short_and_silent_clips_do_not_crash():
    short = listen.measure(sine(1000, 0.1, -6), SR)
    assert short["lufs"] is None and any("0.4 s" in n for n in short["notes"])
    silent = listen.measure(np.zeros((SR, 1)), SR)
    assert silent["silent"] is True and silent["lufs"] is None
    assert silent["sample_peak_dbfs"] == float("-inf")


def test_onsets_of_three_clicks():
    x = np.zeros(SR)
    for at in (0.1, 0.5, 0.9):
        i = int(at * SR)
        x[i:i + 480] = np.exp(-np.arange(480) / 60.0)
    found = listen.onsets(x.reshape(-1, 1), SR)
    assert len(found) == 3
    for got, want in zip(found, (0.1, 0.5, 0.9)):
        assert abs(got - want) < 0.005


def test_compare_says_how_much_louder():
    ref = sine(1000, 1.0, -12)
    cand = sine(1000, 1.0, -6)
    c = listen.compare(cand, ref, SR, SR)
    assert abs(c["diffs"]["sample_peak_dbfs"] - 6) < 0.2
    assert any("louder" in line for line in c["lines"])


def test_picture_writes_a_png(tmp_path):
    p = tmp_path / "x.png"
    listen.picture(sine(440, 0.5, -6), SR, p, "test")
    assert p.is_file() and p.stat().st_size > 10_000


def test_load_reads_a_wav(tmp_path):
    p = tmp_path / "a.wav"
    soundfile.write(str(p), np.hstack([sine(440, 0.5), sine(440, 0.5)]).astype(np.float32), SR)
    audio, sr = listen.load(p)
    assert sr == SR and audio.shape == (SR // 2, 2)
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `cd C:\Users\User\.claude\skills\ears; uv run --group dev pytest -q`
Expected: FAIL with `ModuleNotFoundError: No module named 'listen'`.

- [ ] **Step 4: Write the implementation**

`C:\Users\User\.claude\skills\ears\listen.py`:
```python
"""Ears for a model that can't hear: measurements, pictures and reference comparison for sound clips.

Layers 1-2 of the design in VibeJamKit docs/SKYLINE-DESIGN.md 9.5. Every claim made from this tool should cite a number
or a picture; measurements overrule descriptions.
"""
from __future__ import annotations

import argparse
import json
import math
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

import numpy as np
import soundfile
from scipy import signal

BANDS = {"sub": (0, 60), "low": (60, 250), "mid": (250, 2000), "presence": (2000, 8000), "air": (8000, 1e9)}
VIDEO = {".mp4", ".mkv", ".webm", ".mov"}
EPS = 1e-12


def load(path) -> tuple[np.ndarray, int]:
    path = Path(path)
    if path.suffix.lower() in VIDEO:
        ffmpeg = shutil.which("ffmpeg")
        if not ffmpeg:
            raise RuntimeError("ffmpeg not found on PATH (needed for video files)")
        with tempfile.TemporaryDirectory() as tmp:
            wav = Path(tmp) / "a.wav"
            subprocess.run([ffmpeg, "-v", "error", "-y", "-i", str(path), "-vn", "-ac", "2", "-ar", "48000", str(wav)], check=True)
            data, sr = soundfile.read(str(wav), dtype="float64", always_2d=True)
            return data, sr
    data, sr = soundfile.read(str(path), dtype="float64", always_2d=True)
    return data, sr


def _db(x: float) -> float:
    return 20 * math.log10(x) if x > 0 else float("-inf")


def _level(mono: np.ndarray, sr: int, ms: float = 1.0) -> np.ndarray:
    """A smoothed absolute level (moving average over `ms` milliseconds)."""
    n = max(1, int(sr * ms / 1000))
    return np.convolve(np.abs(mono), np.ones(n) / n, mode="same")


def measure(audio: np.ndarray, sr: int) -> dict:
    audio = np.asarray(audio, dtype=np.float64)
    if audio.ndim == 1:
        audio = audio.reshape(-1, 1)
    mono = audio.mean(axis=1)
    notes: list[str] = []
    peak = float(np.abs(audio).max()) if audio.size else 0.0
    silent = peak < 1e-6
    out = {"seconds": len(audio) / sr, "channels": audio.shape[1], "silent": silent, "notes": notes}
    out["sample_peak_dbfs"] = _db(peak)
    out["dc_offset"] = float(mono.mean()) if len(mono) else 0.0
    out["clipped_samples"] = int((np.abs(audio) >= 0.999).sum())
    if silent:
        notes.append("silent: nothing above -120 dBFS")
        out.update({"lufs": None, "true_peak_dbtp": float("-inf"), "crest_db": None, "attack_ms": None, "tail_ms": None,
                    "centroid_hz": None, "rolloff_hz": None, "flatness": None,
                    "band_share": {k: 0.0 for k in BANDS}, "stereo": {"width": 0.0, "correlation": None}})
        return out

    over = signal.resample_poly(audio, 4, 1, axis=0)
    out["true_peak_dbtp"] = _db(float(np.abs(over).max()))
    rms = float(np.sqrt(np.mean(mono ** 2)))
    out["crest_db"] = _db(float(np.abs(mono).max())) - _db(rms)

    if len(audio) / sr < 0.4:
        out["lufs"] = None
        notes.append("loudness (LUFS) needs at least 0.4 s; this clip is shorter")
    else:
        import pyloudnorm
        out["lufs"] = float(pyloudnorm.Meter(sr).integrated_loudness(audio if audio.shape[1] <= 5 else mono))

    level = _level(mono, sr)
    ipk = int(np.argmax(level))
    lpk = level[ipk]
    start = int(np.argmax(level >= lpk * 0.1))
    near = int(np.argmax(level >= lpk * 10 ** (-3 / 20)))
    out["attack_ms"] = max(0.0, (near - start) / sr * 1000)
    above = np.nonzero(level[ipk:] >= lpk * 1e-3)[0]
    out["tail_ms"] = float(above[-1]) / sr * 1000 if len(above) else 0.0

    spec = np.abs(np.fft.rfft(mono * np.hanning(len(mono)))) ** 2
    freqs = np.fft.rfftfreq(len(mono), 1 / sr)
    total = float(spec.sum()) + EPS
    out["centroid_hz"] = float((freqs * spec).sum() / total)
    cum = np.cumsum(spec) / total
    out["rolloff_hz"] = float(freqs[min(len(freqs) - 1, int(np.searchsorted(cum, 0.85)))])
    out["flatness"] = float(np.exp(np.mean(np.log(spec + EPS))) / (np.mean(spec) + EPS))
    out["band_share"] = {k: float(spec[(freqs >= lo) & (freqs < hi)].sum() / total) for k, (lo, hi) in BANDS.items()}

    if audio.shape[1] >= 2:
        left, right = audio[:, 0], audio[:, 1]
        mid, side = (left + right) / 2, (left - right) / 2
        width = float(np.sqrt(np.mean(side ** 2)) / (np.sqrt(np.mean(mid ** 2)) + EPS))
        corr = float(np.corrcoef(left, right)[0, 1]) if np.std(left) > 0 and np.std(right) > 0 else None
        out["stereo"] = {"width": width, "correlation": corr}
    else:
        out["stereo"] = {"width": 0.0, "correlation": None}
    return out


def onsets(audio: np.ndarray, sr: int, rise_db: float = 12.0, min_gap: float = 0.04) -> list[float]:
    """Times (s) where the level jumps by rise_db within 10 ms: the events of a reload or a burst."""
    mono = np.asarray(audio, dtype=np.float64)
    mono = mono.mean(axis=1) if mono.ndim == 2 else mono
    level = _level(mono, sr, 1.0)
    floor = max(level.max() * 1e-3, 1e-9)
    lag = max(1, int(0.01 * sr))
    ratio = (level[lag:] + floor) / (level[:-lag] + floor)
    hits = np.nonzero(ratio >= 10 ** (rise_db / 20))[0] + lag
    found: list[float] = []
    last = -1e9
    for i in hits:
        t = i / sr
        if t - last >= min_gap:
            start = i - lag + int(np.argmax(level[i - lag:i + 1] >= level[i] * 0.1))
            found.append(start / sr)
            last = t
    return found


def compare(cand: np.ndarray, ref: np.ndarray, sr_c: int, sr_r: int) -> dict:
    a, b = measure(cand, sr_c), measure(ref, sr_r)
    keys = ["sample_peak_dbfs", "true_peak_dbtp", "lufs", "crest_db", "attack_ms", "tail_ms", "centroid_hz", "rolloff_hz", "flatness"]
    diffs = {}
    for k in keys:
        if a.get(k) is not None and b.get(k) is not None and math.isfinite(a[k]) and math.isfinite(b[k]):
            diffs[k] = a[k] - b[k]
    for band in BANDS:
        diffs[f"band_{band}"] = a["band_share"][band] - b["band_share"][band]
    lines = []
    if "sample_peak_dbfs" in diffs and abs(diffs["sample_peak_dbfs"]) >= 1:
        word = "louder" if diffs["sample_peak_dbfs"] > 0 else "quieter"
        lines.append(f"The candidate peaks {abs(diffs['sample_peak_dbfs']):.1f} dB {word} than the reference.")
    if "attack_ms" in diffs and abs(diffs["attack_ms"]) >= 2:
        word = "slower" if diffs["attack_ms"] > 0 else "faster"
        lines.append(f"Its attack is {abs(diffs['attack_ms']):.0f} ms {word} ({a['attack_ms']:.0f} vs {b['attack_ms']:.0f} ms).")
    if "tail_ms" in diffs and abs(diffs["tail_ms"]) >= 0.2 * max(b["tail_ms"] or 1, 1):
        word = "longer" if diffs["tail_ms"] > 0 else "shorter"
        lines.append(f"Its tail is {abs(diffs['tail_ms']):.0f} ms {word} ({a['tail_ms']:.0f} vs {b['tail_ms']:.0f} ms).")
    if "centroid_hz" in diffs and abs(diffs["centroid_hz"]) >= 0.15 * max(b["centroid_hz"] or 1, 1):
        word = "brighter" if diffs["centroid_hz"] > 0 else "darker"
        lines.append(f"It is {word}: spectral centroid {a['centroid_hz']:.0f} vs {b['centroid_hz']:.0f} Hz.")
    for band in BANDS:
        d = diffs[f"band_{band}"]
        if abs(d) >= 0.1:
            lines.append(f"It has {'more' if d > 0 else 'less'} {band} energy ({a['band_share'][band]:.0%} vs {b['band_share'][band]:.0%}).")
    ea, eb = onsets(cand, sr_c), onsets(ref, sr_r)
    if len(ea) != len(eb):
        lines.append(f"It has {len(ea)} sound events; the reference has {len(eb)}.")
    return {"candidate": a, "reference": b, "diffs": diffs, "events": {"candidate": ea, "reference": eb}, "lines": lines}


def picture(audio: np.ndarray, sr: int, png_path, title: str = "") -> None:
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt

    audio = np.asarray(audio, dtype=np.float64)
    mono = audio.mean(axis=1) if audio.ndim == 2 else audio
    t = np.arange(len(mono)) / sr
    m = measure(audio, sr)
    fig, ax = plt.subplots(4, 1, figsize=(12, 11), constrained_layout=True)
    fig.suptitle(title)
    ax[0].plot(t, mono, lw=0.5)
    ax[0].set_title("waveform")
    ax[0].set_xlim(0, t[-1] if len(t) else 1)
    f, tt, s = signal.spectrogram(mono, sr, nperseg=1024, noverlap=768)
    ax[1].pcolormesh(tt, np.maximum(f, 20), 10 * np.log10(s + EPS), shading="auto", cmap="magma")
    ax[1].set_yscale("log")
    ax[1].set_ylim(20, sr / 2)
    ax[1].set_title("spectrogram (log frequency)")
    level = _level(mono, sr, 5.0)
    ax[2].plot(t, 20 * np.log10(level / (level.max() + EPS) + EPS), lw=0.8)
    ax[2].set_ylim(-80, 3)
    ax[2].grid(True, alpha=0.4)
    ax[2].set_title("level (dB below peak)")
    ax[3].bar(list(BANDS), [m["band_share"][k] for k in BANDS])
    ax[3].set_title("energy share by band")
    fig.savefig(str(png_path), dpi=110)
    plt.close(fig)


def _jsonable(x):
    if isinstance(x, float) and not math.isfinite(x):
        return str(x)
    if isinstance(x, dict):
        return {k: _jsonable(v) for k, v in x.items()}
    if isinstance(x, list):
        return [_jsonable(v) for v in x]
    return x


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    sub = ap.add_subparsers(dest="cmd", required=True)
    m = sub.add_parser("measure")
    m.add_argument("file")
    m.add_argument("--png")
    c = sub.add_parser("compare")
    c.add_argument("candidate")
    c.add_argument("reference")
    c.add_argument("--png", help="writes <png>-candidate.png and <png>-reference.png")
    e = sub.add_parser("events")
    e.add_argument("file")
    a = ap.parse_args(argv)
    if a.cmd == "measure":
        audio, sr = load(a.file)
        result = measure(audio, sr)
        if a.png:
            picture(audio, sr, a.png, Path(a.file).name)
    elif a.cmd == "compare":
        ca, csr = load(a.candidate)
        ra, rsr = load(a.reference)
        result = compare(ca, ra, csr, rsr)
        if a.png:
            stem = Path(a.png)
            picture(ca, csr, stem.with_name(stem.stem + "-candidate.png"), "candidate: " + Path(a.candidate).name)
            picture(ra, rsr, stem.with_name(stem.stem + "-reference.png"), "reference: " + Path(a.reference).name)
    else:
        audio, sr = load(a.file)
        result = {"onsets_s": onsets(audio, sr)}
    print(json.dumps(_jsonable(result), indent=2))
    return 0


if __name__ == "__main__":
    sys.exit(main())
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `cd C:\Users\User\.claude\skills\ears; uv run --group dev pytest -q`
Expected: `9 passed`. If the tail test misses by more than 15%, check `_level`'s smoothing first (a 1 ms window on
noise is ragged); don't widen the tolerance past 20%.

- [ ] **Step 6: Write SKILL.md**

`C:\Users\User\.claude\skills\ears\SKILL.md`:
```markdown
---
name: ears
description: Judge sound effects and audio clips without hearing them - loudness, peaks, attack, tail, brightness, band
  energy, stereo, sound-event timing, spectrogram pictures, and comparison against a reference clip. Use whenever a
  sound must be checked or compared ("does this gunshot match the reference", "is the reload in sync", "is there audio
  in this recording", "which take is punchier"), including audio inside mp4 recordings from Unity.
---

# Ears

You can't hear. This skill turns a clip into numbers and pictures you can reason about.

## Run it

    uv run --project ~/.claude/skills/ears python ~/.claude/skills/ears/listen.py measure clip.wav --png clip.png
    uv run --project ~/.claude/skills/ears python ~/.claude/skills/ears/listen.py compare take.wav reference.wav --png cmp.png
    uv run --project ~/.claude/skills/ears python ~/.claude/skills/ears/listen.py events reload.wav

Video files (mp4, mkv, webm, mov) are read through ffmpeg. Read the PNGs with the Read tool.

## Reporting rules

- Every claim cites a number from the JSON or something visible in a picture. No adjectives without a measurement.
- Measurements overrule descriptions (yours or anyone's).
- Comparing against a reference is the main tool; a lone clip can only be checked for faults (clipping, DC offset,
  silence, a missing low end, a tail that rings on).
- For multi-part sounds (reloads, bursts) compare `events` timing first, then each segment.
- Say what you can't tell: "feel" and taste belong to the creator's ears, which always have the final word.
- `lufs` is null for clips under 0.4 s or silent clips; that's expected, not an error.

## Not built yet

Layers 3-4 (local classifiers, and a Gemini critique that only counts when both agree) wait until the creator has set up
Gemini billing (VibeJamKit docs/SKYLINE-DESIGN.md 9.5).
```

- [ ] **Step 7: No git commit** (the skill lives outside any repo). Note its paths in the hand-back.

---

### Task 7: The Unity lock helper and the Skyline Unity scaffolding (scenes, test tone, the editor batch job)

**Files:**
- Create: `tools/skyline/unity-lock.ps1`
- Create: `Assets/JamKit/Templates/SkylineRepo/Scripts/JamKit.SkylineRepo.asmdef`
- Create: `Assets/JamKit/Templates/SkylineRepo/Scripts/SkylineTestTone.cs`
- Create: `Assets/JamKit/Templates/SkylineRepo/Editor/JamKit.SkylineRepo.Editor.asmdef`
- Create: `Assets/JamKit/Templates/SkylineRepo/Editor/SkylineScenes.cs`
- Create: `Assets/JamKit/Templates/SkylineRepo/Editor/SkylinePassBatch.cs`
- Modify: `.gitignore` (append three lines)

**Interfaces:**
- Produces: PowerShell `Enter-UnityLock -Who <string> [-TimeoutMinutes 180] [-Dir <path>]` (throws on timeout),
  `Exit-UnityLock`, `Test-UnityLockSelf`; C# `JamKit.SkylineRepo.EditorTools.SkylineScenes.BuildAll() : string[]`,
  `SkylineScenes.FiringRange`, `SkylineScenes.Gallery` (scene path constants);
  `SkylinePassBatch.RunFromCommandLine()` (args `-skylineOut <dir>`; writes `<dir>/editor-batch.json`; exit 0 only if
  every test passed and both scenes were built), `SkylinePassBatch.Arg(string) : string`,
  `SkylinePassBatch.Quote(string) : string` (JSON string literal).

- [ ] **Step 1: Write the lock helper with its self-test**

`tools/skyline/unity-lock.ps1`:
```powershell
# The shared one-Unity-job lock (docs/UNITY-LOCK.md), for the Skyline Repo runner.
#   . tools/skyline/unity-lock.ps1; Enter-UnityLock -Who 'skyline-repo'; try { ... } finally { Exit-UnityLock }
#   powershell -File tools/skyline/unity-lock.ps1 -LockSelfTest   (a distinct name: pass.ps1 dot-sources this file and has its own -SelfTest)
param([switch]$LockSelfTest)

$script:LockDir = 'C:\Users\User\Desktop\yougotgamesonyophone\.unity-lock'

function Enter-UnityLock {
    param([string]$Who = 'skyline-repo', [int]$TimeoutMinutes = 180, [string]$Dir = $script:LockDir)
    $deadline = (Get-Date).AddMinutes($TimeoutMinutes)
    while ($true) {
        # cmd's mkdir fails atomically when the folder exists (CreateDirectoryW), unlike New-Item -Force
        cmd /c mkdir "`"$Dir`"" 2>$null | Out-Null
        if ($LASTEXITCODE -eq 0) {
            Set-Content -Path (Join-Path $Dir 'owner.txt') -Value "$PID $Who $(Get-Date -Format o)" -Encoding ascii
            $script:HeldLock = $Dir
            return
        }
        $owner = Join-Path $Dir 'owner.txt'
        $ownerPid = 0
        if (Test-Path $owner) { $ownerPid = [int](((Get-Content $owner -Raw) -split ' ')[0]) }
        if ($ownerPid -gt 0 -and -not (Get-Process -Id $ownerPid -ErrorAction SilentlyContinue)) {
            Write-Host "Unity lock: owner $ownerPid is gone; taking over the stale lock."
            Remove-Item -Recurse -Force $Dir
            continue
        }
        if ((Get-Date) -gt $deadline) { throw "Unity lock: still held by '$((Get-Content $owner -Raw).Trim())' after $TimeoutMinutes min" }
        Start-Sleep -Seconds 30
    }
}

function Exit-UnityLock {
    if ($script:HeldLock -and (Test-Path $script:HeldLock)) {
        $owner = Join-Path $script:HeldLock 'owner.txt'
        if ((Test-Path $owner) -and (((Get-Content $owner -Raw) -split ' ')[0] -eq "$PID")) { Remove-Item -Recurse -Force $script:HeldLock }
    }
    $script:HeldLock = $null
}

function Test-UnityLockSelf {
    $dir = Join-Path ([IO.Path]::GetTempPath()) "skyline-lock-selftest-$PID"
    if (Test-Path $dir) { Remove-Item -Recurse -Force $dir }
    # 1. take and release
    Enter-UnityLock -Who 'selftest' -Dir $dir
    if (-not (Test-Path (Join-Path $dir 'owner.txt'))) { throw 'selftest: owner.txt missing' }
    Exit-UnityLock
    if (Test-Path $dir) { throw 'selftest: lock not released' }
    # 2. a stale lock (a pid that doesn't exist) is taken over
    New-Item -ItemType Directory $dir | Out-Null
    Set-Content (Join-Path $dir 'owner.txt') '999999 ghost 2026-01-01' -Encoding ascii
    Enter-UnityLock -Who 'selftest' -Dir $dir -TimeoutMinutes 1
    if (((Get-Content (Join-Path $dir 'owner.txt') -Raw) -split ' ')[0] -ne "$PID") { throw 'selftest: stale lock not taken over' }
    Exit-UnityLock
    # 3. a live owner (our parent process) is not taken over: with a zero timeout it throws instead
    New-Item -ItemType Directory $dir | Out-Null
    $parent = (Get-CimInstance Win32_Process -Filter "ProcessId=$PID").ParentProcessId
    Set-Content (Join-Path $dir 'owner.txt') "$parent livetest 2026-01-01" -Encoding ascii
    $threw = $false
    try { Enter-UnityLock -Who 'selftest' -Dir $dir -TimeoutMinutes 0 } catch { $threw = $true }
    Remove-Item -Recurse -Force $dir
    if (-not $threw) { throw 'selftest: took a lock held by a live process' }
    Write-Host 'unity-lock self-test passed.'
}

if ($LockSelfTest) { Test-UnityLockSelf }
```

Note for case 3: with `-TimeoutMinutes 0` the deadline is "now", so the first failed attempt throws without sleeping.

- [ ] **Step 2: Run the lock self-test**

Run: `powershell -NoProfile -File tools/skyline/unity-lock.ps1 -LockSelfTest`
Expected: `unity-lock self-test passed.` (It uses a temp folder, never the real lock.)

- [ ] **Step 3: Write the Unity scaffolding**

`Assets/JamKit/Templates/SkylineRepo/Scripts/JamKit.SkylineRepo.asmdef`:
```json
{
    "name": "JamKit.SkylineRepo",
    "rootNamespace": "JamKit.SkylineRepo",
    "references": [
        "JamKit.Skyline",
        "JamKit.Core"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

`Assets/JamKit/Templates/SkylineRepo/Scripts/SkylineTestTone.cs`:
```csharp
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>The pipeline's test tone: a 440 Hz beep for 0.5 s every 2 s, so SkylinePass can prove the recordings
    /// carry audio (docs/SKYLINE-PLAN-M0.md, Task 9 stage E). Not for shipping scenes.</summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class SkylineTestTone : MonoBehaviour
    {
        public float frequency = 440f;
        public float beepSeconds = 0.5f;
        public float everySeconds = 2f;

        AudioSource _source;
        float _next;

        void Awake()
        {
            int sr = AudioSettings.outputSampleRate;
            int n = Mathf.CeilToInt(beepSeconds * sr);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                float fade = Mathf.Min(1f, t / 0.005f) * Mathf.Min(1f, (beepSeconds - t) / 0.02f);
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * 0.5f * fade;
            }
            var clip = AudioClip.Create("SkylineTestTone", n, 1, sr, false);
            clip.SetData(data, 0);
            _source = GetComponent<AudioSource>();
            _source.clip = clip;
            _source.spatialBlend = 0f;
            _source.playOnAwake = false;
        }

        void Update()
        {
            if (Time.time < _next) return;
            _next = Time.time + everySeconds;
            _source.Play();
        }
    }
}
```

`Assets/JamKit/Templates/SkylineRepo/Editor/JamKit.SkylineRepo.Editor.asmdef`:
```json
{
    "name": "JamKit.SkylineRepo.Editor",
    "rootNamespace": "JamKit.SkylineRepo.EditorTools",
    "references": [
        "JamKit.SkylineRepo",
        "JamKit.Skyline",
        "JamKit.Core",
        "UnityEditor.TestRunner",
        "UnityEngine.TestRunner"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

`Assets/JamKit/Templates/SkylineRepo/Editor/SkylineScenes.cs`:
```csharp
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JamKit.SkylineRepo.EditorTools
{
    /// <summary>Builds the pipeline's scenes from code each pass (they're generated, not committed): the firing range
    /// and the enemy gallery. In M0 both are empty-but-working: a floor, a light, a camera with an audio listener, and
    /// the test tone in the firing range.</summary>
    public static class SkylineScenes
    {
        public const string Folder = "Assets/JamKit/Templates/SkylineRepo/Generated";
        public const string FiringRange = Folder + "/SkylineFiringRange.unity";
        public const string Gallery = Folder + "/SkylineGallery.unity";

        public static string[] BuildAll()
        {
            Directory.CreateDirectory(Folder);
            Build(FiringRange, withTone: true);
            Build(Gallery, withTone: false);
            AssetDatabase.Refresh();
            return new[] { FiringRange, Gallery };
        }

        static void Build(string path, bool withTone)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(4f, 1f, 4f);

            var cam = new GameObject("Camera");
            cam.tag = "MainCamera";
            cam.AddComponent<Camera>();
            cam.AddComponent<AudioListener>();
            cam.transform.SetPositionAndRotation(new Vector3(0f, 1.6f, -8f), Quaternion.Euler(8f, 0f, 0f));

            if (withTone)
            {
                var tone = new GameObject("TestTone");
                tone.AddComponent<AudioSource>();
                tone.AddComponent<JamKit.SkylineRepo.SkylineTestTone>();
            }

            EditorSceneManager.SaveScene(scene, path);
        }
    }
}
```

`Assets/JamKit/Templates/SkylineRepo/Editor/SkylinePassBatch.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace JamKit.SkylineRepo.EditorTools
{
    /// <summary>SkylinePass stage A (docs/SKYLINE-DESIGN.md 9.4): one batch-mode editor job that runs Skyline's EditMode
    /// tests synchronously and builds the pipeline's scenes, then writes editor-batch.json and exits.
    /// <c>Unity.exe -batchmode -projectPath . -executeMethod JamKit.SkylineRepo.EditorTools.SkylinePassBatch.RunFromCommandLine -skylineOut out/skyline/&lt;stamp&gt;</c></summary>
    public static class SkylinePassBatch
    {
        static readonly string[] TestAssemblies = { "JamKit.Skyline.Tests" };

        sealed class Collector : ICallbacks
        {
            public int passed, failed, skipped;
            public readonly List<string> failures = new List<string>();
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void RunFinished(ITestResultAdaptor result) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.HasChildren) return;
                switch (result.TestStatus)
                {
                    case TestStatus.Passed: passed++; break;
                    case TestStatus.Failed: failed++; failures.Add(result.FullName + ": " + result.Message); break;
                    default: skipped++; break;
                }
            }
        }

        public static void RunFromCommandLine()
        {
            string outDir = Arg("-skylineOut") ?? Path.Combine("out", "skyline", "manual");
            Directory.CreateDirectory(outDir);
            var c = new Collector();
            string error = null;
            string[] scenes = Array.Empty<string>();
            try
            {
                var api = ScriptableObject.CreateInstance<TestRunnerApi>();
                api.RegisterCallbacks(c);
                var filter = new Filter { testMode = TestMode.EditMode, assemblyNames = TestAssemblies };
                api.Execute(new ExecutionSettings(filter) { runSynchronously = true });
                scenes = SkylineScenes.BuildAll();
            }
            catch (Exception e)
            {
                error = e.ToString();
            }

            bool ok = error == null && c.failed == 0 && c.passed > 0 && scenes.Length == 2;
            var sb = new StringBuilder();
            sb.Append("{\"ok\":").Append(ok ? "true" : "false");
            sb.Append(",\"tests\":{\"passed\":").Append(c.passed).Append(",\"failed\":").Append(c.failed).Append(",\"skipped\":").Append(c.skipped);
            sb.Append(",\"failures\":[");
            for (int i = 0; i < c.failures.Count; i++) sb.Append(i > 0 ? "," : "").Append(Quote(c.failures[i]));
            sb.Append("]},\"scenes\":[");
            for (int i = 0; i < scenes.Length; i++) sb.Append(i > 0 ? "," : "").Append(Quote(scenes[i]));
            sb.Append("],\"error\":").Append(error == null ? "null" : Quote(error)).Append('}');
            File.WriteAllText(Path.Combine(outDir, "editor-batch.json"), sb.ToString());
            Debug.Log("SKYLINE_BATCH " + (ok ? "OK" : "FAILED") + " passed=" + c.passed + " failed=" + c.failed);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>The value after <paramref name="name"/> on the command line, or null. Used by SkylineBuild and SkylineCapture too.</summary>
        public static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return null;
        }

        /// <summary>A JSON string literal (the editor has no JSON writer for loose values).</summary>
        public static string Quote(string s) =>
            "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "") + "\"";
    }
}
```

Append to `.gitignore`:
```
# Skyline Repo: scenes the pipeline generates every pass (docs/SKYLINE-PLAN-M0.md Task 7)
/Assets/JamKit/Templates/SkylineRepo/Generated/
/Assets/JamKit/Templates/SkylineRepo/Generated.meta
```

- [ ] **Step 4: Verify what can be verified without Unity**

Run: `powershell -NoProfile -File tools/skyline/test.ps1` (still green) and the lock self-test (Step 2).
The Unity C# is verified by the main session's first pass (Task 10); subagents don't launch Unity.

- [ ] **Step 5: Commit**

```bash
git add -- tools/skyline/unity-lock.ps1 Assets/JamKit/Templates/SkylineRepo/Scripts/JamKit.SkylineRepo.asmdef Assets/JamKit/Templates/SkylineRepo/Scripts/SkylineTestTone.cs Assets/JamKit/Templates/SkylineRepo/Editor/JamKit.SkylineRepo.Editor.asmdef Assets/JamKit/Templates/SkylineRepo/Editor/SkylineScenes.cs Assets/JamKit/Templates/SkylineRepo/Editor/SkylinePassBatch.cs .gitignore
git commit -m "Skyline: the Unity lock helper, the pipeline scenes with a test tone, and the editor batch job (spec 9.4)"
```

---

### Task 8: Frame stats, the frame-time probe, the Skyline build, and the recording job

**Files:**
- Create: `Packages/com.vibejam.jamkit/Runtime/Skyline/FrameStats.cs`
- Create: `Packages/com.vibejam.jamkit/Tests/Skyline/FrameStatsTests.cs`
- Create: `Assets/JamKit/Templates/SkylineRepo/Scripts/SkylinePerfProbe.cs`
- Create: `Assets/JamKit/Templates/SkylineRepo/Editor/SkylineBuild.cs`
- Create: `Assets/JamKit/Templates/SkylineRepo/Editor/Recording/JamKit.SkylineRepo.Recording.asmdef`
- Create: `Assets/JamKit/Templates/SkylineRepo/Editor/Recording/SkylineCapture.cs`

**Interfaces:**
- Consumes: `SkylineScenes.BuildAll()`, `.FiringRange`, `.Gallery`, `SkylinePassBatch.Arg(string)`,
  `SkylinePassBatch.Quote(string)` (Task 7).
- Produces: `FrameStats.Summarize(IReadOnlyList<float> frameSeconds) : FrameSummary` (`frames`, `avgMs`,
  `onePercentLowMs` = mean of the slowest 1% of frames, at least one frame, `worstMs`);
  `SkylineBuild.BuildFromCommandLine()` (args `-skylineOut <dir>` → `<dir>/build/SkylineRepo.exe`, `<dir>/build.json`);
  `SkylinePerfProbe` (player args `-skyline-perf <dir>` → `<dir>/perf.json`, then quits);
  `SkylineCapture.RunFromCommandLine()` (graphics editor, args `-skylineOut <dir>` → `<dir>/rec-<scene>.mp4`,
  `<dir>/still-<scene>.png`, `<dir>/capture.json`).

- [ ] **Step 1: Write the failing frame-stats tests**

`Packages/com.vibejam.jamkit/Tests/Skyline/FrameStatsTests.cs`:
```csharp
using System.Collections.Generic;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    public class FrameStatsTests
    {
        [Test]
        public void OnePercentLowIsTheMeanOfTheSlowestOnePercent()
        {
            var frames = new List<float>();
            for (int i = 0; i < 990; i++) frames.Add(0.008f);
            for (int i = 0; i < 10; i++) frames.Add(0.020f);
            var s = FrameStats.Summarize(frames);
            Assert.That(s.frames, Is.EqualTo(1000));
            Assert.That(s.onePercentLowMs, Is.EqualTo(20f).Within(1e-3f));
            Assert.That(s.worstMs, Is.EqualTo(20f).Within(1e-3f));
            Assert.That(s.avgMs, Is.EqualTo(8.12f).Within(1e-3f));
        }

        [Test]
        public void FewFramesStillGiveOneSlowestFrame()
        {
            var s = FrameStats.Summarize(new List<float> { 0.010f, 0.030f, 0.010f });
            Assert.That(s.onePercentLowMs, Is.EqualTo(30f).Within(1e-3f));
        }

        [Test]
        public void NoFramesGiveZeros()
        {
            var s = FrameStats.Summarize(new List<float>());
            Assert.That(s.frames, Is.EqualTo(0));
            Assert.That(s.avgMs, Is.EqualTo(0f));
        }
    }
}
```

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test core-tests/Skyline/Skyline.Tests.csproj --nologo -v q`
Expected: build FAILS with `CS0103: The name 'FrameStats' does not exist in the current context`.

- [ ] **Step 3: Implement FrameStats and run the tests**

`Packages/com.vibejam.jamkit/Runtime/Skyline/FrameStats.cs`:
```csharp
using System;
using System.Collections.Generic;

namespace JamKit.Skyline
{
    public struct FrameSummary
    {
        public int frames;
        public float avgMs, onePercentLowMs, worstMs;
    }

    /// <summary>Frame-time numbers for the performance bar (docs/SKYLINE-DESIGN.md 10, item 12): the 1% low is the mean
    /// of the slowest 1% of frames (at least one frame), in milliseconds. Blocker: over 16.7 ms in a build.</summary>
    public static class FrameStats
    {
        public static FrameSummary Summarize(IReadOnlyList<float> frameSeconds)
        {
            int n = frameSeconds.Count;
            if (n == 0) return default;
            var sorted = new float[n];
            double sum = 0;
            for (int i = 0; i < n; i++) { sorted[i] = frameSeconds[i]; sum += frameSeconds[i]; }
            Array.Sort(sorted);
            int slow = Math.Max(1, n / 100);
            double slowSum = 0;
            for (int i = n - slow; i < n; i++) slowSum += sorted[i];
            return new FrameSummary
            {
                frames = n,
                avgMs = (float)(sum / n * 1000.0),
                onePercentLowMs = (float)(slowSum / slow * 1000.0),
                worstMs = sorted[n - 1] * 1000f,
            };
        }
    }
}
```

Run: `powershell -NoProfile -File tools/skyline/test.ps1`
Expected: `Skyline and Movement tests passed.`

- [ ] **Step 4: Write the perf probe, the build and the recording job**

`Assets/JamKit/Templates/SkylineRepo/Scripts/SkylinePerfProbe.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using JamKit.Skyline;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>SkylinePass stage D: run the built player with <c>-skyline-perf &lt;dir&gt;</c>; after a 3 s warm-up it
    /// records 20 s of frame times in the first scene, writes perf.json and quits.</summary>
    public sealed class SkylinePerfProbe : MonoBehaviour
    {
        const float Warmup = 3f, Measure = 20f;
        readonly List<float> _frames = new List<float>(4096);
        string _outDir;
        float _start;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            string dir = null;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-skyline-perf") dir = args[i + 1];
            if (dir == null) return;
            var go = new GameObject("SkylinePerfProbe");
            DontDestroyOnLoad(go);
            var probe = go.AddComponent<SkylinePerfProbe>();
            probe._outDir = dir;
            probe._start = Time.realtimeSinceStartup;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
        }

        static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        void Update()
        {
            float t = Time.realtimeSinceStartup - _start;
            if (t < Warmup) return;
            _frames.Add(Time.unscaledDeltaTime);
            if (t < Warmup + Measure) return;
            var s = FrameStats.Summarize(_frames);
            Directory.CreateDirectory(_outDir);
            File.WriteAllText(Path.Combine(_outDir, "perf.json"),
                "{\"frames\":" + s.frames + ",\"avgMs\":" + F(s.avgMs) + ",\"onePercentLowMs\":" + F(s.onePercentLowMs) +
                ",\"worstMs\":" + F(s.worstMs) + ",\"resolution\":\"" + Screen.width + "x" + Screen.height + "\"}");
            enabled = false;
            Application.Quit(0);
        }
    }
}
```

`Assets/JamKit/Templates/SkylineRepo/Editor/SkylineBuild.cs`:
```csharp
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace JamKit.SkylineRepo.EditorTools
{
    /// <summary>SkylinePass stage C: a Windows build of Skyline's own scenes only (the shared build settings are not
    /// touched). <c>-executeMethod JamKit.SkylineRepo.EditorTools.SkylineBuild.BuildFromCommandLine -skylineOut &lt;dir&gt;</c></summary>
    public static class SkylineBuild
    {
        public static void BuildFromCommandLine()
        {
            string outDir = SkylinePassBatch.Arg("-skylineOut") ?? Path.Combine("out", "skyline", "manual");
            string exe = Path.GetFullPath(Path.Combine(outDir, "build", "SkylineRepo.exe"));
            if (!File.Exists(SkylineScenes.FiringRange) || !File.Exists(SkylineScenes.Gallery)) SkylineScenes.BuildAll();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { SkylineScenes.FiringRange, SkylineScenes.Gallery },
                locationPathName = exe,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            bool ok = report.summary.result == BuildResult.Succeeded;
            File.WriteAllText(Path.Combine(outDir, "build.json"),
                "{\"ok\":" + (ok ? "true" : "false") + ",\"exe\":" + SkylinePassBatch.Quote(exe) +
                ",\"seconds\":" + (int)report.summary.totalTime.TotalSeconds + ",\"errors\":" + report.summary.totalErrors + "}");
            Debug.Log("SKYLINE_BUILD " + (ok ? "OK" : "FAILED"));
            EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
```

`Assets/JamKit/Templates/SkylineRepo/Editor/Recording/JamKit.SkylineRepo.Recording.asmdef` (mirrors the package's
`JamKit.Recording.Editor`, so it only compiles when Unity Recorder 5+ is installed):
```json
{
    "name": "JamKit.SkylineRepo.Recording",
    "rootNamespace": "JamKit.SkylineRepo.EditorTools.Recording",
    "references": [
        "JamKit.SkylineRepo",
        "JamKit.SkylineRepo.Editor",
        "Unity.Recorder",
        "Unity.Recorder.Base",
        "Unity.Recorder.Editor"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [
        "JAMKIT_RECORDER"
    ],
    "versionDefines": [
        {
            "name": "com.unity.recorder",
            "expression": "5.0.0",
            "define": "JAMKIT_RECORDER"
        }
    ],
    "noEngineReferences": false
}
```

`Assets/JamKit/Templates/SkylineRepo/Editor/Recording/SkylineCapture.cs`. Entering Play mode reloads the domain in this
project (`EnterPlayModeOptions` is 0), which wipes static fields, so the queue lives in `SessionState`, like
`Packages/com.vibejam.jamkit/Editor/Recording/JamKitRecorder.cs`:
```csharp
using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JamKit.SkylineRepo.EditorTools.Recording
{
    /// <summary>SkylinePass stage B (docs/SKYLINE-DESIGN.md 9.4): a graphics editor session (not batch mode, so audio
    /// plays) that records each pipeline scene to MP4 with audio and takes a still, then exits.
    /// <c>Unity.exe -projectPath . -executeMethod JamKit.SkylineRepo.EditorTools.Recording.SkylineCapture.RunFromCommandLine -skylineOut &lt;dir&gt;</c></summary>
    [InitializeOnLoad]
    public static class SkylineCapture
    {
        const string KeyIndex = "SkylineCapture.Index", KeyOut = "SkylineCapture.Out", KeyActive = "SkylineCapture.Active",
                     KeyResults = "SkylineCapture.Results", KeyStarted = "SkylineCapture.Started";
        const float RecordSeconds = 8f, StillAt = 2f, Settle = 1f, Deadline = 300f;
        static readonly string[] Scenes = { SkylineScenes.FiringRange, SkylineScenes.Gallery };

        static RecorderController _controller;
        static double _phaseStart = -1;
        static bool _stillTaken;

        static SkylineCapture()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        public static void RunFromCommandLine()
        {
            string outDir = Path.GetFullPath(SkylinePassBatch.Arg("-skylineOut") ?? Path.Combine("out", "skyline", "manual"));
            Directory.CreateDirectory(outDir);
            SkylineScenes.BuildAll();
            SessionState.SetString(KeyOut, outDir);
            SessionState.SetInt(KeyIndex, 0);
            SessionState.SetString(KeyResults, "");
            SessionState.SetFloat(KeyStarted, (float)EditorApplication.timeSinceStartup);
            SessionState.SetBool(KeyActive, true);
            OpenAndPlay(0);
        }

        static void OpenAndPlay(int index)
        {
            EditorSceneManager.OpenScene(Scenes[index], OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        static void Tick()
        {
            if (!SessionState.GetBool(KeyActive, false)) return;
            double now = EditorApplication.timeSinceStartup;
            if (now - SessionState.GetFloat(KeyStarted, (float)now) > Deadline) { Finish(false, "deadline"); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
            if (_phaseStart < 0) _phaseStart = now;           // first tick in Play mode (statics reset by the reload)
            if (_controller == null)
            {
                if (now - _phaseStart < Settle) return;
                StartRecording();
                return;
            }
            double rec = now - _phaseStart;
            int index = SessionState.GetInt(KeyIndex, 0);
            string name = Path.GetFileNameWithoutExtension(Scenes[index]);
            string dir = SessionState.GetString(KeyOut, "");
            if (!_stillTaken && rec >= StillAt)
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, "still-" + name + ".png"));
                _stillTaken = true;
            }
            if (rec < RecordSeconds) return;
            _controller.StopRecording();
            _controller = null;
            _phaseStart = -1;
            SessionState.SetString(KeyResults, SessionState.GetString(KeyResults, "") + name + ";");
            index++;
            SessionState.SetInt(KeyIndex, index);
            EditorApplication.isPlaying = false;
            if (index >= Scenes.Length) EditorApplication.delayCall += () => Finish(true, null);
            else EditorApplication.delayCall += () => OpenAndPlay(index);
        }

        static void StartRecording()
        {
            int index = SessionState.GetInt(KeyIndex, 0);
            string name = Path.GetFileNameWithoutExtension(Scenes[index]);
            string dir = SessionState.GetString(KeyOut, "");
            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name = "skyline-" + name;
            movie.Enabled = true;
            movie.EncoderSettings = new CoreEncoderSettings
            {
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
                Codec = CoreEncoderSettings.OutputCodec.MP4,
            };
            movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = 1920, OutputHeight = 1080 };
            movie.AudioInputSettings.PreserveAudio = true;
            movie.OutputFile = Path.Combine(dir, "rec-" + name); // the Recorder adds .mp4
            settings.AddRecorderSettings(movie);
            settings.SetRecordModeToManual();
            settings.FrameRate = 60f;
            settings.CapFrameRate = true;
            _controller = new RecorderController(settings);
            _controller.PrepareRecording();
            _controller.StartRecording();
            _phaseStart = EditorApplication.timeSinceStartup;
            _stillTaken = false;
        }

        static void Finish(bool ok, string error)
        {
            string dir = SessionState.GetString(KeyOut, "");
            SessionState.SetBool(KeyActive, false);
            if (!string.IsNullOrEmpty(dir))
                File.WriteAllText(Path.Combine(dir, "capture.json"),
                    "{\"ok\":" + (ok ? "true" : "false") + ",\"recorded\":" + SkylinePassBatch.Quote(SessionState.GetString(KeyResults, "")) +
                    ",\"error\":" + (error == null ? "null" : SkylinePassBatch.Quote(error)) + "}");
            Debug.Log("SKYLINE_CAPTURE " + (ok ? "OK" : "FAILED " + error));
            EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
```

- [ ] **Step 5: Commit** (Unity verifies the Unity-side files in Task 10)

```bash
git add -- Packages/com.vibejam.jamkit/Runtime/Skyline/FrameStats.cs Packages/com.vibejam.jamkit/Tests/Skyline/FrameStatsTests.cs Assets/JamKit/Templates/SkylineRepo/Scripts/SkylinePerfProbe.cs Assets/JamKit/Templates/SkylineRepo/Editor/SkylineBuild.cs Assets/JamKit/Templates/SkylineRepo/Editor/Recording/JamKit.SkylineRepo.Recording.asmdef Assets/JamKit/Templates/SkylineRepo/Editor/Recording/SkylineCapture.cs
git commit -m "Skyline: frame stats, the frame-time probe, the Skyline-only build, and the recording job with audio (spec 9.4)"
```

---

### Task 9: The SkylinePass runner

**Files:**
- Create: `tools/skyline/pass.ps1`

**Interfaces:**
- Consumes: `tools/unity-env.ps1` (`$RepoRoot`, `Get-UnityEditorPath`, `Assert-ProjectUnlocked`),
  `tools/skyline/unity-lock.ps1` (`Enter-UnityLock`, `Exit-UnityLock`), the four Unity entry points from Tasks 7-8,
  the ears (`listen.py measure`, Task 6).
- Produces: `out/skyline/<yyyyMMdd-HHmm>/manifest.json` (`ok`, `out`, `stages[]` each `{name, ok, seconds, log,
  note}`); exit 0 only when every stage passed. `-SelfTest` runs fake stages (no Unity) to prove a hung stage is
  killed and its lock released.

- [ ] **Step 1: Write the runner**

`tools/skyline/pass.ps1`:
```powershell
# SkylinePass (docs/SKYLINE-DESIGN.md 9.4): four short Unity jobs, each holding the shared lock for one job, then an
# audio check without Unity. A failed stage doesn't stop the others. Main session only; run it through the memory cap:
#   uv run python ~/.local/bin/capped-run.py --session skyline-repo --retries 1 -- powershell -NoProfile -File tools/skyline/pass.ps1
#   powershell -NoProfile -File tools/skyline/pass.ps1 -SelfTest     (no Unity: proves a hung stage is killed and unlocked)
param([string]$Out = '', [switch]$SelfTest)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\..\unity-env.ps1"
. "$PSScriptRoot\unity-lock.ps1"

function Invoke-Stage {
    param([string]$Name, [string]$Exe, [string[]]$StageArgs, [int]$TimeoutSec, [string]$Log, [switch]$Locked)
    $started = Get-Date
    $ok = $false; $note = ''
    try {
        if ($Locked) { Enter-UnityLock -Who "skyline-repo:$Name" }
        $p = Start-Process -FilePath $Exe -ArgumentList $StageArgs -PassThru -WindowStyle Minimized
        $null = $p.Handle
        if (-not $p.WaitForExit($TimeoutSec * 1000)) {
            $note = "timed out after $TimeoutSec s; killed"
            & taskkill /PID $p.Id /T /F 2>$null | Out-Null
        } else {
            $ok = ($p.ExitCode -eq 0)
            if (-not $ok) { $note = "exit code $($p.ExitCode)" }
        }
    } catch {
        $note = $_.Exception.Message
    } finally {
        if ($Locked) { Exit-UnityLock }
    }
    [pscustomobject]@{ name = $Name; ok = $ok; seconds = [int]((Get-Date) - $started).TotalSeconds; log = $Log; note = $note }
}

if ($SelfTest) {
    $script:LockDir = Join-Path ([IO.Path]::GetTempPath()) "skyline-pass-selftest-$PID"
    $r = Invoke-Stage -Name 'hang' -Exe 'powershell' -StageArgs @('-NoProfile', '-Command', 'Start-Sleep 600') -TimeoutSec 5 -Log '' -Locked
    if ($r.ok -or $r.note -notlike 'timed out*') { throw "self-test: hung stage not reported ($($r | ConvertTo-Json -Compress))" }
    if (Test-Path $script:LockDir) { throw 'self-test: lock left behind after a timeout' }
    $r2 = Invoke-Stage -Name 'ok' -Exe 'powershell' -StageArgs @('-NoProfile', '-Command', 'exit 0') -TimeoutSec 30 -Log '' -Locked
    if (-not $r2.ok) { throw 'self-test: a passing stage was reported as failed' }
    $r3 = Invoke-Stage -Name 'fail' -Exe 'powershell' -StageArgs @('-NoProfile', '-Command', 'exit 3') -TimeoutSec 30 -Log '' -Locked
    if ($r3.ok -or $r3.note -ne 'exit code 3') { throw 'self-test: a failing stage was not reported with its exit code' }
    Write-Host 'pass.ps1 self-test passed.'
    exit 0
}

if (-not $Out) { $Out = Join-Path $RepoRoot ("out\skyline\" + (Get-Date -Format 'yyyyMMdd-HHmm')) }
New-Item -ItemType Directory -Force $Out | Out-Null
Assert-ProjectUnlocked
$unity = Get-UnityEditorPath
$proj = "`"$RepoRoot`""
$stages = @()

# A: editor batch job (tests, scenes)
$log = Join-Path $Out 'stage-a-editor-batch.log'
$stages += Invoke-Stage -Name 'A editor batch' -Exe $unity -Locked -TimeoutSec 1800 -Log $log -StageArgs @(
    '-batchmode', '-projectPath', $proj, '-logFile', "`"$log`"",
    '-executeMethod', 'JamKit.SkylineRepo.EditorTools.SkylinePassBatch.RunFromCommandLine', '-skylineOut', "`"$Out`"")

# B: graphics editor recording (audio plays only outside batch mode)
$log = Join-Path $Out 'stage-b-capture.log'
$stages += Invoke-Stage -Name 'B recording' -Exe $unity -Locked -TimeoutSec 900 -Log $log -StageArgs @(
    '-projectPath', $proj, '-logFile', "`"$log`"",
    '-executeMethod', 'JamKit.SkylineRepo.EditorTools.Recording.SkylineCapture.RunFromCommandLine', '-skylineOut', "`"$Out`"")

# C: Windows build of Skyline's scenes
$log = Join-Path $Out 'stage-c-build.log'
$stages += Invoke-Stage -Name 'C build' -Exe $unity -Locked -TimeoutSec 1800 -Log $log -StageArgs @(
    '-batchmode', '-quit', '-projectPath', $proj, '-logFile', "`"$log`"",
    '-executeMethod', 'JamKit.SkylineRepo.EditorTools.SkylineBuild.BuildFromCommandLine', '-skylineOut', "`"$Out`"")

# D: the built player's frame-time probe (windowed 1080p, not batch mode)
$exe = Join-Path $Out 'build\SkylineRepo.exe'
if (Test-Path $exe) {
    $log = Join-Path $Out 'stage-d-perf.log'
    $stages += Invoke-Stage -Name 'D perf' -Exe $exe -Locked -TimeoutSec 180 -Log $log -StageArgs @(
        '-screen-fullscreen', '0', '-screen-width', '1920', '-screen-height', '1080', '-logFile', "`"$log`"",
        '-skyline-perf', "`"$Out`"")
    $perf = Join-Path $Out 'perf.json'
    $last = $stages[-1]
    if (Test-Path $perf) {
        $p = Get-Content $perf -Raw | ConvertFrom-Json
        $last.note = "avg $($p.avgMs) ms, 1% low $($p.onePercentLowMs) ms (blocker over 16.7)"
        if ($p.onePercentLowMs -gt 16.7) { $last.ok = $false }
    } elseif ($last.ok) { $last.ok = $false; $last.note = 'no perf.json written' }
} else {
    $stages += [pscustomobject]@{ name = 'D perf'; ok = $false; seconds = 0; log = ''; note = 'skipped: no build' }
}

# E: audio check without Unity: the firing range recording must contain the 440 Hz test tone
$rec = Join-Path $Out 'rec-SkylineFiringRange.mp4'
$started = Get-Date
$eok = $false; $enote = ''
if (Test-Path $rec) {
    $ears = Join-Path ([Environment]::GetFolderPath('UserProfile')) '.claude\skills\ears'
    $json = & uv run --project $ears python (Join-Path $ears 'listen.py') measure $rec --png (Join-Path $Out 'audio-firing-range.png') 2>&1
    $jsonText = ($json | Out-String)
    Set-Content -Path (Join-Path $Out 'audio-firing-range.json') -Value $jsonText -Encoding utf8
    try {
        $m = $jsonText.Substring($jsonText.IndexOf('{')) | ConvertFrom-Json
        $eok = (-not $m.silent) -and ($m.centroid_hz -gt 300) -and ($m.centroid_hz -lt 700)
        $enote = "silent=$($m.silent) centroid=$([int]$m.centroid_hz) Hz peak=$($m.sample_peak_dbfs) dBFS"
    } catch { $enote = "could not read the ears' output: $($_.Exception.Message)" }
} else { $enote = 'skipped: no firing range recording' }
$stages += [pscustomobject]@{ name = 'E audio check'; ok = $eok; seconds = [int]((Get-Date) - $started).TotalSeconds; log = ''; note = $enote }

$allOk = -not ($stages | Where-Object { -not $_.ok })
[pscustomobject]@{ ok = $allOk; out = $Out; stages = $stages } | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $Out 'manifest.json') -Encoding utf8
$stages | Format-Table name, ok, seconds, note -AutoSize | Out-String | Write-Host
Write-Host "SkylinePass $(if ($allOk) { 'OK' } else { 'FAILED' }): $Out"
if ($allOk) { exit 0 } else { exit 1 }
```

- [ ] **Step 2: Run the self-test (no Unity)**

Run: `powershell -NoProfile -File tools/skyline/pass.ps1 -SelfTest`
Expected: `pass.ps1 self-test passed.` within about 15 s.

- [ ] **Step 3: Commit**

```bash
git add -- tools/skyline/pass.ps1
git commit -m "Skyline: the SkylinePass runner (four one-job lock holds, timeouts, the audio check, a manifest; spec 9.4)"
```

---

### Task 10 (main session only): The first pass, fixes, docs

Subagents stop at Task 9 and hand back. The main session:

- [ ] **Step 1:** Check no `Unity.exe` has the VibeJamKit project open, then run the pass in the background through the
  memory cap: `uv run python ~/.local/bin/capped-run.py --session skyline-repo --retries 1 -- powershell -NoProfile -File tools/skyline/pass.ps1`.
  It waits for the lock by itself.
- [ ] **Step 2:** Read `manifest.json`. Fix compile errors or stage failures (expected on a first pass: Unity API
  details in Tasks 7-8), re-run only after fixing, and keep each fix in its own commit.
- [ ] **Step 3:** When the pass is green, commit the `.meta` files Unity generated for the new Skyline folders and
  files (list them with `git status --short -- Packages/com.vibejam.jamkit/Runtime/Skyline
  Packages/com.vibejam.jamkit/Tests/Skyline Assets/JamKit/Templates/SkylineRepo`), staged by explicit path.
- [ ] **Step 4:** Write `docs/SKYLINE-PIPELINE.md` (how to run the tests, the pass, the forge and the ears; where
  outputs land; what each stage checks), add a `docs/SKYLINE-WORKLOG.md` entry with the pass's numbers (test counts,
  the dodge report line, the frame times, the audio check), and commit both.
- [ ] **Step 5:** Check M0's exit criteria (spec 11.1) one by one in the worklog entry, then write the M1 plan.
