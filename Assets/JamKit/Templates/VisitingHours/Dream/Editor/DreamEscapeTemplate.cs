using JamKit.EditorTools;
using JamKit.Shell;
using UnityEditor;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// "JamKit/Use Template/Dream Escape": Play (and the smoke test) go straight to Visiting Hours' first escape,
    /// standalone, before it's wired into the story. Batch:
    /// <c>Unity -batchmode -quit -executeMethod JamKit.DreamEscape.EditorTools.DreamEscapeTemplate.UseFromCommandLine</c>.
    /// Rebuild Shell Scenes first if the scene or preset is missing. Switch back with "Use Template > Visiting Hours".
    /// </summary>
    public static class DreamEscapeTemplate
    {
        [MenuItem("JamKit/Use Template/Dream Escape")]
        public static void UseMenu() => Use();

        public static void UseFromCommandLine() => TemplateSwitch.RunAndExit(Use);

        public static bool Use()
        {
            var preset = AssetDatabase.LoadAssetAtPath<GameConfig>(DreamEscapeBuilder.PresetPath);
            return TemplateSwitch.Use("Dream Escape", preset, DreamEscapeBuilder.ScenePath);
        }
    }
}
