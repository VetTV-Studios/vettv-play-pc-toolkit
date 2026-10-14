using VetTV.PlayPC.Core.Platform;

namespace VetTV.PlayPC.Core.Checks;

/// <summary>Read-only checks for system requirements and virtualization. Nothing here changes the system.</summary>
public sealed class VirtualizationChecker(ISystemProbe probe)
{
    internal const ulong MinRamBytes = 8UL * 1024 * 1024 * 1024;
    internal const int MinBuild = 19041; // Windows 10 2004
    private const string Cat = "System";

    public IEnumerable<CheckResult> Run()
    {
        var os = probe.GetOs();
        yield return os.Build >= MinBuild
            ? CheckResult.Pass("os.version", Cat, "Windows version", $"{os.Caption} (build {os.Build}).")
            : CheckResult.Fail("os.version", Cat, "Windows version", $"{os.Caption} (build {os.Build}) is older than Windows 10 2004.",
                "Update to Windows 10 2004 or later, or Windows 11.");

        var ram = probe.GetTotalPhysicalMemoryBytes();
        if (ram is null)
            yield return CheckResult.Warn("hw.ram", Cat, "Memory", "Could not read installed memory.", "Check Settings > System > About.");
        else
        {
            // Firmware reserves some RAM, so allow a 5% margin below 8 GB.
            var gb = ram.Value / 1024d / 1024 / 1024;
            yield return ram.Value >= MinRamBytes * 95 / 100
                ? CheckResult.Pass("hw.ram", Cat, "Memory", $"{gb:F1} GB installed.")
                : CheckResult.Fail("hw.ram", Cat, "Memory", $"{gb:F1} GB installed; 8 GB is required.", "Add RAM to reach at least 8 GB.");
        }

        var cpus = probe.GetProcessors();
        var cores = cpus.Sum(c => c.Cores);
        var cpuName = cpus.FirstOrDefault()?.Name ?? "Unknown CPU";
        if (cpus.Count == 0)
            yield return CheckResult.Warn("hw.cpu", Cat, "CPU cores", "Could not read processor info.", "Check Task Manager > Performance > CPU.");
        else
            yield return cores >= 4
                ? CheckResult.Pass("hw.cpu", Cat, "CPU cores", $"{cpuName}: {cores} physical cores.")
                : CheckResult.Fail("hw.cpu", Cat, "CPU cores", $"{cpuName}: {cores} physical cores; 4 are required.", "A CPU with 4 or more physical cores is required.");

        var vt = cpus.Select(c => c.VirtualizationFirmwareEnabled).FirstOrDefault(v => v.HasValue);
        var hv = probe.IsHypervisorPresent();
        if (vt == true || hv == true)
            // When a hypervisor is already running, Windows reports firmware VT as false even though it is on.
            yield return CheckResult.Pass("virt.firmware", Cat, "Hardware virtualization", hv == true ? "A hypervisor is running, so VT-x/AMD-V is on." : "VT-x/AMD-V is enabled in firmware.");
        else if (vt == false)
            yield return CheckResult.Fail("virt.firmware", Cat, "Hardware virtualization", "VT-x/AMD-V is disabled in firmware.",
                "Restart into BIOS/UEFI setup and enable Intel Virtualization Technology (VT-x) or AMD SVM, then save and reboot.");
        else
            yield return CheckResult.Warn("virt.firmware", Cat, "Hardware virtualization", "Could not determine firmware virtualization state.",
                "Check Task Manager > Performance > CPU > Virtualization.");

        var f = probe.GetOptionalFeatures();
        yield return Feature(f, "HypervisorPlatform", "Windows Hypervisor Platform", required: true);
        yield return Feature(f, "VirtualMachinePlatform", "Virtual Machine Platform", required: false);
        yield return Feature(f, "Microsoft-Hyper-V-All", "Hyper-V", required: false);
    }

    private static CheckResult Feature(IReadOnlyDictionary<string, bool> f, string name, string title, bool required)
    {
        var id = "feature." + name;
        if (!f.TryGetValue(name, out var on))
            return CheckResult.Warn(id, Cat, title, "Feature state unknown (not reported by Windows).",
                "Open 'Turn Windows features on or off' to check it manually.");
        if (on) return CheckResult.Pass(id, Cat, title, "Enabled.");
        var fix = $"Enable it in 'Turn Windows features on or off', or run as admin: Enable-WindowsOptionalFeature -Online -FeatureName {name}, then reboot.";
        return required ? CheckResult.Fail(id, Cat, title, "Disabled; Google Play Games requires it.", fix)
                        : CheckResult.Warn(id, Cat, title, "Disabled. Usually optional, but recommended if games fail to start.", fix);
    }
}
