namespace XCom2ConfigParser2.Core;

/// <summary>
/// Represents a diagnostic message (error or warning).
/// </summary>
public readonly struct Diagnostic
{
    public ErrorCode Code { get; }
    public string Message { get; }
    public SpanWithLocation Location { get; }
    public string SourceLine { get; }
    public DiagnosticSeverity Severity { get; }

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
