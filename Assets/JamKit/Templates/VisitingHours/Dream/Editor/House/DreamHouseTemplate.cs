using JamKit.EditorTools;
using JamKit.Shell;
using UnityEditor;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// "JamKit/Use Template/Dream House": Play (and the smoke test) go straight to Night 2's house, standalone, before
    /// it's wired into the story. Batch:
    /// <c>Unity -batchmode -quit -executeMethod JamKit.DreamEscape.EditorTools.DreamHouseTemplate.UseFromCommandLine</c>.
    /// Rebuild Shell Scenes first if the scene or preset is missing. Switch back with "Use Template > Visiting Hours".
    /// </summary>
    public static class DreamHouseTemplate
    {
        [MenuItem("JamKit/Use Template/Dream House")]
        public static void UseMenu() => Use();

        public static void UseFromCommandLine() => TemplateSwitch.RunAndExit(Use);

        public static bool Use()
        {
            var preset = AssetDatabase.LoadAssetAtPath<GameConfig>(DreamHouseBuilder.PresetPath);
            return TemplateSwitch.Use("Dream House", preset, DreamHouseBuilder.ScenePath);
        }
    }
}
