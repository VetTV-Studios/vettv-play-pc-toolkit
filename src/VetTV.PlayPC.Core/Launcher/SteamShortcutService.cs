using System.Diagnostics;

namespace VetTV.PlayPC.Core.Launcher;

/// <summary>
/// Adds a Google Play Games shortcut to Steam as a non-Steam game. The Steam entry just opens the user's own
/// shortcut file through Explorer. shortcuts.vdf is backed up first, and Steam must be closed.
/// </summary>
public sealed class SteamShortcutService(string steamUserdataRoot)
{
    public static string? DefaultUserdataRoot()
    {
        var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var p = Path.Combine(pf86, "Steam", "userdata");
        return Directory.Exists(p) ? p : null;
    }

    public static bool IsSteamRunning() => Process.GetProcessesByName("steam").Length > 0;

    /// <summary>Steam account folders (numeric ids) under userdata.</summary>
    public IReadOnlyList<string> Accounts() => Directory.Exists(steamUserdataRoot)
        ? Directory.GetDirectories(steamUserdataRoot).Where(d => Path.GetFileName(d).All(char.IsDigit) && Path.GetFileName(d) != "0").ToList()
        : Array.Empty<string>();

    /// <summary>Returns false if an entry with the same name already exists.</summary>
    public bool Add(string accountDir, GameShortcut game, bool steamRunning)
    {
        if (steamRunning) throw new InvalidOperationException("Close Steam first; it overwrites shortcuts.vdf on exit.");
        var cfg = Path.Combine(accountDir, "config");
        Directory.CreateDirectory(cfg);
        var file = Path.Combine(cfg, "shortcuts.vdf");

        VdfNode root;
        if (File.Exists(file))
        {
            File.Copy(file, file + ".vettv.bak", overwrite: true);
            using var fs = File.OpenRead(file);
            root = VdfNode.Read(fs);
        }
        else root = new VdfNode();

        if (root["shortcuts"] is not VdfNode list) { list = new VdfNode(); root["shortcuts"] = list; }
        foreach (var (_, v) in list.Items)
            if (v is VdfNode e && string.Equals(e["AppName"] as string ?? e["appname"] as string, game.Name, StringComparison.OrdinalIgnoreCase))
                return false;

        var entry = new VdfNode();
        entry["AppName"] = game.Name;
        entry["Exe"] = "\"C:\\Windows\\explorer.exe\"";
        entry["StartDir"] = "\"C:\\Windows\\\"";
        entry["icon"] = "";
        entry["ShortcutPath"] = "";
        entry["LaunchOptions"] = $"\"{game.ShortcutPath}\"";
        entry["IsHidden"] = 0;
        entry["AllowDesktopConfig"] = 1;
        entry["AllowOverlay"] = 1;
        entry["OpenVR"] = 0;
        entry["LastPlayTime"] = 0;
        var tags = new VdfNode(); tags["0"] = "Google Play Games";
        entry["tags"] = tags;
        list.Items.Add(new(list.Items.Count.ToString(), entry));

        var tmp = file + ".tmp";
        using (var fs = File.Create(tmp)) root.Write(fs);
        File.Move(tmp, file, overwrite: true);
        return true;
    }
}
