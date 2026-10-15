using VetTV.PlayPC.Core.Cleaning;
using VetTV.PlayPC.Core.Features;
using VetTV.PlayPC.Core.Launcher;
using VetTV.PlayPC.Core.Platform;
using VetTV.PlayPC.Core.Presets;
using VetTV.PlayPC.Core.Settings;
using Xunit;

namespace VetTV.PlayPC.Core.Tests;

internal sealed class FakeRegistry : IRegistryStore
{
    public Dictionary<(string, string), RegValue> Values { get; } = new();
    public RegValue? Get(string k, string n) => Values.GetValueOrDefault((k, n));
    public void Set(string k, string n, RegValue v) => Values[(k, n)] = v;
    public void Delete(string k, string n) => Values.Remove((k, n));
}

internal sealed class FakeCommands : ICommandRunner
{
    public string Active = "381b4222-f694-41f0-9685-ff5bb260df2e";
    public bool HighPerfAvailable = true;
    public (int, string) Run(string file, string args)
    {
        if (args == "/getactivescheme") return (0, $"Power Scheme GUID: {Active}  (Balanced)");
        if (args.StartsWith("/setactive "))
        {
            var g = args["/setactive ".Length..];
            if (g == PresetService.HighPerformancePlan && !HighPerfAvailable) return (1, "");
            Active = g; return (0, "");
        }
        return (1, "");
    }
}

internal sealed class TempDir : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("vettv-m2-").FullName;
    public string Combine(params string[] p) => System.IO.Path.Combine(new[] { Path }.Concat(p).ToArray());
    public void Dispose() { try { Directory.Delete(Path, true); } catch { } }
}

public class PresetServiceTests
{
    [Fact]
    public void ApplyThenUndo_RestoresPriorValues_AndDeletesNewOnes()
    {
        using var t = new TempDir();
        var reg = new FakeRegistry();
        reg.Set(PresetService.GameBarKey, "AutoGameModeEnabled", new RegValue(RegValueKind.DWord, "0"));
        var before = new Dictionary<(string, string), RegValue>(reg.Values);
        var cmd = new FakeCommands();
        var svc = new PresetService(reg, cmd, new SettingsStore(t.Combine("s.json")));

        svc.Apply(PresetService.GameMode);
        svc.Apply(PresetService.GpuHighPerf, new[] { @"C:\PG\client.exe" });
        svc.Apply(PresetService.PowerPlan);
        Assert.Equal("1", reg.Get(PresetService.GameBarKey, "AutoGameModeEnabled")!.Data);
        Assert.Equal("GpuPreference=2;", reg.Get(PresetService.GpuPrefKey, @"C:\PG\client.exe")!.Data);
        Assert.Equal(PresetService.HighPerformancePlan, cmd.Active);

        svc.Undo(PresetService.GameMode);
        svc.Undo(PresetService.GpuHighPerf);
        svc.Undo(PresetService.PowerPlan);
        Assert.Equal(before, reg.Values);
        Assert.Equal("381b4222-f694-41f0-9685-ff5bb260df2e", cmd.Active);
        Assert.Empty(new SettingsStore(t.Combine("s.json")).Load().UndoJournal);
    }

    [Fact]
    public void ApplyTwice_KeepsOriginalUndoValues()
    {
        using var t = new TempDir();
        var reg = new FakeRegistry();
        var svc = new PresetService(reg, new FakeCommands(), new SettingsStore(t.Combine("s.json")));
        svc.Apply(PresetService.GameMode);
        svc.Apply(PresetService.GameMode);
        svc.Undo(PresetService.GameMode);
        Assert.Empty(reg.Values);
    }

    [Fact]
    public void PowerPlanUnavailable_ThrowsAndRecordsNothing()
    {
        using var t = new TempDir();
        var store = new SettingsStore(t.Combine("s.json"));
        var svc = new PresetService(new FakeRegistry(), new FakeCommands { HighPerfAvailable = false }, store);
        Assert.Throws<InvalidOperationException>(() => svc.Apply(PresetService.PowerPlan));
        Assert.False(svc.IsApplied(PresetService.PowerPlan));
    }

    [Fact]
    public void GpuPreset_WithoutInstall_Throws()
    {
        using var t = new TempDir();
        var svc = new PresetService(new FakeRegistry(), new FakeCommands(), new SettingsStore(t.Combine("s.json")));
        Assert.Throws<InvalidOperationException>(() => svc.Apply(PresetService.GpuHighPerf, Array.Empty<string>()));
    }
}

