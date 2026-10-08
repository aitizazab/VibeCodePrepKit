using JamKit.EditorTools;
using JamKit.Shell;
using UnityEditor;

namespace JamKit.Doomlike.EditorTools
{
    /// <summary>
    /// "JamKit/Use Template/DOOM-like": Play (and the smoke test) now go to the DOOM-like.
    /// Batch: <c>Unity -batchmode -quit -executeMethod JamKit.Doomlike.EditorTools.DoomlikeTemplate.UseFromCommandLine</c>.
    /// Rebuild Shell Scenes first if the scene or preset is missing.
    /// </summary>
    public static class DoomlikeTemplate
    {
        [MenuItem("JamKit/Use Template/DOOM-like")]
        public static void UseMenu() => Use();

        public static void UseFromCommandLine() => TemplateSwitch.RunAndExit(Use);

        public static bool Use()
        {
            var preset = AssetDatabase.LoadAssetAtPath<GameConfig>(DoomlikeBuilder.PresetPath);
            return TemplateSwitch.Use("DOOM-like", preset, DoomlikeBuilder.ScenePath);
        }
    }
}
