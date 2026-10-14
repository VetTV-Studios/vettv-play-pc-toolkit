# VetTV Play PC Toolkit

An original Windows 10/11 helper for **Google Play Games on PC**: diagnostics, virtualization and GPU checks, launcher shortcuts, safe performance presets, and Developer Emulator sideloading for apps you own.

> **Not affiliated with Google.** This is an independent community project by VetTV Studios. It is not made, endorsed, or supported by Google. "Google Play" is a trademark of Google LLC.

## Status
M1 (Diagnose): read-only install detection, system/virtualization checks, GPU/driver report, and a redacted diagnostics zip. See [docs/features.md](docs/features.md).

## Layout
- `src/VetTV.PlayPC.App`: WPF app (MVVM)
- `src/VetTV.PlayPC.Core`: checks and services
- `src/VetTV.PlayPC.Elevated`: small admin helper
- `src/VetTV.PlayPC.PowerShell`: PowerShell module
- `tests/`: xUnit and Pester tests
- `docs/`: features, exclusions, controller tips, hardware notes

## Build
Requires the .NET 8 SDK on Windows.
```
dotnet build VetTV.PlayPC.sln
dotnet test VetTV.PlayPC.sln
```

## What it won't do
No rooting, binary patching, ARM translation/GApps injection, anti-cheat/DRM/Play Integrity bypass, or redistribution of Google binaries or branding.

## License
MIT. See [LICENSE](LICENSE).
