using System;
using System.Collections.Generic;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.Events;
using NVec3 = System.Numerics.Vector3;

namespace JamKit.Stealth
{
    /// <summary>
    /// A seeker NPC in a scene (docs/STEALTH.md): runs a core <see cref="SeekerBrain"/> against
    /// <see cref="StealthPlayer.Current"/> and moves this object to the brain's position and facing. Its walking map is
    /// the children of <see cref="waypoints"/> (joined where the walk is clear, by physics), the hiding spots are every
    /// <see cref="HidingSpotView"/>. Patrol mode walks the <see cref="patrol"/> loop (child indices); hide-and-seek counts
    /// facing <see cref="countYaw"/>, then seeks. <see cref="Found"/> / <see cref="onFound"/> fire when it finds you; the
    /// game decides what that means. It only thinks while gameplay is allowed (not paused, not loading).
    /// </summary>
    [DefaultExecutionOrder(60)]
    public sealed class SeekerAgent : MonoBehaviour
    {
        public StealthTuning tuning;
        public SeekerMode mode = SeekerMode.Patrol;
        [Tooltip("Its walking map: every child is a point on its floor.")]
        public Transform waypoints;
        [Tooltip("Points within this distance, with a clear walk between them, are joined.")]
        public float connectDistance = 16f;
        [Tooltip("The patrol loop, as child indices of Waypoints.")]
        public List<int> patrol = new List<int>();
        [Tooltip("Hide-and-seek: the way it faces while counting (degrees, 0 = +z).")]
        public float countYaw;
        [Tooltip("What blocks its sight (solid, non-trigger colliders on these layers).")]
        public LayerMask sightMask = ~(1 << 2);
        [Header("Lookout (optional: a trampoline)")]
        [Tooltip("Hide-and-seek: a point it counts on and keeps coming back to, bouncing: its eye (and body) rise at the top of each bounce, so it sees over fences only then. Empty: none.")]
        public Transform lookout;
        public float lookoutPlatform = 0.9f, lookoutBounce = 1.3f, lookoutPeriod = 0.8f, lookoutSeconds = 4f;
        [Tooltip("A sweep comes back to the lookout every this many stops.")]
        public int lookoutEvery = 3;
        public UnityEvent onFound = new UnityEvent();

        /// <summary>(this) when it finds the player.</summary>
        public event Action<SeekerAgent> Found;
        /// <summary>(this, from, to) on every alert-state change.</summary>
        public event Action<SeekerAgent, AlertState, AlertState> StateChanged;
        /// <summary>(this) when it hears a noise.</summary>
        public event Action<SeekerAgent> Heard;
        /// <summary>(caller, how many came) when it calls the others (answer B).</summary>
        public event Action<SeekerAgent, int> Called;

        static readonly List<SeekerAgent> _all = new List<SeekerAgent>();
        readonly StealthDef _fallback = new StealthDef();
        PhysicsStealthWorld _world;
        WaypointGraph _graph;
        SeekerBrain _brain;
        Vector3 _home;
        float _homeYaw;
        bool _frozen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _all.Clear();

        public static IReadOnlyList<SeekerAgent> All => _all;
        public StealthDef Def => tuning != null ? tuning.def : _fallback;
        public SeekerBrain Brain => _brain;
        public AlertState State => _brain != null ? _brain.State : AlertState.Unaware;
        public float Meter => _brain != null ? _brain.Meter : 0f;
        public bool Counting => _brain != null && _brain.Counting;
        public WaypointGraph Graph => _graph;
        /// <summary>Stops thinking and moving (a round is over, a cutscene).</summary>
        public bool Frozen { get => _frozen; set => _frozen = value; }

        void OnEnable() { if (!_all.Contains(this)) _all.Add(this); }
        void OnDisable() => _all.Remove(this);

        void Start() => EnsureBrain();

