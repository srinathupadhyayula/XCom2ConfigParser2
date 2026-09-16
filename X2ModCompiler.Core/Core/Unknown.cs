namespace X2ModCompiler.Core.Core;

/// <summary>
/// Represents a line or directive in the configuration file that does not match any recognized pattern
/// (e.g., malformed section headers or stray text).
/// </summary>
public readonly struct Unknown
{
    /// <summary>Gets the byte span of the unrecognized content.</summary>
    public Span Span { get; }

    /// <summary>Gets the byte span of the previous line, used for diagnostics related to potentially malformed continuations.</summary>
    public Span? PreviousSpan { get; }

    /// <summary>Gets the error code for this unknown directive, if applicable.</summary>
    public ErrorCode? ErrorCode { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Unknown"/> struct.
    /// </summary>
    public Unknown(Span span, Span? previousSpan = null, ErrorCode? errorCode = null)
    {
        Span = span;
        PreviousSpan = previousSpan;
        ErrorCode = errorCode;
    }
}
