using System.Collections.Generic;
using JamKit.Combat;
using UnityEngine;

namespace JamKit.Doomlike
{
    /// <summary>
    /// The player's guns: one core <see cref="WeaponState"/> per weapon (on a copy of its WeaponDef,
    /// so fire-rate upgrades never touch the asset), shared <see cref="AmmoPools"/> by ammo type,
    /// slots 1-9 plus next/previous, a lower-and-raise swap, and auto-switching when a gun runs dry.
    /// <see cref="Tick"/> returns how many shots the current gun fired; DoomGame resolves them.
    /// </summary>
    public sealed class PlayerArsenal
    {
        public sealed class Gun
        {
            public WeaponAsset asset;
            public WeaponDef live;
            public WeaponState state;
            public int ammo = -1;
            public bool owned;
        }

        const float SwapHalf = 0.16f;

        readonly List<Gun> _guns = new List<Gun>();
        readonly List<float> _shotAges = new List<float>(8);
        AmmoPools _ammo;
        int _current = -1, _pending = -1;
        float _swap; // 0 = raised; counts up while lowering, then down while raising
        bool _lowering;
        float _fireRate = 1f;
        bool _dry;

        void OnDry() => _dry = true;

        public AmmoPools Ammo => _ammo;
        public IReadOnlyList<Gun> Guns => _guns;
        public Gun Current => _current >= 0 && _current < _guns.Count ? _guns[_current] : null;
        public WeaponAsset CurrentAsset => Current != null ? Current.asset : null;
        /// <summary>0 = in hand, 1 = fully lowered (for the view).</summary>
        public float Lowered01 => Mathf.Clamp01(_swap / SwapHalf);
        public bool Swapping => _pending >= 0 || _swap > 0f;
        /// <summary>The old gun is going down (the view switches sprites when this ends).</summary>
        public bool Lowering => _pending >= 0;
        public IReadOnlyList<float> LastShotAges => _shotAges;

        public void Setup(DoomlikeConfig config)
        {
            _guns.Clear();
            _ammo = new AmmoPools(config.ammoTypes);
            var sorted = new List<WeaponAsset>();
            if (config.weapons != null)
                foreach (var w in config.weapons) if (w != null) sorted.Add(w);
            sorted.Sort((a, b) => a.slot != b.slot ? a.slot.CompareTo(b.slot) : a.priority.CompareTo(b.priority));
            foreach (var w in sorted)
            {
                var live = DoomStats.Copy(w.def);
                live.magazineSize = 0;
                live.infiniteReserve = !w.UsesAmmo;
                live.autoReloadOnEmpty = false;
                var state = new WeaponState(live);
                state.DryFired += OnDry;
                _guns.Add(new Gun { asset = w, live = live, state = state, ammo = _ammo.IndexOf(w.ammoType) });
            }
        }

        /// <summary>A new run: only the starting weapons, starting ammo, the best one in hand.</summary>
        public void NewRun(DoomlikeConfig config)
        {
            _ammo.CapacityScale = 1f;
            _ammo.Reset();
            for (int i = 0; i < _guns.Count; i++)
            {
                _guns[i].owned = config.startingWeapons != null && config.startingWeapons.Contains(_guns[i].asset);
                _guns[i].state.DryFired -= OnDry;
                _guns[i].state = new WeaponState(_guns[i].live);
                _guns[i].state.DryFired += OnDry;
            }
            _current = -1;
            _pending = -1;
            _swap = 0f;
            SetFireRate(1f);
            int best = BestWithAmmo();
            _current = best >= 0 ? best : FirstOwned();
        }

        public void SetFireRate(float rate)
        {
            _fireRate = rate;
            foreach (var g in _guns) DoomStats.ApplyFireRate(g.asset.def, g.live, rate);
        }

        public Gun Find(WeaponAsset asset)
        {
            for (int i = 0; i < _guns.Count; i++) if (_guns[i].asset == asset) return _guns[i];
            return null;
        }

        int IndexOf(WeaponAsset asset)
        {
            for (int i = 0; i < _guns.Count; i++) if (_guns[i].asset == asset) return i;
            return -1;
        }

