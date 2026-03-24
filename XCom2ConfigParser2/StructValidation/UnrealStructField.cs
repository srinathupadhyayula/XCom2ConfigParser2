namespace XCom2ConfigParser2.StructValidation;

/// <summary>
/// Represents a field in an UnrealScript struct definition.
/// </summary>
/// <summary>
/// Represents a simple name-type pair for a single field within an <see cref="UnrealStructDef"/>.
/// </summary>
public readonly struct UnrealStructField
{
    /// <summary>Gets the name of the field.</summary>
    public string Name { get; }

    /// <summary>Gets the full type name of the field.</summary>
    public string TypeName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnrealStructField"/> struct.
    /// </summary>
    /// <param name="name">The name of the field.</param>
    /// <param name="typeName">The full type name of the field.</param>
    public UnrealStructField(string name, string typeName)
    {
        Name = name;
        TypeName = typeName;
    }
}
