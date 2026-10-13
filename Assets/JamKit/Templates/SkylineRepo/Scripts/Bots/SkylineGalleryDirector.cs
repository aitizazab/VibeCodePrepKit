using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using JamKit.Skyline;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// The enemy gallery's script (docs/SKYLINE-PLAN-M1.md Task 15, with the gondola as the fourth bot). The four bots are
    /// spawned at Start (each its own group, each with its own hidden <see cref="SkylineDummy"/>); then, in the order drone,
    /// floor-care, spotlight, gondola, the main camera cuts to that bot's close-up and its segment runs:
    /// idle 2 s; the dummy is revealed (alert, then two attack cycles; for the gondola: it rides its rail laying mines, and once
    /// a mine on the dummy's line is armed and the gondola is clear of it, the dummy wall-runs past the mine, which triggers,
    /// fuses and blasts); then an invisible shooter by the camera lands Writ hits: plate 0 and plate 1 until each sheds, the
    /// weak point (a spot cracks open), the spot (a break-through), then the chassis until it wrecks. Last, the wrecks are
    /// cleared and four fresh idle bots are put back for the stills (the stills render after the recording).
    /// <para>Logs: <c>SKYLINE_GALLERY &lt;bot&gt; state &lt;State&gt;</c>, <c>plate-shed &lt;plateId&gt;</c>,
    /// <c>spot created</c>, <c>spot hit</c>, <c>wrecked</c>, <c>gondola mine laid|armed|triggered|blast|expired</c>; also
    /// <c>SKYLINE_GALLERY &lt;bot&gt; telegraph t=&lt;s&gt;</c> and <c>attack t=&lt;s&gt;</c> (Time.time, for the telegraph-leads-attack
    /// check), <c>segment</c>, <c>bark</c>, and <c>SKYLINE_GALLERY stills-ready</c> / <c>done</c>. &lt;bot&gt; is the slot's
    /// label: drone, floor-care, spotlight, gondola.</para>
    /// </summary>
    public sealed class SkylineGalleryDirector : MonoBehaviour
    {
        [Serializable]
        public sealed class Slot
        {
            public string label = "drone";
            public string enemyId = SkylineEnemies.DroneId;
            [Tooltip("Position and facing (yaw) of the spawn.")]
            public Transform spawn;
            [Tooltip("The gondola's rail end (others: none).")]
            public Transform railEnd;
            public SkylineDummy dummy;
            public Transform cameraPose;
            public float seconds = 12f;
            [Tooltip("The gondola's dummy wall-runs from the first to the second.")]
            public Transform runFrom, runTo;
        }

        const float LeadIn = 1f, IdleSeconds = 2f, AttackWait = 7f, ShotGap = 0.4f, AfterWreck = 1.2f, StillsHold = 3f;
        const float RecorderWait = 8f, RunSpeed = 5f, MineWait = 9f;

        public Slot[] slots = new Slot[0];
        public Camera mainCamera;
        public Transform widePose;

        readonly Dictionary<SkylineBot, string> _labels = new Dictionary<SkylineBot, string>();
        readonly Dictionary<SkylineBot, int> _attacks = new Dictionary<SkylineBot, int>();
        SkylineBot[] _bots = new SkylineBot[0];
        GunDef _gun;
        int _spawns;

        void Start()
        {
            _gun = SkylineGuns.Writ();
            SkylineBot.SearchArea = new Rect(-60f, -60f, 120f, 120f);
            SkylineBot.StateEntered += OnState;
            SkylineBot.PlateShed += OnPlateShed;
            SkylineBot.SpotCreated += OnSpotCreated;
            SkylineBot.SpotHit += OnSpotHit;
            SkylineBot.Wrecked += OnWrecked;
            SkylineBot.Attacked += OnAttacked;
            SkylineBot.MineEvent += OnMine;
            SkylineBot.Barked += OnBark;
            foreach (var s in slots) if (s != null && s.dummy != null) s.dummy.gameObject.SetActive(false);
            SpawnAll();
            StartCoroutine(Run());
        }

        void OnDestroy()
        {
            SkylineBot.StateEntered -= OnState;
            SkylineBot.PlateShed -= OnPlateShed;
            SkylineBot.SpotCreated -= OnSpotCreated;
            SkylineBot.SpotHit -= OnSpotHit;
            SkylineBot.Wrecked -= OnWrecked;
            SkylineBot.Attacked -= OnAttacked;
            SkylineBot.MineEvent -= OnMine;
            SkylineBot.Barked -= OnBark;
        }

        void SpawnAll()
        {
            _bots = new SkylineBot[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                if (s == null || s.spawn == null) continue;
                Vector3 at = s.spawn.position;
                Vector3 end = s.railEnd != null ? s.railEnd.position : at;
                var bot = SkylineBot.Spawn(s.enemyId, at, s.spawn.eulerAngles.y, (ulong)(1000 + i + 17 * _spawns), s.dummy, new WritTuning(), transform, 100 + i, end);
                if (bot == null) continue;
                _bots[i] = bot;
                _labels[bot] = s.label;
                _attacks[bot] = 0;
            }
            _spawns++;
        }

        static void Log(string line) => Debug.Log("SKYLINE_GALLERY " + line);
        static string T() => Time.time.ToString("0.000", CultureInfo.InvariantCulture);

        void OnState(SkylineBot b, BotState s)
        {
            if (!_labels.TryGetValue(b, out var l)) return;
            Log(l + " state " + s);
            if (s == BotState.Telegraph) Log(l + " telegraph t=" + T());
        }

        void OnPlateShed(SkylineBot b, int plate)
        {
            if (_labels.TryGetValue(b, out var l)) Log(l + " plate-shed " + b.Def.plates[plate].id);
        }

        void OnSpotCreated(SkylineBot b) { if (_labels.TryGetValue(b, out var l)) Log(l + " spot created"); }
        void OnSpotHit(SkylineBot b) { if (_labels.TryGetValue(b, out var l)) Log(l + " spot hit"); }
        void OnWrecked(SkylineBot b) { if (_labels.TryGetValue(b, out var l)) Log(l + " wrecked"); }

        void OnAttacked(SkylineBot b)
        {
            if (!_labels.TryGetValue(b, out var l)) return;
            _attacks[b] = _attacks.TryGetValue(b, out int n) ? n + 1 : 1;
            Log(l + " attack t=" + T());
        }

        void OnMine(SkylineBot b, SkylineMineEvent e, Vector3 at)
        {
            if (!_labels.TryGetValue(b, out var l)) return;
            Log(l + " mine " + e.ToString().ToLowerInvariant());
        }

        void OnBark(SkylineBot b, string id, string line)
        {
            if (_labels.TryGetValue(b, out var l)) Log(l + " bark " + id + " \"" + line + "\"");
        }

        static IEnumerator Wait(float seconds)
        {
            float until = Time.unscaledTime + seconds;
            while (Time.unscaledTime < until) yield return null;
        }

        void Cut(Transform pose)
        {
            if (mainCamera != null && pose != null) mainCamera.transform.SetPositionAndRotation(pose.position, pose.rotation);
        }

        IEnumerator Run()
        {
            // In the editor capture the Recorder starts about a second into Play mode; start from there so it all records.
            if (SkylineRunArgs.Has("-skylineOut"))
            {
                float giveUp = Time.unscaledTime + RecorderWait;
                while (!(Time.captureDeltaTime > 0f) && Time.unscaledTime < giveUp) yield return null;
            }
            Log("start t=" + T());
            Cut(widePose);
            yield return Wait(LeadIn);
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] != null && i < _bots.Length && _bots[i] != null) yield return Segment(i);

            // fresh, idle bots for the stills
            foreach (var b in _bots)
            {
                if (b == null) continue;
                if (b.WreckObject != null) Destroy(b.WreckObject.gameObject);
                _labels.Remove(b);
                _attacks.Remove(b);
                Destroy(b.gameObject);
            }
            foreach (var s in slots) if (s != null && s.dummy != null) s.dummy.gameObject.SetActive(false);
            yield return null;
            SpawnAll();
            Cut(widePose);
            Log("stills-ready");
            yield return Wait(StillsHold);
            Log("done");
        }

        IEnumerator Segment(int i)
        {
            var s = slots[i];
            var bot = _bots[i];
            float end = Time.unscaledTime + s.seconds;
            Cut(s.cameraPose);
            Log(s.label + " segment");
            yield return Wait(IdleSeconds);

            if (s.dummy != null)
            {
                if (s.runFrom != null) s.dummy.transform.SetPositionAndRotation(s.runFrom.position, s.runFrom.rotation);
                s.dummy.gameObject.SetActive(true);
                Log(s.label + " dummy revealed");
            }
            if (bot.Def.layer == ThreatLayer.Walls) yield return WallRun(s, bot);
            else
            {
                float giveUp = Time.unscaledTime + AttackWait;
                while (_attacks[bot] < 2 && Time.unscaledTime < giveUp) yield return null;
                if (_attacks[bot] < 2) Log(s.label + " only " + _attacks[bot] + " attacks before the shooter");
            }
            yield return Wait(0.3f);
            yield return Shoot(bot);
            yield return Wait(AfterWreck);
            if (s.dummy != null) s.dummy.gameObject.SetActive(false);
            while (Time.unscaledTime < end) yield return null;
        }

        /// <summary>Waits until a mine on the dummy's line is armed and the gondola is clear of the line's far end, then runs the
        /// dummy along the wall past it.</summary>
        IEnumerator WallRun(Slot s, SkylineBot bot)
        {
            if (s.runFrom == null || s.runTo == null || s.dummy == null) { yield return Wait(4f); yield break; }
            Vector3 a = s.runFrom.position + Vector3.up * 1.25f, b = s.runTo.position + Vector3.up * 1.25f;
            Vector3 ab = b - a;
            float giveUp = Time.unscaledTime + MineWait;
            while (Time.unscaledTime < giveUp)
            {
                bool armed = false;
                if (bot.Mines != null)
                    foreach (var m in bot.Mines.Mines)
                    {
                        if (m.State != MineState.Armed) continue;
                        Vector3 p = new Vector3(m.Position.X, m.Position.Y, m.Position.Z);
                        float t = Vector3.Dot(p - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude);
                        if (t > 0.15f && t < 0.85f && Vector3.Distance(p, a + ab * t) < bot.Def.mineTriggerRadius * 0.9f) { armed = true; break; }
                    }
                float g = Vector3.Dot(bot.transform.position - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude);
                bool clear = g > 1f + 2f / Mathf.Max(0.1f, ab.magnitude);
                if (armed && clear) break;
                yield return null;
            }
            Log(s.label + " dummy wall-run");
            Vector3 from = s.runFrom.position, to = s.runTo.position;
            float len = Vector3.Distance(from, to), d = 0f;
            while (d < len)
            {
                d = Mathf.Min(len, d + RunSpeed * Time.deltaTime);
                s.dummy.transform.position = Vector3.Lerp(from, to, d / Mathf.Max(1e-4f, len));
                yield return null;
            }
            yield return Wait(0.6f);
            s.dummy.gameObject.SetActive(false);
        }

        Vector3 ShooterPos()
        {
            var t = mainCamera != null ? mainCamera.transform : transform;
            return t.position + t.right * 0.45f - t.up * 0.3f;
        }

        IEnumerator Shoot(SkylineBot bot)
        {
            for (int p = 0; p < bot.Body.PlateCount; p++)
            {
                int guard = 8;
                while (bot.Body.PlateOn(p) && !bot.Body.Wrecked && guard-- > 0)
                {
                    Fire(bot, bot.PlateCollider(p), null);
                    yield return Wait(ShotGap);
                }
            }
            if (!bot.Body.Wrecked)
            {
                Fire(bot, bot.WeakPointCollider, null);
                yield return Wait(ShotGap + 0.2f);
            }
            if (!bot.Body.Wrecked && bot.Spots.Count > 0)
            {
                var spot = bot.Spots.All[bot.Spots.Count - 1];
                Fire(bot, bot.WeakPointCollider, bot.SpotWorld(spot));
                yield return Wait(ShotGap);
            }
            int g2 = 12;
            while (!bot.Body.Wrecked && g2-- > 0)
            {
                Fire(bot, bot.BodyCollider, null);
                yield return Wait(ShotGap);
            }
        }

        /// <summary>One invisible Writ shot from by the camera at <paramref name="col"/> (its point nearest the shooter, or
        /// <paramref name="at"/>): the hit goes through the collider's SkylineHitZone exactly as a real shot's does.</summary>
        void Fire(SkylineBot bot, Collider col, Vector3? at)
        {
            if (bot == null || bot.Body.Wrecked || col == null) return;
            var zone = col.GetComponent<SkylineHitZone>();
            if (zone == null) return;
            Vector3 from = ShooterPos();
            Vector3 p = at ?? col.ClosestPoint(from);
            Vector3 d = p - from;
            float dist = d.magnitude;
            Vector3 dir = dist > 1e-4f ? d / dist : Vector3.forward;
            var info = new SkylineHitInfo
            {
                gun = _gun,
                damage = _gun.damage * _gun.FalloffScale(dist),
                plate = -1,
                point = p,
                normal = -dir,
                direction = dir,
                fromWrit = true,
                knockback = _gun.enemyKnockback,
                time = Time.time,
            };
            zone.TakeHit(in info);
            SkylineTracer.Spawn(from, p, new Color(1f, 0.92f, 0.7f));
            SkylineImpact.Spawn(p, -dir, true);
            SkylineSounds.PlayAt("writ/shot", from, 0.6f);
        }
    }
}
