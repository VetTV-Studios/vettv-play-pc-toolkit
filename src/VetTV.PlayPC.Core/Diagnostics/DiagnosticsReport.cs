using System.Text;
using VetTV.PlayPC.Core.Checks;
using VetTV.PlayPC.Core.Platform;

namespace VetTV.PlayPC.Core.Diagnostics;

public sealed record DiagnosticsReport(DateTimeOffset CreatedAt, OsInfo Os, IReadOnlyList<InstallInfo> Installs,
    IReadOnlyList<GpuAdapter> Gpus, IReadOnlyList<CheckResult> Checks)
{
    public CheckStatus Overall => Checks.Any(c => c.Status == CheckStatus.Fail) ? CheckStatus.Fail
        : Checks.Any(c => c.Status == CheckStatus.Warn) ? CheckStatus.Warn : CheckStatus.Pass;

    public string ToText()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{ToolkitInfo.ProductName} system report");
        sb.AppendLine(ToolkitInfo.Disclaimer);
        sb.AppendLine($"Created: {CreatedAt:O}");
        sb.AppendLine($"OS: {Os.Caption} {Os.Version} (build {Os.Build})");
        sb.AppendLine($"Overall: {Overall.ToString().ToUpperInvariant()}");
        sb.AppendLine();
        foreach (var g in Checks.GroupBy(c => c.Category))
        {
            sb.AppendLine($"== {g.Key} ==");
            foreach (var c in g)
            {
                sb.AppendLine($"[{c.Status.ToString().ToUpperInvariant()}] {c.Title}: {c.Detail}");
                if (c.FixHint is not null) sb.AppendLine($"       Fix: {c.FixHint}");
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }
}

/// <summary>Runs every M1 read-only check.</summary>
public sealed class DiagnosticsRunner(ISystemProbe probe)
{
    public DiagnosticsReport Run()
    {
        var installDetector = new InstallDetector(probe);
        var installs = installDetector.Detect();
        var gpu = new GpuInspector(probe);
        var checks = installDetector.ToChecks(installs)
            .Concat(new VirtualizationChecker(probe).Run())
            .Concat(gpu.Run()).ToList();
        return new DiagnosticsReport(DateTimeOffset.Now, probe.GetOs(), installs, gpu.Adapters(), checks);
    }
}