public class FeatureEnablerTests
{
    [Theory]
    [InlineData("HypervisorPlatform", true)]
    [InlineData("VirtualMachinePlatform", true)]
    [InlineData("Microsoft-Hyper-V-All", false)]
    [InlineData("HypervisorPlatform & calc", false)]
    public void OnlyAllowListedFeatures(string f, bool ok)
    {
        Assert.Equal(ok, FeatureEnabler.IsAllowed(f));
        if (!ok) Assert.Throws<ArgumentException>(() => FeatureEnabler.BuildArguments(f));
    }

    [Fact]
    public void RebootExitCode_MeansRebootRequired()
    {
        Assert.Equal(FeatureEnableOutcome.EnabledRebootRequired, FeatureEnabler.Interpret(3010));
        Assert.Equal(FeatureEnableOutcome.Enabled, FeatureEnabler.Interpret(0));
        Assert.Equal(FeatureEnableOutcome.Failed, FeatureEnabler.Interpret(1050));
    }
}

public class CacheCleanerTests
{
    private sealed class FakeBin : IRecycleBin
    {
        public List<string> Recycled { get; } = new();
        public void Recycle(string path) { Recycled.Add(path); File.Delete(path); }
    }

    private static string Rel(string r) => r.Replace('\\', Path.DirectorySeparatorChar);

    [Fact]
    public void NeverTouchesPathsOutsideAllowList()
    {
        using var t = new TempDir();
        var cache = t.Combine(Rel(@"Google\Play Games\image_cache"));
        Directory.CreateDirectory(Path.Combine(cache, "sub"));
        File.WriteAllText(Path.Combine(cache, "a.bin"), "x");
        File.WriteAllText(Path.Combine(cache, "sub", "b.bin"), "yy");
        var userData = t.Combine(Rel(@"Google\Play Games\Logs"));
        Directory.CreateDirectory(userData);
        var keep = Path.Combine(userData, "save.dat");
        File.WriteAllText(keep, "precious");

        var bin = new FakeBin();
        var c = new CacheCleaner(t.Path, bin);
        var preview = c.Preview();
        Assert.Equal(2, preview.Files.Count);
        Assert.Equal(3, preview.TotalBytes);

        // Tampered preview containing an outside path and a traversal path must be refused.
        var tampered = new CachePreview(preview.Files.Concat(new[]
        {
            new CacheFile(keep, 8),
            new CacheFile(Path.Combine(cache, "..", "Logs", "save.dat"), 8),
        }).ToList(), Array.Empty<string>());
        var (ok, refused, _) = c.Clean(tampered, gpgpcRunning: false);
        Assert.Equal(2, ok);
        Assert.Equal(2, refused);
        Assert.True(File.Exists(keep));
        Assert.All(bin.Recycled, p => Assert.True(c.IsAllowed(p)));
    }

    [Theory]
    [InlineData(@"Google\Play Games\Logs\client.log")]
    [InlineData(@"Google\Play Games\CrashReporting\r.dmp")]
    [InlineData(@"Google\Play Games\startup_flags\f")]
    [InlineData(@"Google\Play Games\userdata_0.cmi")]
    [InlineData(@"Google\Play Games\userdata_abc.cmi\x")]
    // CEF\cache itself and its non-cache siblings, in several case variants.
    [InlineData(@"Google\Play Games\CEF\cache\data.bin")]
    [InlineData(@"Google\Play Games\CEF\Cache\data.bin")]
    [InlineData(@"Google\Play Games\CEF\CACHE\data.bin")]
    [InlineData(@"Google\Play Games\CEF\cache\Local Storage\leveldb\000003.log")]
    [InlineData(@"Google\Play Games\CEF\Cache\local storage\leveldb\000003.log")]
    [InlineData(@"Google\Play Games\CEF\cache\Network\Cookies")]
    [InlineData(@"Google\Play Games\cef\CACHE\NETWORK\Cookies")]
    [InlineData(@"Google\Play Games\CEF\cache\Session Storage\000001.log")]
    [InlineData(@"Google\Play Games\CEF\cache\SESSION STORAGE\x")]
    [InlineData(@"Google\Play Games\CEF\cache\blob_storage\b")]
    [InlineData(@"Google\Play Games\CEF\cache\Blob_Storage\b")]
    [InlineData(@"Google\Play Games\CEF\Dictionaries\en-US.bdic")]
    [InlineData(@"Google\Play Games\cef\dictionaries\en-US.bdic")]
    [InlineData(@"Google\Play Games\CEF\WidevineCdm\4.10\manifest.json")]
    [InlineData(@"Google\Play Games\CEF\widevinecdm\x")]
    [InlineData(@"Google\Play Games\CEF\x.bin")]
    [InlineData(@"Google\Play Games\CEF\Cache\Cache_Data\f")]          // old name-match rule would have allowed this
    [InlineData(@"Google\Play Games\CEF\other\Cache\f")]
    [InlineData(@"Google\Play Games\CEF\cache\Cache\..\Network\Cookies")]
    [InlineData(@"Google\Play Games\CEF\cache\Cache\Network\Cookies")]  // nested forbidden name inside an allowed root
    [InlineData(@"Google\Play Games\image_cache\..\Logs\client.log")]
    [InlineData(@"Google\Play Games Developer Emulator\Cache\x")]
    public void NeverAllowed_RealNonCacheFolders(string rel)
    {
        using var t = new TempDir();
        Assert.False(new CacheCleaner(t.Path, new FakeBin()).IsAllowed(t.Combine(Rel(rel))));
    }

