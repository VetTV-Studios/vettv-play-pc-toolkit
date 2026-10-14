# Features

## M1: Diagnose (read-only)
Nothing in M1 changes your system.

- **Install detection**: finds the Google Play Games consumer client and the Developer Emulator through the Windows uninstall registry keys, falling back to the default install folders. Reports version, Developer Emulator track (Stable/Beta), install path and whether it's running.
- **System & virtualization checks**: Windows build (10 2004+), RAM (8 GB), physical CPU cores (4), firmware VT-x/AMD-V, and the Windows Hypervisor Platform, Virtual Machine Platform and Hyper-V features.
- **GPU & driver report**: adapters, driver version/date, hybrid-GPU guidance, plus specific notes for Intel HD 530, NVIDIA GTX 960M (legacy driver branch) and AMD R9 M390 (Boot Camp).
- **Diagnostics zip**: system report (txt + json), Play Games / Developer Emulator logs, optional DxDiag. With redaction on (the default), user names, machine name, `C:\Users\<name>` paths and email addresses are replaced, and binary files are skipped.

Every check shows **Pass / Warn / Fail**, and every Warn or Fail includes a fix hint.

PowerShell: `Get-VetTVPlayPCInstall`, `Test-VetTVPlayPCVirtualization`, `Get-VetTVPlayPCGpu`.
