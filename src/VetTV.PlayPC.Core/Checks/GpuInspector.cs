using VetTV.PlayPC.Core.Platform;

namespace VetTV.PlayPC.Core.Checks;

/// <summary>GPU and driver report with hybrid-GPU and known-hardware guidance.</summary>
public sealed class GpuInspector(ISystemProbe probe)
{
    private const string Cat = "Graphics";
    private const string GpuPrefHint = "Windows Settings > System > Display > Graphics: add Google Play Games and set it to High performance (discrete GPU).";

    public IReadOnlyList<GpuAdapter> Adapters() => probe.GetGpuAdapters()
        .Where(a => !a.Name.Contains("Basic Display", StringComparison.OrdinalIgnoreCase)
                 && !a.Name.Contains("Remote Display", StringComparison.OrdinalIgnoreCase)).ToList();

    public IEnumerable<CheckResult> Run()
    {
        var adapters = Adapters();
        if (adapters.Count == 0)
        {
            yield return CheckResult.Fail("gpu.none", Cat, "Graphics adapter", "No hardware GPU driver found (only a basic display driver or none).",
                "Install the GPU driver from your PC or GPU maker.");
            yield break;
        }
        var discrete = adapters.Where(IsDiscrete).ToList();
        foreach (var a in adapters)
            yield return Assess(a, hybridHasDiscrete: discrete.Count > 0 && !IsDiscrete(a));
        if (discrete.Count > 0 && adapters.Count > discrete.Count)
            yield return CheckResult.Warn("gpu.hybrid", Cat, "Hybrid graphics",
                $"This PC has integrated + discrete GPUs. Google Play Games should run on {discrete[0].Name}.", GpuPrefHint);
    }

    internal static bool IsDiscrete(GpuAdapter a)
    {
        var n = a.Name;
        return n.Contains("GeForce", StringComparison.OrdinalIgnoreCase) || n.Contains("Quadro", StringComparison.OrdinalIgnoreCase)
            || n.Contains("RTX", StringComparison.OrdinalIgnoreCase) || n.Contains("Radeon R9", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Radeon RX", StringComparison.OrdinalIgnoreCase) || n.Contains("Radeon Pro", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Arc A", StringComparison.OrdinalIgnoreCase) || n.Contains("Arc B", StringComparison.OrdinalIgnoreCase);
    }

    internal static CheckResult Assess(GpuAdapter a, bool hybridHasDiscrete)
    {
        var id = "gpu." + new string(a.Name.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        var drv = $"Driver {a.DriverVersion ?? "unknown"}{(a.DriverDate is { } d ? $" ({d:yyyy-MM-dd})" : "")}";
        var n = a.Name;

        if (n.Contains("HD Graphics 530", StringComparison.OrdinalIgnoreCase) || n.Contains("HD Graphics 5", StringComparison.OrdinalIgnoreCase)
            || n.Contains("HD Graphics 4", StringComparison.OrdinalIgnoreCase))
            return CheckResult.Warn(id, Cat, n, $"{drv}. Below the Intel UHD 630 class guideline.",
                hybridHasDiscrete ? GpuPrefHint : "Performance may be poor; a stronger GPU is recommended.");

        if (n.Contains("960M", StringComparison.OrdinalIgnoreCase))
            return CheckResult.Warn(id, Cat, n, $"{drv}. Supported, older: NVIDIA's 900M series is on the legacy, security-only driver branch (582.xx).",
                "Keep the latest legacy NVIDIA driver installed; no newer feature drivers will ship for this GPU.");

        if (n.Contains("R9 M390", StringComparison.OrdinalIgnoreCase) || n.Contains("R9 M3", StringComparison.OrdinalIgnoreCase))
            return CheckResult.Warn(id, Cat, n, $"{drv}. May not meet requirements; AMD no longer ships drivers for this GPU (Boot Camp).",
                "Use the latest Boot Camp/AMD driver available. The toolkit won't block you, but games may fail or run slowly.");

        if (a.DriverDate is { } dd && dd < DateTime.Now.AddYears(-2))
            return CheckResult.Warn(id, Cat, n, $"{drv}. Driver is over 2 years old.", "Update the GPU driver from your GPU or PC maker.");

        return CheckResult.Pass(id, Cat, n, $"{drv}.");
    }
}
