using System;
using System.Collections.Generic;
using System.Numerics;

namespace JamKit.Movement
{
    public enum GauntletState { Ready, Running, Finished }

    /// <summary>What happened in one <see cref="GauntletClock.Tick"/> (for the HUD and sound).</summary>
    public struct GauntletEvents
    {
        public bool started, finished;
        /// <summary>The marker touched this tick, or -1.</summary>
        public int markerHit;
        /// <summary>A marker passed by without touching it this tick, or -1 (its 2 s count at the finish).</summary>
        public int markerMissed;
        /// <summary>The section completed this tick (its split), or -1.</summary>
        public int split;

        public static GauntletEvents None => new GauntletEvents { markerHit = -1, markerMissed = -1, split = -1 };
    }

    /// <summary>The best run's numbers, kept in the shell's save under <see cref="GauntletClock.SaveKey"/> (JSON).</summary>
    [Serializable]
    public class GauntletRecord
    {
        /// <summary>The best total (penalties included); 0 = none yet.</summary>
        public float best;
        /// <summary>The best run's time at the end of each section (cumulative, penalties not included).</summary>
        public List<float> bestSplits = new List<float>();
        public int bestMissed;
        public int runs;
        /// <summary>The best run's ghost: x, y, z, yaw per sample at <see cref="ghostRate"/> samples a second.</summary>
        public List<float> ghost = new List<float>();
        public float ghostRate = GhostTrack.DefaultRate;
    }

    /// <summary>
    /// The Gauntlet's run, pure (the Unity side feeds it the player's feet and s once a frame): the clock starts
    /// when you cross the gate and stops at the finish line, touch markers are counted (each one missed adds
    /// <see cref="GauntletCourse.MissPenalty"/>), each section's end is a split, compared with the best run's.
    /// A fall doesn't stop the clock (the lab respawns you at the section's start). <see cref="Reset"/> is the
    /// instant restart. No allocation per tick.
    /// </summary>
    public sealed class GauntletClock
    {
        public const string SaveKey = "movementlab_gauntlet";

        readonly GauntletCourse _g;
        readonly bool[] _hit, _missed;
        readonly float[] _splits;
        GauntletState _state;
        float _time;
        int _sectionsDone;
        float _total;
        bool _newBest;

        public GauntletRecord record = new GauntletRecord();

        public GauntletClock(GauntletCourse g)
        {
            _g = g;
            _hit = new bool[g.markers.Count];
            _missed = new bool[g.markers.Count];
            _splits = new float[g.sections.Count];
            Reset();
        }

        public GauntletState State => _state;
        /// <summary>Seconds since the gate (the run's time without penalties).</summary>
        public float Time => _time;
        public int MarkersHit { get { int n = 0; for (int i = 0; i < _hit.Length; i++) if (_hit[i]) n++; return n; } }
        public int MarkerCount => _hit.Length;
        public int Missed => _hit.Length - MarkersHit;
        public float Penalty => Missed * GauntletCourse.MissPenalty;
        /// <summary>The final time (with penalties) once finished.</summary>
        public float Total => _total;
        public bool NewBest => _newBest;
        public int SectionsDone => _sectionsDone;
        public bool Hit(int marker) => _hit[marker];
        public float Split(int section) => section < _sectionsDone ? _splits[section] : 0f;
        public bool HasBestSplit(int section) => record.bestSplits != null && section < record.bestSplits.Count && record.bestSplits[section] > 0f;
        /// <summary>This run's split minus the best run's at the same section (negative: faster).</summary>
        public float SplitDelta(int section) => HasBestSplit(section) ? Split(section) - record.bestSplits[section] : 0f;

        /// <summary>Back to the start: the clock at 0, every marker untouched (the instant restart).</summary>
        public void Reset()
        {
            _state = GauntletState.Ready;
            _time = 0f;
            _total = 0f;
            _sectionsDone = 0;
            _newBest = false;
            for (int i = 0; i < _hit.Length; i++) { _hit[i] = false; _missed[i] = false; }
            for (int i = 0; i < _splits.Length; i++) _splits[i] = 0f;
        }

        /// <summary>Advances the run by <paramref name="dt"/> with the player's feet and distance along the route.
        /// <paramref name="countsForBest"/> false (an autopilot run, attract mode) finishes without touching the record.</summary>
        public GauntletEvents Tick(float dt, Vector3 feet, float s, float radius, float height, bool countsForBest = true)
        {
            var ev = GauntletEvents.None;
            if (_state == GauntletState.Ready)
            {
                // The gate: crossing it from the start pad starts the clock.
                if (s >= _g.gateS && s < _g.gateS + 20f)
                {
                    _state = GauntletState.Running;
                    _time = 0f;
                    ev.started = true;
                }
                else return ev;
            }
            if (_state != GauntletState.Running) return ev;
            _time += dt;

            for (int i = 0; i < _hit.Length; i++)
            {
                if (_hit[i]) continue;
                var m = _g.markers[i];
                if (m.Touches(feet, radius, height))
                {
                    _hit[i] = true;
                    ev.markerHit = i;
                }
                else if (!_missed[i] && s > m.max.Z + 4f)
                {
                    _missed[i] = true;
                    ev.markerMissed = i;
                }
            }

            while (_sectionsDone < _splits.Length && s >= _g.sections[_sectionsDone].endS)
            {
                _splits[_sectionsDone] = _time;
                ev.split = _sectionsDone;
                _sectionsDone++;
            }

            if (s >= _g.finishS)
            {
                // Sections skipped by a chapter jump still get a split (the finish time), so the table is whole.
                while (_sectionsDone < _splits.Length) _splits[_sectionsDone++] = _time;
                _state = GauntletState.Finished;
                _total = _time + Penalty;
                ev.finished = true;
                if (!countsForBest) return ev;
                record.runs++;
                if (record.best <= 0f || _total < record.best)
                {
                    _newBest = true;
                    record.best = _total;
                    record.bestMissed = Missed;
                    if (record.bestSplits == null) record.bestSplits = new List<float>();
                    record.bestSplits.Clear();
                    for (int i = 0; i < _splits.Length; i++) record.bestSplits.Add(_splits[i]);
                }
            }
            return ev;
        }
    }

