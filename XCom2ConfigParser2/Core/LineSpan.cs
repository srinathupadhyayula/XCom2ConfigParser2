namespace XCom2ConfigParser2.Core;

/// <summary>
/// Represents a single line in the source text, including metadata for multiline continuation and original positioning.
/// </summary>
public readonly struct LineSpan
{
    /// <summary>Gets the zero-based byte offset of the start of the line.</summary>
    public int Start { get; }

    /// <summary>Gets the zero-based byte offset of the end of the line.</summary>
    public int End { get; }

    /// <summary>Gets a value indicating whether this line is a continuation of the previous line (via the <c>\</c> marker).</summary>
    public bool IsContinuation { get; }

    /// <summary>Gets a value indicating whether this line continues to the next line (ends with a <c>\</c> marker).</summary>
    public bool HasContinuation { get; }

    /// <summary>Gets the original 1-based line number in the source file before any line merging occurred.</summary>
    public int LineNumber { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LineSpan"/> struct.
    /// </summary>
    public LineSpan(int start, int end, bool isContinuation, bool hasContinuation, int lineNumber)
    {
        Start = start;
        End = end;
        IsContinuation = isContinuation;
        HasContinuation = hasContinuation;
        LineNumber = lineNumber;
    }

    /// <summary> Extracts the raw text of the line from the provided source. </summary>
    public string Extract(string text) => text.Substring(Start, End - Start);
}
