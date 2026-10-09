using JamKit.EditorTools;
using JamKit.Shell;
using UnityEditor;

namespace JamKit.VisitingHours.EditorTools
{
    /// <summary>
    /// "JamKit/Use Template/Visiting Hours": Play (and the smoke test) now go to the slice.
    /// Batch: <c>Unity -batchmode -quit -executeMethod JamKit.VisitingHours.EditorTools.VisitingHoursTemplate.UseFromCommandLine</c>.
    /// Rebuild Shell Scenes first if the scene or preset is missing.
    /// </summary>
    public static class VisitingHoursTemplate
    {
        [MenuItem("JamKit/Use Template/Visiting Hours")]
        public static void UseMenu() => Use();

        public static void UseFromCommandLine() => TemplateSwitch.RunAndExit(Use);

        public static bool Use()
        {
            var preset = AssetDatabase.LoadAssetAtPath<GameConfig>(VisitingHoursBuilder.PresetPath);
            return TemplateSwitch.Use("Visiting Hours", preset, VisitingHoursBuilder.ScenePath);
        }
    }
}
