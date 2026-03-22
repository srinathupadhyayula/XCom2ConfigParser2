namespace XCom2ConfigParser2.Parser;

/// <summary>
/// Represents a terminal value (string, number, boolean, identifier).
/// </summary>
public sealed class TerminalValue : PropValue
{
    public string Text { get; }
    public override PropValueType Type => PropValueType.Terminal;

    public TerminalValue(string text)
    {
        Text = text;
    }
}
