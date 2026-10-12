namespace JamKit.Look.EditorTools
{
    /// <summary>Command-line arguments for the Look's batch methods (-vhScene, -vhOut, ...).</summary>
    public static class VhLookArgs
    {
        public static string Get(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
