namespace XCom2ConfigParser2.Core;

/// <summary>
/// Represents lines that don't match any known pattern.
/// </summary>
public readonly struct Unknown
{
    public Span Span { get; }
    public Span? PreviousSpan { get; } // Previous line (for continuation errors)

    public Unknown(Span span, Span? previousSpan = null)
    {
        Span = span;
        PreviousSpan = previousSpan;
    }
}
