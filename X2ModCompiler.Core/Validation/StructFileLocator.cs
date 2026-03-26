using Microsoft.Extensions.Logging;
using X2ModCompiler.Core.Parser;

namespace X2ModCompiler.Core.Validation;

/// <summary>
/// Provides high-level logic for locating the specific UnrealScript (.uc) file that contains a given struct definition.
/// </summary>
public sealed class StructFileLocator
{
    private readonly Configuration.ParserSettings _settings;
    private readonly ModSrcPathCache? _modSrcCache;
    private readonly ILogger<StructFileLocator> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StructFileLocator"/> class.
    /// </summary>
    /// <param name="settings">The global parser settings.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="modSrcCache">Optional cache for discovered mod source roots.</param>
    public StructFileLocator(Configuration.ParserSettings settings, ILogger<StructFileLocator> logger, ModSrcPathCache? modSrcCache = null)
    {
        _settings = settings;
        _logger = logger;
        _modSrcCache = modSrcCache;
    }

    /// <summary>
    /// Searches for a .uc file containing the specified struct definition across all configured paths.
    /// </summary>
    public ClassFileResult Locate(string structName, string? knownPackageName = null)
    {
        _logger.LogTrace("Locating struct {StructName} (known package: {Package})", structName, knownPackageName ?? "none");
        var searched = new List<string>();
        var searchedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        ClassFileResult? TrySearchRoot(string root, string sourceLabel)
        {
            if (!Directory.Exists(root))
                return null;

            if (!string.IsNullOrEmpty(knownPackageName))
            {
                string packageClassesDir = Path.Combine(root, knownPackageName, "Classes");
                var r = SearchSingleDirectory(packageClassesDir, structName, sourceLabel, searched, searchedDirs);
                if (r != null) return r;
            }
            else
            {
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

        // 4. Highlanders
        if (!string.IsNullOrEmpty(_settings.CommunityHighlanderPath))
        {
            var r = TrySearchRoot(_settings.CommunityHighlanderPath, "CommunityHighlander");
            if (r != null) return r;
        }

        if (!string.IsNullOrEmpty(_settings.AlienHighlanderPath))
        {
            var r = TrySearchRoot(_settings.AlienHighlanderPath, "AlienHighlander");
            if (r != null) return r;
        }

        // 5. AllMods fallback
        if (_modSrcCache != null)
        {
            foreach (var modSrc in _modSrcCache.AllModSrcRoots)
            {
                var r = TrySearchRoot(modSrc, "AllMods");
                if (r != null) return r;
            }
        }

        _logger.LogDebug("Struct {StructName} not found after searching {Count} files", structName, searched.Count);
        return ClassFileResult.NotFound(searched);
    }

    private ClassFileResult? SearchDirectoryRecursive(
        string rootDir,
        string structName,
        string sourceLabel,
        List<string> searched,
        HashSet<string> searchedDirs)
    {
        var stack = new Stack<string>();
        stack.Push(rootDir);

        while (stack.Count > 0)
        {
            string dir = stack.Pop();
            string normalizedDir = NormalizePath(dir);

            if (!searchedDirs.Add(normalizedDir))
                continue;

            IEnumerable<string> ucFiles;
            try
            {
                ucFiles = Directory.EnumerateFiles(dir, "*.uc", SearchOption.TopDirectoryOnly);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogTrace(ex, "Skipping inaccessible directory: {Dir}", dir);
                continue;
            }

            foreach (string ucFile in ucFiles)
            {
                searched.Add(ucFile);
                if (UnrealScriptParser.FileContainsStruct(ucFile, structName))
                {
                    _logger.LogDebug("Found struct {StructName} in {FilePath} ({SourceLabel})", structName, ucFile, sourceLabel);
                    return ClassFileResult.Success(ucFile, sourceLabel);
                }
            }

            IEnumerable<string> subDirs;
            try
            {
                subDirs = Directory.EnumerateDirectories(dir, "*", SearchOption.TopDirectoryOnly);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (string subDir in subDirs)
                stack.Push(subDir);
        }

        return null;
    }

    private ClassFileResult? SearchSingleDirectory(
        string dir,
        string structName,
        string sourceLabel,
        List<string> searched,
        HashSet<string> searchedDirs)
    {
        if (!Directory.Exists(dir)) return null;

        string normalizedDir = NormalizePath(dir);
        if (!searchedDirs.Add(normalizedDir))
            return null;

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
            {
                _logger.LogDebug("Found struct {StructName} in {FilePath} ({SourceLabel})", structName, ucFile, sourceLabel);
                return ClassFileResult.Success(ucFile, sourceLabel);
            }
        }

        return null;
    }

    private static string NormalizePath(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
