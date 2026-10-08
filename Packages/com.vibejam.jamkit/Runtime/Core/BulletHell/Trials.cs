using System;
using System.Collections.Generic;
using JamKit.Roguelite;

namespace JamKit.BulletHell
{
    /// <summary>What a trial asks for, during its next fight room(s).</summary>
    public enum TrialKind
    {
        /// <summary>Don't get hit (a bullet or a body; a hit the companion or a bomb takes doesn't count).</summary>
        NoHit,
        /// <summary>Don't sprint.</summary>
        NoSprint,
        /// <summary>Clear each room within <c>amount</c> seconds.</summary>
        Timer,
        /// <summary>Own <c>amount</c> stacks of upgrades tagged <c>tag</c> by the end of the window.</summary>
        OwnTagged,
        /// <summary>Graze <c>amount</c> bullets over the window.</summary>
        Graze,
        /// <summary>Don't use a bomb.</summary>
        NoBomb,
    }

    /// <summary>
    /// A condition the player takes on before a fight (a Trial board) or as a wager's stake: authored as
    /// data in BulletHellConfig. The window is the next <see cref="rooms"/> Battle or Elite rooms (boss
    /// fights don't count and don't break it).
    /// </summary>
    [Serializable]
    public class TrialDef
    {
        public string id = "untouched";
        public string displayName = "Untouched";
        /// <summary>One line, what to do: "Clear the next fight without being hit."</summary>
        public string description = "Clear the next fight without being hit.";
        public TrialKind kind = TrialKind.NoHit;
        /// <summary>Seconds (Timer), stacks (OwnTagged) or grazes (Graze).</summary>
        public float amount;
        /// <summary>The upgrade tag OwnTagged counts ("gun", "bomb", "move"...).</summary>
        public string tag = "";
        /// <summary>Fight rooms the trial lasts (at least 1).</summary>
        public int rooms = 1;
        /// <summary>Only offered if the player owns an upgrade with this effect id or upgrade id (empty = always):
        /// a no-bomb trial means nothing without a Bomb Bay.</summary>
        public string requires = "";

        public void Validate(string where, List<string> errors)
        {
            if (string.IsNullOrEmpty(id)) errors.Add(where + ": no id.");
            if (string.IsNullOrEmpty(displayName) || string.IsNullOrEmpty(description)) errors.Add(where + ": needs a name and a description.");
            if (rooms < 1 || rooms > 4) errors.Add(where + ": rooms must be 1 to 4.");
            switch (kind)
            {
                case TrialKind.Timer:
                    if (amount < 10f) errors.Add(where + ": a timer under 10 s can't be met.");
                    break;
                case TrialKind.OwnTagged:
                    if (amount < 1f) errors.Add(where + ": own at least 1.");
                    if (string.IsNullOrEmpty(tag)) errors.Add(where + ": no tag to count.");
                    break;
                case TrialKind.Graze:
                    if (amount < 1f) errors.Add(where + ": graze at least 1 bullet.");
                    break;
            }
        }

        /// <summary>The player can take it (its <see cref="requires"/> is owned).</summary>
        public bool Available(UpgradeInventory inventory) =>
            string.IsNullOrEmpty(requires) || (inventory != null && (inventory.Has(requires) || inventory.HasEffect(requires)));
    }

    public enum TrialState { Running, Passed, Failed }

    /// <summary>
    /// One running trial: the game reports hits, sprinting, bombs and grazes as they happen and calls
    /// <see cref="RoomCleared"/> at the end of each fight room. Pure, so it's tested without Unity.
    /// </summary>
    public sealed class TrialTracker
    {
        readonly TrialDef _def;
        int _roomsLeft;
        float _grazes, _roomTime;
        TrialState _state = TrialState.Running;
        string _failReason = "";

        public TrialTracker(TrialDef def)
        {
            _def = def ?? throw new ArgumentNullException(nameof(def));
            _roomsLeft = Math.Max(1, def.rooms);
        }

        public TrialDef Def => _def;
        public TrialState State => _state;
        public bool Running => _state == TrialState.Running;
        public int RoomsLeft => _roomsLeft;
        public float Grazes => _grazes;
        public string FailReason => _failReason;
        /// <summary>A wager or a trial board: what the success pays (set by the game).</summary>
        public RewardSource reward = RewardSource.Trial;
        /// <summary>A curse modifier's source id to lift when the trial ends (wagers), or null.</summary>
        public string curseSource;

        /// <summary>A fight room starts (its clock restarts).</summary>
        public void RoomStarted() { _roomTime = 0f; }

        /// <summary>Time passing in a fight room.</summary>
        public void Tick(float dt)
        {
            if (!Running) return;
            _roomTime += Math.Max(0f, dt);
        }

