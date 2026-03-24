namespace XCom2ConfigParser2.Core;

/// <summary>
/// Correlates a raw byte <see cref="Span"/> with human-readable <see cref="SourceLocation"/> positions.
/// This type is used primarily for detailed error reporting and IDE integration.
/// </summary>
public readonly struct SpanWithLocation
{
    /// <summary>Gets the underlying byte span.</summary>
    public Span Span { get; }

    /// <summary>Gets the human-readable start location (line/column).</summary>
    public SourceLocation Start { get; }

    /// <summary>Gets the human-readable end location (line/column).</summary>
    public SourceLocation End { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SpanWithLocation"/> struct.
    /// </summary>
    public SpanWithLocation(Span span, SourceLocation start, SourceLocation end)
    {
        Span = span;
        Start = start;
        End = end;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SpanWithLocation"/> struct using raw coordinates.
    /// </summary>
    public SpanWithLocation(int spanStart, int spanEnd, int startLine, int startCol, int endLine, int endCol)
        : this(new Span(spanStart, spanEnd), new SourceLocation(startLine, startCol), new SourceLocation(endLine, endCol))
    {
    }

    /// <summary> Returns a string representation of the location range (e.g., "(1,1-1,10)"). </summary>
    public override string ToString() => $"({Start.Line},{Start.Column}-{End.Line},{End.Column})";
}
