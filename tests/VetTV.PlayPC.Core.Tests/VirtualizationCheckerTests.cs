using VetTV.PlayPC.Core.Checks;
using VetTV.PlayPC.Core.Platform;
using Xunit;

namespace VetTV.PlayPC.Core.Tests;

public class VirtualizationCheckerTests
{
    private static Dictionary<string, CheckResult> Run(FakeProbe p) => new VirtualizationChecker(p).Run().ToDictionary(c => c.Id);

    [Fact]
    public void HealthyMachine_AllRequiredPass()
    {
        var r = Run(new FakeProbe());
        Assert.Equal(CheckStatus.Pass, r["os.version"].Status);
        Assert.Equal(CheckStatus.Pass, r["hw.ram"].Status);
        Assert.Equal(CheckStatus.Pass, r["hw.cpu"].Status);
        Assert.Equal(CheckStatus.Pass, r["virt.firmware"].Status);
        Assert.Equal(CheckStatus.Pass, r["feature.HypervisorPlatform"].Status);
        Assert.Equal(CheckStatus.Warn, r["feature.Microsoft-Hyper-V-All"].Status);
    }

    [Fact]
    public void VtDisabled_FailsWithBiosHint()
    {
        var p = new FakeProbe(); p.Cpus[0] = p.Cpus[0] with { VirtualizationFirmwareEnabled = false };
        var c = Run(p)["virt.firmware"];
        Assert.Equal(CheckStatus.Fail, c.Status);
        Assert.Contains("BIOS", c.FixHint);
    }

    [Fact]
    public void HypervisorRunning_PassesEvenIfFirmwareFlagFalse()
    {
        var p = new FakeProbe { Hypervisor = true }; p.Cpus[0] = p.Cpus[0] with { VirtualizationFirmwareEnabled = false };
        Assert.Equal(CheckStatus.Pass, Run(p)["virt.firmware"].Status);
    }

    [Fact]
    public void LowSpecs_Fail()
    {
        var p = new FakeProbe { Ram = 4UL << 30, Os = new("Windows 10", "10.0.18363", 18363) };
        p.Cpus[0] = new("Dual core", 2, 4, true);
        p.Features["HypervisorPlatform"] = false;
        var r = Run(p);
        Assert.Equal(CheckStatus.Fail, r["hw.ram"].Status);
        Assert.Equal(CheckStatus.Fail, r["hw.cpu"].Status);
        Assert.Equal(CheckStatus.Fail, r["os.version"].Status);
        Assert.Equal(CheckStatus.Fail, r["feature.HypervisorPlatform"].Status);
        Assert.Contains("HypervisorPlatform", r["feature.HypervisorPlatform"].FixHint);
    }

    [Fact]
    public void RamJustUnder8GbDueToReservation_Passes()
    {
        Assert.Equal(CheckStatus.Pass, Run(new FakeProbe { Ram = (ulong)(7.9 * (1UL << 30)) })["hw.ram"].Status);
    }

    [Fact]
    public void UnknownFeature_WarnsWithHint()
    {
        var p = new FakeProbe(); p.Features.Clear();
        var c = Run(p)["feature.HypervisorPlatform"];
        Assert.Equal(CheckStatus.Warn, c.Status);
        Assert.NotNull(c.FixHint);
    }
}