        public void PlayerHit()
        {
            if (Running && _def.kind == TrialKind.NoHit) Fail("You were hit.");
        }

        public void Sprinted()
        {
            if (Running && _def.kind == TrialKind.NoSprint) Fail("You sprinted.");
        }

        public void BombUsed()
        {
            if (Running && _def.kind == TrialKind.NoBomb) Fail("You used a bomb.");
        }

        public void Grazed(int n)
        {
            if (Running && n > 0) _grazes += n;
        }

        /// <summary>A Battle or Elite room was cleared. Returns the state after it (Passed on the window's last
        /// room if nothing failed; OwnTagged and Graze pass as soon as they're met).</summary>
        public TrialState RoomCleared(UpgradeInventory inventory)
        {
            if (!Running) return _state;
            if (_def.kind == TrialKind.Timer && _roomTime > _def.amount)
            {
                Fail("Too slow: " + Math.Round(_roomTime) + " s.");
                return _state;
            }
            _roomsLeft--;
            bool met = Met(inventory);
            if (met && (_def.kind == TrialKind.OwnTagged || _def.kind == TrialKind.Graze)) _state = TrialState.Passed;
            else if (_roomsLeft <= 0)
            {
                if (met) _state = TrialState.Passed;
                else Fail(_def.kind == TrialKind.Graze ? "Only " + (int)_grazes + " grazes." : "Not enough " + _def.tag + " upgrades.");
            }
            return _state;
        }

        bool Met(UpgradeInventory inventory)
        {
            switch (_def.kind)
            {
                case TrialKind.OwnTagged: return TaggedStacks(inventory, _def.tag) >= _def.amount;
                case TrialKind.Graze: return _grazes >= _def.amount;
                default: return true; // the fail-on-event kinds pass unless something failed them
            }
        }

        void Fail(string why)
        {
            _state = TrialState.Failed;
            _failReason = why;
        }

        /// <summary>The trial's line for the HUD: "Untouched: 1 fight left".</summary>
        public string Status()
        {
            switch (_state)
            {
                case TrialState.Passed: return _def.displayName + ": done!";
                case TrialState.Failed: return _def.displayName + ": failed";
            }
            string left = _roomsLeft == 1 ? "1 fight left" : _roomsLeft + " fights left";
            switch (_def.kind)
            {
                case TrialKind.Graze: return _def.displayName + ": " + (int)_grazes + "/" + (int)_def.amount + ", " + left;
                case TrialKind.Timer: return _def.displayName + ": " + (int)_roomTime + "/" + (int)_def.amount + " s";
                default: return _def.displayName + ": " + left;
            }
        }

        /// <summary>Stacks of owned upgrades carrying <paramref name="tag"/>.</summary>
        public static int TaggedStacks(UpgradeInventory inventory, string tag)
        {
            if (inventory == null || string.IsNullOrEmpty(tag)) return 0;
            int n = 0;
            var owned = inventory.Owned;
            for (int i = 0; i < owned.Count; i++)
            {
                var d = owned[i];
                if (d.tags != null && d.tags.Contains(tag)) n += inventory.Count(d.id);
            }
            return n;
        }
    }

    /// <summary>What a wager costs up front.</summary>
    public enum WagerStake { Life, Coins, Curse }

    /// <summary>
    /// A risky encounter: pay a stake now (a life, coins, or a curse that lasts until the condition
    /// ends), win a reward if the condition is met. Data in BulletHellConfig.
    /// </summary>
    [Serializable]
    public class WagerDef
    {
        public string id = "blood_price";
        public string displayName = "Blood Price";
        public string description = "Lose a life now. Clear the next fight without being hit: choose an epic.";
        public WagerStake stake = WagerStake.Life;
        /// <summary>Lives or coins staked.</summary>
        public int amount = 1;
        /// <summary>The curse (Curse stakes): a stat change that lasts until the condition ends.</summary>
        public StatModifier curse = new StatModifier(ShmupStats.MoveSpeed, StatOp.PercentAdd, -0.2f);
        public TrialDef condition = new TrialDef();
        public RewardSource reward = RewardSource.Wager;

        public void Validate(string where, List<string> errors)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(displayName) || string.IsNullOrEmpty(description))
                errors.Add(where + ": needs an id, a name and a description.");
            if (stake != WagerStake.Curse && amount < 1) errors.Add(where + ": stake at least 1.");
            if (stake == WagerStake.Curse && (curse == null || Array.IndexOf(ShmupStats.All, curse.stat) < 0))
                errors.Add(where + ": the curse needs a known stat.");
            if (condition == null) errors.Add(where + ": no condition.");
            else condition.Validate(where + " condition", errors);
        }
    }
}
