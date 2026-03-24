namespace XCom2ConfigParser2.Parser;

/// <summary>
/// Represents a terminal leaf node in the property value AST, containing a simple string representation 
/// of a literal value (e.g., a number, boolean, or identifier).
/// </summary>
public sealed class TerminalValue : PropValue
{
    /// <summary>Gets the raw text representation of the value.</summary>
    public string Text { get; }

    /// <inheritdoc />
    public override PropValueType Type => PropValueType.Terminal;

    /// <summary>
    /// Initializes a new instance of the <see cref="TerminalValue"/> class.
    /// </summary>
    /// <param name="text">The raw text value.</param>
    public TerminalValue(string text)
    {
        Text = text;
    }
}
