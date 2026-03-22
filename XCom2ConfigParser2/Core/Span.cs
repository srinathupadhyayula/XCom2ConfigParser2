namespace XCom2ConfigParser2.Core;

/// <summary>
/// Represents a byte range in the source text.
/// </summary>
public readonly struct Span
{
    public int Start { get; }      // Byte offset of first character
    public int End { get; }        // Byte offset after last character
    public int Length => End - Start;

    public Span(int start, int end)
    {
        Start = start;
        End = end;
    }

    public string Extract(string source) => source.Substring(Start, Length);
}
