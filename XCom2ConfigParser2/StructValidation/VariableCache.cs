using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XCom2ConfigParser2.StructValidation;

/// <summary>
/// Persistent cache for resolved config variable types.
/// Maps (sectionName, propertyName) to a <see cref="VariableTypeResolutionResult"/>.
/// One file per variable in 'variablesmap' subdirectory.
/// </summary>
/// <summary>
/// Manages a persistent disk-based cache for resolved configuration variable types.
/// Maps section-property pairs to their identified type metadata, optimizing verification passes.
/// </summary>
/// <remarks>
/// Like <see cref="StructCache"/>, this cache implements content-based integrity checks via SHA-256 hashing 
/// and supports negative-cache entries to avoid repeatedly searching for invalid or missing properties.
/// </remarks>
public sealed class VariableCache
{
    private readonly string _cacheDir;
    private readonly TimeSpan _maxAge = TimeSpan.FromHours(24);
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// Represents a single entry in the variable cache.
    /// </summary>
    public sealed class CachedVariableEntry
    {
        /// <summary>Gets or sets the configuration section name.</summary>
        public string SectionName { get; set; } = string.Empty;

        /// <summary>Gets or sets the property name.</summary>
        public string PropertyName { get; set; } = string.Empty;

        /// <summary>Gets or sets the result of the type resolution operation.</summary>
        public VariableTypeResolutionResult Result { get; set; } = null!;

        /// <summary>Gets or sets the hash of the source file containing the variable declaration.</summary>
        public string SourceHash { get; set; } = string.Empty;

        /// <summary>Gets or sets the indexing timestamp.</summary>
        public DateTime LastIndexed { get; set; }

        /// <summary>Gets or sets a value indicating whether the variable could not be resolved.</summary>
        public bool NotFound { get; set; }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VariableCache"/> class.
    /// </summary>
    /// <param name="cacheRootDir">The root directory for cache storage.</param>
    public VariableCache(string cacheRootDir)
    {
        _cacheDir = Path.Combine(cacheRootDir, "variablesmap");
        Directory.CreateDirectory(_cacheDir);
    }

    /// <summary>
    /// Finalizes and saves any pending cache changes. (Currently a no-op as changes are flushed immediately).
    /// </summary>
    public void Save()
    {
        // No-op for directory-based cache, files are saved immediately in Set()
    }

    /// <summary>
    /// Purges the entire variable cache.
    /// </summary>
    public void Clear()
    {
        if (!Directory.Exists(_cacheDir)) return;
        foreach (var file in Directory.EnumerateFiles(_cacheDir, "*.json"))
        {
            try { File.Delete(file); } catch { }
        }
    }

    /// <summary>
    /// Purges all negative cache (not found) entries.
    /// </summary>
    public void ClearNegativeEntries()
    {
        if (!Directory.Exists(_cacheDir)) return;
        foreach (var file in Directory.EnumerateFiles(_cacheDir, "*.json"))
        {
            try
            {
                var entry = JsonSerializer.Deserialize<CachedVariableEntry>(File.ReadAllText(file));
                if (entry?.NotFound == true)
                {
                    File.Delete(file);
                }
            }
            catch { }
        }
    }

    /// <summary>
    /// Attempts to retrieve a cached variable resolution result.
    /// </summary>
    /// <param name="sectionName">The name of the configuration section.</param>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="result">When this method returns, contains the resolution result if successful; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if a valid cache entry was found; otherwise, <c>false</c>.</returns>
    public bool TryGet(string sectionName, string propertyName, out VariableTypeResolutionResult result)
    {
        string cacheFile = GetCachePath(sectionName, propertyName);
        if (!File.Exists(cacheFile))
        {
            result = null!;
            return false;
        }

        try
        {
            var entry = JsonSerializer.Deserialize<CachedVariableEntry>(File.ReadAllText(cacheFile));
            if (entry == null || entry.NotFound)
            {
                result = null!;
                return false;
            }

            // Validate age
            if (DateTime.UtcNow - entry.LastIndexed > _maxAge)
            {
                result = null!;
                return false;
            }

            // Validate source file if found
            if (entry.Result.Found && !string.IsNullOrEmpty(entry.Result.ClassFilePath))
            {
                if (!File.Exists(entry.Result.ClassFilePath))
                {
                    result = null!;
                    return false;
                }

                if (ComputeHash(entry.Result.ClassFilePath) != entry.SourceHash)
                {
                    result = null!;
                    return false;
                }
            }

            result = entry.Result;
            return true;
        }
        catch
        {
            result = null!;
            return false;
        }
    }

    /// <summary>
    /// Persists a variable resolution result to the cache.
    /// </summary>
    /// <param name="sectionName">The configuration section name.</param>
    /// <param name="propertyName">The property name.</param>
    /// <param name="result">The resolution result to cache.</param>
    public void Set(string sectionName, string propertyName, VariableTypeResolutionResult result)
    {
        string hash = "";
        if (result.Found && !string.IsNullOrEmpty(result.ClassFilePath) && File.Exists(result.ClassFilePath))
        {
            hash = ComputeHash(result.ClassFilePath);
        }

        var entry = new CachedVariableEntry
        {
            SectionName = sectionName,
            PropertyName = propertyName,
            Result = result,
            SourceHash = hash,
            LastIndexed = DateTime.UtcNow,
            NotFound = !result.Found
        };

        string cacheFile = GetCachePath(sectionName, propertyName);
        File.WriteAllText(cacheFile, JsonSerializer.Serialize(entry, _jsonOptions));
    }

    /// <summary> Gets the total number of entries in the variable cache. </summary>
    public int Count => Directory.Exists(_cacheDir) ? Directory.GetFiles(_cacheDir, "*.json").Length : 0;

    private string GetCachePath(string sectionName, string propertyName)
    {
        string safeName = $"{sectionName}_{propertyName}";
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            safeName = safeName.Replace(c, '_');
        }
        
        // Handle potentially very long paths by hashing if too long
        if (safeName.Length > 150)
        {
            using var sha1 = SHA1.Create();
            byte[] hash = sha1.ComputeHash(System.Text.Encoding.UTF8.GetBytes(safeName));
            safeName = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        return Path.Combine(_cacheDir, safeName + ".json");
    }

    private static string ComputeHash(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(stream);
        return "sha256:" + BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}
