using VetTV.PlayPC.Core.Checks;
using VetTV.PlayPC.Core.Platform;
using Xunit;

namespace VetTV.PlayPC.Core.Tests;

public class GpuInspectorTests
{
    [Fact]
    public void AsusHybrid_WarnsHd530_Flags960MLegacy_AndHybridHint()
    {
        var p = new FakeProbe();
        p.Gpus.Add(new("Intel(R) HD Graphics 530", "31.0.101.2111", DateTime.Now.AddMonths(-6), null, null));
        p.Gpus.Add(new("NVIDIA GeForce GTX 960M", "32.0.15.8278", DateTime.Now.AddMonths(-2), null, null));
        var r = new GpuInspector(p).Run().ToList();
        Assert.Contains(r, c => c.Title.Contains("HD Graphics 530") && c.Status == CheckStatus.Warn && c.FixHint!.Contains("High performance"));
        Assert.Contains(r, c => c.Title.Contains("960M") && c.Detail.Contains("Supported, older"));
        Assert.Contains(r, c => c.Id == "gpu.hybrid");
    }

    [Fact]
    public void BootCampR9M390_MayNotMeetRequirements()
    {
        var p = new FakeProbe();
        p.Gpus.Add(new("AMD Radeon R9 M390", "15.200.1062.1004", new DateTime(2015, 6, 1), null, null));
        var c = Assert.Single(new GpuInspector(p).Run());
        Assert.Equal(CheckStatus.Warn, c.Status);
        Assert.Contains("May not meet requirements", c.Detail);
    }

    [Fact]
    public void BasicDisplayOnly_Fails()
    {
        var p = new FakeProbe();
        p.Gpus.Add(new("Microsoft Basic Display Adapter", null, null, null, null));
        Assert.Equal(CheckStatus.Fail, Assert.Single(new GpuInspector(p).Run()).Status);
    }

    [Fact]
    public void ModernGpu_Passes()
    {
        var p = new FakeProbe();
        p.Gpus.Add(new("NVIDIA GeForce RTX 4060", "32.0.15.6094", DateTime.Now.AddMonths(-1), null, null));
        Assert.Equal(CheckStatus.Pass, Assert.Single(new GpuInspector(p).Run()).Status);
    }
}
