using System.Diagnostics;
using System.Management;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace VetTV.PlayPC.Core.Platform;

/// <summary>Real probe using the registry, WMI/CIM and the file system. Strictly read-only.</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsSystemProbe : ISystemProbe
{
    private const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    public IReadOnlyList<UninstallEntry> GetUninstallEntries()
    {
        var list = new List<UninstallEntry>();
        foreach (var (hive, view) in new[] {
            (RegistryHive.LocalMachine, RegistryView.Registry64), (RegistryHive.LocalMachine, RegistryView.Registry32),
            (RegistryHive.CurrentUser, RegistryView.Default) })
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, view);
                using var key = root.OpenSubKey(UninstallKey);
                if (key is null) continue;
                foreach (var sub in key.GetSubKeyNames())
                {
                    using var k = key.OpenSubKey(sub);
                    if (k?.GetValue("DisplayName") is not string name) continue;
                    list.Add(new UninstallEntry(name, k.GetValue("DisplayVersion") as string,
                        k.GetValue("InstallLocation") as string, k.GetValue("Publisher") as string));
                }
            }
            catch (Exception) { /* key not readable; skip */ }
        }
        return list;
    }

    public bool DirectoryExists(string path) => Directory.Exists(path);
    public bool FileExists(string path) => File.Exists(path);
    public string? GetFileVersion(string path) => File.Exists(path) ? FileVersionInfo.GetVersionInfo(path).FileVersion : null;
    public IReadOnlyList<string> GetRunningProcessNames() => Process.GetProcesses().Select(p => p.ProcessName).ToList();

    public IReadOnlyList<ProcessorInfo> GetProcessors() => Query("SELECT Name, NumberOfCores, NumberOfLogicalProcessors, VirtualizationFirmwareEnabled FROM Win32_Processor",
        o => new ProcessorInfo(Str(o["Name"]) ?? "Unknown CPU", Convert.ToInt32(o["NumberOfCores"] ?? 0),
            Convert.ToInt32(o["NumberOfLogicalProcessors"] ?? 0), o["VirtualizationFirmwareEnabled"] as bool?));

    public ulong? GetTotalPhysicalMemoryBytes() =>
        Query("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem", o => (ulong?)Convert.ToUInt64(o["TotalPhysicalMemory"] ?? 0UL)).FirstOrDefault();

    public bool? IsHypervisorPresent() =>
        Query("SELECT HypervisorPresent FROM Win32_ComputerSystem", o => o["HypervisorPresent"] as bool?).FirstOrDefault();

    public IReadOnlyDictionary<string, bool> GetOptionalFeatures()
    {
        // InstallState: 1 = Enabled, 2 = Disabled, 3 = Absent.
        var d = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var (n, s) in Query("SELECT Name, InstallState FROM Win32_OptionalFeature", o => (Str(o["Name"]) ?? "", Convert.ToInt32(o["InstallState"] ?? 0))))
            if (n.Length > 0) d[n] = s == 1;
        return d;
    }

    public IReadOnlyList<GpuAdapter> GetGpuAdapters() => Query("SELECT Name, DriverVersion, DriverDate, AdapterRAM, VideoProcessor FROM Win32_VideoController",
        o => new GpuAdapter(Str(o["Name"]) ?? "Unknown GPU", Str(o["DriverVersion"]), ParseCim(Str(o["DriverDate"])),
            o["AdapterRAM"] is null ? null : Convert.ToUInt64(o["AdapterRAM"]), Str(o["VideoProcessor"])));

    public OsInfo GetOs() => Query("SELECT Caption, Version, BuildNumber FROM Win32_OperatingSystem",
        o => new OsInfo(Str(o["Caption"]) ?? "Windows", Str(o["Version"]) ?? "", int.TryParse(Str(o["BuildNumber"]), out var b) ? b : 0))
        .FirstOrDefault() ?? new OsInfo("Windows", Environment.OSVersion.VersionString, Environment.OSVersion.Version.Build);

    public string ExpandPath(string path) => Environment.ExpandEnvironmentVariables(path);

    private static List<T> Query<T>(string wql, Func<ManagementBaseObject, T> map)
    {
        var r = new List<T>();
        try
        {
            using var s = new ManagementObjectSearcher(wql);
            foreach (var o in s.Get()) using (o) r.Add(map(o));
        }
        catch (Exception) { /* WMI unavailable: callers treat empty as unknown */ }
        return r;
    }

    private static string? Str(object? o) => o?.ToString();

    private static DateTime? ParseCim(string? s)
    {
        if (s is null) return null;
        try { return ManagementDateTimeConverter.ToDateTime(s); } catch { return null; }
    }
}
