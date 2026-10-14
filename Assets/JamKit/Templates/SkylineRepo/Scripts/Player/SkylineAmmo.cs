using System;
using System.Collections.Generic;
using System.Globalization;
using JamKit.Skyline;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// The special-ammo claims (docs/SKYLINE-HUD.md "Ammo"; plan task A2): a kill leaves a claim on its wreck, and the claim is
    /// paid into the <see cref="SpecialAmmoLedger"/> when the wreck is stripped, together with the plating. One claim a wreck,
    /// paid once. Plain bookkeeping with no engine calls, so an EditMode test drives it with any object as the key.
    /// </summary>
    public sealed class SkylineAmmoClaims
    {
        struct Claim
        {
            public object key;
            public bool special;
            public string gun;
        }

        readonly SpecialAmmoLedger _ledger;
        readonly List<Claim> _claims = new List<Claim>();

        public SkylineAmmoClaims(SpecialAmmoLedger ledger)
        {
            _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        }

        public int Count => _claims.Count;

        public object KeyAt(int index) => _claims[index].key;

        int IndexOf(object key)
        {
            for (int i = 0; i < _claims.Count; i++) if (ReferenceEquals(_claims[i].key, key)) return i;
            return -1;
        }

        public bool Has(object key) => key != null && IndexOf(key) >= 0;

        /// <summary>Records that <paramref name="key"/> (the wreck) carries the ammo of a kill by <paramref name="gun"/> (a
        /// special gun's kill counts toward the 1-in-3, any other gun's pays 1). False, and no claim, for a null key, a gun-less
        /// kill (nothing the player shot) or a wreck that already has a claim.</summary>
        public bool Add(object key, GunDef gun)
        {
            if (key == null || gun == null || IndexOf(key) >= 0) return false;
            _claims.Add(new Claim { key = key, special = gun.special, gun = gun.id });
            return true;
        }

        /// <summary>Pays the claim on <paramref name="key"/> into the ledger and removes it. Returns the shots it added (0 or
        /// more), or -1 when there is no claim (never claimed, or already paid). <paramref name="gunId"/> is the killing gun and
        /// <paramref name="special"/> whether it was a special one.</summary>
        public int Collect(object key, out string gunId, out bool special)
        {
            gunId = null;
            special = false;
            int i = key == null ? -1 : IndexOf(key);
            if (i < 0) return -1;
            var c = _claims[i];
            _claims.RemoveAt(i);
            gunId = c.gun;
            special = c.special;
            return _ledger.Credit(c.special);
        }

        /// <summary>Drops the claim on <paramref name="key"/> unpaid (its wreck went stale or was removed).</summary>
        public void Forget(object key)
        {
            int i = key == null ? -1 : IndexOf(key);
            if (i >= 0) _claims.RemoveAt(i);
        }

        public void Clear() => _claims.Clear();
    }

    /// <summary>
    /// Special ammo in play (plan task A2; docs/SKYLINE-HUD.md "Ammo"). The Writ has no reserve; the Breacher's reserve is
    /// <see cref="Ledger"/>. A kill by <see cref="SkylineBot.Killed"/> puts a claim on the dead bot's wreck; the touch that
    /// strips the wreck's plating (<see cref="SkylineWreck"/>, the strip window <c>Wreck.freshSeconds</c>) pays it: 1 shot for
    /// a kill by a primary gun, 1 shot per 3 kills by special guns. A wreck that goes stale or is removed unstripped pays
    /// nothing. Logs <c>SKYLINE_AMMO +n (kill by &lt;gun&gt;)</c> on a payment, and raises <see cref="SpecialGained"/> when
    /// shots were added (the green ammo flash listens).
    /// <para>The Breacher's gun object is replaced when the player restarts (a new <see cref="Loadout"/>), so each frame the
    /// component re-binds the ledger to whichever loadout the player holds. The director calls <see cref="ResetRun"/> on a wreck.</para>
    /// </summary>
    public sealed class SkylineAmmo : MonoBehaviour
    {
        public static SkylineAmmo Instance { get; private set; }

        /// <summary>Raised once per payment that added shots (the shots added, always above 0), from the main thread.</summary>
        public static event Action<int> SpecialGained;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            SpecialGained = null;
        }

        public SkylinePlayer player;

        SpecialAmmoLedger _ledger;
        SkylineAmmoClaims _claims;
        Loadout _bound;
        bool _subscribed;

        /// <summary>The special guns' shared reserve.</summary>
        public SpecialAmmoLedger Ledger => _ledger ?? (_ledger = MakeLedger());

        public SkylineAmmoClaims Claims => _claims ?? (_claims = new SkylineAmmoClaims(Ledger));

        SpecialAmmoLedger MakeLedger()
        {
            var l = new SpecialAmmoLedger();
            l.Gained += OnGained;
            return l;
        }

        /// <summary>The one ammo component for <paramref name="player"/>'s scene (made on first call).</summary>
        public static SkylineAmmo Ensure(SkylinePlayer player)
        {
            if (Instance == null)
            {
                var go = new GameObject("SkylineAmmo");
                Instance = go.AddComponent<SkylineAmmo>();
            }
            if (player != null) Instance.player = player;
            return Instance;
        }

        void Awake()
        {
            if (Instance == null) Instance = this;
        }

        void OnEnable()
        {
            if (_subscribed) return;
            _subscribed = true;
            SkylineBot.Killed += OnKilled;
        }

        void OnDisable()
        {
            if (!_subscribed) return;
            _subscribed = false;
            SkylineBot.Killed -= OnKilled;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnGained(int shots) => SpecialGained?.Invoke(shots);

        void OnKilled(SkylineBot bot, GunDef gun)
        {
            if (gun == null) return;
            var wreck = bot != null ? bot.WreckObject : null;
            if (wreck == null)
            {
                // nothing to strip: the ammo is paid on the kill itself
                Log(Ledger.Credit(gun.special), gun.id, gun.special);
                return;
            }
            RegisterKill(wreck, gun);
        }

        /// <summary>A kill by <paramref name="gun"/> whose ammo is paid when <paramref name="key"/> (the wreck) is stripped.</summary>
        public bool RegisterKill(object key, GunDef gun) => Claims.Add(key, gun);

        void Update()
        {
            if (player == null) player = FindAnyObjectByType<SkylinePlayer>();
            if (player != null) SyncLoadout(player.Loadout);
            Poll(Time.time);
        }

        /// <summary>Pays the claims of wrecks that have been stripped, and drops those whose strip window ran out or that no
        /// longer exist. Once a frame.</summary>
        public void Poll(float now)
        {
            var claims = Claims;
            for (int i = claims.Count - 1; i >= 0; i--)
            {
                var wreck = claims.KeyAt(i) as SkylineWreck;
                if (wreck == null || wreck.Data == null)
                {
                    claims.Forget(claims.KeyAt(i));
                    continue;
                }
                if (wreck.Data.stripped)
                {
                    int added = claims.Collect(wreck, out string gunId, out bool special);
                    if (added >= 0) Log(added, gunId, special);
                }
                else if (!wreck.Data.Fresh(now)) claims.Forget(wreck);   // stale: the pickup is lost with the plating
            }
        }

        void Log(int added, string gunId, bool special)
        {
            if (added < 0) return;
            string by = " (kill by " + gunId;
            if (added == 0 && special)
                by += ", " + Ledger.KillProgress.ToString(CultureInfo.InvariantCulture) + "/" + Ledger.Def.killsPerShot.ToString(CultureInfo.InvariantCulture) + " toward the next shot";
            else if (added == 0) by += ", reserve full";
            Debug.Log("SKYLINE_AMMO +" + added.ToString(CultureInfo.InvariantCulture) + by + ") reserve " + Ledger.Shots.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>Makes every special gun of <paramref name="loadout"/> draw its reserve from the ledger; a new loadout
        /// (the player restarted) is bound afresh and the old guns are let go.</summary>
        public void SyncLoadout(Loadout loadout)
        {
            if (ReferenceEquals(loadout, _bound)) return;
            if (_bound != null)
                for (int i = 0; i < _bound.Count; i++)
                    if (_bound[i].Def.special) Ledger.Unbind(_bound[i]);
            _bound = loadout;
            if (loadout == null) return;
            for (int i = 0; i < loadout.Count; i++)
                if (loadout[i].Def.special) Ledger.Bind(loadout[i]);
        }

        /// <summary>A new run (the director calls it on a wreck): the reserve back to its starting shots, the kill counter to
        /// zero, and every unpaid claim dropped.</summary>
        public void ResetRun()
        {
            Claims.Clear();
            Ledger.Reset();
            Debug.Log("SKYLINE_AMMO reset reserve " + Ledger.Shots.ToString(CultureInfo.InvariantCulture));
        }
    }
}
