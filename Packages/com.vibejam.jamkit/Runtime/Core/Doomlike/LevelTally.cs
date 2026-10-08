using System;

namespace JamKit.Doomlike
{
    /// <summary>The end-of-level screen's numbers: time, kills, secrets and items, as counts and
    /// percentages (an empty category counts as 100%, so a level with no secrets isn't a "0%").</summary>
    public sealed class LevelTally
    {
        public string levelName = "";
        public float time;
        public float parSeconds;
        public int kills, totalEnemies;
        public int secrets, totalSecrets;
        public int items, totalItems;

        public int KillPercent => Percent(kills, totalEnemies);
        public int SecretPercent => Percent(secrets, totalSecrets);
        public int ItemPercent => Percent(items, totalItems);
        public bool UnderPar => parSeconds > 0f && time <= parSeconds;

        /// <summary>a / b as a whole percentage rounded down, 100 when b is 0, never above 100.</summary>
        public static int Percent(int a, int b)
        {
            if (b <= 0) return 100;
            if (a <= 0) return 0;
            return Math.Min(100, (int)((long)a * 100 / b));
        }

        public void Reset(string name, int enemies, int secretCount, int itemCount, float par)
        {
            levelName = name ?? "";
            time = 0f;
            parSeconds = par;
            kills = secrets = items = 0;
            totalEnemies = Math.Max(0, enemies);
            totalSecrets = Math.Max(0, secretCount);
            totalItems = Math.Max(0, itemCount);
        }

        /// <summary>Adds another level's numbers (the run total on the victory screen).</summary>
        public void Add(LevelTally other)
        {
            if (other == null) return;
            time += other.time;
            parSeconds += other.parSeconds;
            kills += other.kills;
            totalEnemies += other.totalEnemies;
            secrets += other.secrets;
            totalSecrets += other.totalSecrets;
            items += other.items;
            totalItems += other.totalItems;
        }

        public LevelTally Clone() => (LevelTally)MemberwiseClone();
    }
}
