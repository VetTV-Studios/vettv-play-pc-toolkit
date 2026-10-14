using System.IO.Compression;
using System.Text.Json;

namespace VetTV.PlayPC.Core.Settings;

public enum UndoKind { Registry, PowerPlan }

/// <summary>A prior value captured before a preset changed it. Existed=false means "delete on undo".</summary>
public sealed record UndoEntry(string PresetId, UndoKind Kind, string Target, string? Name, bool Existed, string? PriorKind, string? PriorData);

public sealed class ToolkitSettings
{
    public int SchemaVersion { get; set; } = 1;
    public List<UndoEntry> UndoJournal { get; set; } = new();
    public List<string> AppliedPresets { get; set; } = new();
    /// <summary>Desktop shortcuts this toolkit created (full paths).</summary>
    public List<string> CreatedShortcuts { get; set; } = new();
    public List<string> PinnedGames { get; set; } = new();
}

/// <summary>Toolkit's own JSON settings, plus backup/restore of settings and toolkit-created shortcuts only.</summary>
public sealed class SettingsStore(string settingsPath)
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VetTV", "PlayPCToolkit", "settings.json");

    public string SettingsPath => settingsPath;

    public ToolkitSettings Load()
    {
        if (!File.Exists(settingsPath)) return new ToolkitSettings();
        try { return JsonSerializer.Deserialize<ToolkitSettings>(File.ReadAllText(settingsPath)) ?? new(); }
        catch (JsonException) { return new ToolkitSettings(); }
    }

    public void Save(ToolkitSettings s)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        var tmp = settingsPath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(s, Json));
        File.Move(tmp, settingsPath, overwrite: true);
    }

    public void Backup(string zipPath)
    {
        var s = Load();
        if (File.Exists(zipPath)) File.Delete(zipPath);
        using var z = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        using (var w = new StreamWriter(z.CreateEntry("settings.json").Open())) w.Write(JsonSerializer.Serialize(s, Json));
        for (var i = 0; i < s.CreatedShortcuts.Count; i++)
            if (File.Exists(s.CreatedShortcuts[i]))
                z.CreateEntryFromFile(s.CreatedShortcuts[i], $"shortcuts/{i}/{Path.GetFileName(s.CreatedShortcuts[i])}");
    }

    /// <summary>Restores settings and re-creates toolkit shortcuts at their original paths. Returns restored shortcut count.</summary>
    public int Restore(string zipPath)
    {
        using var z = ZipFile.OpenRead(zipPath);
        var entry = z.GetEntry("settings.json") ?? throw new InvalidDataException("Not a VetTV Play PC Toolkit backup (settings.json missing).");
        ToolkitSettings s;
        using (var r = new StreamReader(entry.Open())) s = JsonSerializer.Deserialize<ToolkitSettings>(r.ReadToEnd()) ?? new();
        var restored = 0;
        for (var i = 0; i < s.CreatedShortcuts.Count; i++)
        {
            var target = s.CreatedShortcuts[i];
            var e = z.GetEntry($"shortcuts/{i}/{Path.GetFileName(target)}");
            var ext = Path.GetExtension(target).ToLowerInvariant();
            if (e is null || (ext != ".lnk" && ext != ".url")) continue; // only shortcut files are ever written
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            e.ExtractToFile(target, overwrite: true);
            restored++;
        }
        Save(s);
        return restored;
    }
}
