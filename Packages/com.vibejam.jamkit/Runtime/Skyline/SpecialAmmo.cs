using System;
using System.Collections.Generic;
using JamKit.Combat;

namespace JamKit.Skyline
{
    /// <summary>The special-ammo economy's numbers (docs/SKYLINE-HUD.md "Ammo"; the creator's starting values, to be balanced
    /// together in play): a wreck from a kill by a primary weapon gives <see cref="perPrimaryKill"/> special shots; kills by
    /// special weapons give 1 shot per <see cref="killsPerShot"/> kills; the run starts with <see cref="start"/> shots in
    /// reserve and the reserve holds at most <see cref="max"/>.</summary>
    public sealed class SpecialAmmoDef { public int perPrimaryKill = 1, killsPerShot = 3, start = 2, max = 8; }

    /// <summary>
    /// The special weapons' shared reserve (a primary weapon, the Writ, has none: infinite reserve). <see cref="Shots"/> is
    /// the Breacher's reserve, the spare shells outside its magazine. Call <see cref="Credit"/> once per kill (the
    /// kill's wreck is what carries the ammo). Bind a special <see cref="SkylineGun"/> with <see cref="Bind"/> and its
    /// WeaponState reserve follows the ledger (gains, spends, Reset), while the ledger follows the gun's reloads (a reload
    /// moves shells from the reserve into the magazine). Shots over <see cref="SpecialAmmoDef.max"/> are lost, not banked.
    /// </summary>
    public sealed class SpecialAmmoLedger
    {
        sealed class Binding
        {
            public SkylineGun gun;
            public Action reloaded;
        }

        readonly SpecialAmmoDef _def;
        readonly List<Binding> _bound = new List<Binding>();
        int _shots, _specialKills;

        public SpecialAmmoLedger(SpecialAmmoDef def = null)
        {
            _def = def ?? new SpecialAmmoDef();
            _shots = Clamp(_def.start);
        }

        public SpecialAmmoDef Def => _def;

        /// <summary>The special shots in reserve now (0 to <see cref="SpecialAmmoDef.max"/>).</summary>
        public int Shots => _shots;

        /// <summary>Special kills counted toward the next shot (0 to killsPerShot - 1). It persists across wrecks until
        /// <see cref="Reset"/>.</summary>
        public int KillProgress => _specialKills;

        /// <summary>Raised with the number of shots a kill added (always more than 0), after the reserve is updated.</summary>
        public event Action<int> Gained;

        /// <summary>Credits one kill. <paramref name="killedWithSpecial"/>: the killing gun was a special weapon. Returns the
        /// shots this kill added (0 for the first and second special kill, or when the reserve is full).</summary>
        public int Credit(bool killedWithSpecial)
        {
            int earned;
            if (killedWithSpecial)
            {
                int per = _def.killsPerShot < 1 ? 1 : _def.killsPerShot;
                _specialKills++;
                if (_specialKills >= per) { _specialKills = 0; earned = 1; }
                else earned = 0;
            }
            else earned = _def.perPrimaryKill < 0 ? 0 : _def.perPrimaryKill;
            int room = Math.Max(0, _def.max - _shots);
            int added = Math.Min(earned, room);   // the excess is ignored, not banked
            if (added <= 0) return 0;
            _shots += added;
            Push();
            Gained?.Invoke(added);
            return added;
        }

        /// <summary>Spends one shot from the reserve. False when it is empty.</summary>
        public bool TryConsume()
        {
            if (_shots <= 0) return false;
            _shots--;
            Push();
            return true;
        }

        /// <summary>A new run: the reserve back to <see cref="SpecialAmmoDef.start"/> and the kill counter to zero.</summary>
        public void Reset()
        {
            _shots = Clamp(_def.start);
            _specialKills = 0;
            Push();
        }

        /// <summary>Makes <paramref name="gun"/> (a special gun) draw its reserve from this ledger: its WeaponState reserve is
        /// set to <see cref="Shots"/> now and after every change, and the ledger takes back whatever a reload leaves in it.
        /// Bind each special gun once; a second call for the same gun does nothing.</summary>
        public void Bind(SkylineGun gun)
        {
            if (gun == null) throw new ArgumentNullException(nameof(gun));
            if (!gun.Def.special) throw new ArgumentException(gun.Def.id + " is not a special gun: it has an infinite reserve", nameof(gun));
            for (int i = 0; i < _bound.Count; i++) if (_bound[i].gun == gun) return;
            var b = new Binding { gun = gun };
            b.reloaded = () => { _shots = Clamp(gun.State.Reserve); Push(); };
            gun.State.ReloadCompleted += b.reloaded;
            _bound.Add(b);
            gun.State.SetReserve(_shots);
        }

        /// <summary>Stops feeding <paramref name="gun"/>. Its reserve stays as it is.</summary>
        public void Unbind(SkylineGun gun)
        {
            for (int i = 0; i < _bound.Count; i++)
                if (_bound[i].gun == gun)
                {
                    gun.State.ReloadCompleted -= _bound[i].reloaded;
                    _bound.RemoveAt(i);
                    return;
                }
        }

        int Clamp(int shots) => Math.Max(0, Math.Min(_def.max, shots));

        void Push()
        {
            for (int i = 0; i < _bound.Count; i++) _bound[i].gun.State.SetReserve(_shots);
        }
    }
}
