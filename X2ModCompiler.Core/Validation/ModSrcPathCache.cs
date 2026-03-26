using Microsoft.Extensions.Logging;

namespace X2ModCompiler.Core.Validation;

/// <summary>
/// Proactively discovers and caches the set of all UnrealScript source roots within the mods directory hierarchy.
/// </summary>
public sealed class ModSrcPathCache
{
    private readonly List<string> _modSrcRoots;
    private readonly ILogger<ModSrcPathCache> _logger;

    /// <summary>
    /// Gets the collection of all discovered mod source roots, excluding those already explicitly specified in configuration.
    /// </summary>
    public IReadOnlyList<string> AllModSrcRoots => _modSrcRoots;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModSrcPathCache"/> class.
    /// </summary>
    /// <param name="settings">The global parser configuration settings.</param>
    /// <param name="logger">The logger instance.</param>
    public ModSrcPathCache(Configuration.ParserSettings settings, ILogger<ModSrcPathCache> logger)
    {
        _modSrcRoots = new List<string>();
        _logger = logger;

        if (string.IsNullOrEmpty(settings.AllModsRoot) || !Directory.Exists(settings.AllModsRoot))
        {
            _logger.LogDebug("AllModsRoot not configured or does not exist: {AllModsRoot}", settings.AllModsRoot);
            return;
        }

        _logger.LogInformation("Discovering mod source roots in {AllModsRoot}", settings.AllModsRoot);

        var alreadyConfigured = BuildAlreadyConfiguredSet(settings);

        IEnumerable<string> topLevelDirs;
        try
        {
            topLevelDirs = Directory.EnumerateDirectories(settings.AllModsRoot, "*", SearchOption.TopDirectoryOnly);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Failed to enumerate mod directories in {AllModsRoot}", settings.AllModsRoot);
            return;
        }

        foreach (string modDir in topLevelDirs)
        {
            string srcDir = Path.Combine(modDir, "Src");
            if (!Directory.Exists(srcDir))
            {
                _logger.LogTrace("Skipping mod {ModDir} (no Src folder found)", modDir);
                continue;
            }

            string normalizedSrc = NormalizePath(srcDir);
            if (!alreadyConfigured.Contains(normalizedSrc))
            {
                _logger.LogDebug("Discovered mod source root: {SrcDir}", srcDir);
                _modSrcRoots.Add(srcDir);
            }
            else
            {
                _logger.LogTrace("Skipping mod source root {SrcDir} (already explicitly configured)", srcDir);
            }
        }

        _logger.LogInformation("Mod source discovery complete. Found {Count} roots.", _modSrcRoots.Count);
    }

    private static HashSet<string> BuildAlreadyConfiguredSet(Configuration.ParserSettings settings)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddIfNotEmpty(string? path)
        {
            if (!string.IsNullOrEmpty(path))
                set.Add(NormalizePath(path));
        }

        AddIfNotEmpty(settings.LocalSrcRoot);
        AddIfNotEmpty(settings.CommunityHighlanderPath);
        AddIfNotEmpty(settings.AlienHighlanderPath);
        foreach (var modPath in settings.ModsCompiledAgainst)
            AddIfNotEmpty(modPath);

        if (!string.IsNullOrEmpty(settings.SdkRoot))
            AddIfNotEmpty(Path.Combine(settings.SdkRoot, "Development", "SrcOrig"));

        return set;
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
