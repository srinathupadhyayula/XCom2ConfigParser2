namespace XCom2ConfigParser2.Core;

/// <summary>
/// Represents a line in the source text with continuation information.
/// </summary>
public readonly struct LineSpan
{
    public int Start { get; }
    public int End { get; }
    public bool IsContinuation { get; }  // True if continues from previous
    public bool HasContinuation { get; } // True if continues to next
    public int LineNumber { get; }       // 1-based line number (original, before merging)

    public LineSpan(int start, int end, bool isContinuation, bool hasContinuation, int lineNumber)
    {
        Start = start;
        End = end;
        IsContinuation = isContinuation;
        HasContinuation = hasContinuation;
        LineNumber = lineNumber;
    }

    public string Extract(string text) => text.Substring(Start, End - Start);
}
