using VetTV.PlayPC.Core.Platform;

namespace VetTV.PlayPC.Core.Tests;

internal sealed class FakeProbe : ISystemProbe
{
    public List<UninstallEntry> Uninstall { get; } = new();
    public HashSet<string> Dirs { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> FileVersions { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Processes { get; } = new();
    public List<ProcessorInfo> Cpus { get; } = new() { new("Intel Core i7-6700HQ", 4, 8, true) };
    public ulong? Ram { get; set; } = 16UL << 30;
    public bool? Hypervisor { get; set; } = false;
    public Dictionary<string, bool> Features { get; } = new(StringComparer.OrdinalIgnoreCase)
        { ["HypervisorPlatform"] = true, ["VirtualMachinePlatform"] = true, ["Microsoft-Hyper-V-All"] = false };
    public List<GpuAdapter> Gpus { get; } = new();
    public OsInfo Os { get; set; } = new("Microsoft Windows 11 Home", "10.0.22631", 22631);

    public IReadOnlyList<UninstallEntry> GetUninstallEntries() => Uninstall;
    public bool DirectoryExists(string path) => Dirs.Contains(path);
    public bool FileExists(string path) => FileVersions.ContainsKey(path) || FileVersions.ContainsKey(path.Replace('/', '\\'));
    public string? GetFileVersion(string path) => FileVersions.GetValueOrDefault(path);
    public IReadOnlyList<string> GetRunningProcessNames() => Processes;
    public IReadOnlyList<ProcessorInfo> GetProcessors() => Cpus;
    public ulong? GetTotalPhysicalMemoryBytes() => Ram;
    public bool? IsHypervisorPresent() => Hypervisor;
    public IReadOnlyDictionary<string, bool> GetOptionalFeatures() => Features;
    public IReadOnlyList<GpuAdapter> GetGpuAdapters() => Gpus;
    public OsInfo GetOs() => Os;
    public string ExpandPath(string path) => path.Replace("%ProgramFiles%", @"C:\Program Files");
}
