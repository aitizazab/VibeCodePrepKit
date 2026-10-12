using JamKit.EditorTools;
using JamKit.Shell;
using UnityEditor;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// "JamKit/Use Template/Dream Street": Play (and the smoke test) go straight to Night 1's street on the hill, standalone,
    /// before it's wired into the story. Batch:
    /// <c>Unity -batchmode -quit -executeMethod JamKit.DreamEscape.EditorTools.DreamStreetTemplate.UseFromCommandLine</c>.
    /// Rebuild Shell Scenes first if the scene or preset is missing. Switch back with "Use Template > Visiting Hours".
    /// </summary>
    public static class DreamStreetTemplate
    {
        [MenuItem("JamKit/Use Template/Dream Street")]
        public static void UseMenu() => Use();

        public static void UseFromCommandLine() => TemplateSwitch.RunAndExit(Use);

        public static bool Use()
        {
            var preset = AssetDatabase.LoadAssetAtPath<GameConfig>(DreamStreetBuilder.PresetPath);
            return TemplateSwitch.Use("Dream Street", preset, DreamStreetBuilder.ScenePath);
        }
    }
}