        public bool Owns(WeaponAsset asset) => Find(asset) is Gun g && g.owned;

        /// <summary>Picks up a gun: owned from now on, plus its pickup ammo. Switches to it if it's new
        /// and better. Returns false when nothing was gained (owned and full).</summary>
        public bool Give(WeaponAsset asset, float ammoScale = 1f)
        {
            int i = IndexOf(asset);
            if (i < 0) return false;
            var g = _guns[i];
            bool isNew = !g.owned;
            g.owned = true;
            int added = g.ammo >= 0 ? _ammo.Add(g.ammo, Mathf.RoundToInt(asset.pickupAmmo * ammoScale)) : 0;
            if (isNew && (Current == null || asset.priority > Current.asset.priority)) Switch(i);
            return isNew || added > 0;
        }

        public void GiveAll()
        {
            foreach (var g in _guns) g.owned = true;
            _ammo.FillAll();
        }

        public bool HasAmmo(Gun g) => g != null && (g.ammo < 0 || _ammo.Amount(g.ammo) >= Mathf.Max(1, g.live.ammoPerShot));

        /// <summary>Selects the owned weapon in this slot (pressing a slot key).</summary>
        public bool SelectSlot(int slot)
        {
            for (int i = 0; i < _guns.Count; i++)
                if (_guns[i].owned && _guns[i].asset.slot == slot && i != _current) { Switch(i); return true; }
            return false;
        }

        /// <summary>Next (+1) or previous (-1) owned weapon with ammo (the scroll wheel, the pad's Y).</summary>
        public bool Cycle(int dir)
        {
            if (_guns.Count == 0) return false;
            int start = _pending >= 0 ? _pending : _current;
            for (int step = 1; step <= _guns.Count; step++)
            {
                int i = ((start + dir * step) % _guns.Count + _guns.Count) % _guns.Count;
                if (_guns[i].owned && HasAmmo(_guns[i]) && i != _current) { Switch(i); return true; }
            }
            return false;
        }

        void Switch(int i)
        {
            if (i == _current && _pending < 0) return;
            if (_current < 0) { _current = i; return; }
            _pending = i;
            _lowering = true;
        }

        int FirstOwned()
        {
            for (int i = 0; i < _guns.Count; i++) if (_guns[i].owned) return i;
            return -1;
        }

        int BestWithAmmo()
        {
            int best = -1;
            for (int i = 0; i < _guns.Count; i++)
            {
                var g = _guns[i];
                if (!g.owned || !HasAmmo(g)) continue;
                if (best < 0 || g.asset.priority > _guns[best].asset.priority) best = i;
            }
            return best;
        }

        /// <summary>Advances swaps and the gun. Returns shots fired this frame (their ages are in
        /// <see cref="LastShotAges"/>). <paramref name="dryFired"/>: the trigger clicked on empty.</summary>
        public int Tick(float dt, bool held, bool pressed, out bool dryFired)
        {
            dryFired = false;
            _shotAges.Clear();
            if (_pending >= 0)
            {
                if (_lowering)
                {
                    _swap += dt;
                    if (_swap >= SwapHalf)
                    {
                        _current = _pending;
                        _pending = -1;
                        _lowering = false;
                        _swap = SwapHalf;
                    }
                }
                return 0;
            }
            if (_swap > 0f)
            {
                _swap = Mathf.Max(0f, _swap - dt);
                return 0;
            }
            var g = Current;
            if (g == null) return 0;
            _dry = false;
            _ammo.Feed(g.state, g.ammo);
            int shots = g.state.Tick(dt, held, pressed, _shotAges);
            _ammo.Collect(g.state, g.ammo);
            if (_dry)
            {
                dryFired = true;
                int best = BestWithAmmo();
                if (best >= 0 && best != _current) Switch(best);
            }
            return shots;
        }

        /// <summary>After running dry mid-burst: swap to the best gun that still has ammo.</summary>
        public void SwitchIfEmpty()
        {
            if (_pending >= 0 || HasAmmo(Current)) return;
            int best = BestWithAmmo();
            if (best >= 0 && best != _current) Switch(best);
        }
    }
}
