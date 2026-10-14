using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VetTV.PlayPC.Core.Checks;
using VetTV.PlayPC.Core.Developer;
using VetTV.PlayPC.Core.Platform;
using VetTV.PlayPC.Core.Tips;

namespace VetTV.PlayPC.App;

/// <summary>M3: Developer Emulator sideloading + logcat, and controller tips.</summary>
[SupportedOSPlatform("windows")]
public partial class DeveloperViewModel : ObservableObject
{
    private readonly WindowsSystemProbe _probe = new();
    private AdbService? _adb;
    private Process? _logcat;
    private const int MaxLogLines = 5000;

    public string Disclaimer => AdbService.Disclaimer;
    public IReadOnlyList<ControllerTip> Tips => ControllerTips.All;
    public ObservableCollection<string> Packages { get; } = new();
    public ObservableCollection<string> LogLines { get; } = new();
    [ObservableProperty] private string? _selectedPackage;
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private string _logFilter = "";
    [ObservableProperty] private string _bundletoolPath = "";
    [ObservableProperty] private bool _isLogging;

    private bool EnsureAdb()
    {
        if (_adb is not null) return true;
        try { _adb = AdbService.Create(new InstallDetector(_probe).Detect(), _probe, new ProcessCommandRunner()); return true; }
        catch (SideloadRefusedException ex) { Status = ex.Message; return false; }
    }

    private async Task Do(string busy, Func<AdbService, string> action)
    {
        if (!EnsureAdb()) return;
        Status = busy;
        try { Status = await Task.Run(() => action(_adb!)); }
        catch (Exception ex) { Status = ex.Message; }
    }

    [RelayCommand] private Task ConnectAsync() => Do("Connecting to localhost:6520...", a => a.Connect() is var r && r.Success ? "Connected to the Developer Emulator." : $"Connect failed: {r.Output}. Is the Developer Emulator running?");

    [RelayCommand]
    private async Task RefreshPackagesAsync()
    {
        if (!EnsureAdb()) return;
        var list = await Task.Run(() => _adb!.ListPackages());
        Packages.Clear();
        foreach (var p in list) Packages.Add(p);
        Status = $"{Packages.Count} installed apps.";
    }

    [RelayCommand]
    private async Task InstallAsync()
    {
        if (!EnsureAdb()) return;
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Android app (*.apk;*.aab)|*.apk;*.aab" };
        if (dlg.ShowDialog() != true) return;
        if (MessageBox.Show($"{Disclaimer}\n\nInstall {System.IO.Path.GetFileName(dlg.FileName)}?", "Install app", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        var isAab = dlg.FileName.EndsWith(".aab", StringComparison.OrdinalIgnoreCase);
        if (isAab && string.IsNullOrWhiteSpace(BundletoolPath)) { Status = "Set the path to your bundletool .jar first (AAB installs need it)."; return; }
        await Do("Installing...", a => (isAab ? a.InstallAab(dlg.FileName, BundletoolPath) : a.InstallApk(dlg.FileName)) is var r && r.Success ? "Installed." : $"Install failed: {r.Output}");
        await RefreshPackagesAsync();
    }

    [RelayCommand]
    private async Task UninstallAsync()
    {
        if (SelectedPackage is null || MessageBox.Show($"Uninstall {SelectedPackage} from the Developer Emulator?", "Uninstall", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        var pkg = SelectedPackage;
        await Do("Uninstalling...", a => a.Uninstall(pkg) is var r && r.Success ? $"Uninstalled {pkg}." : $"Uninstall failed: {r.Output}");
        await RefreshPackagesAsync();
    }

    [RelayCommand]
    private void ToggleLogcat()
    {
        if (_logcat is not null) { StopLogcat(); return; }
        if (!EnsureAdb()) return;
        string args;
        try { args = _adb!.LogcatArguments(string.IsNullOrWhiteSpace(LogFilter) ? null : LogFilter.Trim()); }
        catch (ArgumentException ex) { Status = ex.Message; return; }
        LogLines.Clear();
        var p = new Process { StartInfo = new ProcessStartInfo(_adb!.AdbPath, args) { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true }, EnableRaisingEvents = true };
        p.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                LogLines.Add(e.Data);
                while (LogLines.Count > MaxLogLines) LogLines.RemoveAt(0);
            });
        };
        p.Exited += (_, _) => Application.Current.Dispatcher.BeginInvoke(() => { _logcat = null; IsLogging = false; });
        p.Start(); p.BeginOutputReadLine();
        _logcat = p; IsLogging = true; Status = "Logcat running.";
    }

    public void StopLogcat()
    {
        try { if (_logcat is { HasExited: false }) _logcat.Kill(); } catch { }
        _logcat?.Dispose(); _logcat = null; IsLogging = false;
    }

    [RelayCommand]
    private void BrowseBundletool()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "bundletool (*.jar)|*.jar" };
        if (dlg.ShowDialog() == true) BundletoolPath = dlg.FileName;
    }
}