    [Theory]
    [InlineData(@"Google\Play Games\CEF\cache\Cache\Cache_Data\f_000001")]
    [InlineData(@"Google\Play Games\CEF\cache\Code Cache\js\index")]
    [InlineData(@"Google\Play Games\CEF\cache\GPUCache\data_1")]
    [InlineData(@"Google\Play Games\CEF\cache\DawnCache\data_0")]
    [InlineData(@"Google\Play Games\CEF\cache\GrShaderCache\data_0")]
    [InlineData(@"Google\Play Games\CEF\cache\ShaderCache\data_0")]
    [InlineData(@"Google\Play Games\cef\CACHE\gpucache\data_1")]          // case-insensitive like Windows
    public void Allowed_ExactCefCacheFoldersOnly(string rel)
    {
        using var t = new TempDir();
        Assert.True(new CacheCleaner(t.Path, new FakeBin()).IsAllowed(t.Combine(Rel(rel))));
    }

    [Fact]
    public void Preview_RealMsiLayout_FindsOnlyCaches()
    {
        using var t = new TempDir();
        void Make(string rel) { var f = t.Combine(Rel(rel)); Directory.CreateDirectory(Path.GetDirectoryName(f)!); File.WriteAllText(f, "x"); }
        Make(@"Google\Play Games\image_cache\a.png");
        Make(@"Google\Play Games\profile_image_cache\p.png");
        Make(@"Google\Play Games\CEF\cache\Cache\Cache_Data\f_1");
        Make(@"Google\Play Games\CEF\cache\Code Cache\js\c1");
        Make(@"Google\Play Games\CEF\cache\DawnCache\d1");
        Make(@"Google\Play Games\CEF\cache\GPUCache\g1");
        Make(@"Google\Play Games\CEF\cache\Local Storage\leveldb\ls.log");
        Make(@"Google\Play Games\CEF\cache\Network\Cookies");
        Make(@"Google\Play Games\CEF\cache\Session Storage\ss.log");
        Make(@"Google\Play Games\CEF\cache\blob_storage\b");
        Make(@"Google\Play Games\CEF\cache\top-level.bin");
        Make(@"Google\Play Games\CEF\Dictionaries\en-US.bdic");
        Make(@"Google\Play Games\CEF\WidevineCdm\manifest.json");
        Make(@"Google\Play Games\Logs\client.log");
        Make(@"Google\Play Games\CrashReporting\c.dmp");
        Make(@"Google\Play Games\startup_flags\f");
        Make(@"Google\Play Games\userdata_0.cmi\d");
        var p = new CacheCleaner(t.Path, new FakeBin()).Preview();
        var names = p.Files.Select(f => Path.GetFileName(f.Path)).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "a.png", "c1", "d1", "f_1", "g1", "p.png" }, names);
    }

    [Fact]
    public void RefusesWhileRunning()
    {
        using var t = new TempDir();
        var c = new CacheCleaner(t.Path, new FakeBin());
        Assert.Throws<InvalidOperationException>(() => c.Clean(c.Preview(), gpgpcRunning: true));
    }

    [Fact]
    public void AllowListRootItself_AndSiblingPrefix_NotAllowed()
    {
        using var t = new TempDir();
        var c = new CacheCleaner(t.Path, new FakeBin());
        Assert.False(c.IsAllowed(t.Combine(Rel(@"Google\Play Games\image_cache"))));
        Assert.False(c.IsAllowed(t.Combine(Rel(@"Google\Play Games\image_cache_evil\x.bin"))));
        Assert.False(c.IsAllowed(t.Combine(Rel(@"Google\Play Games\Cache\x.bin"))));
        Assert.True(c.IsAllowed(t.Combine(Rel(@"Google\Play Games\image_cache\x.bin"))));
        Assert.True(c.IsAllowed(t.Combine(Rel(@"Google\Play Games\profile_image_cache\a\x.bin"))));
    }

    [Fact]
    public void SymlinkInsideCache_IsNotFollowed()
    {
        using var t = new TempDir();
        var outside = t.Combine("outside"); Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "secret.txt"), "s");
        var cache = t.Combine(Rel(@"Google\Play Games\image_cache")); Directory.CreateDirectory(cache);
        try { Directory.CreateSymbolicLink(Path.Combine(cache, "link"), outside); }
        catch (Exception) { return; } // no symlink privilege on this runner
        var p = new CacheCleaner(t.Path, new FakeBin()).Preview();
        Assert.DoesNotContain(p.Files, f => f.Path.EndsWith("secret.txt"));
        Assert.Single(p.SkippedFolders);
    }
}

