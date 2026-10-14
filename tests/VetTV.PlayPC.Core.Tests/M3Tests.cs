using VetTV.PlayPC.Core.Checks;
using VetTV.PlayPC.Core.Developer;
using VetTV.PlayPC.Core.Diagnostics;
using VetTV.PlayPC.Core.Platform;
using Xunit;

namespace VetTV.PlayPC.Core.Tests;

internal sealed class RecordingRunner : ICommandRunner
{
    public List<(string File, string Args)> Calls { get; } = new();
    public Func<string, string, (int, string)> Respond { get; set; } = (_, _) => (0, "");
    public (int ExitCode, string Output) Run(string file, string args) { Calls.Add((file, args)); return Respond(file, args); }
}

public class MsiLaptopFixtureTests
{
    [Fact]
    public void Msi_AllSystemChecksPass_AndHybridGuidanceMentionsNvidia()
    {
        var p = FixtureProbe.Load("msi-rtx3050-optimus.json");
        var rep = new DiagnosticsRunner(p).Run();
        Assert.DoesNotContain(rep.Checks, c => c.Status == CheckStatus.Fail);
        Assert.All(rep.Checks.Where(c => c.Category == "System" && c.Id != "feature.Microsoft-Hyper-V-All"), c => Assert.Equal(CheckStatus.Pass, c.Status));
        var rtx = rep.Checks.Single(c => c.Title.Contains("RTX 3050"));
        Assert.Equal(CheckStatus.Pass, rtx.Status);
        var hybrid = rep.Checks.Single(c => c.Id == "gpu.hybrid");
        Assert.Contains("RTX 3050", hybrid.Detail);
        Assert.Contains("NVIDIA Control Panel", hybrid.FixHint);
        Assert.Contains("High performance", hybrid.FixHint);
    }

    [Fact]
    public void Msi_UhdIsNotFlaggedAsBelowGuideline()
    {
        var p = FixtureProbe.Load("msi-rtx3050-optimus.json");
        var uhd = new GpuInspector(p).Run().Single(c => c.Title.Contains("UHD"));
        Assert.Equal(CheckStatus.Pass, uhd.Status);
    }

    [Fact]
    public void Msi_DetectsBothInstalls()
    {
        var r = new InstallDetector(FixtureProbe.Load("msi-rtx3050-optimus.json")).Detect();
        Assert.All(r, i => Assert.True(i.Installed));
    }
}

public class AdbServiceTests
{
    private static AdbService ForMsi(RecordingRunner run)
    {
        var p = FixtureProbe.Load("msi-rtx3050-optimus.json");
        return AdbService.Create(new InstallDetector(p).Detect(), p, run);
    }

    [Fact]
    public void ConsumerOnly_RefusesClearly()
    {
        var p = FixtureProbe.Load("consumer-only.json");
        var ex = Assert.Throws<SideloadRefusedException>(() => AdbService.Create(new InstallDetector(p).Detect(), p, new RecordingRunner()));
        Assert.Contains("does not support sideloading", ex.Message);
    }

    [Fact]
    public void UsesBundledAdbAndLocalhost6520()
    {
        var run = new RecordingRunner { Respond = (_, a) => a.StartsWith("connect") ? (0, "connected to localhost:6520") : (0, "Success") };
        var adb = ForMsi(run);
        Assert.EndsWith(Path.Combine("current", "emulator", "adb.exe"), adb.AdbPath);
        Assert.True(adb.Connect().Success);
        Assert.Equal("connect localhost:6520", run.Calls[0].Args);

        var apk = Path.Combine(Path.GetTempPath(), $"vettv-{Guid.NewGuid():N}.apk");
        File.WriteAllText(apk, "x");
        try { Assert.True(adb.InstallApk(apk).Success); } finally { File.Delete(apk); }
        Assert.StartsWith("-s localhost:6520 install -r ", run.Calls[1].Args);
        Assert.All(run.Calls, c => Assert.Equal(adb.AdbPath, c.File));
    }

    [Fact]
    public void InstallFailureOutput_IsReportedAsFailure()
    {
        var run = new RecordingRunner { Respond = (_, _) => (0, "Failure [INSTALL_FAILED_VERSION_DOWNGRADE]") };
        var apk = Path.Combine(Path.GetTempPath(), $"vettv-{Guid.NewGuid():N}.apk");
        File.WriteAllText(apk, "x");
        try { Assert.False(ForMsi(run).InstallApk(apk).Success); } finally { File.Delete(apk); }
    }

    [Fact]
    public void ListPackages_ParsesThirdPartyList()
    {
        var run = new RecordingRunner { Respond = (_, _) => (0, "package:com.vettv.demo\r\npackage:com.example.game\r\ngarbage\n") };
        Assert.Equal(new[] { "com.example.game", "com.vettv.demo" }, ForMsi(run).ListPackages());
        Assert.Equal("-s localhost:6520 shell pm list packages -3", run.Calls[0].Args);
    }

    [Theory]
    [InlineData("com.vettv.demo", true)]
    [InlineData("com.vettv.demo; reboot", false)]
    [InlineData("nodots", false)]
    [InlineData("com.x && rm -rf /", false)]
    public void Uninstall_ValidatesPackageName(string pkg, bool ok)
    {
        var run = new RecordingRunner { Respond = (_, _) => (0, "Success") };
        var adb = ForMsi(run);
        if (ok) Assert.True(adb.Uninstall(pkg).Success);
        else Assert.Throws<ArgumentException>(() => adb.Uninstall(pkg));
    }

    [Fact]
    public void RejectsWrongFileTypesAndBadLogcatFilters()
    {
        var adb = ForMsi(new RecordingRunner());
        Assert.Throws<ArgumentException>(() => adb.InstallApk("game.exe"));
        Assert.Throws<ArgumentException>(() => adb.InstallAab("game.apk", "bundletool.jar"));
        Assert.Equal("-s localhost:6520 logcat -v time MyGame:D *:S", adb.LogcatArguments("MyGame:D *:S"));
        Assert.Throws<ArgumentException>(() => adb.LogcatArguments("x; shell rm"));
    }

    [Fact]
    public void Aab_UsesUserBundletoolWithBundledAdb()
    {
        var tmp = Directory.CreateTempSubdirectory("vettv-aab-").FullName;
        try
        {
            var aab = Path.Combine(tmp, "g.aab"); File.WriteAllText(aab, "x");
            var jar = Path.Combine(tmp, "bundletool.jar"); File.WriteAllText(jar, "x");
            var run = new RecordingRunner();
            var adb = ForMsi(run);
            Assert.True(adb.InstallAab(aab, jar).Success);
            Assert.Contains("build-apks", run.Calls[0].Args);
            Assert.Contains("install-apks", run.Calls[1].Args);
            Assert.All(run.Calls, c => Assert.Contains($"--adb=\"{adb.AdbPath}\"", c.Args));
        }
        finally { Directory.Delete(tmp, true); }
    }
}
