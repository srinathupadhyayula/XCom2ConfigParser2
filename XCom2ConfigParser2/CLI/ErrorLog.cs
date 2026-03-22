using XCom2ConfigParser2.Core;

namespace XCom2ConfigParser2.CLI;

/// <summary>
/// Represents a single error/warning entry in the log.
/// </summary>
public sealed class ErrorLogEntry
{
    public ErrorCode Code { get; set; }
    public DiagnosticSeverity Severity { get; set; }
    public string FilePath { get; set; } = "";
    public int Line { get; set; }
    public int Column { get; set; }
    public int EndLine { get; set; }
    public int EndColumn { get; set; }
    public string Message { get; set; } = "";
    public string SourceLine { get; set; } = "";

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
/// Holds all errors grouped by category for log file output.
/// </summary>
public sealed class ErrorLog
{
    public string ProjectRoot { get; set; } = "";
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public ValidationResultSummary Summary { get; set; } = new();
    
    // Errors grouped by ErrorCode
    public Dictionary<ErrorCode, List<ErrorLogEntry>> ErrorsByCategory { get; set; } = new();

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

    public int TotalErrors => ErrorsByCategory
        .Where(kvp => kvp.Value.Any(e => e.Severity == DiagnosticSeverity.Error))
        .Sum(kvp => kvp.Value.Count(e => e.Severity == DiagnosticSeverity.Error));

    public int TotalWarnings => ErrorsByCategory
        .Where(kvp => kvp.Value.Any(e => e.Severity == DiagnosticSeverity.Warning))
        .Sum(kvp => kvp.Value.Count(e => e.Severity == DiagnosticSeverity.Warning));
}
