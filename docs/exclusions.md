# Exclusions

The toolkit will **never**:

- root the Android environment or install Magisk;
- patch or modify Google Play Games binaries, images or config files (the toolkit only changes documented Windows settings and its own files);
- inject ARM translation (libhoudini) or GApps;
- sideload into the regular (consumer) Google Play Games app. Sideloading is only offered for the official **Developer Emulator**, using the Emulator's own bundled adb at `localhost:6520`;
- bypass anti-cheat, DRM, Play Integrity or region locks;
- help share accounts;
- distribute Google binaries or Google branding. `bundletool` is not bundled; you download it yourself.

Safety rules in the code:

- Windows features: only `HypervisorPlatform` and `VirtualMachinePlatform`, through a UAC-elevated helper that rejects anything else.
- Presets: HKCU values and the power plan only, each with Undo.
- Cache cleaner: only the folders on its allow-list, with a preview first, never while Google Play Games is running, and always to the Recycle Bin.
- Background apps: polite close requests only, never a force-kill.
- APK/AAB installs: you must confirm a notice to install only apps you own or have rights to.
- No telemetry.
