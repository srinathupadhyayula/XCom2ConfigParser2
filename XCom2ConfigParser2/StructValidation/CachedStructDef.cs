namespace XCom2ConfigParser2.StructValidation;

/// <summary>
/// Cached struct definition for persistent storage.
/// When <see cref="NotFound"/> is true, this is a negative-cache sentinel indicating
/// the struct was searched for and not found. All other fields will be default.
/// </summary>
public sealed class CachedStructDef
{
    public string StructName { get; set; } = "";
    public string SourceFile { get; set; } = "";
    public string SourceHash { get; set; } = "";
    public DateTime LastIndexed { get; set; }
    public List<StructField> Fields { get; set; } = new();
    public List<string> NestedStructs { get; set; } = new();
    public bool ResolvedNestedStructs { get; set; }
    /// <summary>
    /// When true, this entry is a negative cache marker (struct was not found).
    /// </summary>
    public bool NotFound { get; set; }
}
