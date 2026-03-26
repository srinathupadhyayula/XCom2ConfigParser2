namespace X2ModCompiler.Core.Core;

/// <summary>
/// Represents a diagnostic message (e.g., error, warning, or informational) produced during the configuration parsing process.
/// Contains the error code, human-readable message, and precise location within the source file.
/// </summary>
public readonly struct Diagnostic
{
    /// <summary>
    /// Gets the unique error code associated with this diagnostic.
    /// </summary>
    public ErrorCode Code { get; }

    /// <summary>
    /// Gets the human-readable description of the diagnostic.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the precise span and location (file, line, column) where the diagnostic was triggered.
    /// </summary>
    public SpanWithLocation Location { get; }

    /// <summary>
    /// Gets the full content of the source line where the diagnostic occurred, useful for IDE reporting and CLI output.
    /// </summary>
    public string SourceLine { get; }

    /// <summary>
    /// Gets the severity level of the diagnostic (Error, Warning, or Info).
    /// </summary>
    public DiagnosticSeverity Severity { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Diagnostic"/> struct.
    /// </summary>
    /// <param name="code">The error code.</param>
    /// <param name="message">The descriptive message.</param>
    /// <param name="location">The location in the source.</param>
    /// <param name="sourceLine">The original source line text.</param>
    /// <param name="severity">The severity of the diagnostic.</param>
    public Diagnostic(
        ErrorCode code,
        string message,
        SpanWithLocation location,
        string sourceLine,
        DiagnosticSeverity severity = DiagnosticSeverity.Error)
    {
        Code = code;
        Message = message;
        Location = location;
        SourceLine = sourceLine;
        Severity = severity;
    }

    /// <summary>
    /// Returns a formatted string representation of the diagnostic suitable for console output.
    /// </summary>
    /// <returns>A string in the format "Location: Code (Severity): Message".</returns>
    public override string ToString()
    {
        var severityStr = Severity switch
        {
            DiagnosticSeverity.Error => "Error",
            DiagnosticSeverity.Warning => "Warning",
            DiagnosticSeverity.Info => "Info",
            _ => "Unknown"
        };
        return $"{Location}: {Code} ({severityStr}): {Message}";
    }
}
