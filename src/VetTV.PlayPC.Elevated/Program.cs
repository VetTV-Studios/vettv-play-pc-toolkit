// VetTV.PlayPC.Elevated: tiny admin helper. Only enables allow-listed Windows optional features via DISM.
using System.Diagnostics;
using VetTV.PlayPC.Core.Features;

if (args.Length != 2 || args[0] != "enable-feature")
{
    Console.Error.WriteLine("Usage: VetTV.PlayPC.Elevated enable-feature <HypervisorPlatform|VirtualMachinePlatform>");
    return FeatureEnabler.ExitBadArgs;
}
if (!FeatureEnabler.IsAllowed(args[1]))
{
    Console.Error.WriteLine($"Refusing: '{args[1]}' is not on the allow-list.");
    return FeatureEnabler.ExitNotAllowed;
}

var dism = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "dism.exe");
using var p = Process.Start(new ProcessStartInfo(dism, $"/Online /Enable-Feature /FeatureName:{args[1]} /All /NoRestart /Quiet")
    { UseShellExecute = false, CreateNoWindow = true })!;
p.WaitForExit();
// DISM: 0 = done, 3010 = done, reboot required. Anything else is a failure.
return p.ExitCode is FeatureEnabler.ExitOk or FeatureEnabler.ExitRebootRequired ? p.ExitCode : 1000;
