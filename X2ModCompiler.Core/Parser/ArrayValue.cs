namespace X2ModCompiler.Core.Parser;

/// <summary>
/// Represents an array of values in the AST.
/// Examples: <c>(Item1, Item2, Item3)</c> or <c>("String1", 123, true)</c>.
/// </summary>
public sealed class ArrayValue : PropValue
{
    /// <summary>Gets the ordered list of property value elements in the array.</summary>
    public List<PropValue> Elements { get; }

    /// <inheritdoc />
    public override PropValueType Type => PropValueType.Array;

    /// <summary>
    /// Initializes a new, empty instance of the <see cref="ArrayValue"/> class.
    /// </summary>
    public ArrayValue()
    {
        Elements = new List<PropValue>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ArrayValue"/> class with the specified elements.
    /// </summary>
    /// <param name="elements">The list of values to include in the array.</param>
    public ArrayValue(List<PropValue> elements)
    {
        Elements = elements;
    }
}
