using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XCom2ConfigParser2.StructValidation;

/// <summary>
/// Persistent cache for resolved config variable types.
/// Maps (sectionName, propertyName) to a <see cref="VariableTypeResolutionResult"/>.
/// </summary>
public sealed class VariableCache
{
    private readonly record struct CacheKey(string SectionName, string PropertyName);

    public sealed class CachedVariableEntry
    {
        public VariableTypeResolutionResult Result { get; set; } = null!;
        public string SourceHash { get; set; } = string.Empty;
        public DateTime LastIndexed { get; set; }
    }

    private readonly Dictionary<string, Dictionary<string, CachedVariableEntry>> _cache;
    private readonly string _cacheFile;
    private readonly TimeSpan _maxAge = TimeSpan.FromHours(24);
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private bool _isDirty = false;

    public VariableCache(string cacheDir)
    {
        Directory.CreateDirectory(cacheDir);
        _cacheFile = Path.Combine(cacheDir, "variables.json");
        _cache = LoadCache();
    }

    private Dictionary<string, Dictionary<string, CachedVariableEntry>> LoadCache()
    {
        if (!File.Exists(_cacheFile))
            return new(StringComparer.OrdinalIgnoreCase);

        try
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, CachedVariableEntry>>>(File.ReadAllText(_cacheFile));
            if (data == null)
                return new(StringComparer.OrdinalIgnoreCase);

            var dict = new Dictionary<string, Dictionary<string, CachedVariableEntry>>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in data)
            {
                dict[kvp.Key] = new Dictionary<string, CachedVariableEntry>(kvp.Value, StringComparer.OrdinalIgnoreCase);
            }
            return dict;
        }
        catch
        {
            return new(StringComparer.OrdinalIgnoreCase);
        }
    }

    public void Save()
    {
        if (!_isDirty)
            return;

        try
        {
            File.WriteAllText(_cacheFile, JsonSerializer.Serialize(_cache, _jsonOptions));
            _isDirty = false;
        }
        catch { /* Ignore IO errors on cache save */ }
    }

    public void Clear()
    {
        _cache.Clear();
        _isDirty = true;
        Save();
    }

    public bool TryGet(string sectionName, string propertyName, out VariableTypeResolutionResult result)
    {
        if (_cache.TryGetValue(sectionName, out var props) && props.TryGetValue(propertyName, out var entry))
        {
            // Validate age
            if (DateTime.UtcNow - entry.LastIndexed > _maxAge)
            {
                result = null!;
                return false;
            }

            // If it's a found variable, validate source file hash
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

        result = null!;
        return false;
    }

    public void Set(string sectionName, string propertyName, VariableTypeResolutionResult result)
    {
        if (!_cache.TryGetValue(sectionName, out var props))
        {
            props = new Dictionary<string, CachedVariableEntry>(StringComparer.OrdinalIgnoreCase);
            _cache[sectionName] = props;
        }

        string hash = "";
        if (result.Found && !string.IsNullOrEmpty(result.ClassFilePath) && File.Exists(result.ClassFilePath))
        {
            hash = ComputeHash(result.ClassFilePath);
        }

        props[propertyName] = new CachedVariableEntry
        {
            Result = result,
            SourceHash = hash,
            LastIndexed = DateTime.UtcNow
        };

        _isDirty = true;
    }

    public int Count => _cache.Values.Sum(v => v.Count);

    private static string ComputeHash(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(stream);
        return "sha256:" + BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}
