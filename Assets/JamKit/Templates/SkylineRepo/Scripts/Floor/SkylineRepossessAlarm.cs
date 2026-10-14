using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// One asset's alarm (task R2; "clamping is loud"): CONCIERGE's sting once, and a spinning amber beacon on the wall nearest the
    /// asset that turns until the asset is towed (or a restart). Raised by <see cref="SkylineRepossess"/> on the asset's
    /// MeterStarted, once per asset per run. Not a MonoBehaviour.
    /// </summary>
    public sealed class SkylineRepossessAlarm
    {
        /// <summary>The placeholder alarm sound (an existing CONCIERGE clip); the sound pass replaces this name.</summary>
        public const string AlarmSound = "concierge/sting";
        /// <summary>How far the beacon looks for a wall (m), the height of the search and of the beacon on a wall, how far it
        /// stands off the wall, and the spin (deg/s).</summary>
        public const float WallSearch = 14f, BeaconHeight = 2.6f, StandOff = 0.14f, SpinDegPerSecond = 240f;
        const int Rays = 16;

        static readonly RaycastHit[] Hits = new RaycastHit[16];

        readonly Transform _root;
        readonly Transform _spin;
        readonly Renderer _bulb;
        readonly Light _lamp;
        readonly Material _bright, _dim;
        bool _on, _blinkBright, _onWall;

        public Transform Root => _root;
        public bool On => _on;
        /// <summary>Where the beacon stands (world).</summary>
        public Vector3 Position => _root.position;
        /// <summary>True when a wall was found within <see cref="WallSearch"/> (else the beacon hangs over the asset).</summary>
        public bool OnWall => _onWall;

        SkylineRepossessAlarm(Transform parent, string name)
        {
            _root = new GameObject(name).transform;
            if (parent != null) _root.SetParent(parent, false);
            _bright = SkylineBotLook.Glow(SkylineRepossessFx.AmberHdr);
            _dim = SkylineBotLook.Glow(SkylineRepossessFx.AmberDim);
            var dark = SkylineBotLook.Lit(SkylineBotLook.Dark);
            SkylineRepossessFx.Piece(PrimitiveType.Cylinder, "Mount", _root, Vector3.zero, new Vector3(0.3f, 0.04f, 0.3f), dark);
            _spin = new GameObject("Spin").transform;
            _spin.SetParent(_root, false);
            _spin.localPosition = new Vector3(0f, 0.12f, 0f);
            _bulb = SkylineRepossessFx.Piece(PrimitiveType.Sphere, "Bulb", _spin, Vector3.zero, new Vector3(0.22f, 0.22f, 0.22f), _bright).GetComponent<Renderer>();
            // the rotating beam of light: a spot that sweeps round the room
            var aim = new GameObject("Beam aim").transform;
            aim.SetParent(_spin, false);
            aim.localRotation = Quaternion.Euler(-70f, 0f, 0f);       // out along the mount's up (the wall's normal), 20 degrees off it: spinning sweeps a cone
            _lamp = SkylineRepossessFx.MakeLight(aim, "Beacon light", LightType.Spot, new Color(1f, 0.62f, 0.1f), 7f, 16f, 42f);
            _root.gameObject.SetActive(false);
        }

        public static SkylineRepossessAlarm Build(Transform parent, string name) => new SkylineRepossessAlarm(parent, name);

        /// <summary>Raises the alarm for an asset standing at <paramref name="asset"/>: places the beacon on the nearest wall (a
        /// horizontal search from the asset), switches it on and plays the sting from it. Returns where the beacon stands.</summary>
        public Vector3 Raise(Vector3 asset)
        {
            Vector3 from = asset + Vector3.up * BeaconHeight;
            Vector3 best = Vector3.zero, bestNormal = Vector3.zero;
            float bestD = float.MaxValue;
            for (int k = 0; k < Rays; k++)
            {
                float a = k * Mathf.PI * 2f / Rays;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                int n = Physics.RaycastNonAlloc(from, dir, Hits, WallSearch, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++)
                {
                    var c = Hits[i].collider;
                    if (c == null || SkylineBot.IgnoredBySight(c, null)) continue;
                    if (c.GetComponentInParent<SkylinePlayer>() != null) continue;
                    if (Hits[i].distance < bestD) { bestD = Hits[i].distance; best = Hits[i].point; bestNormal = Hits[i].normal; }
                }
            }
            if (bestD < float.MaxValue)
            {
                bestNormal.y = 0f;
                if (bestNormal.sqrMagnitude < 1e-4f) bestNormal = -(best - from).normalized;
                bestNormal.Normalize();
                _root.position = best + bestNormal * StandOff;
                _root.rotation = Quaternion.LookRotation(bestNormal, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);   // the mount's up is the wall's out
                _onWall = true;
            }
            else
            {
                _root.position = asset + Vector3.up * (BeaconHeight + 0.8f);
                _root.rotation = Quaternion.Euler(180f, 0f, 0f);          // hanging: the mount's up points down over the asset
                _onWall = false;
            }
            _on = true;
            _root.gameObject.SetActive(true);
            SkylineRepossessFx.Sound(AlarmSound, _root.position);
            return _root.position;
        }

        /// <summary>Spins the beacon and blinks the bulb. Cheap when off.</summary>
        public void Tick(float dt)
        {
            if (!_on) return;
            _spin.Rotate(0f, SpinDegPerSecond * dt, 0f, Space.Self);
            bool bright = Mathf.Repeat(Time.time * 3f, 1f) < 0.5f;
            if (bright != _blinkBright || _bulb.sharedMaterial == null)
            {
                _blinkBright = bright;
                _bulb.sharedMaterial = bright ? _bright : _dim;
            }
        }

        /// <summary>The asset is gone or the run restarted: the beacon goes out.</summary>
        public void Stop()
        {
            _on = false;
            _root.gameObject.SetActive(false);
        }
    }
}
