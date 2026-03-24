namespace XCom2ConfigParser2.StructValidation;

/// <summary>
/// Encapsulates the outcome of a struct resolution attempt. 
/// Provides access to the successfully resolved definition or detailed failure information for diagnostics.
/// </summary>
public sealed class StructResolutionResult
{
    /// <summary>Gets a value indicating whether the struct was successfully found and parsed.</summary>
    public bool Found { get; }

    /// <summary>Gets the resolved struct definition if <see cref="Found"/> is true.</summary>
    public CachedStructDef? StructDef { get; }

    /// <summary>Gets a description of where the struct was resolved from (e.g., "Cache", "Mod Source", "SDK").</summary>
    public string ResolutionSource { get; }

    /// <summary>Gets an optional error message if resolution failed due to an exception or circular reference.</summary>
    public string? ErrorMessage { get; }

    /// <summary>Gets the list of file paths that were searched before concluding resolution failed.</summary>
    public IReadOnlyList<string> SearchedPaths { get; }

    private StructResolutionResult(bool found, CachedStructDef? structDef, string resolutionSource, string? errorMessage, IReadOnlyList<string> searchedPaths)
    {
        Found = found;
        StructDef = structDef;
        ResolutionSource = resolutionSource;
        ErrorMessage = errorMessage;
        SearchedPaths = searchedPaths;
    }

    /// <summary>Creates a successful resolution result.</summary>
    public static StructResolutionResult Success(CachedStructDef def, string source) =>
        new(true, def, source, null, Array.Empty<string>());

    /// <summary>Creates a result indicating the struct could not be located in any searched paths.</summary>
    public static StructResolutionResult NotFound(IReadOnlyList<string> searchedPaths) =>
        new(false, null, "", null, searchedPaths);

    /// <summary>Creates a result representing an error during the resolution process.</summary>
    public static StructResolutionResult Error(string message) =>
        new(false, null, "", message, Array.Empty<string>());
}

/// <summary>
/// Provides logic for locating, parsing, and caching UnrealScript struct definitions.
/// Handles recursive resolution of nested structs and utilizes both positive and negative caching 
/// to optimize repetitive lookups during configuration validation.
/// </summary>
public sealed class StructDefinitionResolver
{
    private readonly StructCache _cache;
    private readonly StructFileLocator _locator;
    private readonly HashSet<string> _resolving = new();  // Cycle detection
    private const int MaxRecursionDepth = 10;

    /// <summary>
    /// Initializes a new instance of the <see cref="StructDefinitionResolver"/> class.
    /// </summary>
    /// <param name="settings">The global parser settings.</param>
    /// <param name="cache"> пре-existing struct cache to use for lookups and persistence.</param>
    /// <param name="modSrcCache">Optional cache for mod source file locations.</param>
    public StructDefinitionResolver(Configuration.ParserSettings settings, StructCache cache, ModSrcPathCache? modSrcCache = null)
    {
        _cache = cache;
        _locator = new StructFileLocator(settings, modSrcCache);
    }

    /// <summary>
    /// Resolves a struct definition by name. Checks the cache first, then searches the file system if necessary.
    /// </summary>
    /// <param name="structName">The case-insensitive name of the struct to find.</param>
    /// <param name="depth">Current recursion depth for nested struct resolution.</param>
    /// <returns>A <see cref="StructResolutionResult"/> containing the outcome of the search.</returns>
    public StructResolutionResult Resolve(string structName, int depth = 0)
    {
        // Check positive cache first
        if (_cache.TryGet(structName, out var cached))
            return StructResolutionResult.Success(cached!, "Cache");

        // Check negative cache — means we already know it can't be found
        if (_cache.IsKnownNotFound(structName))
            return StructResolutionResult.NotFound(Array.Empty<string>());

        // Cycle detection
        if (_resolving.Contains(structName))
            return StructResolutionResult.Error($"Circular struct reference: {structName}");

        _resolving.Add(structName);
        try
        {
            // Search for struct definition
            var searchResult = _locator.Locate(structName);
            if (!searchResult.Found)
            {
                // Save negative cache entry so we don't search again this session
                _cache.SaveNotFound(structName);
                return StructResolutionResult.NotFound(searchResult.SearchedPaths);
            }

            // Parse struct definition using the unified parser
            var structDef = UnrealScriptParser.FindStruct(searchResult.FilePath!, structName);
            if (structDef == null || structDef.Fields.Count == 0)
            {
                _cache.SaveNotFound(structName);
                return StructResolutionResult.NotFound(searchResult.SearchedPaths);
            }

            // Map parsed fields to StructField objects
            var fields = structDef.Fields.Select(f => new StructField
            {
                Name = f.Name,
                TypeName = f.TypeName,
            }).ToList();

            // Resolve nested structs (recursive)
            var nestedStructs = new List<string>();
            foreach (var field in fields)
            {
                if (field.IsStruct && depth < MaxRecursionDepth)
                {
                    var nestedResult = Resolve(field.BaseType, depth + 1);
                    if (nestedResult.Found)
                        nestedStructs.Add(field.BaseType);
                }
            }

            // Create and cache the result
            var cachedDef = new CachedStructDef
            {
                StructName = structName,
                SourceFile = searchResult.FilePath!,
                SourceHash = ComputeHash(searchResult.FilePath!),
                LastIndexed = DateTime.UtcNow,
                Fields = fields,
                NestedStructs = nestedStructs,
                ResolvedNestedStructs = true,
            };

            _cache.Save(cachedDef);

            return StructResolutionResult.Success(cachedDef, searchResult.Source);
        }
        finally
        {
            _resolving.Remove(structName);
        }
    }

    /// <summary>
    /// Computes a SHA256 hash of the specified file to detect changes and invalidate cache entries.
    /// </summary>
    private static string ComputeHash(string filePath)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(stream);
        return "sha256:" + BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}
