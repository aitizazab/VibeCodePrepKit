using JamKit.EditorTools;
using JamKit.Shell;
using UnityEditor;

namespace JamKit.BulletHell.EditorTools
{
    /// <summary>
    /// "JamKit/Use Template/Bullet Hell": Play (and the smoke test) now go to the bullet hell.
    /// Batch: <c>Unity -batchmode -quit -executeMethod JamKit.BulletHell.EditorTools.BulletHellTemplate.UseFromCommandLine</c>.
    /// Rebuild Shell Scenes first if the scene or preset is missing.
    /// </summary>
    public static class BulletHellTemplate
    {
        [MenuItem("JamKit/Use Template/Bullet Hell")]
        public static void UseMenu() => Use();

        public static void UseFromCommandLine() => TemplateSwitch.RunAndExit(Use);

        public static bool Use()
        {
            var preset = AssetDatabase.LoadAssetAtPath<GameConfig>(BulletHellBuilder.PresetPath);
            return TemplateSwitch.Use("Bullet Hell", preset, BulletHellBuilder.ScenePath);
        }
    }
}
