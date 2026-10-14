namespace VetTV.PlayPC.Core.Platform;

/// <summary>Uninstall-registry entry (HKLM/HKCU ...\Uninstall\*).</summary>
public sealed record UninstallEntry(string DisplayName, string? DisplayVersion, string? InstallLocation, string? Publisher);

public sealed record ProcessorInfo(string Name, int Cores, int LogicalProcessors, bool? VirtualizationFirmwareEnabled);

public sealed record GpuAdapter(string Name, string? DriverVersion, DateTime? DriverDate, ulong? AdapterRamBytes, string? VideoProcessor);

public sealed record OsInfo(string Caption, string Version, int Build);

/// <summary>Read-only view of the machine. Windows implementation in <see cref="WindowsSystemProbe"/>; tests use fakes.</summary>
public interface ISystemProbe
{
    IReadOnlyList<UninstallEntry> GetUninstallEntries();
    bool DirectoryExists(string path);
    bool FileExists(string path);
    string? GetFileVersion(string path);
    IReadOnlyList<string> GetRunningProcessNames();
    IReadOnlyList<ProcessorInfo> GetProcessors();
    ulong? GetTotalPhysicalMemoryBytes();
    bool? IsHypervisorPresent();
    /// <summary>Optional feature name -> enabled. Missing key = unknown.</summary>
    IReadOnlyDictionary<string, bool> GetOptionalFeatures();
    IReadOnlyList<GpuAdapter> GetGpuAdapters();
    OsInfo GetOs();
    string ExpandPath(string path);
}
