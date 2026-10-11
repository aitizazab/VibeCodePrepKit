using JamKit.EditorTools;
using JamKit.Shell;
using UnityEditor;

namespace JamKit.DeepBlockLab.EditorTools
{
    /// <summary>
    /// "JamKit/Use Template/Deep Block": Play (and the smoke test) go to the walkable greybox of the deep block.
    /// Batch: <c>Unity -batchmode -quit -executeMethod JamKit.DeepBlockLab.EditorTools.DeepBlockTemplate.UseFromCommandLine</c>.
    /// Rebuild Shell Scenes first if the scene or preset is missing. Switch back with "Use Template > Visiting Hours".
    /// </summary>
    public static class DeepBlockTemplate
    {
        [MenuItem("JamKit/Use Template/Deep Block")]
        public static void UseMenu() => Use();

        public static void UseFromCommandLine() => TemplateSwitch.RunAndExit(Use);

        public static bool Use()
        {
            var preset = AssetDatabase.LoadAssetAtPath<GameConfig>(DeepBlockBuilder.PresetPath);
            return TemplateSwitch.Use("Deep Block", preset, DeepBlockBuilder.ScenePath);
        }
    }
}
