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
- **Performance presets (undoable)**: per-app GPU preference set to High performance for `client.exe` and `crosvm.exe` (verified on a real install) (`HKCU\Software\Microsoft\DirectX\UserGpuPreferences`), Game Mode on (`HKCU\Software\Microsoft\GameBar`), and the High performance power plan. The previous values are saved in `settings.json` before each change, and **Undo** restores them exactly (or deletes values that didn't exist before). Google Play Games' own settings are shown as steps to follow; its files are never edited.
- **Heavy background apps**: lists windowed apps using more than 300 MB. You can ask a selected app to close politely, like clicking X. Nothing is ever force-killed.
- **Launcher**: finds game shortcuts in the Start Menu `Google Play Games` folders, then launches them, copies them to the Desktop, pins them, or adds them to Steam as a non-Steam game. The Steam entry opens your own shortcut file through Explorer. Steam must be closed, and `shortcuts.vdf` is backed up to `shortcuts.vdf.vettv.bak` first.
- **Cache cleaner**: allow-list verified on a real install (Oct 2026). Under `%LOCALAPPDATA%\Google\Play Games\` it cleans only:
  - `image_cache\` and `profile_image_cache\` (whole folders);
  - exactly `CEF\cache\Cache`, `CEF\cache\Code Cache`, `CEF\cache\GPUCache`, `CEF\cache\DawnCache` (and `CEF\cache\GrShaderCache` / `CEF\cache\ShaderCache` if present). These are exact paths with no name matching, so `CEF\cache` itself (which also holds `Local Storage`, `Network` (cookies), `Session Storage` and `blob_storage`), `CEF\Dictionaries` and `CEF\WidevineCdm` are never touched, whatever their letter case. CEF layout verified on the MSI (Oct 15, 2026).

  It **never** cleans `Logs`, `CrashReporting`, `startup_flags`, `userdata_*`, `Local Storage`, `Network`, `Session Storage`, `blob_storage`, `Dictionaries` or `WidevineCdm` (not even when nested), or anything in the Developer Emulator folder (not verified yet). It always shows a preview with sizes first, refuses to run while Google Play Games is open, skips symlinks/junctions, re-checks every path before cleaning, and sends files to the Recycle Bin.
- **Backup/restore**: the toolkit's own `settings.json` plus the shortcuts it created, in a zip.

## M3: Developer Emulator & Polish
- **Sideloading (Developer Emulator only)**: connect to `localhost:6520` with the Emulator's bundled `adb.exe` (`...\Play Games Developer Emulator\current\emulator\adb.exe`); install `.apk` (`adb install -r`) or `.aab` (with your own `bundletool.jar`: `build-apks --connected-device` then `install-apks`); list user-installed packages; uninstall a selected package (validated name). If only the consumer client is installed, it refuses and explains why.
- **Logcat viewer**: live `adb logcat -v time`, with an optional validated `Tag:Priority` filter and a 5,000-line rolling buffer. Diagnostics export (M1) already includes `AndroidSerial.log`.
- **Controller tips**: see [controller-tips.md](controller-tips.md).
- **Installer**: MSI (WiX v5, per-machine, Start Menu shortcut, upgrades cleanly) + portable zip + SHA256SUMS + winget manifests, built by the tag-only release workflow. See [releasing.md](releasing.md).
