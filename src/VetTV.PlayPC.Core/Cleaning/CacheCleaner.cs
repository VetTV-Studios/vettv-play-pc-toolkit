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
    /// <summary>
    /// Relative to %LOCALAPPDATA%. Whole folders that are pure caches (verified on a real install, Oct 2026).
    /// Logs, CrashReporting, startup_flags and userdata_* are never allowed.
    /// </summary>
    public static IReadOnlyList<string> AllowList { get; } = new[]
    {
        @"Google\Play Games\image_cache",
        @"Google\Play Games\profile_image_cache",
    };

    /// <summary>
    /// CEF (Chromium Embedded) mixes caches with cookies/local storage, so only folders with these exact
    /// Chromium cache names, anywhere under CEF (max depth 4), are allowed. Everything else in CEF is left alone.
    /// </summary>
    public const string CefRoot = @"Google\Play Games\CEF";
    public static IReadOnlyList<string> CefCacheFolderNames { get; } = new[] { "Cache", "Code Cache", "GPUCache", "GrShaderCache", "ShaderCache", "DawnCache", "DawnGraphiteCache", "DawnWebGPUCache" };
    private const int CefMaxDepth = 4;

    /// <summary>Folder names that are never cleaned, even if nested inside an allowed folder.</summary>
    public static IReadOnlyList<string> NeverAllowed { get; } = new[] { "Logs", "CrashReporting", "startup_flags" };

    private string Full(string rel) => Path.GetFullPath(Path.Combine(localAppData, rel.Replace('\\', Path.DirectorySeparatorChar)));

    public IReadOnlyList<string> AllowedRoots() => AllowList.Select(Full).ToList();

    private static bool Under(string full, string root) =>
        full.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    /// <summary>True only if the path is inside an allowed cache folder (after normalising, so "..\" can't escape).</summary>
    public bool IsAllowed(string path)
    {
        var full = Path.GetFullPath(path);
        string[] segs;
        if (AllowedRoots().Any(r => Under(full, r)))
            segs = full.Split(Path.DirectorySeparatorChar);
        else
        {
            var cef = Full(CefRoot);
            if (!Under(full, cef)) return false;
            var rel = Path.GetRelativePath(cef, full).Split(Path.DirectorySeparatorChar);
            // Some ancestor folder (not the file itself) within the depth limit must be a Chromium cache folder.
            var idx = Array.FindIndex(rel, 0, Math.Min(rel.Length - 1, CefMaxDepth),
                seg => CefCacheFolderNames.Contains(seg, StringComparer.OrdinalIgnoreCase));
            if (idx < 0) return false;
            segs = rel;
        }
        return !segs.Any(seg => NeverAllowed.Contains(seg, StringComparer.OrdinalIgnoreCase)
                             || seg.StartsWith("userdata", StringComparison.OrdinalIgnoreCase));
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
        var cefRoot = Full(CefRoot);
        if (Directory.Exists(cefRoot) && !IsReparsePoint(cefRoot)) FindCefCaches(cefRoot, 0, files, skipped);
        return new CachePreview(files, skipped);
    }

    private void FindCefCaches(string dir, int depth, List<CacheFile> files, List<string> skipped)
    {
        if (depth >= CefMaxDepth) return;
        foreach (var d in Directory.EnumerateDirectories(dir))
        {
            if (IsReparsePoint(d)) { skipped.Add(d); continue; }
            if (CefCacheFolderNames.Contains(Path.GetFileName(d), StringComparer.OrdinalIgnoreCase)) Walk(d, files, skipped);
            else FindCefCaches(d, depth + 1, files, skipped);
        }
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
