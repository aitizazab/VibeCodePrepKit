using JamKit.Skyline;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// A wrecked bot's husk on the floor under where it fell (2.4: "aggression heals"). While its <see cref="Wreck"/> is fresh
    /// and unstripped it throws sparks; it goes dark when stale (after <see cref="Wreck.freshSeconds"/>, 8 s) or once stripped.
    /// The player strips it by coming within <see cref="StripRadius"/> m (flat, and less than 2.5 m above it, as FightSim):
    /// <c>PlayerArmor.Strip(Data, Time.time, Difficulties.Normal().scrapScale)</c> (recovered scrap is inside Strip). Logs
    /// <c>SKYLINE_BOT stripped &lt;bot&gt; plating +&lt;n&gt;</c>.
    /// </summary>
    public sealed class SkylineWreck : MonoBehaviour
    {
        public const float StripRadius = 1.8f;
        const float MaxStripHeight = 2.5f;

        public Wreck Data;
        public string botName = "";
        /// <summary>Whoever can strip it (the player); anything that isn't a <see cref="SkylinePlayer"/> never strips.</summary>
        public ISkylineTarget stripper;

        Renderer[] _renderers;
        float _nextSpark;
        bool _dark;
        readonly float _scrapScale = Difficulties.Normal().scrapScale;

        /// <summary>Leaves a husk of <paramref name="def"/> at <paramref name="floorPoint"/>, wrecked now.</summary>
        public static SkylineWreck Create(EnemyDef def, string botName, Vector3 floorPoint, float yaw, ISkylineTarget stripper, Transform parent)
        {
            var root = new GameObject("Wreck " + botName);
            if (parent != null) root.transform.SetParent(parent, true);
            root.transform.SetPositionAndRotation(floorPoint, Quaternion.Euler(0f, yaw, 0f));
            var w = root.AddComponent<SkylineWreck>();
            w.Data = new Wreck(def, Time.time);
            w.botName = botName;
            w.stripper = stripper;
            var c = SkylineBotLook.White * 0.55f;
            c.a = 1f;
            float r = Mathf.Clamp(def.bodyRadius, 0.35f, 0.9f);
            // a squashed chassis, a broken-off panel and a strut: reads as remains, not as a live bot
            SkylineBotLook.Part(PrimitiveType.Sphere, "Husk", root.transform, new Vector3(0f, r * 0.3f, 0f), new Vector3(r * 1.8f, r * 0.6f, r * 1.5f), c, false, new Vector3(0f, 0f, 8f));
            SkylineBotLook.Part(PrimitiveType.Cube, "Panel", root.transform, new Vector3(r * 0.7f, 0.05f, r * 0.4f), new Vector3(r * 0.8f, 0.04f, r * 0.5f), SkylineBotLook.Gold * 0.6f, false, new Vector3(0f, 30f, 12f));
            SkylineBotLook.Part(PrimitiveType.Cube, "Strut", root.transform, new Vector3(-r * 0.6f, 0.08f, -r * 0.3f), new Vector3(0.06f, 0.06f, r * 1.2f), SkylineBotLook.Grey, false, new Vector3(10f, -40f, 0f));
            w._renderers = root.GetComponentsInChildren<Renderer>();
            return w;
        }

        public bool Fresh => Data != null && Data.Fresh(Time.time);

        void Update()
        {
            if (Data == null) return;
            float now = Time.time;
            if (Data.Fresh(now))
            {
                TryStrip(now);
                _nextSpark -= Time.deltaTime;
                if (_nextSpark <= 0f && !Data.stripped)
                {
                    _nextSpark = Random.Range(0.12f, 0.3f);
                    float r = Mathf.Clamp(Data.def.bodyRadius, 0.35f, 0.9f);
                    SkylineSpark.Burst(transform.position + new Vector3(Random.Range(-r, r) * 0.6f, r * 0.45f, Random.Range(-r, r) * 0.5f), Vector3.up, Random.Range(2, 5), 3f);
                }
            }
            if (!_dark && !Data.Fresh(now)) GoDark();
        }

        void TryStrip(float now)
        {
            var p = stripper as SkylinePlayer;
            if (p == null || p.Armor == null) return;
            Vector3 feet = p.transform.position, at = transform.position;
            float dx = feet.x - at.x, dz = feet.z - at.z;
            if (dx * dx + dz * dz > StripRadius * StripRadius || feet.y - at.y >= MaxStripHeight) return;
            float gained = p.Armor.Strip(Data, now, _scrapScale);
            if (Data.stripped)
                Debug.Log("SKYLINE_BOT stripped " + botName + " plating +" + gained.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
        }

        void GoDark()
        {
            _dark = true;
            if (_renderers == null) return;
            var m = SkylineBotLook.Lit(SkylineBotLook.Scorched);
            foreach (var r in _renderers) if (r != null) r.sharedMaterial = m;
        }
    }
}
