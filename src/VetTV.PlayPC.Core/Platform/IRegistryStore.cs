using System.Runtime.Versioning;
using Microsoft.Win32;

namespace VetTV.PlayPC.Core.Platform;

public enum RegValueKind { String, DWord }

public sealed record RegValue(RegValueKind Kind, string Data);

/// <summary>Current-user registry access. Presets only ever touch HKCU.</summary>
public interface IRegistryStore
{
    RegValue? Get(string keyPath, string name);
    void Set(string keyPath, string name, RegValue value);
    void Delete(string keyPath, string name);
}

[SupportedOSPlatform("windows")]
public sealed class WindowsRegistryStore : IRegistryStore
{
    public RegValue? Get(string keyPath, string name)
    {
        using var k = Registry.CurrentUser.OpenSubKey(keyPath);
        var v = k?.GetValue(name);
        return v switch { null => null, int i => new RegValue(RegValueKind.DWord, i.ToString()), _ => new RegValue(RegValueKind.String, v.ToString()!) };
    }

    public void Set(string keyPath, string name, RegValue value)
    {
        using var k = Registry.CurrentUser.CreateSubKey(keyPath, writable: true);
        if (value.Kind == RegValueKind.DWord) k.SetValue(name, int.Parse(value.Data), RegistryValueKind.DWord);
        else k.SetValue(name, value.Data, RegistryValueKind.String);
    }

    public void Delete(string keyPath, string name)
    {
        using var k = Registry.CurrentUser.OpenSubKey(keyPath, writable: true);
        k?.DeleteValue(name, throwOnMissingValue: false);
    }
}

/// <summary>Runs a command and returns exit code + stdout. Used for powercfg.</summary>
public interface ICommandRunner { (int ExitCode, string Output) Run(string file, string args); }

public sealed class ProcessCommandRunner : ICommandRunner
{
    public (int ExitCode, string Output) Run(string file, string args)
    {
        using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(file, args)
            { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true })!;
        var o = p.StandardOutput.ReadToEnd();
        p.WaitForExit(30_000);
        return (p.ExitCode, o);
    }
}
