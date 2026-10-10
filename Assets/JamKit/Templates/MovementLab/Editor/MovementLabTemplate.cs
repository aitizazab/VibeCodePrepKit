using JamKit.EditorTools;
using JamKit.Shell;
using UnityEditor;

namespace JamKit.MovementLab.EditorTools
{
    /// <summary>
    /// "JamKit/Use Template/Movement Lab": Play (and the smoke test) go to the movement feel-test course.
    /// Batch: <c>Unity -batchmode -quit -executeMethod JamKit.MovementLab.EditorTools.MovementLabTemplate.UseFromCommandLine</c>.
    /// Rebuild Shell Scenes first if the scene or preset is missing. Switch back with "Use Template > Visiting Hours".
    /// </summary>
    public static class MovementLabTemplate
    {
        [MenuItem("JamKit/Use Template/Movement Lab")]
        public static void UseMenu() => Use();

        public static void UseFromCommandLine() => TemplateSwitch.RunAndExit(Use);

        public static bool Use()
        {
            var preset = AssetDatabase.LoadAssetAtPath<GameConfig>(MovementLabBuilder.PresetPath);
            return TemplateSwitch.Use("Movement Lab", preset, MovementLabBuilder.ScenePath);
        }
    }
}
