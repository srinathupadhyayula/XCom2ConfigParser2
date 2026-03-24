namespace XCom2ConfigParser2.StructValidation;

/// <summary>
/// Locates UnrealScript class files (.uc) using paths from configuration settings.
///
/// Search order (matches StructFileLocator for consistency):
///   1. Local Src
///   2. Mods Compiled Against
///   3. Community Highlander
///   4. Alien Highlander
///   5. SDK SrcOrig
///   6. All Mods (via ModSrcPathCache, package-targeted)
/// </summary>
/// <summary>
/// Provides high-level logic for locating UnrealScript class source files (.uc) across the project hierarchy.
/// </summary>
/// <remarks>
/// Searches are performed in a specific priority order:
/// <list type="number">
/// <item><description>Local Source Root</description></item>
/// <item><description>Explicitly configured Mod Dependencies</description></item>
/// <item><description>Community Highlander Source</description></item>
/// <item><description>Alien Highlander Source</description></item>
/// <item><description>Base SDK Source (SrcOrig)</description></item>
/// <item><description>Global Mod Search (via <see cref="ModSrcPathCache"/>)</description></item>
/// </list>
/// </remarks>
public sealed class ClassFileLocator
{
    private readonly Configuration.ParserSettings _settings;
    private readonly ModSrcPathCache? _modSrcCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClassFileLocator"/> class.
    /// </summary>
    /// <param name="settings">The global parser configuration settings.</param>
    /// <param name="modSrcCache">Optional cache of discovered mod source roots.</param>
    public ClassFileLocator(Configuration.ParserSettings settings, ModSrcPathCache? modSrcCache = null)
    {
        _settings = settings;
        _modSrcCache = modSrcCache;
    }

    /// <summary>
    /// Searches for the .uc file corresponding to the specified package and class name.
    /// </summary>
    /// <param name="packageName">The name of the package (e.g., "XComGame").</param>
    /// <param name="className">The name of the class (e.g., "X2Ability_Grenadier").</param>
    /// <returns>A <see cref="ClassFileResult"/> containing the discovered file path or a log of searched locations.</returns>
    public ClassFileResult Locate(string packageName, string className)
    {
        var searched = new List<string>();
        string fileName = $"{className}.uc";

        ClassFileResult TryInRoot(string root, string sourceLabel)
        {
            if (string.IsNullOrEmpty(packageName))
            {
                // Global search: search all package subdirectories in this root
                try
                {
                    foreach (var pkgDir in Directory.EnumerateDirectories(root))
                    {
                        string primary = Path.Combine(pkgDir, "Classes", fileName);
                        if (File.Exists(primary))
                            return ClassFileResult.Success(primary, sourceLabel);
                        
                        string alt = Path.Combine(pkgDir, fileName);
                        if (File.Exists(alt))
                            return ClassFileResult.Success(alt, sourceLabel);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Skip inaccessible roots or continue search
                }
                
                return ClassFileResult.NotFound(searched);
            }

            // Convention: <root>/<Package>/Classes/<Class>.uc
            string primaryPath = Path.Combine(root, packageName, "Classes", fileName);
            if (File.Exists(primaryPath))
                return ClassFileResult.Success(primaryPath, sourceLabel);
            searched.Add(primaryPath);

            // Fallback (no Classes subdirectory): <root>/<Package>/<Class>.uc
            string altPath = Path.Combine(root, packageName, fileName);
            if (File.Exists(altPath))
                return ClassFileResult.Success(altPath, sourceLabel);
            searched.Add(altPath);

            return ClassFileResult.NotFound(searched);
        }

        // 1. Local Src
        if (!string.IsNullOrEmpty(_settings.LocalSrcRoot))
        {
            var r = TryInRoot(_settings.LocalSrcRoot, "LocalSrc");
            if (r.Found) return r;
        }

        // 2. Mods Compiled Against
        foreach (var modPath in _settings.ModsCompiledAgainst)
        {
            var r = TryInRoot(modPath, "ModCompiledAgainst");
            if (r.Found) return r;
        }

        // 3. Community Highlander
        if (!string.IsNullOrEmpty(_settings.CommunityHighlanderPath))
        {
            var r = TryInRoot(_settings.CommunityHighlanderPath, "CommunityHighlander");
            if (r.Found) return r;
        }

        // 4. Alien Highlander
        if (!string.IsNullOrEmpty(_settings.AlienHighlanderPath))
        {
            var r = TryInRoot(_settings.AlienHighlanderPath, "AlienHighlander");
            if (r.Found) return r;
        }

        // 5. SDK SrcOrig (base game source — should come AFTER Highlander overrides)
        if (!string.IsNullOrEmpty(_settings.SdkRoot))
        {
            string sdkSrc = Path.Combine(_settings.SdkRoot, "Development", "SrcOrig");
            var r = TryInRoot(sdkSrc, "SDK");
            if (r.Found) return r;
        }

        // 6. AllMods fallback (package-targeted via ModSrcPathCache)
        if (_modSrcCache != null)
        {
            foreach (var modSrc in _modSrcCache.AllModSrcRoots)
            {
                var r = TryInRoot(modSrc, "AllMods");
                if (r.Found) return r;
            }
        }

        return ClassFileResult.NotFound(searched);
    }
}
