using MemoryPack;

namespace X2ModCompiler.Core.StructValidation;

/// <summary>
/// Represents a structured data transfer object for persisting resolved struct definitions in the <see cref="StructCache"/>.
/// </summary>
[MemoryPackable]
public sealed partial class CachedStructDef
{
    /// <summary>Gets or sets the case-insensitive name of the struct.</summary>
    [MemoryPackOrder(0)]
    public string StructName { get; set; } = "";

    /// <summary>Gets or sets the absolute path to the source .uc file where the struct was discovered.</summary>
    [MemoryPackOrder(1)]
    public string SourceFile { get; set; } = "";

    /// <summary>Gets or sets a cryptographic hash (typically SHA-256) of the source file content at the time of indexing.</summary>
    [MemoryPackOrder(2)]
    public string SourceHash { get; set; } = "";

    /// <summary>Gets or sets the timestamp of the last successful indexing operation.</summary>
    [MemoryPackOrder(3)]
    public DateTime LastIndexed { get; set; }

    /// <summary>Gets or sets the collection of fields defined within the struct.</summary>
    [MemoryPackOrder(4)]
    public List<StructField> Fields { get; set; } = new();

    /// <summary>Gets or sets the list of other struct names referenced by fields within this struct.</summary>
    [MemoryPackOrder(5)]
    public List<string> NestedStructs { get; set; } = new();

    /// <summary>Gets or sets a value indicating whether all nested struct references have been successfully resolved.</summary>
    [MemoryPackOrder(6)]
    public bool ResolvedNestedStructs { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this entry represents a negative search result (struct not found).
    /// </summary>
    [MemoryPackOrder(7)]
    public bool NotFound { get; set; }
}