        public void EnsureBrain()
        {
            if (_brain != null) return;
            _world = new PhysicsStealthWorld { mask = sightMask };
            _world.Ignore(transform);
            if (StealthPlayer.Current != null) _world.Ignore(StealthPlayer.Current.transform);
            _graph = new WaypointGraph();
            if (waypoints != null)
                for (int i = 0; i < waypoints.childCount; i++)
                {
                    var w = waypoints.GetChild(i).position;
                    _graph.Add(new NVec3(w.x, transform.position.y, w.z));
                }
            _graph.AutoConnect(_world, connectDistance);
            _home = transform.position;
            _homeYaw = transform.eulerAngles.y;
            _brain = new SeekerBrain(Def, _world, _graph, HidingSpotView.Spots, ToN(_home), _homeYaw) { rules = StealthRules.Shared };
            _brain.SetPatrol(patrol);
            if (lookout != null)
            {
                var l = lookout.position;
                _brain.SetLookout(new NVec3(l.x, transform.position.y, l.z), lookoutPlatform, lookoutBounce, lookoutPeriod, lookoutEvery, lookoutSeconds);
            }
            if (mode == SeekerMode.HideAndSeek) _brain.StartHideAndSeek(ToN(_home), countYaw);
        }

        /// <summary>Back to where it started: patrolling, or counting again for hide-and-seek (optionally for
        /// <paramref name="countSeconds"/>; negative: the tuning's count).</summary>
        public void Restart(float countSeconds = -1f)
        {
            EnsureBrain();
            _frozen = false;
            if (StealthPlayer.Current != null) _world.Ignore(StealthPlayer.Current.transform);
            if (mode == SeekerMode.HideAndSeek) _brain.StartHideAndSeek(ToN(_home), countYaw, countSeconds);
            else _brain.Place(ToN(_home), _homeYaw);
            Apply();
        }

        /// <summary>Switches mode and restarts.</summary>
        public void SetMode(SeekerMode m, float countSeconds = -1f)
        {
            mode = m;
            Restart(countSeconds);
        }

        void Update()
        {
            if (_brain == null || _frozen) return;
            if (GameRoot.Exists && !GameRoot.Input.GameplayAllowed) return;
            var player = StealthPlayer.Current;
            var target = player != null ? player.Target : default;
            var noise = player != null ? player.Noise : default;
            _brain.Tick(Time.deltaTime, target, noise);
            Apply();
            _brain.rules = StealthRules.Shared;
            var ev = _brain.Events;
            if (ev.stateChanged) StateChanged?.Invoke(this, ev.from, ev.to);
            if (ev.heard) Heard?.Invoke(this);
            if (ev.called && _brain.rules != null && _brain.rules.dreamPushesBack)
            {
                // The call (answer B): every other guard within earshot of it turns wary and comes.
                int came = 0;
                for (int i = 0; i < _all.Count; i++)
                {
                    var other = _all[i];
                    if (other == this || other == null || other._brain == null || other._frozen || !other.isActiveAndEnabled) continue;
                    if ((other.transform.position - transform.position).magnitude > Def.callRadius) continue;
                    if (other._brain.HearCall(_brain.CallSpot)) came++;
                }
                Called?.Invoke(this, came);
            }
            if (ev.found)
            {
                Found?.Invoke(this);
                onFound?.Invoke();
            }
        }

        void Apply()
        {
            var p = _brain.Position;
            transform.SetPositionAndRotation(new Vector3(p.X, p.Y + _brain.EyeLift, p.Z), Quaternion.Euler(0f, _brain.Yaw, 0f));
        }

        static NVec3 ToN(Vector3 v) => new NVec3(v.x, v.y, v.z);

        void OnDrawGizmosSelected()
        {
            if (waypoints == null) return;
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.8f);
            for (int i = 0; i < waypoints.childCount; i++) Gizmos.DrawWireSphere(waypoints.GetChild(i).position, 0.3f);
            if (_graph == null) return;
            for (int a = 0; a < _graph.Count; a++)
                foreach (int b in _graph.Neighbours(a))
                    if (b > a) Gizmos.DrawLine(new Vector3(_graph[a].X, _graph[a].Y + 0.1f, _graph[a].Z), new Vector3(_graph[b].X, _graph[b].Y + 0.1f, _graph[b].Z));
        }
    }
}
