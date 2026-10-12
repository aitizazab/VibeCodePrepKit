using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace JamKit.Look.EditorTools
{
    /// <summary>
    /// The three post-processing profiles (Look/Profiles/), built in code so every value is reviewable here:
    /// <list type="bullet">
    /// <item><b>VH_GoldenHour</b>: the hill. ACES; warm white balance; split toning (cool shadows, warm highlights, not orange
    /// soup); bloom on highlights; vignette and grain; the atmosphere (haze + sun shafts).</item>
    /// <item><b>VH_HouseInterior</b>: the Lasname house at night-in-the-dream / evening: warmer, softer, more bloom on lamps.</item>
    /// <item><b>VH_HospitalDay</b>: the ward: cool daylight, a faint fluorescent green in the shadows, lower saturation.</item>
    /// </list>
    /// </summary>
    public static class VhLookProfiles
    {
        public const string Dir = VhLookLibrary.LookRoot + "Profiles/";
        public const string GoldenHour = Dir + "VH_GoldenHour.asset";
        public const string HouseInterior = Dir + "VH_HouseInterior.asset";
        public const string HospitalDay = Dir + "VH_HospitalDay.asset";

        [MenuItem("JamKit/Look/Rebuild the Post Profiles")]
        public static void BuildAll()
        {
            BuildGoldenHour(VhLookSky.Default);
            BuildHouseInterior();
            BuildHospitalDay();
            AssetDatabase.SaveAssets();
        }

        static VolumeProfile Profile(string path)
        {
            System.IO.Directory.CreateDirectory(Dir);
            var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (p == null)
            {
                p = ScriptableObject.CreateInstance<VolumeProfile>();
                { VhLookLibrary.EnsureAssetFolder(path); AssetDatabase.CreateAsset(p, path); }
            }
            return p;
        }

        static T Comp<T>(VolumeProfile p) where T : VolumeComponent
        {
            if (!p.TryGet<T>(out var c))
            {
                c = p.Add<T>(false);
                c.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(c, p);
            }
            c.active = true;
            return c;
        }

        static Vector4 Tone(float r, float g, float b, float w = 0f) => new Vector4(r, g, b, w);

        public static VolumeProfile BuildGoldenHour(string sky)
        {
            var p = Profile(GoldenHour);
            var tm = Comp<Tonemapping>(p);
            tm.mode.Override(TonemappingMode.ACES);

            var ca = Comp<ColorAdjustments>(p);
            // Pass 4 (round 3, sampled: concrete orange (117, 83, 59), asphalt red-brown, warm shadows, the lawn's blue clipped, a
            // grey zenith): the warmth now comes from the sun alone; the grade is near-neutral, cools the shadows, lifts the
            // ground a third of a stop and takes saturation down.
            ca.postExposure.Override(0.3f);
            ca.contrast.Override(5f);
            ca.saturation.Override(-8f);
            ca.colorFilter.Override(Color.white);

            var wb = Comp<WhiteBalance>(p);
            wb.temperature.Override(2f);
            wb.tint.Override(0f);

            var st = Comp<SplitToning>(p);
            // Round 1: lilac-grey lawns and roads in shade. Half the lilac, and a warm lift in the shadows (bounce off the
            // sunlit ground).
            st.shadows.Override(new Color(0.43f, 0.47f, 0.57f));    // shadows lean cool (sky-lit)
            st.highlights.Override(new Color(0.54f, 0.51f, 0.47f)); // a light warm touch on highlights
            st.balance.Override(0f);

            var smh = Comp<ShadowsMidtonesHighlights>(p);
            smh.shadows.Override(Tone(0.97f, 1.0f, 1.05f, 0.02f));
            smh.midtones.Override(Tone(1f, 1f, 1f, 0f));
            smh.highlights.Override(Tone(1.02f, 1.0f, 0.98f, 0f));

            var bloom = Comp<Bloom>(p);
            // Pass 3: less bloom (round 2's cream wash sunward), no HQ filtering and a quarter-resolution start (the Perf
            // agent's list: 0.2-0.5 ms).
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.7f);
            bloom.scatter.Override(0.62f);
            bloom.tint.Override(new Color(1f, 0.95f, 0.88f));
            bloom.highQualityFiltering.Override(false);
            bloom.downscale.Override(BloomDownscaleMode.Quarter);

            var vig = Comp<Vignette>(p);
            vig.intensity.Override(0.22f);
            vig.smoothness.Override(0.45f);
            vig.color.Override(new Color(0.06f, 0.05f, 0.05f));

            var grain = Comp<FilmGrain>(p);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.12f);
            grain.response.Override(0.8f);

            var atmo = Comp<VhAtmosphere>(p);
            VhLookSky.HazeColors(sky, out var away, out var towards);
            // Pass 3 (round 2: "near ground washed, far hills sharp: backwards"): the near haze halved and starting later, the
            // far haze carrying the distance, so far is always paler than near, the ring included. (Round 2's captures also
            // had no haze on distant geometry at all: see VhAtmosphereFeature's depth binding.)
            atmo.hazeDensity.Override(0.0016f);
            atmo.hazeBaseHeight.Override(0f);
            atmo.hazeHeightFalloff.Override(0.02f);
            atmo.hazeStart.Override(25f);
            atmo.hazeColor.Override(away);
            atmo.hazeSunColor.Override(towards);
            atmo.hazeSunFocus.Override(5f);
            atmo.hazeMaxOpacity.Override(0.6f);
            // Far haze (round 1: "the far hill is as sharp and saturated as the near"): grows with distance from 150 m.
            atmo.farHazeDensity.Override(0.0034f);
            atmo.farHazeStart.Override(120f);
            atmo.farHazeHeightFalloff.Override(0.0024f);
            atmo.farHazeMaxOpacity.Override(0.95f);
            atmo.shaftIntensity.Override(0.6f);
            atmo.shaftTint.Override(new Color(1f, 0.8f, 0.55f));
            atmo.shaftThreshold.Override(1.1f);
            atmo.shaftLength.Override(0.75f);
            atmo.shaftRadius.Override(0.6f);

            EditorUtility.SetDirty(p);
            return p;
        }

        public static VolumeProfile BuildHouseInterior()
        {
            var p = Profile(HouseInterior);
            Comp<Tonemapping>(p).mode.Override(TonemappingMode.ACES);
            var ca = Comp<ColorAdjustments>(p);
            ca.postExposure.Override(0.25f);
            ca.contrast.Override(6f);
            ca.saturation.Override(6f);
            ca.colorFilter.Override(new Color(1f, 0.96f, 0.9f));
            var wb = Comp<WhiteBalance>(p);
            wb.temperature.Override(20f);
            wb.tint.Override(4f);
            var st = Comp<SplitToning>(p);
            st.shadows.Override(new Color(0.46f, 0.47f, 0.56f));
            st.highlights.Override(new Color(0.62f, 0.52f, 0.42f));
            st.balance.Override(-5f);
            var bloom = Comp<Bloom>(p);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.8f);
            bloom.scatter.Override(0.75f);
            bloom.tint.Override(new Color(1f, 0.88f, 0.74f));
            bloom.highQualityFiltering.Override(false);
            bloom.downscale.Override(BloomDownscaleMode.Quarter);
            var vig = Comp<Vignette>(p);
            vig.intensity.Override(0.28f);
            vig.smoothness.Override(0.5f);
            vig.color.Override(new Color(0.1f, 0.05f, 0.03f));
            var grain = Comp<FilmGrain>(p);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.15f);
            var atmo = Comp<VhAtmosphere>(p);
            atmo.hazeDensity.Override(0f);
            atmo.farHazeDensity.Override(0f);
            atmo.shaftIntensity.Override(0f);
            EditorUtility.SetDirty(p);
            return p;
        }

        public static VolumeProfile BuildHospitalDay()
        {
            var p = Profile(HospitalDay);
            Comp<Tonemapping>(p).mode.Override(TonemappingMode.ACES);
            var ca = Comp<ColorAdjustments>(p);
            ca.postExposure.Override(0.2f);
            ca.contrast.Override(8f);
            ca.saturation.Override(-14f);
            ca.colorFilter.Override(new Color(0.96f, 0.99f, 1f));
            var wb = Comp<WhiteBalance>(p);
            wb.temperature.Override(-12f);
            wb.tint.Override(-5f);
            var smh = Comp<ShadowsMidtonesHighlights>(p);
            smh.shadows.Override(Tone(0.96f, 1.02f, 1.0f, -0.02f));   // a faint fluorescent green-cyan in the shadows
            smh.highlights.Override(Tone(1f, 1f, 1.02f, 0.02f));
            var bloom = Comp<Bloom>(p);
            bloom.threshold.Override(1.15f);
            bloom.intensity.Override(0.4f);
            bloom.scatter.Override(0.65f);
            bloom.highQualityFiltering.Override(false);
            bloom.downscale.Override(BloomDownscaleMode.Quarter);
            var vig = Comp<Vignette>(p);
            vig.intensity.Override(0.18f);
            vig.smoothness.Override(0.4f);
            var grain = Comp<FilmGrain>(p);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.1f);
            var atmo = Comp<VhAtmosphere>(p);
            atmo.hazeDensity.Override(0f);
            atmo.farHazeDensity.Override(0f);
            atmo.shaftIntensity.Override(0f);
            EditorUtility.SetDirty(p);
            return p;
        }
    }
}
