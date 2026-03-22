namespace XCom2ConfigParser2.StructValidation;

/// <summary>
/// Result of struct definition resolution.
/// </summary>
public sealed class StructResolutionResult
{
    public bool Found { get; }
    public CachedStructDef? StructDef { get; }
    public string ResolutionSource { get; }
    public string? ErrorMessage { get; }
    public IReadOnlyList<string> SearchedPaths { get; }

    private StructResolutionResult(bool found, CachedStructDef? structDef, string resolutionSource, string? errorMessage, IReadOnlyList<string> searchedPaths)
    {
        Found = found;
        StructDef = structDef;
        ResolutionSource = resolutionSource;
        ErrorMessage = errorMessage;
        SearchedPaths = searchedPaths;
    }

    public static StructResolutionResult Success(CachedStructDef def, string source) =>
        new(true, def, source, null, Array.Empty<string>());

    public static StructResolutionResult NotFound(IReadOnlyList<string> searchedPaths) =>
        new(false, null, "", null, searchedPaths);

    public static StructResolutionResult Error(string message) =>
        new(false, null, "", message, Array.Empty<string>());
}

/// <summary>
/// Finds and resolves struct definitions with caching and recursive nested struct resolution.
/// Delegates all .uc file parsing to <see cref="UnrealScriptParser"/>.
/// </summary>
public sealed class StructDefinitionResolver
{
    private readonly StructCache _cache;
    private readonly StructFileLocator _locator;
    private readonly HashSet<string> _resolving = new();  // Cycle detection
    private const int MaxRecursionDepth = 10;

    public StructDefinitionResolver(Configuration.ParserSettings settings, StructCache cache, ModSrcPathCache? modSrcCache = null)
    {
        _cache = cache;
        _locator = new StructFileLocator(settings, modSrcCache);
    }

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

    private static string ComputeHash(string filePath)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(stream);
        return "sha256:" + BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}
