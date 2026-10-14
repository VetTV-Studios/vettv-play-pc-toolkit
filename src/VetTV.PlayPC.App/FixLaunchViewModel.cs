using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.Versioning;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VetTV.PlayPC.Core.Checks;
using VetTV.PlayPC.Core.Cleaning;
using VetTV.PlayPC.Core.Features;
using VetTV.PlayPC.Core.Launcher;
using VetTV.PlayPC.Core.Platform;
using VetTV.PlayPC.Core.Presets;
using VetTV.PlayPC.Core.Settings;

namespace VetTV.PlayPC.App;

public sealed partial class PresetItem(PresetInfo info, bool applied) : ObservableObject
{
    public PresetInfo Info { get; } = info;
    [ObservableProperty] private bool _applied = applied;
}

/// <summary>M2: every system-changing action asks for confirmation first.</summary>
[SupportedOSPlatform("windows")]
public partial class FixLaunchViewModel : ObservableObject
{
    private readonly WindowsSystemProbe _probe = new();
    private readonly SettingsStore _store = new(SettingsStore.DefaultPath);
    private readonly PresetService _presets;
    private readonly ShortcutService _shortcuts;
    private readonly CacheCleaner _cleaner;
    private CachePreview? _preview;

    public FixLaunchViewModel()
    {
        _presets = new PresetService(new WindowsRegistryStore(), new ProcessCommandRunner(), _store);
        _shortcuts = new ShortcutService(ShortcutService.DefaultStartMenuRoots(), _store);
        _cleaner = new CacheCleaner(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), new WindowsRecycleBin());
        foreach (var p in PresetService.All) Presets.Add(new PresetItem(p, _presets.IsApplied(p.Id)));
        RefreshGames();
    }

    public IReadOnlyList<string> Features => FeatureEnabler.AllowedFeatures;
    public ObservableCollection<PresetItem> Presets { get; } = new();
    public ObservableCollection<GameShortcut> Games { get; } = new();
    public ObservableCollection<BackgroundApp> HeavyApps { get; } = new();
    public ObservableCollection<CacheFile> CacheFiles { get; } = new();
    [ObservableProperty] private GameShortcut? _selectedGame;
    [ObservableProperty] private BackgroundApp? _selectedApp;
    [ObservableProperty] private string _status = "";

    private static bool Confirm(string text) =>
        MessageBox.Show(text, "VetTV Play PC Toolkit", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    [RelayCommand]
    private async Task EnableFeatureAsync(string feature)
    {
        if (!Confirm($"Enable the Windows feature '{feature}'?\n\nWindows will ask for administrator permission (UAC) and you'll need to restart afterwards.")) return;
        var helper = Path.Combine(AppContext.BaseDirectory, FeatureEnabler.HelperExe);
        Status = $"Enabling {feature}...";
        var outcome = await Task.Run(() => FeatureEnabler.RunElevated(helper, feature));
        Status = outcome switch
        {
            FeatureEnableOutcome.Enabled => $"{feature} is enabled.",
            FeatureEnableOutcome.EnabledRebootRequired => $"{feature} enabled. Restart Windows to finish.",
            FeatureEnableOutcome.Cancelled => "Cancelled at the UAC prompt; nothing changed.",
            _ => $"Enabling {feature} failed ({outcome}).",
        };
        if (outcome == FeatureEnableOutcome.EnabledRebootRequired &&
            Confirm($"{feature} was enabled. Restart now? Save your work first."))
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("shutdown", "/r /t 30 /c \"VetTV Play PC Toolkit: restarting to finish enabling Windows features.\"") { UseShellExecute = false, CreateNoWindow = true });
    }

    [RelayCommand]
    private void TogglePreset(PresetItem item)
    {
        try
        {
            if (item.Applied)
            {
                if (!Confirm($"Undo '{item.Info.Title}' and restore your previous settings?")) return;
                _presets.Undo(item.Info.Id); item.Applied = false; Status = $"Restored settings from before '{item.Info.Title}'.";
            }
            else
            {
                if (!Confirm($"{item.Info.Description}\n\nYour current values are saved so you can undo this. Apply?")) return;
                var install = new InstallDetector(_probe).Detect().FirstOrDefault(i => i.Product == PlayGamesProduct.Consumer);
                _presets.Apply(item.Info.Id, GpgpcExecutables.Find(install?.InstallPath));
                item.Applied = true; Status = $"Applied '{item.Info.Title}'. Use Undo to revert.";
            }
        }
        catch (Exception ex) { Status = ex.Message; }
    }

    [RelayCommand]
    private void ScanHeavyApps()
    {
        HeavyApps.Clear();
        foreach (var a in BackgroundAppAdvisor.HeavyApps()) HeavyApps.Add(a);
        Status = $"{HeavyApps.Count} apps using more than 300 MB.";
    }

    [RelayCommand]
    private void CloseSelectedApp()
    {
        if (SelectedApp is null || !Confirm($"Ask '{SelectedApp.Title}' to close? It may prompt you to save.")) return;
        Status = BackgroundAppAdvisor.RequestClose(SelectedApp.Pid) ? "Close requested." : "That app didn't accept a close request.";
    }

    [RelayCommand]
    private void RefreshGames()
    {
        Games.Clear();
        foreach (var g in _shortcuts.FindGames()) Games.Add(g);
        Status = Games.Count == 0 ? "No Google Play Games shortcuts found in the Start Menu." : $"{Games.Count} games found.";
    }

    [RelayCommand] private void LaunchGame() { if (SelectedGame is not null) ShortcutService.Launch(SelectedGame); }

    [RelayCommand]
    private void CopyToDesktop()
    {
        if (SelectedGame is null) return;
        Status = $"Shortcut copied to {_shortcuts.CopyTo(SelectedGame)}";
    }

    [RelayCommand]
    private void AddToSteam()
    {
        if (SelectedGame is null) return;
        var root = SteamShortcutService.DefaultUserdataRoot();
        if (root is null) { Status = "Steam not found."; return; }
        var steam = new SteamShortcutService(root);
        var account = steam.Accounts().FirstOrDefault();
        if (account is null) { Status = "No Steam account folder found."; return; }
        if (!Confirm($"Add '{SelectedGame.Name}' to Steam as a non-Steam game?\nSteam must be closed. shortcuts.vdf is backed up first.")) return;
        try { Status = steam.Add(account, SelectedGame, SteamShortcutService.IsSteamRunning()) ? "Added to Steam." : "Already in Steam."; }
        catch (Exception ex) { Status = ex.Message; }
    }

    [RelayCommand]
    private void PreviewCache()
    {
        _preview = _cleaner.Preview();
        CacheFiles.Clear();
        foreach (var f in _preview.Files) CacheFiles.Add(f);
        Status = $"{_preview.Files.Count} cache files, {_preview.TotalBytes / 1024d / 1024:F1} MB (allow-listed folders only).";
    }

    [RelayCommand]
    private void CleanCache()
    {
        if (_preview is null || _preview.Files.Count == 0) { Status = "Preview first."; return; }
        if (!Confirm($"Send {_preview.Files.Count} cache files ({_preview.TotalBytes / 1024d / 1024:F1} MB) to the Recycle Bin?")) return;
        var running = new InstallDetector(_probe).Detect().Any(i => i.Running);
        try
        {
            var (ok, refused, failed) = _cleaner.Clean(_preview, running);
            Status = $"Recycled {ok} files. Refused {refused}, failed {failed}.";
            PreviewCache();
        }
        catch (Exception ex) { Status = ex.Message; }
    }

    [RelayCommand]
    private void Backup()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog { FileName = $"VetTV-PlayPC-backup-{DateTime.Now:yyyyMMdd}.zip", Filter = "Zip|*.zip" };
        if (dlg.ShowDialog() != true) return;
        _store.Backup(dlg.FileName); Status = $"Backed up to {dlg.FileName}";
    }

    [RelayCommand]
    private void Restore()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Zip|*.zip" };
        if (dlg.ShowDialog() != true || !Confirm("Replace the toolkit's settings and shortcuts with this backup?")) return;
        try { Status = $"Restored settings and {_store.Restore(dlg.FileName)} shortcuts."; }
        catch (Exception ex) { Status = ex.Message; }
    }
}
