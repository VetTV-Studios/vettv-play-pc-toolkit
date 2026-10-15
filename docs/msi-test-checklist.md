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
- [x] **Cache folder names**: *verified Oct 15, 2026.* `%LOCALAPPDATA%\Google\Play Games` contains `CEF`, `CrashReporting`, `image_cache`, `Logs`, `profile_image_cache`, `startup_flags`, `userdata_*.cmi`. The allow-list is now `image_cache`, `profile_image_cache`, plus Chromium cache folders inside `CEF` only.
- [ ] **CEF contents**: run `dir /s /ad "%LOCALAPPDATA%\Google\Play Games\CEF"` and report the folder tree, to confirm the cache folders (e.g. `...\Cache`, `...\GPUCache`) and that cookies/local storage sit elsewhere. Then Clean > Preview should list only files from `image_cache`, `profile_image_cache` and CEF cache folders.
- [ ] **Developer Emulator cache folders**: list `%LOCALAPPDATA%\Google\Play Games Developer Emulator\` (`dir /ad`). Nothing there is cleaned until it's verified.
- [x] **Executable names for the GPU preset**: *verified Oct 15, 2026.* `C:\Program Files\Google\Play Games` has `client.exe`, `crosvm.exe`, `Service.exe`, `Bootstrapper.exe`, `gpu_check.exe`, `PlayPackageManager.exe` (no `GooglePlayGames.exe`). The preset now targets `client.exe` and `crosvm.exe` only.
- [ ] **GPU preset effect**: after Apply, Windows Settings > System > Display > Graphics lists `client.exe` and `crosvm.exe` as High performance, and a game shows load on GPU 1 (NVIDIA). If the game still runs on the iGPU, note which process uses the GPU (Task Manager > Details > "GPU engine" column).
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
