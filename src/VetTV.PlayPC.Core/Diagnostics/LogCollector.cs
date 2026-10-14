namespace VetTV.PlayPC.Core.Diagnostics;

public sealed record LogSource(string Label, string Directory);

/// <summary>Known log folders for the consumer client and Developer Emulator.</summary>
public static class LogCollector
{
    public static IReadOnlyList<LogSource> DefaultSources(Func<string, string> expand) => new[]
    {
        new LogSource("PlayGames", expand(@"%LOCALAPPDATA%\Google\Play Games\Logs")),
        new LogSource("DeveloperEmulator", expand(@"%LOCALAPPDATA%\Google\Play Games Developer Emulator\Logs")),
    };

    internal static readonly string[] TextExtensions = { ".log", ".txt", ".json", ".xml", ".ini", ".csv" };
    internal const long MaxFileBytes = 50L * 1024 * 1024;
}
