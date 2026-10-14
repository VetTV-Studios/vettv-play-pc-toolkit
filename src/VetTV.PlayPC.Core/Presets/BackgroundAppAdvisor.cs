using System.Diagnostics;

namespace VetTV.PlayPC.Core.Presets;

public sealed record BackgroundApp(int Pid, string Name, string Title, long WorkingSetBytes);

/// <summary>Lists heavy windowed apps and closes only the ones the user picks, politely (like clicking X). Never force-kills.</summary>
public static class BackgroundAppAdvisor
{
    private static readonly HashSet<string> Protected = new(StringComparer.OrdinalIgnoreCase)
        { "explorer", "client", "crosvm", "VetTV.PlayPC.App", "dwm", "csrss", "winlogon", "System", "Idle", "steam" };

    public static IReadOnlyList<BackgroundApp> HeavyApps(long minBytes = 300L * 1024 * 1024)
    {
        var list = new List<BackgroundApp>();
        foreach (var p in Process.GetProcesses())
        {
            using (p)
            {
                try
                {
                    if (Protected.Contains(p.ProcessName) || p.MainWindowHandle == IntPtr.Zero || p.WorkingSet64 < minBytes) continue;
                    list.Add(new BackgroundApp(p.Id, p.ProcessName, p.MainWindowTitle, p.WorkingSet64));
                }
                catch (Exception) { /* access denied / exited */ }
            }
        }
        return list.OrderByDescending(a => a.WorkingSetBytes).ToList();
    }

    public static bool RequestClose(int pid)
    {
        try { using var p = Process.GetProcessById(pid); return !Protected.Contains(p.ProcessName) && p.CloseMainWindow(); }
        catch (Exception) { return false; }
    }
}