    /// <summary>
    /// A recorded run, sampled at a fixed rate (x, y, z, yaw), and its replay: the ghost of your best run. The
    /// buffer is allocated once; recording and sampling don't allocate.
    /// </summary>
    public sealed class GhostTrack
    {
        public const float DefaultRate = 20f;
        public const int MaxSamples = 20 * 300; // five minutes at 20 Hz

        readonly float[] _data = new float[MaxSamples * 4];
        int _count;
        float _acc;
        public float rate = DefaultRate;

        public int Count => _count;
        public float Duration => _count > 1 ? (_count - 1) / rate : 0f;

        public void Clear()
        {
            _count = 0;
            _acc = 0f;
        }

        /// <summary>Adds the first sample at once, then one every 1/rate seconds of <paramref name="dt"/>.</summary>
        public void Record(float dt, Vector3 p, float yaw)
        {
            if (_count == 0) { Push(p, yaw); _acc = 0f; return; }
            _acc += dt;
            float step = 1f / rate;
            while (_acc >= step && _count < MaxSamples)
            {
                _acc -= step;
                Push(p, yaw);
            }
        }

        void Push(Vector3 p, float yaw)
        {
            if (_count >= MaxSamples) return;
            int i = _count * 4;
            _data[i] = p.X; _data[i + 1] = p.Y; _data[i + 2] = p.Z; _data[i + 3] = yaw;
            _count++;
        }

        /// <summary>Where the ghost is <paramref name="t"/> seconds in (interpolated); false past its end.</summary>
        public bool Sample(float t, out Vector3 p, out float yaw)
        {
            p = Vector3.Zero;
            yaw = 0f;
            if (_count == 0 || t < 0f) return false;
            float f = t * rate;
            int a = (int)f;
            if (a >= _count - 1)
            {
                int l = (_count - 1) * 4;
                p = new Vector3(_data[l], _data[l + 1], _data[l + 2]);
                yaw = _data[l + 3];
                return a < _count + (int)rate; // linger a second at the finish
            }
            float k = f - a;
            int i = a * 4, j = i + 4;
            p = new Vector3(_data[i] + (_data[j] - _data[i]) * k, _data[i + 1] + (_data[j + 1] - _data[i + 1]) * k, _data[i + 2] + (_data[j + 2] - _data[i + 2]) * k);
            float dy = _data[j + 3] - _data[i + 3];
            if (dy > 180f) dy -= 360f;
            if (dy < -180f) dy += 360f;
            yaw = _data[i + 3] + dy * k;
            return true;
        }

        public void CopyTo(List<float> into)
        {
            into.Clear();
            for (int i = 0; i < _count * 4; i++) into.Add(_data[i]);
        }

        public void CopyFrom(GhostTrack other)
        {
            _count = other._count;
            rate = other.rate;
            Array.Copy(other._data, _data, _count * 4);
        }

        public void LoadFrom(List<float> from, float sampleRate)
        {
            Clear();
            if (from == null) return;
            rate = sampleRate > 0f ? sampleRate : DefaultRate;
            int n = Math.Min(from.Count / 4, MaxSamples);
            for (int i = 0; i < n * 4; i++) _data[i] = from[i];
            _count = n;
        }
    }
}

namespace JamKit.Movement
{
    /// <summary>
    /// The developer's records shipped with the lab (Data/DeveloperRecords.json, from the creator's own save,
    /// tools/movementlab/creator-records-2026-10-10.json): the board's "the developer" times and the developer's
    /// ghost. The autopilot's times are a test driver's, kept in tests and logs only (the creator, 2026-10-10:
    /// their real runs beat it by 10-15 s). JSON with the save's keys, read by JsonUtility.
    /// </summary>
    [Serializable]
    public class DeveloperRecords
    {
        public string note;
        public DeveloperRecordSet records = new DeveloperRecordSet();

        /// <summary>Mags's time: a fair casual target, 1.5x the developer's, rounded to the nearest 2.5 s.</summary>
        public static float MagsTimeFor(float developer) => developer > 0f ? (float)(Math.Round(developer * 1.5 / 2.5) * 2.5) : 0f;
    }

    [Serializable]
    public class DeveloperRecordSet
    {
        public GauntletRecord movementlab_gauntlet;
        public GauntletRecord movementlab_dreamtool;
    }
}
