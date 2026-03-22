namespace XCom2ConfigParser2.StructValidation;

/// <summary>
/// Locates .uc files containing struct definitions across all configured source paths.
///
/// Key improvements over the previous version:
/// - Per-folder and per-file exception handling — one inaccessible folder
///   does NOT abort the rest of the search.
/// - Package-aware search: when packageName is known, limits search to
///   {root}/{packageName}/Classes/ only, skipping unrelated packages.
/// - Uses ModSrcPathCache for AllMods fallback — mod Src dirs are discovered once.
/// - Delegates all file reading to UnrealScriptParser (encoding-resilient).
/// - Uses streaming line-based check (FileContainsStruct) instead of ReadAllText.
///
/// Search order (same as ClassFileLocator):
///   1. Local Src
///   2. Mods Compiled Against
///   3. SDK SrcOrig
///   4. Community Highlander
///   5. Alien Highlander
///   6. All Mods (via ModSrcPathCache)
/// </summary>
public sealed class StructFileLocator
{
    private readonly Configuration.ParserSettings _settings;
    private readonly ModSrcPathCache? _modSrcCache;

    public StructFileLocator(Configuration.ParserSettings settings, ModSrcPathCache? modSrcCache = null)
    {
        _settings = settings;
        _modSrcCache = modSrcCache;
    }

    /// <summary>
    /// Locates a .uc file containing a definition for the given struct.
    /// </summary>
    /// <param name="structName">The struct name to find.</param>
    /// <param name="knownPackageName">
    /// Optional. When the package the struct belongs to is already known,
    /// the search can be limited to that package's directory only.
    /// </param>
    public ClassFileResult Locate(string structName, string? knownPackageName = null)
    {
        var searched = new List<string>();
        var searchedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        ClassFileResult? TrySearchRoot(string root, string sourceLabel)
        {
            if (!Directory.Exists(root))
                return null;

            if (!string.IsNullOrEmpty(knownPackageName))
            {
                // Package-aware: only look in {root}/{PackageName}/Classes/
                string packageClassesDir = Path.Combine(root, knownPackageName, "Classes");
                var r = SearchSingleDirectory(packageClassesDir, structName, sourceLabel, searched, searchedDirs);
                if (r != null) return r;
            }
            else
            {
                // Full recursive search with per-directory exception handling
                var r = SearchDirectoryRecursive(root, structName, sourceLabel, searched, searchedDirs);
                if (r != null) return r;
            }

            return null;
        }

        // 1. Local Src
        if (!string.IsNullOrEmpty(_settings.LocalSrcRoot))
        {
            var r = TrySearchRoot(_settings.LocalSrcRoot, "LocalSrc");
            if (r != null) return r;
        }

        // 2. Mods Compiled Against
        foreach (var modPath in _settings.ModsCompiledAgainst)
        {
            var r = TrySearchRoot(modPath, "ModCompiledAgainst");
            if (r != null) return r;
        }

        // 3. SDK SrcOrig
        if (!string.IsNullOrEmpty(_settings.SdkRoot))
        {
            string sdkSrc = Path.Combine(_settings.SdkRoot, "Development", "SrcOrig");
            var r = TrySearchRoot(sdkSrc, "SDK");
            if (r != null) return r;
        }

        // 4. Community Highlander
        if (!string.IsNullOrEmpty(_settings.CommunityHighlanderPath) &&
            Directory.Exists(_settings.CommunityHighlanderPath))
        {
            var r = TrySearchRoot(_settings.CommunityHighlanderPath, "CommunityHighlander");
            if (r != null) return r;
        }

        // 5. Alien Highlander
        if (!string.IsNullOrEmpty(_settings.AlienHighlanderPath) &&
            Directory.Exists(_settings.AlienHighlanderPath))
        {
            var r = TrySearchRoot(_settings.AlienHighlanderPath, "AlienHighlander");
            if (r != null) return r;
        }

        // 6. AllMods fallback — uses pre-computed ModSrcPathCache
        if (_modSrcCache != null)
        {
            foreach (var modSrc in _modSrcCache.AllModSrcRoots)
            {
                var r = TrySearchRoot(modSrc, "AllMods");
                if (r != null) return r;
            }
        }

        return ClassFileResult.NotFound(searched);
    }

    // -------------------------------------------------------------------------
    // Search helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Recursively searches a root directory for a .uc file containing the struct.
    /// Handles UnauthorizedAccessException and IOException PER DIRECTORY/FILE,
    /// so one inaccessible location does not abort the rest of the search.
    /// </summary>
    private static ClassFileResult? SearchDirectoryRecursive(
        string rootDir,
        string structName,
        string sourceLabel,
        List<string> searched,
        HashSet<string> searchedDirs)
    {
        // Use a stack-based manual recursion to enable per-folder exception handling
        var stack = new Stack<string>();
        stack.Push(rootDir);

        while (stack.Count > 0)
        {
            string dir = stack.Pop();
            string normalizedDir = NormalizePath(dir);

            if (!searchedDirs.Add(normalizedDir))
                continue;  // Already processed

            // Search .uc files in this directory
            IEnumerable<string> ucFiles;
            try
            {
                ucFiles = Directory.EnumerateFiles(dir, "*.uc", SearchOption.TopDirectoryOnly);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;  // Skip inaccessible directory but continue with others
            }

            foreach (string ucFile in ucFiles)
            {
                searched.Add(ucFile);
                if (UnrealScriptParser.FileContainsStruct(ucFile, structName))
                    return ClassFileResult.Success(ucFile, sourceLabel);
            }

            // Enqueue subdirectories for processing
            IEnumerable<string> subDirs;
            try
            {
                subDirs = Directory.EnumerateDirectories(dir, "*", SearchOption.TopDirectoryOnly);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;  // Can't enumerate subdirs; skip
            }

            foreach (string subDir in subDirs)
                stack.Push(subDir);
        }

        return null;
    }

    /// <summary>
    /// Searches a single directory (non-recursive) for a .uc file containing the struct.
    /// </summary>
    private static ClassFileResult? SearchSingleDirectory(
        string dir,
        string structName,
        string sourceLabel,
        List<string> searched,
        HashSet<string> searchedDirs)
    {
        if (!Directory.Exists(dir)) return null;

        string normalizedDir = NormalizePath(dir);
        if (!searchedDirs.Add(normalizedDir))
            return null;  // Already searched

        IEnumerable<string> ucFiles;
        try
        {
            ucFiles = Directory.EnumerateFiles(dir, "*.uc", SearchOption.TopDirectoryOnly);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        foreach (string ucFile in ucFiles)
        {
            searched.Add(ucFile);
            if (UnrealScriptParser.FileContainsStruct(ucFile, structName))
                return ClassFileResult.Success(ucFile, sourceLabel);
        }

        return null;
    }

    private static string NormalizePath(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
