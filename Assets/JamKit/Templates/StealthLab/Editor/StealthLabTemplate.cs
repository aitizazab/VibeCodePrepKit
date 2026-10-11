using JamKit.EditorTools;
using JamKit.Shell;
using UnityEditor;

namespace JamKit.StealthLab.EditorTools
{
    /// <summary>
    /// "JamKit/Use Template/Stealth Lab": Play (and the smoke test) go to the stealth kit's test yard.
    /// Batch: <c>Unity -batchmode -quit -executeMethod JamKit.StealthLab.EditorTools.StealthLabTemplate.UseFromCommandLine</c>.
    /// Rebuild Shell Scenes first if the scene or preset is missing. Switch back with "Use Template > Visiting Hours".
    /// </summary>
    public static class StealthLabTemplate
    {
        [MenuItem("JamKit/Use Template/Stealth Lab")]
        public static void UseMenu() => Use();

        public static void UseFromCommandLine() => TemplateSwitch.RunAndExit(Use);

        public static bool Use()
        {
            var preset = AssetDatabase.LoadAssetAtPath<GameConfig>(StealthLabBuilder.PresetPath);
            return TemplateSwitch.Use("Stealth Lab", preset, StealthLabBuilder.ScenePath);
        }
    }
}
