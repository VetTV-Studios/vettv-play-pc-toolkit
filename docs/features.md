# Features

## M1: Diagnose (read-only)
Nothing in M1 changes your system.

- **Install detection**: finds the Google Play Games consumer client and the Developer Emulator through the Windows uninstall registry keys, falling back to the default install folders. Reports version, Developer Emulator track (Stable/Beta), install path and whether it's running.
- **System & virtualization checks**: Windows build (10 2004+), RAM (8 GB), physical CPU cores (4), firmware VT-x/AMD-V, and the Windows Hypervisor Platform, Virtual Machine Platform and Hyper-V features.
- **GPU & driver report**: adapters, driver version/date, hybrid-GPU guidance, plus specific notes for Intel HD 530, NVIDIA GTX 960M (legacy driver branch) and AMD R9 M390 (Boot Camp).
- **Diagnostics zip**: system report (txt + json), Play Games / Developer Emulator logs, optional DxDiag. With redaction on (the default), user names, machine name, `C:\Users\<name>` paths and email addresses are replaced, and binary files are skipped.

Every check shows **Pass / Warn / Fail**, and every Warn or Fail includes a fix hint.

PowerShell: `Get-VetTVPlayPCInstall`, `Test-VetTVPlayPCVirtualization`, `Get-VetTVPlayPCGpu`.

## M2: Fix & Launch
Every system-changing action is opt-in and asks for confirmation first.

- **One-click Windows features**: enables only `HypervisorPlatform` or `VirtualMachinePlatform`. It goes through the small `VetTV.PlayPC.Elevated` helper, which shows a UAC prompt (`requireAdministrator`), refuses any feature not on the allow-list, and runs DISM with `/NoRestart`. When Windows reports that a restart is needed (exit code 3010), you're offered a restart in 30 seconds, which you can decline.
- **Performance presets (undoable)**: per-app GPU preference set to High performance (`HKCU\Software\Microsoft\DirectX\UserGpuPreferences`), Game Mode on (`HKCU\Software\Microsoft\GameBar`), and the High performance power plan. The previous values are saved in `settings.json` before each change, and **Undo** restores them exactly (or deletes values that didn't exist before). Google Play Games' own settings are shown as steps to follow; its files are never edited.
- **Heavy background apps**: lists windowed apps using more than 300 MB. You can ask a selected app to close politely, like clicking X. Nothing is ever force-killed.
- **Launcher**: finds game shortcuts in the Start Menu `Google Play Games` folders, then launches them, copies them to the Desktop, pins them, or adds them to Steam as a non-Steam game. The Steam entry opens your own shortcut file through Explorer. Steam must be closed, and `shortcuts.vdf` is backed up to `shortcuts.vdf.vettv.bak` first.
- **Cache cleaner**: works only in these folders under `%LOCALAPPDATA%`: `Google\Play Games\{Cache,GPUCache,Code Cache,ShaderCache}` and `Google\Play Games Developer Emulator\{Cache,GPUCache}`. It always shows a preview with sizes first, refuses to run while Google Play Games is open, skips symlinks/junctions, re-checks every path before cleaning, and sends files to the Recycle Bin. *Verify that these folder names match what's actually on your machine.*
- **Backup/restore**: the toolkit's own `settings.json` plus the shortcuts it created, in a zip.
