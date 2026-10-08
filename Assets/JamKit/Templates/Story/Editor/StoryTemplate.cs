using JamKit.EditorTools;
using JamKit.Shell;
using UnityEditor;

namespace JamKit.Narrative.EditorTools
{
    /// <summary>
    /// "JamKit/Use Template/Story": Play (and the smoke test) now go to the story.
    /// Batch: <c>Unity -batchmode -quit -executeMethod JamKit.Narrative.EditorTools.StoryTemplate.UseFromCommandLine</c>.
    /// Rebuild Shell Scenes first if the scene or preset is missing.
    /// </summary>
    public static class StoryTemplate
    {
        [MenuItem("JamKit/Use Template/Story")]
        public static void UseMenu() => Use();

        public static void UseFromCommandLine() => TemplateSwitch.RunAndExit(Use);

        public static bool Use()
        {
            var preset = AssetDatabase.LoadAssetAtPath<GameConfig>(StoryBuilder.PresetPath);
            return TemplateSwitch.Use("Story", preset, StoryBuilder.ScenePath);
        }
    }
}
