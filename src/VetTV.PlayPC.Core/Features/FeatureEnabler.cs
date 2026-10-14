using System.ComponentModel;
using System.Diagnostics;

namespace VetTV.PlayPC.Core.Features;

public enum FeatureEnableOutcome { Enabled, EnabledRebootRequired, Cancelled, Failed, NotAllowed }

/// <summary>
/// Shared contract between the app and VetTV.PlayPC.Elevated. Only allow-listed Windows optional features
/// can be enabled; the helper refuses anything else.
/// </summary>
public static class FeatureEnabler
{
    public static IReadOnlyList<string> AllowedFeatures { get; } = new[] { "HypervisorPlatform", "VirtualMachinePlatform" };

    public const string HelperExe = "VetTV.PlayPC.Elevated.exe";
    public const int ExitOk = 0, ExitRebootRequired = 3010, ExitNotAllowed = 2, ExitBadArgs = 1;

    public static bool IsAllowed(string feature) => AllowedFeatures.Contains(feature, StringComparer.Ordinal);

    public static string BuildArguments(string feature) =>
        IsAllowed(feature) ? $"enable-feature {feature}" : throw new ArgumentException($"Feature '{feature}' is not on the allow-list.");

    public static FeatureEnableOutcome Interpret(int exitCode) => exitCode switch
    {
        ExitOk => FeatureEnableOutcome.Enabled,
        ExitRebootRequired => FeatureEnableOutcome.EnabledRebootRequired,
        ExitNotAllowed => FeatureEnableOutcome.NotAllowed,
        _ => FeatureEnableOutcome.Failed,
    };

    /// <summary>Launches the helper with a UAC prompt ("runas"). Caller must confirm with the user first.</summary>
    public static FeatureEnableOutcome RunElevated(string helperPath, string feature)
    {
        var psi = new ProcessStartInfo(helperPath, BuildArguments(feature)) { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
        try
        {
            using var p = Process.Start(psi);
            if (p is null) return FeatureEnableOutcome.Failed;
            p.WaitForExit();
            return Interpret(p.ExitCode);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { return FeatureEnableOutcome.Cancelled; } // user said No to UAC
    }
}