public class SettingsAndLauncherTests
{
    [Fact]
    public void BackupRestore_RoundTrips()
    {
        using var t = new TempDir();
        var start = t.Combine("Start", ShortcutService.GpgpcFolderName);
        Directory.CreateDirectory(start);
        File.WriteAllText(Path.Combine(start, "Cool Game.url"), "[InternetShortcut]\nURL=googleplaygames://launch/?id=x");
        File.WriteAllText(Path.Combine(start, "Google Play Games.lnk"), "app");
        var store = new SettingsStore(t.Combine("cfg", "settings.json"));
        var svc = new ShortcutService(new[] { t.Combine("Start") }, store);

        var games = svc.FindGames();
        var game = Assert.Single(games);
        var desk = t.Combine("Desktop");
        var copied = svc.CopyTo(game, desk);
        svc.TogglePin(game);
        var s = store.Load(); s.AppliedPresets.Add("game-mode"); store.Save(s);
        var before = File.ReadAllText(store.SettingsPath);

        var zip = t.Combine("backup.zip");
        store.Backup(zip);
        File.Delete(copied);
        File.Delete(store.SettingsPath);

        Assert.Equal(1, store.Restore(zip));
        Assert.True(File.Exists(copied));
        Assert.Equal(before, File.ReadAllText(store.SettingsPath));
        Assert.Contains("Cool Game", store.Load().PinnedGames);
    }

    [Fact]
    public void Steam_AddsEntry_KeepsExisting_AndBacksUp()
    {
        using var t = new TempDir();
        var acct = t.Combine("userdata", "12345");
        Directory.CreateDirectory(Path.Combine(acct, "config"));
        var existing = new VdfNode(); var list = new VdfNode(); var e0 = new VdfNode();
        e0["AppName"] = "Old Game"; e0["Exe"] = "\"C:\\old.exe\""; e0["IsHidden"] = 0; list["0"] = e0; existing["shortcuts"] = list;
        var file = Path.Combine(acct, "config", "shortcuts.vdf");
        using (var fs = File.Create(file)) existing.Write(fs);

        var steam = new SteamShortcutService(t.Combine("userdata"));
        Assert.Equal(acct, Assert.Single(steam.Accounts()));
        var game = new GameShortcut("Cool Game", @"C:\Users\u\Start\Cool Game.url");
        Assert.True(steam.Add(acct, game, steamRunning: false));
        Assert.False(steam.Add(acct, game, steamRunning: false));
        Assert.Throws<InvalidOperationException>(() => steam.Add(acct, game, steamRunning: true));
        Assert.True(File.Exists(file + ".vettv.bak"));

        using var rs = File.OpenRead(file);
        var root = VdfNode.Read(rs);
        var items = ((VdfNode)root["shortcuts"]!).Items.Select(i => (VdfNode)i.Value).ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal("Old Game", items[0]["AppName"]);
        Assert.Equal("Cool Game", items[1]["AppName"]);
        Assert.Contains("Cool Game.url", (string)items[1]["LaunchOptions"]!);
    }
}

public class GpgpcExecutablesTests
{
    [Fact]
    public void FindsOnlyClientAndCrosvm()
    {
        var dir = Directory.CreateTempSubdirectory("vettv-exe-").FullName;
        try
        {
            foreach (var e in new[] { "client.exe", "crosvm.exe", "Service.exe", "Bootstrapper.exe", "gpu_check.exe", "PlayPackageManager.exe", "GooglePlayGames.exe" })
                File.WriteAllText(Path.Combine(dir, e), "");
            var found = GpgpcExecutables.Find(dir).Select(Path.GetFileName).OrderBy(n => n).ToArray();
            Assert.Equal(new[] { "client.exe", "crosvm.exe" }, found);
        }
        finally { Directory.Delete(dir, true); }
    }
}
