namespace XCom2ConfigParser2.StructValidation;

/// <summary>
/// Scans configured UnrealScript source directories to pre-populate the <see cref="StructCache"/>.
/// This indexing phase is typically run at application startup to ensure that subsequent configuration 
/// validation can perform fast, cache-only lookups for struct definitions.
/// </summary>
/// <remarks>
/// Parallel processing is used for file-system enumeration and content parsing to maximize throughput.
/// </remarks>
public sealed class StructIndexer
{
    private readonly Configuration.ParserSettings _settings;
    private readonly StructCache _cache;
    private readonly ModSrcPathCache _modSrcCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="StructIndexer"/> class.
    /// </summary>
    /// <param name="settings">The global parser settings.</param>
    /// <param name="cache">The struct cache to be populated.</param>
    /// <param name="modSrcCache">The cache containing mod source directory mappings.</param>
    public StructIndexer(Configuration.ParserSettings settings, StructCache cache, ModSrcPathCache modSrcCache)
    {
        _settings = settings;
        _cache = cache;
        _modSrcCache = modSrcCache;
    }

    /// <summary>
    /// Recursively scans all search roots for .uc files and indexes any struct definitions found.
    /// Utilizes <see cref="Parallel.ForEach"/> to process files concurrently.
    /// </summary>
    /// <param name="progress">Optional provider for tracking indexing progress.</param>
    /// <returns>An <see cref="IndexingResult"/> summarizing the scan results.</returns>
    public IndexingResult IndexAll(IProgress<IndexingProgress>? progress = null)
    {
        var roots = BuildSearchRoots();

        // Pre-collect all .uc files from all roots
        var allErrors = new System.Collections.Concurrent.ConcurrentBag<string>();
        var allFiles = new List<string>();

        foreach (var (rootDir, _) in roots)
        {
            if (!Directory.Exists(rootDir))
                continue;

            foreach (string ucFile in EnumerateUcFiles(rootDir, allErrors))
                allFiles.Add(ucFile);
        }

        int filesScanned = 0;
        int structsFound = 0;

        // Parallel processing using Parallel.ForEach with thread-safe counters
        var lockObj = new object();
        Parallel.ForEach(allFiles, ucFile =>
        {
            int scannedIncrement = 1;
            int structsIncrement = 0;

            var structDefs = UnrealScriptParser.ParseStructs(ucFile);

            foreach (var structDef in structDefs)
            {
                // Skip if a valid positive cache entry already exists
                if (_cache.TryGet(structDef.Name, out _))
                    continue;

                string hash = ComputeHash(ucFile);
                var cachedDef = new CachedStructDef
                {
                    StructName = structDef.Name,
                    SourceFile = ucFile,
                    SourceHash = hash,
                    LastIndexed = DateTime.UtcNow,
                    Fields = structDef.Fields.Select(f => new StructField
                    {
                        Name = f.Name,
                        TypeName = f.TypeName,
                    }).ToList(),
                    NestedStructs = new List<string>(),
                    ResolvedNestedStructs = false,
                };

                lock (lockObj)
                {
                    // Double-check after acquiring lock to avoid duplicate cache writes
                    if (!_cache.TryGet(structDef.Name, out _))
                    {
                        _cache.Save(cachedDef);
                        structsIncrement++;
                    }
                }
            }

            int currentFiles;
            lock (lockObj)
            {
                filesScanned += scannedIncrement;
                structsFound += structsIncrement;
                currentFiles = filesScanned;
            }

            progress?.Report(new IndexingProgress(currentFiles, ucFile));
        });

        return new IndexingResult(filesScanned, structsFound, 0, allErrors.ToList());
    }

    /// <summary>
    /// Aggregates all potential UnrealScript source roots specified in the configuration.
    /// </summary>
    private List<(string Dir, string Label)> BuildSearchRoots()
    {
        var roots = new List<(string, string)>();

        if (!string.IsNullOrEmpty(_settings.LocalSrcRoot))
            roots.Add((_settings.LocalSrcRoot, "LocalSrc"));

        foreach (var modPath in _settings.ModsCompiledAgainst)
            roots.Add((modPath, "ModCompiledAgainst"));

        if (!string.IsNullOrEmpty(_settings.SdkRoot))
        {
            string sdkSrc = Path.Combine(_settings.SdkRoot, "Development", "SrcOrig");
            roots.Add((sdkSrc, "SDK"));
        }

        if (!string.IsNullOrEmpty(_settings.CommunityHighlanderPath))
            roots.Add((_settings.CommunityHighlanderPath, "CommunityHighlander"));

        if (!string.IsNullOrEmpty(_settings.AlienHighlanderPath))
            roots.Add((_settings.AlienHighlanderPath, "AlienHighlander"));

        foreach (var modSrc in _modSrcCache.AllModSrcRoots)
            roots.Add((modSrc, "AllMods"));

        return roots;
    }

    /// <summary>
    /// Efficiently enumerates .uc files in a directory tree without loading the entire list into memory.
    /// Handles IO errors and unauthorized access by logging them to the error collection instead of failing.
    /// </summary>
    private static IEnumerable<string> EnumerateUcFiles(string rootDir, System.Collections.Concurrent.ConcurrentBag<string> errors)
    {
        var stack = new Stack<string>();
        stack.Push(rootDir);

        while (stack.Count > 0)
        {
            string dir = stack.Pop();

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(dir, "*.uc", SearchOption.TopDirectoryOnly);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                errors.Add($"Cannot read directory '{dir}': {ex.Message}");
                continue;
            }

            foreach (string file in files)
                yield return file;

            IEnumerable<string> subDirs;
            try
            {
                subDirs = Directory.EnumerateDirectories(dir, "*", SearchOption.TopDirectoryOnly);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                errors.Add($"Cannot enumerate subdirectories of '{dir}': {ex.Message}");
                continue;
            }

            foreach (string subDir in subDirs)
                stack.Push(subDir);
        }
    }

    /// <summary>
    /// Computes a SHA256 hash of the specified file for cache validation.
    /// </summary>
    private static string ComputeHash(string filePath)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(stream);
        return "sha256:" + BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}

/// <summary>
/// Represents a progress update during an indexing operation.
/// </summary>
public readonly struct IndexingProgress
{
    /// <summary>Gets the total number of files processed so far.</summary>
    public int FilesProcessed { get; }
    
    /// <summary>Gets the path of the file currently being processed.</summary>
    public string CurrentFile { get; }

    public IndexingProgress(int filesProcessed, string currentFile)
    {
        FilesProcessed = filesProcessed;
        CurrentFile = currentFile;
    }
}

/// <summary>
/// Encapsulates the final results and statistics of an indexing run.
/// </summary>
public sealed class IndexingResult
{
    /// <summary>Gets the total number of files successfully scanned.</summary>
    public int FilesScanned { get; }
    
    /// <summary>Gets the total number of individual struct definitions discovered.</summary>
    public int StructsFound { get; }

    /// <summary>Gets the number of files skipped (e.g., due to valid cache entries).</summary>
    public int FilesSkipped { get; }

    /// <summary>Gets a list of non-fatal errors encountered during the scan.</summary>
    public IReadOnlyList<string> Errors { get; }

    public IndexingResult(int filesScanned, int structsFound, int filesSkipped, IReadOnlyList<string> errors)
    {
        FilesScanned = filesScanned;
        StructsFound = structsFound;
        FilesSkipped = filesSkipped;
        Errors = errors;
    }
}
