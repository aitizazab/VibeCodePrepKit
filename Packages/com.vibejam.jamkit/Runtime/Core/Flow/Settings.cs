using System;

namespace JamKit.Flow
{
    public enum Difficulty { Story, Normal, Hard }

    /// <summary>
    /// Player options, saved inside <see cref="SaveData"/>. The assist fields are set by
    /// <see cref="ApplyDifficulty"/> as a preset but stay ordinary saved fields, so an accessibility
    /// menu can tweak one (slower bullets on Normal) without inventing a fourth difficulty.
    /// Game code reads the assists, never the difficulty enum, so there's one place to tune.
    /// </summary>
    [Serializable]
    public class Settings
    {
        public float masterVolume = 0.8f;
        public float musicVolume = 0.7f;
        public float sfxVolume = 0.8f;
        public float mouseSensitivity = 1f;
        public bool invertY;
        public bool fullscreen = true;
        /// <summary>0 turns camera shake and flashes' shake off (accessibility), 1 is full.</summary>
        public float screenShake = 1f;
        public Difficulty difficulty = Difficulty.Normal;

        public float enemyBulletSpeedScale = 1f;
        public float enemyDensityScale = 1f;
        public float damageTakenScale = 1f;
        public int extraLives = 1;
        /// <summary>Aim assist strength, 0 (off) .. 1 (strong). Mainly for gamepad aiming.</summary>
        public float aimAssist = 0.25f;

        public const float MinSensitivity = 0.05f, MaxSensitivity = 10f;
        public const float MinScale = 0.1f, MaxScale = 3f;
        public const int MaxExtraLives = 99;

        /// <summary>Set the assists to the preset for <see cref="difficulty"/>.</summary>
        public void ApplyDifficulty()
        {
            switch (difficulty)
            {
                case Difficulty.Story:
                    enemyBulletSpeedScale = 0.7f; enemyDensityScale = 0.6f; damageTakenScale = 0.5f;
                    extraLives = 3; aimAssist = 0.6f;
                    break;
                case Difficulty.Hard:
                    enemyBulletSpeedScale = 1.2f; enemyDensityScale = 1.3f; damageTakenScale = 1.5f;
                    extraLives = 0; aimAssist = 0f;
                    break;
                default:
                    enemyBulletSpeedScale = 1f; enemyDensityScale = 1f; damageTakenScale = 1f;
                    extraLives = 1; aimAssist = 0.25f;
                    break;
            }
        }

        public void SetDifficulty(Difficulty d)
        {
            difficulty = d;
            ApplyDifficulty();
        }

        /// <summary>
        /// Repair values from old saves or hand-edited files: NaN/infinite go back to the default,
        /// everything else is clamped into range. Returns true if anything changed.
        /// </summary>
        public bool Clamp()
        {
            var d = new Settings();
            bool changed = false;
            masterVolume = Fix(masterVolume, 0f, 1f, d.masterVolume, ref changed);
            musicVolume = Fix(musicVolume, 0f, 1f, d.musicVolume, ref changed);
            sfxVolume = Fix(sfxVolume, 0f, 1f, d.sfxVolume, ref changed);
            mouseSensitivity = Fix(mouseSensitivity, MinSensitivity, MaxSensitivity, d.mouseSensitivity, ref changed);
            screenShake = Fix(screenShake, 0f, 1f, d.screenShake, ref changed);
            if (!Enum.IsDefined(typeof(Difficulty), difficulty))
            {
                difficulty = Difficulty.Normal;
                ApplyDifficulty();
                changed = true;
            }
            enemyBulletSpeedScale = Fix(enemyBulletSpeedScale, MinScale, MaxScale, d.enemyBulletSpeedScale, ref changed);
            enemyDensityScale = Fix(enemyDensityScale, MinScale, MaxScale, d.enemyDensityScale, ref changed);
            damageTakenScale = Fix(damageTakenScale, MinScale, MaxScale, d.damageTakenScale, ref changed);
            aimAssist = Fix(aimAssist, 0f, 1f, d.aimAssist, ref changed);
            int lives = extraLives < 0 ? 0 : extraLives > MaxExtraLives ? MaxExtraLives : extraLives;
            if (lives != extraLives) { extraLives = lives; changed = true; }
            return changed;
        }

        static float Fix(float v, float min, float max, float fallback, ref bool changed)
        {
            float r = float.IsNaN(v) || float.IsInfinity(v) ? fallback : v < min ? min : v > max ? max : v;
            if (!r.Equals(v)) changed = true;
            return r;
        }

        public Settings Clone() => (Settings)MemberwiseClone();
    }
}
