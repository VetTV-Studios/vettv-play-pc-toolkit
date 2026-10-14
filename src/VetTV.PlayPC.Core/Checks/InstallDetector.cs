using VetTV.PlayPC.Core.Platform;

namespace VetTV.PlayPC.Core.Checks;

public enum PlayGamesProduct { Consumer, DeveloperEmulator }

public sealed record InstallInfo(PlayGamesProduct Product, bool Installed, string? Version, string? InstallPath, string? Track, bool Running, string Source);

/// <summary>Detects the consumer client and the Developer Emulator via uninstall keys, then a path probe.</summary>
public sealed class InstallDetector(ISystemProbe probe)
{
    internal const string ConsumerName = "Google Play Games";
    internal const string EmulatorName = "Google Play Games Developer Emulator";
    internal const string ConsumerDefaultPath = @"%ProgramFiles%\Google\Play Games";
    internal const string EmulatorDefaultPath = @"%ProgramFiles%\Google\Play Games Developer Emulator";

    public IReadOnlyList<InstallInfo> Detect()
    {
        var entries = probe.GetUninstallEntries();
        var procs = probe.GetRunningProcessNames();
        return new[] { DetectOne(PlayGamesProduct.Consumer, entries, procs), DetectOne(PlayGamesProduct.DeveloperEmulator, entries, procs) };
    }

    private InstallInfo DetectOne(PlayGamesProduct product, IReadOnlyList<UninstallEntry> entries, IReadOnlyList<string> procs)
    {
        var isEmu = product == PlayGamesProduct.DeveloperEmulator;
        var entry = entries.FirstOrDefault(e => isEmu
            ? e.DisplayName.Contains(EmulatorName, StringComparison.OrdinalIgnoreCase)
            : e.DisplayName.Trim().Equals(ConsumerName, StringComparison.OrdinalIgnoreCase)
              || (e.DisplayName.StartsWith(ConsumerName, StringComparison.OrdinalIgnoreCase)
                  && !e.DisplayName.Contains("Developer", StringComparison.OrdinalIgnoreCase)));

        var defaultPath = probe.ExpandPath(isEmu ? EmulatorDefaultPath : ConsumerDefaultPath);
        string? path = !string.IsNullOrWhiteSpace(entry?.InstallLocation) ? entry!.InstallLocation : null;
        string source = entry is not null ? "registry" : "none";
        if (path is null && probe.DirectoryExists(defaultPath)) { path = defaultPath; if (entry is null) source = "path"; }

        var version = entry?.DisplayVersion;
        if (version is null && path is not null)
        {
            var exe = Path.Combine(path, "current", isEmu ? @"emulator\adb.exe" : "client.exe");
            version = probe.GetFileVersion(exe);
        }

        string? track = null;
        if (isEmu && entry is not null)
            track = entry.DisplayName.Contains("Beta", StringComparison.OrdinalIgnoreCase) ? "Beta" : "Stable";

        var running = procs.Any(p => isEmu
            ? p.Contains("emulator", StringComparison.OrdinalIgnoreCase) && path is not null
            : p.Equals("client", StringComparison.OrdinalIgnoreCase) || p.Equals("crosvm", StringComparison.OrdinalIgnoreCase)
              || p.Contains("PlayGames", StringComparison.OrdinalIgnoreCase));
        var installed = entry is not null || path is not null;
        return new InstallInfo(product, installed, version, path, track, installed && running, source);
    }

    public IEnumerable<CheckResult> ToChecks(IReadOnlyList<InstallInfo> infos)
    {
        foreach (var i in infos)
        {
            var name = i.Product == PlayGamesProduct.Consumer ? "Google Play Games" : "Developer Emulator";
            var id = i.Product == PlayGamesProduct.Consumer ? "install.consumer" : "install.emulator";
            if (i.Installed)
                yield return CheckResult.Pass(id, "Install", $"{name} installed",
                    $"Version {i.Version ?? "unknown"}{(i.Track is null ? "" : $", {i.Track} track")}, {(i.Running ? "running" : "not running")}. Path: {i.InstallPath ?? "unknown"} (found via {i.Source}).");
            else if (i.Product == PlayGamesProduct.Consumer)
                yield return CheckResult.Warn(id, "Install", $"{name} not installed", "No install found in the registry or default path.",
                    "Install Google Play Games from play.google.com/googleplaygames.");
            else
                yield return CheckResult.Pass(id, "Install", $"{name} not installed", "Optional: only needed by developers for sideloading apps you own.");
        }
    }
}
