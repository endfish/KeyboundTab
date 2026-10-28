namespace KeyboundTab.Native;

internal static class RuntimeCompatibility
{
    // Input node, focused interface, Chinese text node and duplicate instances
    // confirmed in the 2026-10-28 game capture. Function RVAs checked offline.
    // This is a playable preview; manual Enter and shortcut conflicts still need playtesting.
    private static readonly string SupportedGameVersion = "2026.10.06.0000.0000";

    public static bool CanWrite(string gameVersion) =>
        gameVersion == SupportedGameVersion;

    public static string ReadGameVersion()
    {
        var executable = Environment.ProcessPath;
        if (executable == null)
            return "unknown";
        var path = Path.Combine(Path.GetDirectoryName(executable)!, "ffxivgame.ver");
        return File.Exists(path) ? File.ReadAllText(path).Trim() : "unknown";
    }
}
