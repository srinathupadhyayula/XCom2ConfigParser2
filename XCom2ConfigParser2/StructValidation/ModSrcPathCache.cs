namespace XCom2ConfigParser2.StructValidation;

/// <summary>
/// Discovers and caches the set of all Src directories within mod folders.
/// A mod directory is only considered a source root if it has a "Src" subdirectory.
///
/// This discovery runs once at startup and is then consulted by
/// ClassFileLocator and StructFileLocator for their AllMods fallback search,
/// avoiding repeated expensive directory enumerations.
/// </summary>
/// <summary>
/// Proactively discovers and caches the set of all UnrealScript source roots within the mods directory hierarchy.
/// </summary>
/// <remarks>
/// A mod subfolder is only recognized as a source root if it contains a <c>Src</c> subdirectory.
/// This discovery process runs once during application initialization to optimize subsequent file location queries,
/// avoiding repeated and expensive file system traversals.
/// </remarks>
public sealed class ModSrcPathCache
{
    private readonly List<string> _modSrcRoots;

    /// <summary>
    /// Gets the collection of all discovered mod source roots, excluding those already explicitly specified in configuration.
    /// </summary>
    public IReadOnlyList<string> AllModSrcRoots => _modSrcRoots;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModSrcPathCache"/> class.
    /// Traverses the <see cref="Configuration.ParserSettings.AllModsRoot"/> to identify valid source packages.
    /// </summary>
    /// <param name="settings">The global parser configuration settings.</param>
    public ModSrcPathCache(Configuration.ParserSettings settings)
    {
        _modSrcRoots = new List<string>();

        if (string.IsNullOrEmpty(settings.AllModsRoot) || !Directory.Exists(settings.AllModsRoot))
            return;

        // Build set of already-explicitly-configured paths so we don't double-search
        var alreadyConfigured = BuildAlreadyConfiguredSet(settings);

        // Enumerate only top-level subdirectories of AllModsRoot
        // (mods are direct children — no deep recursion needed here)
        IEnumerable<string> topLevelDirs;
        try
        {
            topLevelDirs = Directory.EnumerateDirectories(settings.AllModsRoot, "*", SearchOption.TopDirectoryOnly);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (string modDir in topLevelDirs)
        {
            string srcDir = Path.Combine(modDir, "Src");
            if (!Directory.Exists(srcDir))
                continue;

            string normalizedSrc = NormalizePath(srcDir);
            if (!alreadyConfigured.Contains(normalizedSrc))
                _modSrcRoots.Add(srcDir);
        }
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

        // Also add the SDK SrcOrig path
        if (!string.IsNullOrEmpty(settings.SdkRoot))
            AddIfNotEmpty(Path.Combine(settings.SdkRoot, "Development", "SrcOrig"));

        return set;
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
