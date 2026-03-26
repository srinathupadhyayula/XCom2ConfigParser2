using System.Security.Cryptography;
using MemoryPack;

namespace X2ModCompiler.Core.StructValidation;

/// <summary>
/// Persistent cache for resolved struct definitions.
/// Supports both positive (found) and negative (not found) entries.
/// Negative entries are cleared at startup to prevent stale false-negatives
/// from persisting after logic fixes.
/// </summary>
/// <summary>
/// Manages a persistent disk-based cache for resolved UnrealScript struct definitions.
/// Optimizes repeated validation runs by storing parsed metadata and tracking source file integrity via hashing.
/// </summary>
/// <remarks>
/// The cache supports both positive entries (found structs) and negative entries (explicitly missing structs) 
/// to prevent redundant exhaustive searches across the entire source hierarchy.
/// </remarks>
public sealed class StructCache
{
    private readonly string _cacheDir;
    private readonly TimeSpan _maxAge = TimeSpan.FromHours(24);

    /// <summary>
    /// Initializes a new instance of the <see cref="StructCache"/> class.
    /// </summary>
    /// <param name="cacheRootDir">The root directory where the cache should be stored.</param>
    public StructCache(string cacheRootDir)
    {
        _cacheDir = Path.Combine(cacheRootDir, "structsmap");
        Directory.CreateDirectory(_cacheDir);
    }

    /// <summary>
    /// Attempts to retrieve a cached struct definition by name.
    /// Validates that the cache entry has not expired and that the original source file has not changed.
    /// </summary>
    /// <param name="structName">The case-insensitive name of the struct.</param>
    /// <param name="cached">When this method returns, contains the cached definition if successful; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if a valid cache entry was found; otherwise, <c>false</c>.</returns>
    public bool TryGet(string structName, out CachedStructDef? cached)
    {
        string cacheFile = GetCachePath(structName);
        if (!File.Exists(cacheFile))
        {
            cached = null;
            return false;
        }

        try
        {
            var bytes = File.ReadAllBytes(cacheFile);
            var entry = MemoryPackSerializer.Deserialize<CachedStructDef>(bytes);
            if (entry == null || entry.NotFound)
            {
                // Not a positive result
                cached = null;
                return false;
            }

            // Validate cache age
            if (DateTime.UtcNow - entry.LastIndexed > _maxAge)
            {
                cached = null;
                return false;
            }

            // Validate source file still exists
            if (!File.Exists(entry.SourceFile))
            {
                cached = null;
                return false;
            }

            // Validate source file hash hasn't changed
            string currentHash = ComputeHash(entry.SourceFile);
            if (currentHash != entry.SourceHash)
            {
                cached = null;
                return false;
            }

            cached = entry;
            return true;
        }
        catch (MemoryPackSerializationException)
        {
            cached = null;
            return false;
        }
    }

    /// <summary>
    /// Determines if a negative cache entry exists for the specified struct name.
    /// </summary>
    /// <param name="structName">The name of the struct to check.</param>
    /// <returns><c>true</c> if the struct was previously confirmed as not found; otherwise, <c>false</c>.</returns>
    public bool IsKnownNotFound(string structName)
    {
        string cacheFile = GetCachePath(structName);
        if (!File.Exists(cacheFile))
            return false;

        try
        {
            var bytes = File.ReadAllBytes(cacheFile);
            var entry = MemoryPackSerializer.Deserialize<CachedStructDef>(bytes);
            return entry?.NotFound == true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Persists a successfully resolved struct definition to the cache.
    /// </summary>
    /// <param name="def">The struct definition to cache.</param>
    public void Save(CachedStructDef def)
    {
        string cacheFile = GetCachePath(def.StructName);
        var bytes = MemoryPackSerializer.Serialize(def);
        File.WriteAllBytes(cacheFile, bytes);
    }

    /// <summary>
    /// Records a negative cache entry for a struct that could not be located.
    /// </summary>
    /// <param name="structName">The name of the missing struct.</param>
    public void SaveNotFound(string structName)
    {
        var sentinel = new CachedStructDef
        {
            StructName = structName,
            LastIndexed = DateTime.UtcNow,
            NotFound = true,
        };
        string cacheFile = GetCachePath(structName);
        var bytes = MemoryPackSerializer.Serialize(sentinel);
        File.WriteAllBytes(cacheFile, bytes);
    }

    /// <summary>
    /// Purges all negative cache (not found) entries.
    /// Typically called at startup to allow retrying failed lookups after codebase updates or logic fixes.
    /// </summary>
    public void ClearNegativeEntries()
    {
        if (!Directory.Exists(_cacheDir))
            return;

        foreach (var file in Directory.EnumerateFiles(_cacheDir, "*.mempack"))
        {
            try
            {
                var bytes = File.ReadAllBytes(file);
                var entry = MemoryPackSerializer.Deserialize<CachedStructDef>(bytes);
                if (entry?.NotFound == true)
                    File.Delete(file);
            }
            catch
            {
                // Ignore read/delete errors for individual entries
            }
        }
    }

    /// <summary>
    /// Completely clears the struct cache.
    /// </summary>
    public void Clear()
    {
        if (!Directory.Exists(_cacheDir))
            return;

        foreach (var file in Directory.EnumerateFiles(_cacheDir, "*.mempack"))
        {
            try { File.Delete(file); }
            catch { /* Ignore */ }
        }
    }

    /// <summary>
    /// Gets the file system path for a cache entry based on the struct name.
    /// </summary>
    public string GetCachePath(string structName) =>
        Path.Combine(_cacheDir, $"{structName}.mempack");

    private static string ComputeHash(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(stream);
        return "sha256:" + BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}
