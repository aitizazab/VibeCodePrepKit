using JamKit.Movement;
using UnityEngine;

namespace JamKit.MovementLab
{
    /// <summary>
    /// Reads the developer's shipped records (Data/DeveloperRecords.json, core <see cref="DeveloperRecords"/>): the
    /// creator's best Gauntlet and dream tool runs, with their splits and the Gauntlet ghost. Parsed once per asset.
    /// </summary>
    public static class DeveloperRecordsAsset
    {
        public const string AssetPath = "Assets/JamKit/Templates/MovementLab/Data/DeveloperRecords.json";

        static TextAsset _parsedFrom;
        static DeveloperRecords _parsed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _parsedFrom = null;
            _parsed = null;
        }

        public static DeveloperRecords Parse(TextAsset asset)
        {
            if (asset == null) return null;
            if (asset == _parsedFrom) return _parsed;
            _parsedFrom = asset;
            try { _parsed = JsonUtility.FromJson<DeveloperRecords>(asset.text); }
            catch (System.Exception e)
            {
                Debug.LogWarning("Movement Lab: the developer's records couldn't be read. " + e.Message);
                _parsed = null;
            }
            return _parsed;
        }

        static GauntletRecord Valid(GauntletRecord r) => r != null && r.best > 0f ? r : null;

        public static GauntletRecord Gauntlet(TextAsset asset) => Valid(Parse(asset)?.records?.movementlab_gauntlet);
        public static GauntletRecord DreamTool(TextAsset asset) => Valid(Parse(asset)?.records?.movementlab_dreamtool);
    }
}
