# MSI laptop test checklist (RTX 3050 + Intel UHD)

Run this on the MSI after installing the MSI package from a draft release, or the `unsigned-installer-ci` CI artifact. Tick each box and record anything surprising in an issue.

## Setup
- [ ] Install `VetTV.PlayPC.Toolkit-*.msi`. A Start Menu entry appears. (Unsigned builds trigger SmartScreen: More info > Run anyway.)
- [ ] Google Play Games (consumer) installed. For the M3 steps, also install the **Developer Emulator** (Stable).

## M1: Diagnose
- [ ] Diagnose > Run: consumer shows **installed + correct version** (compare with Google Play Games > Settings > About).
- [ ] Developer Emulator shows installed, version and **Stable/Beta** track correctly (and "not installed" before you install it).
- [ ] "Running" is correct with Google Play Games open and closed.
- [ ] Windows version, RAM, CPU cores, VT-x, Windows Hypervisor Platform, Virtual Machine Platform all **Pass**; every Warn/Fail has a fix hint.
- [ ] GPU rows: Intel UHD **Pass**, RTX 3050 Laptop **Pass** with driver version/date, plus a **Hybrid graphics** warning that mentions the NVIDIA Control Panel.
- [ ] Export diagnostics zip (redaction on): contains `system-report.txt/json`, `manifest.txt`, Play Games logs, `dxdiag.txt`; search it for your Windows user name, PC name and email: **none present**.

## M2: Fix & Launch
- [ ] Fix > Enable `HypervisorPlatform` (if it's already on, test `VirtualMachinePlatform`, or disable then re-enable): **UAC prompt** appears; choosing No changes nothing; choosing Yes enables it and offers a restart.
- [ ] *Use the high-performance GPU* > Apply, then check Windows Settings > System > Display > Graphics shows Google Play Games set to High performance, and a game shows load on GPU 1 (NVIDIA). Undo restores the previous setting.
- [ ] Game Mode and High performance power plan: Apply, then Undo restores the previous values (`powercfg /getactivescheme` before/after). Note: some Modern Standby laptops have no High performance plan, so the toolkit should say so.
- [ ] Launch tab lists installed games; Play launches; Copy to Desktop works; Add to Steam (Steam closed) shows the game in Steam and it launches.
- [ ] Clean tab: Preview lists only cache files, Clean refuses while Google Play Games is open, files go to the Recycle Bin, and games/sign-in still work afterwards.
- [ ] Backup, delete the Desktop shortcut, Restore: shortcut and settings come back.

### M2 guesses to verify (please report the real values)
- [ ] **Cache folder names**: list `%LOCALAPPDATA%\Google\Play Games\` and `%LOCALAPPDATA%\Google\Play Games Developer Emulator\` (`dir /ad`). The toolkit assumes `Cache`, `GPUCache`, `Code Cache`, `ShaderCache` (consumer) and `Cache`, `GPUCache` (emulator). Report the actual cache folders and anything that looks like user data.
- [ ] **Executable names for the GPU preset**: with a game running, Task Manager > Details: note the Google Play Games process names and their paths (right-click > Open file location). The toolkit looks for `client.exe`, `crosvm.exe`, `GooglePlayGames.exe` under the install folder.
- [ ] **Install detection source**: `reg query HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall /s /f "Google Play Games"`, and note DisplayName, DisplayVersion and InstallLocation.
- [ ] **Steam multi-account**: if this PC has more than one Steam login, check which account received the shortcut (the toolkit currently uses the first `userdata\<id>` folder). Report how many accounts exist.

## M3: Developer Emulator
- [ ] With **only** the consumer client installed, the Developer Emulator tab gives a clear refusal message for Connect/Install.
- [ ] With the Developer Emulator running: Connect says "Connected".
- [ ] Install a **test APK you own** (e.g. a debug build or Android Studio sample): it installs and appears in Refresh apps, and launches in the Emulator.
- [ ] (Optional) Install an `.aab` with your downloaded `bundletool.jar` (Java required).
- [ ] Uninstall the test app: it disappears from the list.
- [ ] Logcat Start shows live lines; the filter `ActivityManager:I *:S` narrows the output; Stop ends it, and closing the app ends adb logcat (no stray `adb.exe` in Task Manager other than the emulator's own server).
- [ ] Controller tips tab readable; an Xbox controller works in a controller-supported game.

## Record
- Toolkit version / build:
- Windows build, NVIDIA driver, Intel driver:
- Google Play Games + Developer Emulator versions:
- Issues found:
