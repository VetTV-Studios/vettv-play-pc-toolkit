using System.Diagnostics;
using VetTV.PlayPC.Core.Settings;

namespace VetTV.PlayPC.Core.Launcher;

public sealed record GameShortcut(string Name, string ShortcutPath);

/// <summary>Finds the Start Menu shortcuts Google Play Games creates and launches/copies them. Uses the user's own shortcut files only.</summary>
public sealed class ShortcutService(IEnumerable<string> startMenuRoots, SettingsStore store)
{
    public const string GpgpcFolderName = "Google Play Games";
    private static readonly string[] Ext = { ".lnk", ".url" };

    public static IEnumerable<string> DefaultStartMenuRoots() => new[]
    {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"),
    };

    public IReadOnlyList<GameShortcut> FindGames()
    {
        var result = new Dictionary<string, GameShortcut>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in startMenuRoots)
        {
            var dir = Path.Combine(root, GpgpcFolderName);
            if (!Directory.Exists(dir)) continue;
            foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                if (!Ext.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)) continue;
                var name = Path.GetFileNameWithoutExtension(f);
                // Skip the client's own entries (app, uninstaller); keep games.
                if (name.Equals(GpgpcFolderName, StringComparison.OrdinalIgnoreCase) || name.Contains("Uninstall", StringComparison.OrdinalIgnoreCase)) continue;
                result.TryAdd(name, new GameShortcut(name, f));
            }
        }
        return result.Values.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public static void Launch(GameShortcut game) =>
        Process.Start(new ProcessStartInfo(game.ShortcutPath) { UseShellExecute = true })?.Dispose();

    /// <summary>Copies the game's shortcut to a folder (Desktop by default) and records it for backup/restore.</summary>
    public string CopyTo(GameShortcut game, string? folder = null)
    {
        folder ??= Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        Directory.CreateDirectory(folder);
        var dest = Path.Combine(folder, Path.GetFileName(game.ShortcutPath));
        File.Copy(game.ShortcutPath, dest, overwrite: true);
        var s = store.Load();
        if (!s.CreatedShortcuts.Contains(dest, StringComparer.OrdinalIgnoreCase)) s.CreatedShortcuts.Add(dest);
        store.Save(s);
        return dest;
    }

    public void TogglePin(GameShortcut game)
    {
        var s = store.Load();
        if (!s.PinnedGames.Remove(game.Name)) s.PinnedGames.Add(game.Name);
        store.Save(s);
    }
}
