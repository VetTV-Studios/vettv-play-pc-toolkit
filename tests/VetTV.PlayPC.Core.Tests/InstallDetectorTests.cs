using VetTV.PlayPC.Core.Checks;
using VetTV.PlayPC.Core.Platform;
using Xunit;

namespace VetTV.PlayPC.Core.Tests;

public class InstallDetectorTests
{
    [Fact]
    public void NothingInstalled_ReportsNotInstalled()
    {
        var r = new InstallDetector(new FakeProbe()).Detect();
        Assert.All(r, i => Assert.False(i.Installed));
    }

    [Fact]
    public void ConsumerFromRegistry_ReportsVersionAndRunning()
    {
        var p = new FakeProbe();
        p.Uninstall.Add(new("Google Play Games", "25.9.1000.5", @"C:\Program Files\Google\Play Games", "Google LLC"));
        p.Processes.Add("crosvm");
        var c = new InstallDetector(p).Detect().Single(i => i.Product == PlayGamesProduct.Consumer);
        Assert.True(c.Installed);
        Assert.Equal("25.9.1000.5", c.Version);
        Assert.True(c.Running);
        Assert.Equal("registry", c.Source);
        Assert.False(new InstallDetector(p).Detect().Single(i => i.Product == PlayGamesProduct.DeveloperEmulator).Installed);
    }

    [Fact]
    public void EmulatorEntry_IsNotMistakenForConsumer_AndTrackDetected()
    {
        var p = new FakeProbe();
        p.Uninstall.Add(new("Google Play Games Developer Emulator Beta", "25.8.0", null, "Google LLC"));
        var r = new InstallDetector(p).Detect();
        Assert.False(r.Single(i => i.Product == PlayGamesProduct.Consumer).Installed);
        var e = r.Single(i => i.Product == PlayGamesProduct.DeveloperEmulator);
        Assert.True(e.Installed);
        Assert.Equal("Beta", e.Track);
    }

    [Fact]
    public void PathProbeFallback_UsesFileVersion()
    {
        var p = new FakeProbe();
        p.Dirs.Add(@"C:\Program Files\Google\Play Games");
        p.FileVersions[Path.Combine(@"C:\Program Files\Google\Play Games", "current", "client.exe")] = "25.1.0.0";
        var c = new InstallDetector(p).Detect().Single(i => i.Product == PlayGamesProduct.Consumer);
        Assert.True(c.Installed);
        Assert.Equal("path", c.Source);
        Assert.Equal("25.1.0.0", c.Version);
    }

    [Fact]
    public void Checks_NonPassHaveFixHints()
    {
        var d = new InstallDetector(new FakeProbe());
        Assert.All(d.ToChecks(d.Detect()).Where(c => c.Status != CheckStatus.Pass), c => Assert.False(string.IsNullOrEmpty(c.FixHint)));
    }
}
