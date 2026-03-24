using XCom2ConfigParser2.Core;

namespace XCom2ConfigParser2.CLI;

/// <summary>
/// Represents a standardized, serializable log entry for a single validation diagnostic.
/// </summary>
public sealed class ErrorLogEntry
{
    /// <summary>Gets or sets the error code identifying the type of diagnostic.</summary>
    public ErrorCode Code { get; set; }

    /// <summary>Gets or sets the severity of the diagnostic.</summary>
    public DiagnosticSeverity Severity { get; set; }

    /// <summary>Gets or sets the absolute path to the file containing the error.</summary>
    public string FilePath { get; set; } = "";

    /// <summary>Gets or sets the 1-based start line number.</summary>
    public int Line { get; set; }

    /// <summary>Gets or sets the 1-based start column number.</summary>
    public int Column { get; set; }

    /// <summary>Gets or sets the 1-based end line number.</summary>
    public int EndLine { get; set; }

    /// <summary>Gets or sets the 1-based end column number.</summary>
    public int EndColumn { get; set; }

    /// <summary>Gets or sets the descriptive error message, sanitized of newlines.</summary>
    public string Message { get; set; } = "";

    /// <summary>Gets or sets the original line of source code that triggered the diagnostic.</summary>
    public string SourceLine { get; set; } = "";

    /// <summary>
    /// Converts a core <see cref="Diagnostic"/> into an <see cref="ErrorLogEntry"/>.
    /// </summary>
    /// <param name="diagnostic">The source diagnostic.</param>
    /// <param name="filePath">The file path to associate with the entry.</param>
    /// <returns>A new <see cref="ErrorLogEntry"/> instance.</returns>
    public static ErrorLogEntry FromDiagnostic(Diagnostic diagnostic, string filePath)
    {
        return new ErrorLogEntry
        {
            Code = diagnostic.Code,
            Severity = diagnostic.Severity,
            FilePath = filePath,
            Line = diagnostic.Location.Start.Line,
            Column = diagnostic.Location.Start.Column,
            EndLine = diagnostic.Location.End.Line,
            EndColumn = diagnostic.Location.End.Column,
            Message = diagnostic.Message.Replace("\r\n", " ").Replace("\n", " "),
            SourceLine = diagnostic.SourceLine
        };
    }
}

/// <summary>
/// Orchestrates the collection of validation results across multiple files, grouped by category for comprehensive reporting.
/// </summary>
/// <remarks>
/// This model is designed for persistent storage and can be serialized to both human-readable text (via <see cref="ErrorLogWriter"/>)
/// and machine-readable JSON.
/// </remarks>
public sealed class ErrorLog
{
    /// <summary>Gets or sets the root directory of the project being validated.</summary>
    public string ProjectRoot { get; set; } = "";

    /// <summary>Gets or sets the timestamp when the report was generated.</summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets the session summary data.</summary>
    public ValidationResultSummary Summary { get; set; } = new();

    /// <summary>Gets or sets the collection of error entries, indexed by their <see cref="ErrorCode"/> category.</summary>
    public Dictionary<ErrorCode, List<ErrorLogEntry>> ErrorsByCategory { get; set; } = new();

    /// <summary>
    /// Adds a new diagnostic to the log, automatically categorizing it by its error code.
    /// </summary>
    /// <param name="diagnostic">The identified diagnostic.</param>
    /// <param name="filePath">The file where the diagnostic originated.</param>
    public void Add(Diagnostic diagnostic, string filePath)
    {
        var entry = ErrorLogEntry.FromDiagnostic(diagnostic, filePath);
        
        if (!ErrorsByCategory.TryGetValue(diagnostic.Code, out var list))
        {
            list = new List<ErrorLogEntry>();
            ErrorsByCategory[diagnostic.Code] = list;
        }
        
        list.Add(entry);
    }

    /// <summary>Gets the aggregate count of all error-level entries across all categories.</summary>
    public int TotalErrors => ErrorsByCategory
        .Where(kvp => kvp.Value.Any(e => e.Severity == DiagnosticSeverity.Error))
        .Sum(kvp => kvp.Value.Count(e => e.Severity == DiagnosticSeverity.Error));

    /// <summary>Gets the aggregate count of all warning-level entries across all categories.</summary>
    public int TotalWarnings => ErrorsByCategory
        .Where(kvp => kvp.Value.Any(e => e.Severity == DiagnosticSeverity.Warning))
        .Sum(kvp => kvp.Value.Count(e => e.Severity == DiagnosticSeverity.Warning));
}
