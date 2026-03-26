namespace X2ModCompiler.Core.Parser;

/// <summary>
/// Represents a complex struct value in the AST. 
/// Examples: <c>(Prop1=Value1, Prop2=Value2)</c>.
/// </summary>
public sealed class StructValue : PropValue
{
    /// <summary>Gets the collection of property assignments defined within the struct.</summary>
    public List<PropAssignment> Children { get; }

    /// <inheritdoc />
    public override PropValueType Type => PropValueType.Struct;

    /// <summary>
    /// Initializes a new, empty instance of the <see cref="StructValue"/> class.
    /// </summary>
    public StructValue()
    {
        Children = new List<PropAssignment>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StructValue"/> class with the specified children.
    /// </summary>
    /// <param name="children">The list of property assignments contained in the struct.</param>
    public StructValue(List<PropAssignment> children)
    {
        Children = children;
    }
}
