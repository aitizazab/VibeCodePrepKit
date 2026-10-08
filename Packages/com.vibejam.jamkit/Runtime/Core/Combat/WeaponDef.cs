using System;

namespace JamKit.Combat
{
    /// <summary>Semi: one shot per trigger press. Auto: fires while held. Burst: a press fires
    /// <see cref="WeaponDef.burstCount"/> shots, finishing the burst even if the trigger is released.</summary>
    public enum FireMode { Semi, Auto, Burst }

    /// <summary>
    /// A gun's tuning, shared by every <see cref="WeaponState"/> that uses it, so weapons are data
    /// (one asset per gun) rather than code. Read live, so tweaking it mid-play takes effect at once.
    /// </summary>
    [Serializable]
    public sealed class WeaponDef
    {
        /// <summary>Rounds per magazine. 0 means no magazine: shots draw straight from the reserve
        /// and the gun never reloads (DOOM-style, or a bullet-hell gun with infinite reserve).</summary>
        public int magazineSize = 12;
        /// <summary>Spare ammo at spawn (the magazine starts full on top of this).</summary>
        public int startingReserve = 48;
        /// <summary>Most spare ammo pickups can raise the reserve to. 0 means no cap.</summary>
        public int maxReserve;
        /// <summary>Reserve never runs out: reloads always fill, magazine-less guns never empty.</summary>
        public bool infiniteReserve;
        /// <summary>Ammo each shot costs (a BFG might take 40).</summary>
        public int ammoPerShot = 1;

        public FireMode fireMode = FireMode.Semi;
        /// <summary>Seconds between shots (Auto, Semi), or from a burst's last shot to the next burst.</summary>
        public float fireInterval = 0.15f;
        public int burstCount = 3;
        /// <summary>Seconds between shots inside a burst.</summary>
        public float burstInterval = 0.06f;

        public float reloadTime = 1.2f;
        /// <summary>Starts a reload by itself as soon as the magazine can't pay for a shot.</summary>
        public bool autoReloadOnEmpty = true;
    }
}
