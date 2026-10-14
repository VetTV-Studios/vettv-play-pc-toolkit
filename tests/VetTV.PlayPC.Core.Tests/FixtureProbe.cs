using System.Text.Json;
using VetTV.PlayPC.Core.Platform;

namespace VetTV.PlayPC.Core.Tests;

/// <summary>Loads a hardware fixture (Fixtures/*.json) into a FakeProbe.</summary>
internal static class FixtureProbe
{
    public static FakeProbe Load(string name)
    {
        var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name))).RootElement;
        var p = new FakeProbe();
        var os = doc.GetProperty("os");
        p.Os = new OsInfo(os.GetProperty("caption").GetString()!, os.GetProperty("version").GetString()!, os.GetProperty("build").GetInt32());
        p.Ram = doc.GetProperty("ramBytes").GetUInt64();
        p.Hypervisor = doc.GetProperty("hypervisorPresent").GetBoolean();
        p.Cpus.Clear();
        foreach (var c in doc.GetProperty("cpus").EnumerateArray())
            p.Cpus.Add(new ProcessorInfo(c.GetProperty("name").GetString()!, c.GetProperty("cores").GetInt32(), c.GetProperty("logical").GetInt32(), c.GetProperty("vtFirmware").GetBoolean()));
        p.Features.Clear();
        foreach (var f in doc.GetProperty("features").EnumerateObject()) p.Features[f.Name] = f.Value.GetBoolean();
        foreach (var g in doc.GetProperty("gpus").EnumerateArray())
            p.Gpus.Add(new GpuAdapter(g.GetProperty("name").GetString()!, g.GetProperty("driverVersion").GetString(),
                DateTime.Now.AddDays(-g.GetProperty("driverAgeDays").GetInt32()),
                g.TryGetProperty("adapterRam", out var r) ? r.GetUInt64() : null, null));
        foreach (var u in doc.GetProperty("uninstall").EnumerateArray())
            p.Uninstall.Add(new UninstallEntry(u.GetProperty("displayName").GetString()!, u.GetProperty("displayVersion").GetString(),
                u.GetProperty("installLocation").GetString(), "Google LLC"));
        foreach (var f in doc.GetProperty("files").EnumerateArray()) p.FileVersions[f.GetString()!] = "1.0";
        return p;
    }
}
