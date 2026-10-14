using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>Load-time work spread over the scene's first frames (judge round 2, perf): each frame runs queued jobs until
    /// <see cref="BudgetMs"/> is spent (always at least one), so building every bot body ahead and decoding every sound
    /// doesn't become one long frame, and nothing of it is left for a fight. Jobs run in the order queued; the host lives in
    /// the active scene and removes itself when the queue is empty.</summary>
    public sealed class SkylineWarmQueue : MonoBehaviour
    {
        const double BudgetMs = 4.0;
        static readonly Queue<Action> _jobs = new Queue<Action>();
        static SkylineWarmQueue _host;
        static int _done;
        static readonly Stopwatch _watch = new Stopwatch();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _jobs.Clear();
            _host = null;
            _done = 0;
        }

        /// <summary>Queues <paramref name="job"/> for one of the coming frames.</summary>
        public static void Add(Action job)
        {
            if (job == null) return;
            _jobs.Enqueue(job);
            if (_host == null) _host = new GameObject("Skyline warm queue").AddComponent<SkylineWarmQueue>();
        }

        /// <summary>Runs every queued job now (a test, or a scene that must be ready this frame).</summary>
        public static void Flush()
        {
            while (_jobs.Count > 0) Run(_jobs.Dequeue());
        }

        /// <summary>Jobs still waiting.</summary>
        public static int Pending => _jobs.Count;

        static void Run(Action job)
        {
            try { job(); }
            catch (Exception e) { UnityEngine.Debug.LogException(e); }
            _done++;
        }

        void Update()
        {
            _watch.Restart();
            do
            {
                if (_jobs.Count == 0) break;
                Run(_jobs.Dequeue());
            }
            while (_watch.Elapsed.TotalMilliseconds < BudgetMs);
            if (_jobs.Count > 0) return;
            UnityEngine.Debug.Log("SKYLINE_PREWARM queue done: " + _done + " jobs by frame " + Time.frameCount + " (" + Time.realtimeSinceStartup.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " s)");
            _host = null;
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (_host != this) return;
            _host = null;
            _jobs.Clear();   // its scene is going: what was queued for it goes too
        }
    }
}
