namespace XCom2ConfigParser2.StructValidation;

/// <summary>
/// Provides high-level logic for locating the specific UnrealScript (.uc) file that contains a given struct definition.
/// Searches through the configured codebase hierarchy, including local source, mod dependencies, and the base SDK.
/// </summary>
/// <remarks>
/// This locator is designed to be resilient and efficient:
/// <list type="bullet">
/// <item><description><b>Fault Tolerance:</b> Utilizes per-directory exception handling to ensure that inaccessible folders do not abort the entire search process.</description></item>
/// <item><description><b>Package Awareness:</b> Can limit search scope to a specific package directory if the package name is known.</description></item>
/// <item><description><b>Performance:</b> Uses streaming line-based checks via <see cref="UnrealScriptParser.FileContainsStruct"/> to avoid reading entire file contents into memory.</description></item>
/// </list>
/// </remarks>
public sealed class StructFileLocator
{
    private readonly Configuration.ParserSettings _settings;
    private readonly ModSrcPathCache? _modSrcCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="StructFileLocator"/> class.
    /// </summary>
    /// <param name="settings">The global parser settings.</param>
    /// <param name="modSrcCache">Optional cache for discovered mod source roots.</param>
    public StructFileLocator(Configuration.ParserSettings settings, ModSrcPathCache? modSrcCache = null)
    {
        _settings = settings;
        _modSrcCache = modSrcCache;
    }

    /// <summary>
    /// Searches for a .uc file containing the specified struct definition across all configured paths.
    /// </summary>
    /// <param name="structName">The case-insensitive name of the struct to find.</param>
    /// <param name="knownPackageName">Optional. The name of the package the struct is expected to reside in, used to optimize the search.</param>
    /// <returns>A <see cref="ClassFileResult"/> containing the path to the file if found, or the list of searched paths if missing.</returns>
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

    /// <summary>
    /// Recursively searches a directory tree for a .uc file containing the target struct.
    /// Employs a stack-based traversal to allow per-directory error handling and avoid stack overflows on deep trees.
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
    /// Performs a non-recursive search within a single directory for a file containing the struct definition.
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

    /// <summary>
    /// Normalizes a directory path for consistent comparison in the search history.
    /// </summary>
    private static string NormalizePath(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
