using VetTV.PlayPC.Core.Platform;
using VetTV.PlayPC.Core.Settings;

namespace VetTV.PlayPC.Core.Presets;

public sealed record PresetInfo(string Id, string Title, string Description);

/// <summary>
/// Applies supported Windows-level performance settings and records prior values so each preset can be undone.
/// Only HKCU registry values documented by Windows and powercfg are touched; Google Play Games' own files are never written.
/// </summary>
public sealed class PresetService(IRegistryStore registry, ICommandRunner commands, SettingsStore store)
{
    public const string GpuPrefKey = @"Software\Microsoft\DirectX\UserGpuPreferences";
    public const string GameBarKey = @"Software\Microsoft\GameBar";
    public const string HighPerformancePlan = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";

    public const string GpuHighPerf = "gpu-high-performance";
    public const string GameMode = "game-mode";
    public const string PowerPlan = "power-high-performance";

    public static IReadOnlyList<PresetInfo> All { get; } = new[]
    {
        new PresetInfo(GpuHighPerf, "Use the high-performance GPU", "Sets the Windows per-app graphics preference for Google Play Games to High performance."),
        new PresetInfo(GameMode, "Turn on Game Mode", "Enables Windows Game Mode for the current user."),
        new PresetInfo(PowerPlan, "High performance power plan", "Switches to the built-in High performance power plan (plugged-in recommended)."),
    };

    /// <summary>Apply a preset. <paramref name="gpgpcExecutables"/> is needed for the GPU preset.</summary>
    public void Apply(string presetId, IEnumerable<string>? gpgpcExecutables = null)
    {
        var s = store.Load();
        if (s.AppliedPresets.Contains(presetId)) return; // idempotent: keep the original "before" values
        var journal = new List<UndoEntry>();
        switch (presetId)
        {
            case GpuHighPerf:
                var exes = gpgpcExecutables?.Where(e => !string.IsNullOrWhiteSpace(e)).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new();
                if (exes.Count == 0) throw new InvalidOperationException("Google Play Games executables not found; install it first.");
                foreach (var exe in exes) SetReg(journal, presetId, GpuPrefKey, exe, new RegValue(RegValueKind.String, "GpuPreference=2;"));
                break;
            case GameMode:
                SetReg(journal, presetId, GameBarKey, "AutoGameModeEnabled", new RegValue(RegValueKind.DWord, "1"));
                SetReg(journal, presetId, GameBarKey, "AllowAutoGameMode", new RegValue(RegValueKind.DWord, "1"));
                break;
            case PowerPlan:
                var prior = ActivePowerScheme() ?? throw new InvalidOperationException("Could not read the active power plan.");
                var (code, _) = commands.Run("powercfg", $"/setactive {HighPerformancePlan}");
                if (code != 0) throw new InvalidOperationException("The High performance power plan isn't available on this PC (common on Modern Standby laptops).");
                journal.Add(new UndoEntry(presetId, UndoKind.PowerPlan, prior, null, true, null, null));
                break;
            default: throw new ArgumentException($"Unknown preset '{presetId}'.", nameof(presetId));
        }
        s.UndoJournal.AddRange(journal);
        s.AppliedPresets.Add(presetId);
        store.Save(s);
    }

    /// <summary>Restores every value the preset changed to what it was before.</summary>
    public void Undo(string presetId)
    {
        var s = store.Load();
        var entries = s.UndoJournal.Where(e => e.PresetId == presetId).Reverse().ToList();
        foreach (var e in entries)
        {
            if (e.Kind == UndoKind.PowerPlan) commands.Run("powercfg", $"/setactive {e.Target}");
            else if (!e.Existed) registry.Delete(e.Target, e.Name!);
            else registry.Set(e.Target, e.Name!, new RegValue(Enum.Parse<RegValueKind>(e.PriorKind!), e.PriorData!));
        }
        s.UndoJournal.RemoveAll(e => e.PresetId == presetId);
        s.AppliedPresets.Remove(presetId);
        store.Save(s);
    }

    public bool IsApplied(string presetId) => store.Load().AppliedPresets.Contains(presetId);

    internal string? ActivePowerScheme()
    {
        var (code, output) = commands.Run("powercfg", "/getactivescheme");
        if (code != 0) return null;
        var m = System.Text.RegularExpressions.Regex.Match(output, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
        return m.Success ? m.Value : null;
    }

    private void SetReg(List<UndoEntry> journal, string preset, string key, string name, RegValue value)
    {
        var prior = registry.Get(key, name);
        journal.Add(new UndoEntry(preset, UndoKind.Registry, key, name, prior is not null, prior?.Kind.ToString(), prior?.Data));
        registry.Set(key, name, value);
    }
}

public static class GpgpcExecutables
{
    private static readonly string[] Names = { "client.exe", "crosvm.exe", "GooglePlayGames.exe" };

    /// <summary>Finds GPGPC executables under the detected install folder for the per-app GPU preference.</summary>
    public static IReadOnlyList<string> Find(string? installPath)
    {
        if (installPath is null || !Directory.Exists(installPath)) return Array.Empty<string>();
        return Directory.EnumerateFiles(installPath, "*.exe", new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 4, IgnoreInaccessible = true })
            .Where(f => Names.Contains(Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)).ToList();
    }
}
