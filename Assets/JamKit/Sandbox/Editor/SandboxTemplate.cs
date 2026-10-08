using JamKit.EditorTools;
using UnityEditor;

namespace JamKit.Sandbox.EditorTools
{
    /// <summary>
    /// "JamKit/Use Template/Sandbox": back to the shell's sandbox. Its preset
    /// (Assets/JamKit/Sandbox/SandboxGameConfig.asset) is saved from the active config the first time
    /// the shell is rebuilt while the sandbox is active.
    /// Batch: <c>-executeMethod JamKit.Sandbox.EditorTools.SandboxTemplate.UseFromCommandLine</c>.
    /// </summary>
    public static class SandboxTemplate
    {
        public const string PresetPath = "Assets/JamKit/Sandbox/SandboxGameConfig.asset";
        public const string ScenePath = SceneBuilder.ScenesDir + "/Sandbox.unity";

        [MenuItem("JamKit/Use Template/Sandbox")]
        public static void UseMenu() => Use();

        public static void UseFromCommandLine() => TemplateSwitch.RunAndExit(Use);

        public static bool Use()
        {
            var preset = TemplateSwitch.EnsurePresetFromActive(PresetPath, "Sandbox");
            return TemplateSwitch.Use("Sandbox", preset, ScenePath);
        }

        /// <summary>Runs after the sandbox scene is built: keeps a preset of the sandbox's config values.</summary>
        [JamKitBuildStep(101)]
        static void SavePreset(SceneBuilder.Context ctx) => TemplateSwitch.EnsurePresetFromActive(PresetPath, "Sandbox");
    }
}
