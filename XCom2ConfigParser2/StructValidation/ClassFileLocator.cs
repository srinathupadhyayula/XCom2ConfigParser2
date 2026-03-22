namespace XCom2ConfigParser2.StructValidation;

/// <summary>
/// Locates UnrealScript class files (.uc) using paths from configuration settings.
///
/// Search order (matches StructFileLocator for consistency):
///   1. Local Src
///   2. Mods Compiled Against
///   3. SDK SrcOrig
///   4. Community Highlander
///   5. Alien Highlander
///   6. All Mods (via ModSrcPathCache, package-targeted)
/// </summary>
public sealed class ClassFileLocator
{
    private readonly Configuration.ParserSettings _settings;
    private readonly ModSrcPathCache? _modSrcCache;

    public ClassFileLocator(Configuration.ParserSettings settings, ModSrcPathCache? modSrcCache = null)
    {
        _settings = settings;
        _modSrcCache = modSrcCache;
    }

    public ClassFileResult Locate(string packageName, string className)
    {
        var searched = new List<string>();
        string fileName = $"{className}.uc";

        ClassFileResult TryInRoot(string root, string sourceLabel)
        {
            // Convention: <root>/<Package>/Classes/<Class>.uc
            string primary = Path.Combine(root, packageName, "Classes", fileName);
            if (File.Exists(primary))
                return ClassFileResult.Success(primary, sourceLabel);
            searched.Add(primary);

            // Fallback (no Classes subdirectory): <root>/<Package>/<Class>.uc
            string alt = Path.Combine(root, packageName, fileName);
            if (File.Exists(alt))
                return ClassFileResult.Success(alt, sourceLabel);
            searched.Add(alt);

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

        // 3. SDK SrcOrig (base game source — should come before Highlander overrides)
        if (!string.IsNullOrEmpty(_settings.SdkRoot))
        {
            string sdkSrc = Path.Combine(_settings.SdkRoot, "Development", "SrcOrig");
            var r = TryInRoot(sdkSrc, "SDK");
            if (r.Found) return r;
        }

        // 4. Community Highlander
        if (!string.IsNullOrEmpty(_settings.CommunityHighlanderPath))
        {
            var r = TryInRoot(_settings.CommunityHighlanderPath, "CommunityHighlander");
            if (r.Found) return r;
        }

        // 5. Alien Highlander
        if (!string.IsNullOrEmpty(_settings.AlienHighlanderPath))
        {
            var r = TryInRoot(_settings.AlienHighlanderPath, "AlienHighlander");
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
