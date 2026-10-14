using System.Collections.ObjectModel;
using System.Runtime.Versioning;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VetTV.PlayPC.Core;
using VetTV.PlayPC.Core.Checks;
using VetTV.PlayPC.Core.Diagnostics;
using VetTV.PlayPC.Core.Platform;

namespace VetTV.PlayPC.App;

[SupportedOSPlatform("windows")]
public partial class MainViewModel : ObservableObject
{
    private readonly WindowsSystemProbe _probe = new();
    private DiagnosticsReport? _report;

    public ObservableCollection<CheckResult> Results { get; } = new();
    public string Disclaimer => ToolkitInfo.Disclaimer;
    public FixLaunchViewModel Fix { get; } = new();
    public DeveloperViewModel Developer { get; } = new();

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(RunCommand), nameof(ExportCommand))] private bool _isBusy;
    [ObservableProperty] private string _status = "Click 'Run diagnostics' to check this PC.";
    [ObservableProperty] private bool _redact = true;
    [ObservableProperty] private bool _includeDxDiag = true;

    private bool CanRun() => !IsBusy;
    private bool CanExport() => !IsBusy && _report is not null;

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunAsync()
    {
        IsBusy = true; Status = "Running checks...";
        try
        {
            _report = await Task.Run(() => new DiagnosticsRunner(_probe).Run());
            Results.Clear();
            foreach (var r in _report.Checks) Results.Add(r);
            Status = $"Overall: {_report.Overall}. {Results.Count(r => r.Status == CheckStatus.Fail)} fail, {Results.Count(r => r.Status == CheckStatus.Warn)} warn.";
        }
        catch (Exception ex) { Status = $"Checks failed: {ex.Message}"; }
        finally { IsBusy = false; ExportCommand.NotifyCanExecuteChanged(); }
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportAsync()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog { FileName = $"VetTV-PlayPC-diagnostics-{DateTime.Now:yyyyMMdd-HHmm}.zip", Filter = "Zip archive|*.zip" };
        if (dlg.ShowDialog() != true || _report is null) return;
        IsBusy = true; Status = "Exporting diagnostics...";
        try
        {
            var report = _report; var opts = new ExportOptions(Redact, IncludeDxDiag);
            var path = await Task.Run(() => new DiagnosticsExporter(Redactor.ForCurrentUser())
                .Export(dlg.FileName, report, LogCollector.DefaultSources(_probe.ExpandPath), opts));
            Status = $"Saved {path}";
        }
        catch (Exception ex) { Status = $"Export failed: {ex.Message}"; }
        finally { IsBusy = false; }
    }
}
