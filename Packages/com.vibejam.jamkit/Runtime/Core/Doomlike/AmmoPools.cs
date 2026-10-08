using System;
using System.Collections.Generic;
using JamKit.Combat;

namespace JamKit.Doomlike
{
    /// <summary>One kind of ammo, shared by every gun that fires it (the pistol and the rivet gun
    /// both eat bullets).</summary>
    [Serializable]
    public class AmmoTypeDef
    {
        public string id = "bullets";
        public string displayName = "Bullets";
        /// <summary>Four letters or fewer for the HUD ("BULL").</summary>
        public string shortName = "BULL";
        public int start = 50;
        public int max = 200;
    }

    /// <summary>
    /// Ammo by type, shared across weapons. The core <see cref="WeaponState"/> keeps a per-gun
    /// reserve, so pooled guns use magazine-less WeaponDefs (magazineSize 0): <see cref="Feed"/> puts
    /// the pool into the gun's reserve before its Tick and <see cref="Collect"/> takes what's left
    /// back after. Index -1 means "no ammo" (fists): give that gun infiniteReserve.
    /// </summary>
    public sealed class AmmoPools
    {
        readonly List<AmmoTypeDef> _types = new List<AmmoTypeDef>();
        int[] _amounts;
        float _capacityScale = 1f;

        public event Action Changed;

        public AmmoPools(IReadOnlyList<AmmoTypeDef> types)
        {
            if (types != null)
                for (int i = 0; i < types.Count; i++)
                    if (types[i] != null) _types.Add(types[i]);
            _amounts = new int[_types.Count];
            Reset();
        }

        public int Count => _types.Count;
        public AmmoTypeDef Type(int index) => _types[index];

        /// <summary>Multiplies every maximum (an "ammo capacity" upgrade). Amounts above the new max
        /// are kept until spent, never cut.</summary>
        public float CapacityScale
        {
            get => _capacityScale;
            set
            {
                _capacityScale = value > 0.01f ? value : 0.01f;
                Changed?.Invoke();
            }
        }

        public int IndexOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < _types.Count; i++)
                if (string.Equals(_types[i].id, id, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        public int Amount(int index) => index >= 0 && index < _amounts.Length ? _amounts[index] : 0;

        public int Max(int index) =>
            index >= 0 && index < _types.Count ? Math.Max(0, (int)Math.Round(_types[index].max * (double)_capacityScale)) : 0;

        public bool IsFull(int index) => index >= 0 && Amount(index) >= Max(index);

        /// <summary>Adds up to the max; returns how much was taken (0 = full: leave the pickup).</summary>
        public int Add(int index, int amount)
        {
            if (index < 0 || index >= _amounts.Length || amount <= 0) return 0;
            int room = Math.Max(0, Max(index) - _amounts[index]);
            int added = Math.Min(room, amount);
            if (added <= 0) return 0;
            _amounts[index] += added;
            Changed?.Invoke();
            return added;
        }

        public bool Has(int index, int amount) => index < 0 || Amount(index) >= amount;

        public bool Spend(int index, int amount)
        {
            if (index < 0) return true;
            if (index >= _amounts.Length || _amounts[index] < amount) return false;
            _amounts[index] -= amount;
            Changed?.Invoke();
            return true;
        }

        public void Set(int index, int amount)
        {
            if (index < 0 || index >= _amounts.Length) return;
            int v = Math.Max(0, amount);
            if (v == _amounts[index]) return;
            _amounts[index] = v;
            Changed?.Invoke();
        }

        /// <summary>Every pool to max (debug "give all", a full-ammo upgrade).</summary>
        public void FillAll()
        {
            for (int i = 0; i < _amounts.Length; i++) _amounts[i] = Math.Max(_amounts[i], Max(i));
            Changed?.Invoke();
        }

        /// <summary>Back to each type's starting amount (a new run).</summary>
        public void Reset()
        {
            for (int i = 0; i < _types.Count; i++) _amounts[i] = Math.Max(0, Math.Min(_types[i].start, Max(i)));
            Changed?.Invoke();
        }

        /// <summary>Before the gun's Tick: its reserve becomes this pool.</summary>
        public void Feed(WeaponState weapon, int index)
        {
            if (weapon == null || index < 0) return;
            weapon.SetReserve(Amount(index));
        }

        /// <summary>After the gun's Tick: the pool becomes what the gun has left.</summary>
        public void Collect(WeaponState weapon, int index)
        {
            if (weapon == null || index < 0) return;
            Set(index, weapon.Reserve);
        }

        /// <summary>Shots the pool can pay for at this cost.</summary>
        public int ShotsLeft(int index, int ammoPerShot)
        {
            if (index < 0) return int.MaxValue;
            return ammoPerShot <= 0 ? int.MaxValue : Amount(index) / ammoPerShot;
        }
    }
}
