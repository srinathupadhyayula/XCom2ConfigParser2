using System.Security.Cryptography;
using System.Text.Json;

namespace XCom2ConfigParser2.StructValidation;

/// <summary>
/// Persistent cache for resolved struct definitions.
/// Supports both positive (found) and negative (not found) entries.
/// Negative entries are cleared at startup to prevent stale false-negatives
/// from persisting after logic fixes.
/// </summary>
public sealed class StructCache
{
    private readonly string _cacheDir;
    private readonly TimeSpan _maxAge = TimeSpan.FromHours(24);

    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public StructCache(string cacheDir)
    {
        _cacheDir = cacheDir;
        Directory.CreateDirectory(cacheDir);
    }

    /// <summary>
    /// Tries to get a cached struct definition.
    /// Returns false if cache is missing, expired, or source file has changed.
    /// Will NOT return negative-cache entries — use <see cref="IsKnownNotFound"/> for those.
    /// </summary>
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
            var entry = JsonSerializer.Deserialize<CachedStructDef>(File.ReadAllText(cacheFile));
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
        catch (JsonException)
        {
            cached = null;
            return false;
        }
    }

    /// <summary>
    /// Returns true if a negative cache entry exists for this struct name,
    /// meaning we have already searched for it and confirmed it cannot be found.
    /// </summary>
    public bool IsKnownNotFound(string structName)
    {
        string cacheFile = GetCachePath(structName);
        if (!File.Exists(cacheFile))
            return false;

        try
        {
            var entry = JsonSerializer.Deserialize<CachedStructDef>(File.ReadAllText(cacheFile));
            return entry?.NotFound == true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Saves a successfully resolved struct definition to disk.
    /// </summary>
    public void Save(CachedStructDef def)
    {
        string cacheFile = GetCachePath(def.StructName);
        File.WriteAllText(cacheFile, JsonSerializer.Serialize(def, _jsonOptions));
    }

    /// <summary>
    /// Saves a negative cache sentinel for a struct that could not be found.
    /// This prevents repeated exhaustive searches for the same struct name.
    /// </summary>
    public void SaveNotFound(string structName)
    {
        var sentinel = new CachedStructDef
        {
            StructName = structName,
            LastIndexed = DateTime.UtcNow,
            NotFound = true,
        };
        string cacheFile = GetCachePath(structName);
        File.WriteAllText(cacheFile, JsonSerializer.Serialize(sentinel, _jsonOptions));
    }

    /// <summary>
    /// Removes all negative cache entries from disk.
    /// Should be called at startup so that stale "not found" entries from
    /// previous (possibly buggy) runs do not affect the current session.
    /// </summary>
    public void ClearNegativeEntries()
    {
        if (!Directory.Exists(_cacheDir))
            return;

        foreach (var file in Directory.EnumerateFiles(_cacheDir, "*.json"))
        {
            try
            {
                var entry = JsonSerializer.Deserialize<CachedStructDef>(File.ReadAllText(file));
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
    /// Clears ALL cached struct definitions (positive and negative).
    /// Called when --force-reindex is specified.
    /// </summary>
    public void Clear()
    {
        if (!Directory.Exists(_cacheDir))
            return;

        foreach (var file in Directory.EnumerateFiles(_cacheDir, "*.json"))
        {
            try { File.Delete(file); }
            catch { /* Ignore */ }
        }
    }

    public string GetCachePath(string structName) =>
        Path.Combine(_cacheDir, $"{structName}.json");

    private static string ComputeHash(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(stream);
        return "sha256:" + BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}
