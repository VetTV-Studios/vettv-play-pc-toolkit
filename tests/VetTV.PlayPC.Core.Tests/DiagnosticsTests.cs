using System.IO.Compression;
using VetTV.PlayPC.Core.Checks;
using VetTV.PlayPC.Core.Diagnostics;
using Xunit;

namespace VetTV.PlayPC.Core.Tests;

public class DiagnosticsTests
{
    [Fact]
    public void Redactor_RemovesEmailsProfilePathsAndNames()
    {
        var r = new Redactor(new[] { "vetuser", "VET-PC" });
        var s = r.Redact(@"user vetuser on VET-PC, path C:\Users\vetuser\AppData, mail vet@example.com");
        Assert.DoesNotContain("vetuser", s);
        Assert.DoesNotContain("VET-PC", s);
        Assert.DoesNotContain("vet@example.com", s);
        Assert.Contains(@"C:\Users\<user>\AppData", s);
    }

    [Fact]
    public void Runner_ProducesAllCategories()
    {
        var p = new FakeProbe();
        p.Gpus.Add(new("NVIDIA GeForce RTX 4060", "1", DateTime.Now, null, null));
        var rep = new DiagnosticsRunner(p).Run();
        Assert.Contains(rep.Checks, c => c.Category == "Install");
        Assert.Contains(rep.Checks, c => c.Category == "System");
        Assert.Contains(rep.Checks, c => c.Category == "Graphics");
        Assert.All(rep.Checks.Where(c => c.Status != CheckStatus.Pass), c => Assert.False(string.IsNullOrEmpty(c.FixHint)));
    }

    [Fact]
    public void Export_ZipContainsRedactedLogsAndReport()
    {
        var tmp = Directory.CreateTempSubdirectory("vettv-test-").FullName;
        try
        {
            var logs = Path.Combine(tmp, "Logs");
            Directory.CreateDirectory(logs);
            File.WriteAllText(Path.Combine(logs, "AndroidSerial.log"), @"boot ok for vet@example.com at C:\Users\vetuser\x");
            File.WriteAllBytes(Path.Combine(logs, "dump.bin"), new byte[] { 1, 2, 3 });

            var p = new FakeProbe();
            var rep = new DiagnosticsRunner(p).Run();
            var zipPath = Path.Combine(tmp, "out.zip");
            new DiagnosticsExporter(new Redactor(new[] { "vetuser" }))
                .Export(zipPath, rep, new[] { new LogSource("DeveloperEmulator", logs), new LogSource("PlayGames", Path.Combine(tmp, "missing")) },
                    new ExportOptions(Redact: true, IncludeDxDiag: false));

            using var z = ZipFile.OpenRead(zipPath);
            var names = z.Entries.Select(e => e.FullName).ToList();
            Assert.Contains("system-report.txt", names);
            Assert.Contains("system-report.json", names);
            Assert.Contains("manifest.txt", names);
            Assert.Contains("logs/DeveloperEmulator/AndroidSerial.log", names);
            Assert.DoesNotContain("logs/DeveloperEmulator/dump.bin", names);
            using var sr = new StreamReader(z.GetEntry("logs/DeveloperEmulator/AndroidSerial.log")!.Open());
            var log = sr.ReadToEnd();
            Assert.DoesNotContain("vet@example.com", log);
            Assert.DoesNotContain("vetuser", log);
        }
        finally { Directory.Delete(tmp, true); }
    }
}
