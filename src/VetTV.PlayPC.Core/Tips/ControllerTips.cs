namespace VetTV.PlayPC.Core.Tips;

public sealed record ControllerTip(string Title, string Body);

public static class ControllerTips
{
    public static IReadOnlyList<ControllerTip> All { get; } = new ControllerTip[]
    {
        new("Built-in support", "Google Play Games on PC supports keyboard and mouse, and many games support controllers. Look for the controls overlay in a game (Shift+Tab in many titles) to see key mappings."),
        new("Xbox / XInput controllers", "Xbox controllers (USB, Bluetooth, or wireless adapter) work best because games see them as standard XInput gamepads. Connect before launching the game."),
        new("PlayStation controllers", "DualShock 4 / DualSense may not be recognized as XInput. Launch the game through Steam (Launch tab > Add to Steam) and enable Steam Input for that shortcut, or use DS4Windows to present an Xbox-style controller."),
        new("Steam Input", "For a non-Steam shortcut: Steam > game > Properties > Controller > Enable Steam Input. Steam's overlay layouts can map buttons to keys for games without controller support."),
        new("Lenovo Legion Go / handhelds", "Use the device's controller mode that presents an XInput gamepad. Set Windows scaling to 100-150% for readable game UI, and plug in for the High performance preset."),
        new("Troubleshooting", "If a controller isn't detected, unplug other controllers, close remapping tools you aren't using (only one should run), and reconnect before launching the game."),
    };
}
