using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using VetTV.PlayPC.Core.Checks;

namespace VetTV.PlayPC.Core.Diagnostics;

public sealed record ExportOptions(bool Redact = true, bool IncludeDxDiag = true);

/// <summary>Builds a diagnostics zip: system report (txt + json), check results, logs, optional DxDiag.</summary>
public sealed class DiagnosticsExporter(Redactor redactor)
{
    public string Export(string zipPath, DiagnosticsReport report, IEnumerable<LogSource> logSources, ExportOptions options)
    {
        if (File.Exists(zipPath)) File.Delete(zipPath);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(zipPath))!);
        string R(string s) => options.Redact ? redactor.Redact(s) : s;

        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        Write(zip, "system-report.txt", R(report.ToText()));
        Write(zip, "system-report.json", R(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true })));

        var manifest = new StringBuilder();
        manifest.AppendLine($"VetTV Play PC Toolkit diagnostics. {ToolkitInfo.Disclaimer}");
        manifest.AppendLine($"Created: {DateTimeOffset.Now:O}. Redaction: {(options.Redact ? "on" : "off")}.");

        foreach (var src in logSources)
        {
            if (!Directory.Exists(src.Directory)) { manifest.AppendLine($"[{src.Label}] not found"); continue; }
            foreach (var file in Directory.EnumerateFiles(src.Directory, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(src.Directory, file).Replace('\\', '/');
                var entry = $"logs/{src.Label}/{R(rel)}";
                try
                {
                    var info = new FileInfo(file);
                    if (info.Length > LogCollector.MaxFileBytes) { manifest.AppendLine($"skipped (too large): {entry}"); continue; }
                    if (LogCollector.TextExtensions.Contains(info.Extension, StringComparer.OrdinalIgnoreCase) || info.Extension.Length == 0)
                    {
                        // Logs may be open by GPGPC; share read/write.
                        using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                        using var sr = new StreamReader(fs);
                        Write(zip, entry, R(sr.ReadToEnd()));
                    }
                    else if (!options.Redact)
                        zip.CreateEntryFromFile(file, entry);
                    else { manifest.AppendLine($"skipped (binary, redaction on): {entry}"); continue; }
                    manifest.AppendLine($"added: {entry}");
                }
                catch (Exception ex) { manifest.AppendLine($"error reading {entry}: {ex.GetType().Name}"); }
            }
        }

        if (options.IncludeDxDiag && OperatingSystem.IsWindows())
        {
            var dx = TryDxDiag();
            if (dx is not null) Write(zip, "dxdiag.txt", R(dx)); else manifest.AppendLine("dxdiag: unavailable");
        }

        Write(zip, "manifest.txt", manifest.ToString());
        return zipPath;
    }

    private static void Write(ZipArchive zip, string name, string content)
    {
        using var w = new StreamWriter(zip.CreateEntry(name, CompressionLevel.Optimal).Open(), new UTF8Encoding(false));
        w.Write(content);
    }

    private static string? TryDxDiag()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"vettv-dxdiag-{Guid.NewGuid():N}.txt");
        try
        {
            using var p = Process.Start(new ProcessStartInfo("dxdiag", $"/whql:off /t \"{tmp}\"") { UseShellExecute = false, CreateNoWindow = true });
            if (p is null || !p.WaitForExit(90_000)) { try { p?.Kill(); } catch { } return null; }
            return File.Exists(tmp) ? File.ReadAllText(tmp) : null;
        }
        catch (Exception) { return null; }
        finally { try { File.Delete(tmp); } catch { } }
    }
}
