using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XCom2ConfigParser2.StructValidation;

/// <summary>
/// Persistent cache for resolved config variable types.
/// Maps (sectionName, propertyName) to a <see cref="VariableTypeResolutionResult"/>.
/// One file per variable in 'variablesmap' subdirectory.
/// </summary>
public sealed class VariableCache
{
    private readonly string _cacheDir;
    private readonly TimeSpan _maxAge = TimeSpan.FromHours(24);
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public sealed class CachedVariableEntry
    {
        public string SectionName { get; set; } = string.Empty;
        public string PropertyName { get; set; } = string.Empty;
        public VariableTypeResolutionResult Result { get; set; } = null!;
        public string SourceHash { get; set; } = string.Empty;
        public DateTime LastIndexed { get; set; }
        public bool NotFound { get; set; }
    }

    public VariableCache(string cacheRootDir)
    {
        _cacheDir = Path.Combine(cacheRootDir, "variablesmap");
        Directory.CreateDirectory(_cacheDir);
    }

    public void Save()
    {
        // No-op for directory-based cache, files are saved immediately in Set()
    }

    public void Clear()
    {
        if (!Directory.Exists(_cacheDir)) return;
        foreach (var file in Directory.EnumerateFiles(_cacheDir, "*.json"))
        {
            try { File.Delete(file); } catch { }
        }
    }

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
