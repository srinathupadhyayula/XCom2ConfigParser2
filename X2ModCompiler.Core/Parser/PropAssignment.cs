namespace X2ModCompiler.Core.Parser;

/// <summary>
/// Represents a single property assignment found within a struct value or a top-level directive.
/// Supports both plain assignments (<c>Name=Value</c>) and indexed array elements (<c>Name[0]=Value</c>).
/// </summary>
public readonly struct PropAssignment
{
    /// <summary>Gets the property name.</summary>
    public string Name { get; }

    /// <summary>Gets the optional array index if using the explicit <c>Prop[Index]</c> syntax.</summary>
    public string? Index { get; }

    /// <summary>Gets the associated property value node.</summary>
    public PropValue Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PropAssignment"/> struct.
    /// </summary>
    public PropAssignment(string name, string? index, PropValue value)
    {
        Name = name;
        Index = index;
        Value = value;
    }
}
