namespace XCom2ConfigParser2.StructValidation;

/// <summary>
/// Represents a parsed UnrealScript struct definition.
/// </summary>
public sealed class UnrealStructDef
{
    public string StructName { get; }
    public string ClassName { get; }
    public string PackageName { get; }
    public string SourceFilePath { get; }
    public IReadOnlyList<UnrealStructField> Fields { get; }
    public IReadOnlySet<string> FieldNames { get; }

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
