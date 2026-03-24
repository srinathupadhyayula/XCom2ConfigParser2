namespace XCom2ConfigParser2.StructValidation;

/// <summary>
/// Represents a parsed UnrealScript struct definition.
/// </summary>
/// <summary>
/// Represents a fully resolved and immutable definition of an UnrealScript struct.
/// Contains comprehensive metadata about the struct's origin and its constituent fields.
/// </summary>
public sealed class UnrealStructDef
{
    /// <summary>Gets the case-insensitive name of the struct.</summary>
    public string StructName { get; }

    /// <summary>Gets the name of the class where this struct is defined.</summary>
    public string ClassName { get; }

    /// <summary>Gets the name of the package containing the class.</summary>
    public string PackageName { get; }

    /// <summary>Gets the absolute file system path to the source .uc file.</summary>
    public string SourceFilePath { get; }

    /// <summary>Gets the ordered list of fields defined within the struct.</summary>
    public IReadOnlyList<UnrealStructField> Fields { get; }

    /// <summary>Gets a set of all field names within the struct for efficient existence checks.</summary>
    public IReadOnlySet<string> FieldNames { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnrealStructDef"/> class.
    /// </summary>
    public UnrealStructDef(
        string structName,
        string className,
        string packageName,
        string sourceFilePath,
        IReadOnlyList<UnrealStructField> fields)
    {
        StructName = structName;
        ClassName = className;
        PackageName = packageName;
        SourceFilePath = sourceFilePath;
        Fields = fields;
        FieldNames = new HashSet<string>(fields.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);
    }
}
