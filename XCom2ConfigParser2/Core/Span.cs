namespace XCom2ConfigParser2.Core;

/// <summary>
/// Represents a contiguous range of characters within the source text using absolute byte offsets.
/// </summary>
public readonly struct Span
{
    /// <summary>Gets the zero-based byte offset of the first character in the span.</summary>
    public int Start { get; }

    /// <summary>Gets the zero-based byte offset immediately following the last character in the span.</summary>
    public int End { get; }

    /// <summary>Gets the total number of characters in the span.</summary>
    public int Length => End - Start;

    /// <summary>
    /// Initializes a new instance of the <see cref="Span"/> struct.
    /// </summary>
    public Span(int start, int end)
    {
        Start = start;
        End = end;
    }

    /// <summary> Extracts the substring defined by this span from the provided source text. </summary>
    public string Extract(string source) => source.Substring(Start, Length);
}
