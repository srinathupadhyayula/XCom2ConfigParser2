namespace XCom2ConfigParser2.Core;

/// <summary>
/// Combines byte span with start/end positions.
/// </summary>
public readonly struct SpanWithLocation
{
    public Span Span { get; }
    public SourceLocation Start { get; }
    public SourceLocation End { get; }

    public SpanWithLocation(Span span, SourceLocation start, SourceLocation end)
    {
        Span = span;
        Start = start;
        End = end;
    }

    public SpanWithLocation(int spanStart, int spanEnd, int startLine, int startCol, int endLine, int endCol)
        : this(new Span(spanStart, spanEnd), new SourceLocation(startLine, startCol), new SourceLocation(endLine, endCol))
    {
    }

    public override string ToString() => $"({Start.Line},{Start.Column}-{End.Line},{End.Column})";
}
