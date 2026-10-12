using UnityEngine;

namespace JamKit.Argument
{
    /// <summary>
    /// Points the core at the prose files the creator edits (Resources/ArgumentText/: lena_night2.txt and walt_night1.txt
    /// for the battles' lines, screen.txt for the screen's words; docs/ARGUMENT-PROTOTYPE.md, "The prose files"). Runs before the first
    /// scene; <see cref="Ensure"/> does the same for editor code and tests. The text is read once per play session
    /// (edit the file, press Play again).
    /// </summary>
    public static class ArgumentContent
    {
        public const string Folder = "ArgumentText/";
        public const string ScreenFile = "screen";

        static string _screen, _lena, _walt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _screen = null;
            _lena = null;
            _walt = null;
            Ensure();
        }

        /// <summary>Sets the sources (cheap; safe to call any time).</summary>
        public static void Ensure()
        {
            ArgumentStrings.SharedSource = () => _screen ?? (_screen = Load(ScreenFile));
            DreamLenaDemo.ProseSource = () => _lena ?? (_lena = Load(DreamLenaDemo.ProseFile));
            WaltNight1.ProseSource = () => _walt ?? (_walt = Load(WaltNight1.ProseFile));
        }

        /// <summary>A prose file's text from Resources/ArgumentText/ ("" if it's missing: every line then shows as its id).</summary>
        public static string Load(string name)
        {
            var asset = Resources.Load<TextAsset>(Folder + name);
            if (asset == null)
            {
                Debug.LogWarning("Argument: no prose file Resources/" + Folder + name + ".txt; lines show as their ids.");
                return "";
            }
            return asset.text;
        }
    }
}
