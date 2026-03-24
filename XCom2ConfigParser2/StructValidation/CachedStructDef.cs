namespace XCom2ConfigParser2.StructValidation;

/// <summary>
/// Cached struct definition for persistent storage.
/// When <see cref="NotFound"/> is true, this is a negative-cache sentinel indicating
/// the struct was searched for and not found. All other fields will be default.
/// </summary>
/// <summary>
/// Represents a structured data transfer object for persisting resolved struct definitions in the <see cref="StructCache"/>.
/// </summary>
public sealed class CachedStructDef
{
    /// <summary>Gets or sets the case-insensitive name of the struct.</summary>
    public string StructName { get; set; } = "";

    /// <summary>Gets or sets the absolute path to the source .uc file where the struct was discovered.</summary>
    public string SourceFile { get; set; } = "";

    /// <summary>Gets or sets a cryptographic hash (typically SHA-256) of the source file content at the time of indexing.</summary>
    public string SourceHash { get; set; } = "";

    /// <summary>Gets or sets the timestamp of the last successful indexing operation.</summary>
    public DateTime LastIndexed { get; set; }

    /// <summary>Gets or sets the collection of fields defined within the struct.</summary>
    public List<StructField> Fields { get; set; } = new();

    /// <summary>Gets or sets the list of other struct names referenced by fields within this struct.</summary>
    public List<string> NestedStructs { get; set; } = new();

    /// <summary>Gets or sets a value indicating whether all nested struct references have been successfully resolved.</summary>
    public bool ResolvedNestedStructs { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this entry represents a negative search result (struct not found).
    /// </summary>
    public bool NotFound { get; set; }
}
