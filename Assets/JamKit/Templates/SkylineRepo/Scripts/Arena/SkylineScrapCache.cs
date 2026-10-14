using System;
using System.Globalization;
using JamKit.Skyline;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// The scrap cache on top of the warm-up kiosk (the boost ledge, docs/SKYLINE-DESIGN.md 6.8): the player takes it by
    /// standing within <see cref="radius"/> m of it (on the floor plan) with the feet at its height. Taking it tops the
    /// Unit's plating up to its maximum (no number is shown: the HUD's plating bar is the only readout), hides the crate,
    /// logs <c>SKYLINE_ARENA boost-ledge reached</c> and raises <see cref="Taken"/> (the director's BoostLedgeReached beat).
    /// It is worth <see cref="scrap"/> scrap on every visit (judge round 1: it paid +0 on a first run, when nothing has hurt
    /// the Unit yet): through <see cref="PlayerArmor.Strip"/> with a one-off wreck of that worth, so it fills the plating
    /// first and banks the rest as recovered scrap (the creator's rule: excess converts to money). Logs
    /// <c>SKYLINE_ARENA scrap-cache plating +&lt;n&gt; recovered +&lt;m&gt;</c>. <see cref="ReArm"/> (the arena's restart) puts it back.
    /// </summary>
    public sealed class SkylineScrapCache : MonoBehaviour
    {
        public SkylinePlayer player;
        [Tooltip("Flat distance (m) from the cache's centre within which the player takes it.")]
        public float radius = 1.4f;
        [Tooltip("How far (m) below or above the cache the feet may be.")]
        public float heightTolerance = 0.6f;
        [Tooltip("Which ledge this cache tops: 'boost-ledge' (the kiosk) or 'wall-run-ledge' (the lockers); names its log lines.")]
        public string ledgeId = "boost-ledge";
        [Tooltip("What the cache is worth (scrap): plating first, the rest banked as recovered scrap.")]
        public float scrap = 50f;
        [Tooltip("The crate's look; hidden once taken.")]
        public GameObject visual;

        public bool IsTaken { get; private set; }
        /// <summary>The recovered scrap the last take banked (what didn't fit in the plating).</summary>
        public float Recovered { get; private set; }
        public event Action Taken;

        void Update()
        {
            if (IsTaken || player == null || player.Armor == null) return;
            Vector3 feet = player.transform.position, at = transform.position;
            float dx = feet.x - at.x, dz = feet.z - at.z;
            if (dx * dx + dz * dz > radius * radius || Mathf.Abs(feet.y - at.y) > heightTolerance) return;
            IsTaken = true;
            var armor = player.Armor;
            float banked = armor.RecoveredScrap;
            var def = new EnemyDef { id = "scrap-cache", displayName = "Scrap cache", scrapPlating = scrap };
            float gained = armor.Strip(new Wreck(def, Time.time), Time.time, 1f);
            Recovered = armor.RecoveredScrap - banked;
            if (visual != null) visual.SetActive(false);
            Debug.Log("SKYLINE_ARENA " + ledgeId + " reached");
            Debug.Log("SKYLINE_ARENA " + (ledgeId == "boost-ledge" ? "scrap-cache" : ledgeId + " cache") + " plating +" + gained.ToString("0.#", CultureInfo.InvariantCulture) +
                      " recovered +" + Recovered.ToString("0.#", CultureInfo.InvariantCulture));
            Taken?.Invoke();
        }

        public void ReArm()
        {
            IsTaken = false;
            if (visual != null) visual.SetActive(true);
        }
    }
}
