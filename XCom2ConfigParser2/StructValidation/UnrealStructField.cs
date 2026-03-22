namespace XCom2ConfigParser2.StructValidation;

/// <summary>
/// Represents a field in an UnrealScript struct definition.
/// </summary>
public readonly struct UnrealStructField
{
    public string Name { get; }
    public string TypeName { get; }

    public UnrealStructField(string name, string typeName)
    {
        Name = name;
        TypeName = typeName;
    }
}
