# Hardware notes

## MSI laptop: RTX 3050 Laptop 4 GB + Intel UHD (Optimus): primary test machine
- **Expected result**: all system checks pass; the RTX 3050 Laptop GPU is well above the guideline; Intel UHD passes; a **Hybrid graphics** warning reminds you to put Google Play Games on the NVIDIA GPU.
- **dGPU preset (do this)**:
  1. Toolkit **Fix** tab > *Use the high-performance GPU* > Apply. This sets the Windows per-app preference to High performance for `client.exe` and `crosvm.exe` (the Google Play Games processes verified on this MSI), and Undo restores it.
  2. If a game still runs on the iGPU: NVIDIA Control Panel > Manage 3D settings > Program Settings > add `C:\Program Files\Google\Play Games\...\client.exe` and `crosvm.exe` > *High-performance NVIDIA processor*.
  3. Optional: if your model has a MUX switch, MSI Center > Features > GPU Switch > *Discrete Graphics Mode* forces the dGPU for everything (needs a reboot and uses more battery). Switch back to MSHybrid for battery life.
  4. Plug in the charger. On battery, both MSI and NVIDIA limit GPU power.
- **Verify**: with a game running, Task Manager > Performance > GPU 1 (NVIDIA) should show load, or add the "GPU engine" column on the Details tab.
- **Note**: Windows' WMI caps reported VRAM at 4 GB (32-bit field), so the 3050's 4 GB may show as roughly 4095 MB. That is expected.
- **Virtualization**: MSI firmware usually ships with Intel VT-x on. If the toolkit says it's off: reboot into BIOS (Del during boot) > Advanced > *Intel Virtualization Tech* > Enabled. Many MSI laptops hide Advanced; press Right Ctrl+Right Shift+Left Alt+F2 on the BIOS screen.
- Test fixture: `tests/VetTV.PlayPC.Core.Tests/Fixtures/msi-rtx3050-optimus.json` (synthetic values; replace them with real ones from the checklist).

## Asus: Intel HD 530 + GTX 960M
HD 530 is below the Intel UHD 630 class guideline. Set Google Play Games to the GTX 960M (same dGPU preset steps as above). The 960M uses NVIDIA's legacy, security-only driver branch (582.xx): supported, but older.

## iMac: AMD R9 M390 (Boot Camp)
AMD no longer ships drivers. The toolkit reports "may not meet requirements" but never blocks you.
