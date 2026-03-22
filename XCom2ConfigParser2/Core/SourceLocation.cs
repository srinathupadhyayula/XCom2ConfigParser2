namespace XCom2ConfigParser2.Core;

/// <summary>
/// Human-readable position (line/column).
/// </summary>
public readonly struct SourceLocation
{
    public int Line { get; }       // 1-based line number
    public int Column { get; }     // 1-based column number

    public SourceLocation(int line, int column)
    {
        Line = line;
        Column = column;
    }

    public override string ToString() => $"({Line},{Column})";
}
