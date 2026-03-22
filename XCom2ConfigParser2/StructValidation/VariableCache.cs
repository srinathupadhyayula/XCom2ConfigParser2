namespace XCom2ConfigParser2.StructValidation;

/// <summary>
/// Session-scoped in-memory cache for resolved config variable types.
/// Maps (sectionName, propertyName) to a <see cref="VariableTypeResolutionResult"/>.
///
/// This cache exists only for the duration of one CLI invocation.
/// Its purpose: when multiple config files define values for the same
/// [Package.Class] property, the variable type lookup from the .uc file
/// only needs to happen once. Subsequent lookups hit this cache.
/// </summary>
public sealed class VariableCache
{
    private readonly record struct CacheKey(string SectionName, string PropertyName);

    private readonly Dictionary<CacheKey, VariableTypeResolutionResult> _cache = new();

    /// <summary>
    /// Tries to retrieve a previously resolved variable type.
    /// </summary>
    public bool TryGet(string sectionName, string propertyName, out VariableTypeResolutionResult result)
    {
        var key = new CacheKey(
            sectionName.ToLowerInvariant(),
            propertyName.ToLowerInvariant());

        return _cache.TryGetValue(key, out result!);
    }

    /// <summary>
    /// Stores a variable type resolution result (positive or negative).
    /// </summary>
    public void Set(string sectionName, string propertyName, VariableTypeResolutionResult result)
    {
        var key = new CacheKey(
            sectionName.ToLowerInvariant(),
            propertyName.ToLowerInvariant());

        _cache[key] = result;
    }

    /// <summary>
    /// Returns the number of cached entries (for diagnostics/logging).
    /// </summary>
    public int Count => _cache.Count;
}
