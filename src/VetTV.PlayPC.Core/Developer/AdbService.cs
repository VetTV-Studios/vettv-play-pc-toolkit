using System.Text.RegularExpressions;
using VetTV.PlayPC.Core.Checks;
using VetTV.PlayPC.Core.Platform;

namespace VetTV.PlayPC.Core.Developer;

public sealed class SideloadRefusedException(string message) : InvalidOperationException(message);

public sealed record AdbResult(bool Success, string Output);

/// <summary>
/// Developer Emulator sideloading using ONLY the adb bundled with the Developer Emulator, against localhost:6520.
/// Refuses when only the consumer client is installed (it does not support sideloading).
/// </summary>
public sealed partial class AdbService
{
    public const string Endpoint = "localhost:6520";
    public const string Disclaimer = "Only install apps you own or have the rights to install. Sideloading works with the Google Play Games Developer Emulator only.";

    private readonly ICommandRunner _runner;
    public string AdbPath { get; }

    private AdbService(ICommandRunner runner, string adbPath) { _runner = runner; AdbPath = adbPath; }

    /// <summary>Creates the service from detected installs, or throws <see cref="SideloadRefusedException"/> with a clear reason.</summary>
    public static AdbService Create(IReadOnlyList<InstallInfo> installs, ISystemProbe probe, ICommandRunner runner)
    {
        var emu = installs.FirstOrDefault(i => i.Product == PlayGamesProduct.DeveloperEmulator);
        var consumer = installs.FirstOrDefault(i => i.Product == PlayGamesProduct.Consumer);
        if (emu is not { Installed: true } || emu.InstallPath is null)
            throw new SideloadRefusedException(consumer is { Installed: true }
                ? "Only the regular Google Play Games app is installed. It does not support sideloading. Install the Google Play Games Developer Emulator (developer.android.com/games/playgames/emulator) to sideload your own apps."
                : "The Google Play Games Developer Emulator is not installed.");
        var adb = Path.Combine(emu.InstallPath, "current", "emulator", "adb.exe");
        if (!probe.FileExists(adb))
            throw new SideloadRefusedException($"The Developer Emulator's bundled adb was not found at {adb}. Reinstall the Developer Emulator.");
        return new AdbService(runner, adb);
    }

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_]*(\.[A-Za-z0-9_]+)+$")]
    private static partial Regex PackageRegex();

    public static bool IsValidPackageName(string p) => p.Length <= 255 && PackageRegex().IsMatch(p);

    private AdbResult Adb(string args)
    {
        var (code, output) = _runner.Run(AdbPath, args);
        return new AdbResult(code == 0 && !output.Contains("Failure", StringComparison.Ordinal) && !output.Contains("error:", StringComparison.OrdinalIgnoreCase), output.Trim());
    }

    private AdbResult Device(string args) => Adb($"-s {Endpoint} {args}");

    public AdbResult Connect()
    {
        var r = Adb($"connect {Endpoint}");
        var ok = r.Output.Contains("connected to", StringComparison.OrdinalIgnoreCase) && !r.Output.Contains("cannot", StringComparison.OrdinalIgnoreCase);
        return r with { Success = ok };
    }

    public AdbResult InstallApk(string apkPath)
    {
        if (!string.Equals(Path.GetExtension(apkPath), ".apk", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Choose an .apk file.");
        if (!File.Exists(apkPath)) throw new FileNotFoundException("APK not found.", apkPath);
        return Device($"install -r \"{apkPath}\"");
    }

    /// <summary>AAB install via the user's own bundletool jar (not bundled): build-apks for the connected device, then install-apks.</summary>
    public AdbResult InstallAab(string aabPath, string bundletoolJar, string javaExe = "java")
    {
        if (!string.Equals(Path.GetExtension(aabPath), ".aab", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Choose an .aab file.");
        if (!File.Exists(aabPath)) throw new FileNotFoundException("AAB not found.", aabPath);
        if (!File.Exists(bundletoolJar)) throw new FileNotFoundException("bundletool jar not found. Download it from github.com/google/bundletool/releases.", bundletoolJar);
        var apks = Path.Combine(Path.GetTempPath(), $"vettv-{Guid.NewGuid():N}.apks");
        try
        {
            var common = $"--adb=\"{AdbPath}\" --device-id={Endpoint}";
            var (b, bo) = _runner.Run(javaExe, $"-jar \"{bundletoolJar}\" build-apks --bundle=\"{aabPath}\" --output=\"{apks}\" --connected-device {common}");
            if (b != 0) return new AdbResult(false, bo.Trim());
            var (i, io) = _runner.Run(javaExe, $"-jar \"{bundletoolJar}\" install-apks --apks=\"{apks}\" {common}");
            return new AdbResult(i == 0, io.Trim());
        }
        finally { try { File.Delete(apks); } catch { } }
    }

    /// <summary>Third-party (user-installed) packages.</summary>
    public IReadOnlyList<string> ListPackages()
    {
        var r = Device("shell pm list packages -3");
        if (!r.Success) return Array.Empty<string>();
        return r.Output.Split('\n').Select(l => l.Trim()).Where(l => l.StartsWith("package:")).Select(l => l["package:".Length..])
            .Where(IsValidPackageName).OrderBy(p => p, StringComparer.Ordinal).ToList();
    }

    public AdbResult Uninstall(string package)
    {
        if (!IsValidPackageName(package)) throw new ArgumentException($"'{package}' is not a valid package name.");
        return Device($"uninstall {package}");
    }

    /// <summary>Arguments for a streaming logcat process (run by the UI). Optional tag/priority filter like "MyGame:D *:S".</summary>
    public string LogcatArguments(string? filter = null)
    {
        if (filter is not null && !LogcatFilterRegex().IsMatch(filter)) throw new ArgumentException("Filter must look like 'Tag:Priority' entries, e.g. 'MyGame:D *:S'.");
        return $"-s {Endpoint} logcat -v time {filter}".TrimEnd();
    }

    [GeneratedRegex(@"^([A-Za-z0-9_.*\-]+:[VDIWEFS])( [A-Za-z0-9_.*\-]+:[VDIWEFS])*$")]
    private static partial Regex LogcatFilterRegex();
}
