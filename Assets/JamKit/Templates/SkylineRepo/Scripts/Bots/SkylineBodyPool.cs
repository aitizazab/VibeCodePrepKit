using System.Collections.Generic;
using JamKit.Skyline;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// Bot bodies built ahead (judge round 2, perf E N5: the worst frames, 20-29 ms, came as fights started and groups woke,
    /// with no garbage collection). Spawning a bot used to instantiate its whole model (20-40 parts), add a box collider to
    /// every part, clear the weak point's boxes and add a hit zone to every collider, all on the frame its encounter
    /// started, for every bot of the encounter at once. Now <see cref="Fill"/> queues those builds on
    /// <see cref="SkylineWarmQueue"/> at scene start (one or two a frame), parks each finished body switched off under one
    /// holder far below the arena, and <see cref="SkylineBotBodies.Build"/> takes one with <see cref="Take"/>: it is moved
    /// under the new bot, given its owner and switched on. A body is used once (a wreck keeps it); when none is ready (the
    /// pool not yet filled, a restarted fight, a scene that didn't fill it) the bot builds its own as before, so nothing
    /// depends on the pool but the frame time.
    /// </summary>
    public static class SkylineBodyPool
    {
        static readonly Dictionary<string, Stack<SkylineBotParts>> _ready = new Dictionary<string, Stack<SkylineBotParts>>();
        static Transform _holder;
        static int _built, _taken;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _ready.Clear();
            _holder = null;
            _built = _taken = 0;
        }

        /// <summary>Queues one body for each id in <paramref name="enemyIds"/> (an id per bot to come, in the order they will be
        /// needed).</summary>
        public static void Fill(IEnumerable<string> enemyIds)
        {
            foreach (var id in enemyIds)
            {
                var def = SkylineEnemies.Find(id);
                if (def == null) continue;
                SkylineWarmQueue.Add(() => BuildOne(def));
            }
        }

        /// <summary>The bodies ready for <paramref name="enemyId"/>.</summary>
        public static int Ready(string enemyId) => _ready.TryGetValue(enemyId, out var s) ? s.Count : 0;

        static void BuildOne(EnemyDef def)
        {
            if (_holder == null)
            {
                _ready.Clear();
                // active, so the box colliders size themselves to their meshes as they are added; far below the arena and
                // switched off before the frame is drawn, so never seen
                _holder = new GameObject("Bot bodies (built ahead)").transform;
                _holder.position = new Vector3(0f, -3000f, 0f);
            }
            var slot = new GameObject(def.id + " (ready)").transform;
            slot.SetParent(_holder, false);
            var parts = SkylineBotBodies.BuildAhead(def, slot);
            if (parts == null)
            {
                Object.Destroy(slot.gameObject);
                return;
            }
            slot.gameObject.SetActive(false);
            if (!_ready.TryGetValue(def.id, out var stack)) _ready[def.id] = stack = new Stack<SkylineBotParts>();
            stack.Push(parts);
            _built++;
        }

        /// <summary>A ready body for <paramref name="def"/> moved under <paramref name="root"/> (at its origin, as a fresh build
        /// would be), its hit zones owned by <paramref name="owner"/>, switched on; null when none is ready.</summary>
        public static SkylineBotParts Take(EnemyDef def, Transform root, MonoBehaviour owner)
        {
            if (def == null || root == null || _holder == null) return null;
            if (!_ready.TryGetValue(def.id, out var stack)) return null;
            while (stack.Count > 0)
            {
                var p = stack.Pop();
                if (p == null || p.rig == null) continue;   // destroyed with an old scene
                var slot = p.rig.parent;
                p.rig.SetParent(root, false);
                if (p.mount != null) p.mount.SetParent(root, false);
                foreach (var z in p.rig.GetComponentsInChildren<SkylineHitZone>(true)) z.owner = owner;
                if (p.mount != null) foreach (var z in p.mount.GetComponentsInChildren<SkylineHitZone>(true)) z.owner = owner;
                if (slot != null && slot != _holder) Object.Destroy(slot.gameObject);
                _taken++;
                return p;
            }
            return null;
        }

        /// <summary>"12 built, 8 taken".</summary>
        public static string Describe() => _built + " built, " + _taken + " taken";
    }
}
