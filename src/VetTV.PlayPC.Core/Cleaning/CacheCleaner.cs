namespace VetTV.PlayPC.Core.Cleaning;

public sealed record CacheFile(string Path, long Bytes);

public sealed record CachePreview(IReadOnlyList<CacheFile> Files, IReadOnlyList<string> SkippedFolders)
{
    public long TotalBytes => Files.Sum(f => f.Bytes);
}

/// <summary>Sends a file to the Recycle Bin. Windows impl uses the shell; tests use a fake.</summary>
public interface IRecycleBin { void Recycle(string path); }

public sealed class WindowsRecycleBin : IRecycleBin
{
    public void Recycle(string path) => Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path,
        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
}

/// <summary>
/// Cleans only allow-listed cache folders under %LOCALAPPDATA%, only while Google Play Games is closed,
/// always previewing first and sending files to the Recycle Bin. Never touches user/game data.
/// </summary>
public sealed class CacheCleaner(string localAppData, IRecycleBin recycleBin)
{
    /// <summary>Relative to %LOCALAPPDATA%. The ONLY folders this cleaner will ever read or clean.</summary>
    public static IReadOnlyList<string> AllowList { get; } = new[]
    {
        @"Google\Play Games\Cache",
        @"Google\Play Games\GPUCache",
        @"Google\Play Games\Code Cache",
        @"Google\Play Games\ShaderCache",
        @"Google\Play Games Developer Emulator\Cache",
        @"Google\Play Games Developer Emulator\GPUCache",
    };

    public IReadOnlyList<string> AllowedRoots() =>
        AllowList.Select(r => Path.GetFullPath(Path.Combine(localAppData, r.Replace('\\', Path.DirectorySeparatorChar)))).ToList();

    /// <summary>True only if the path is inside one of the allow-listed roots (after normalising, so "..\" can't escape).</summary>
    public bool IsAllowed(string path)
    {
        var full = Path.GetFullPath(path);
        return AllowedRoots().Any(root =>
            full.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    }

    public CachePreview Preview()
    {
        var files = new List<CacheFile>();
        var skipped = new List<string>();
        foreach (var root in AllowedRoots())
        {
            if (!Directory.Exists(root)) continue;
            if (IsReparsePoint(root)) { skipped.Add(root); continue; }
            Walk(root, files, skipped);
        }
        return new CachePreview(files, skipped);
    }

    private void Walk(string dir, List<CacheFile> files, List<string> skipped)
    {
        foreach (var f in Directory.EnumerateFiles(dir))
            if (!IsReparsePoint(f) && IsAllowed(f)) files.Add(new CacheFile(f, new FileInfo(f).Length));
        foreach (var d in Directory.EnumerateDirectories(dir))
        {
            // Never follow junctions/symlinks: they could point outside the allow-list.
            if (IsReparsePoint(d)) { skipped.Add(d); continue; }
            Walk(d, files, skipped);
        }
    }

    /// <summary>Recycles the previewed files. Refuses while GPGPC runs, and re-checks every path against the allow-list.</summary>
    public (int Recycled, int Refused, int Failed) Clean(CachePreview preview, bool gpgpcRunning)
    {
        if (gpgpcRunning) throw new InvalidOperationException("Close Google Play Games before cleaning its cache.");
        int ok = 0, refused = 0, failed = 0;
        foreach (var f in preview.Files)
        {
            if (!IsAllowed(f.Path) || IsReparsePoint(f.Path)) { refused++; continue; }
            try { if (File.Exists(f.Path)) { recycleBin.Recycle(f.Path); ok++; } }
            catch (Exception) { failed++; }
        }
        return (ok, refused, failed);
    }

    private static bool IsReparsePoint(string path)
    {
        try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; }
        catch (Exception) { return true; } // unknown: treat as unsafe
    }
}
