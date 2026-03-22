using Spectre.Console;
using System.Text.Json;
using System.Text.Json.Serialization;
using XCom2ConfigParser2.Core;

namespace XCom2ConfigParser2.CLI;

/// <summary>
/// Output modes for the CLI.
/// </summary>
public enum OutputMode
{
    Default,
    Json,
    Quiet,
    Summary
}

/// <summary>
/// Summary of validation results.
/// </summary>
public sealed class ValidationResultSummary
{
    public int FilesProcessed { get; set; }
    public int FilesWithErrors { get; set; }
    public int TotalErrors { get; set; }
    public int TotalWarnings { get; set; }
}

/// <summary>
/// JSON output schema for file results.
/// </summary>
public sealed class JsonFileResult
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("errors")]
    public List<JsonErrorResult> Errors { get; set; } = new();
}

/// <summary>
/// JSON output schema for individual errors.
/// </summary>
public sealed class JsonErrorResult
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("line")]
    public int Line { get; set; }

    [JsonPropertyName("column")]
    public int Column { get; set; }

    [JsonPropertyName("endLine")]
    public int EndLine { get; set; }

    [JsonPropertyName("endColumn")]
    public int EndColumn { get; set; }

    [JsonPropertyName("sourceLine")]
    public string SourceLine { get; set; } = "";

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "Error";
}

/// <summary>
/// JSON output schema for the complete result.
/// </summary>
public sealed class JsonOutput
{
    [JsonPropertyName("files")]
    public List<JsonFileResult> Files { get; set; } = new();

    [JsonPropertyName("summary")]
    public ValidationResultSummary Summary { get; set; } = new();
}

/// <summary>
/// Formats validation results for output.
/// </summary>
public static class OutputFormatter
{
    /// <summary>
    /// Formats a single file's diagnostics for output.
    /// </summary>
    public static void Format(
        string filePath,
        IReadOnlyList<Diagnostic> diagnostics,
        OutputMode mode,
        IAnsiConsole console)
    {
        switch (mode)
        {
            case OutputMode.Default:
            case OutputMode.Quiet:
                FormatConsole(filePath, diagnostics, mode == OutputMode.Quiet, console);
                break;

            case OutputMode.Json:
                // JSON is formatted at a higher level
                break;

            case OutputMode.Summary:
                // Summary is formatted at a higher level
                break;
        }
    }

    private static void FormatConsole(
        string filePath,
        IReadOnlyList<Diagnostic> diagnostics,
        bool quiet,
        IAnsiConsole console)
    {
        foreach (var diagnostic in diagnostics)
        {
            string severityStr = diagnostic.Severity switch
            {
                DiagnosticSeverity.Error => "Error",
                DiagnosticSeverity.Warning => "Warning",
                DiagnosticSeverity.Info => "Info",
                _ => "Unknown"
            };

            string color = diagnostic.Severity == DiagnosticSeverity.Error ? "red" : "yellow";
            console.MarkupLine($"[{color}]{filePath}{diagnostic.Location}: {diagnostic.Code}: {diagnostic.Message}[/]");

            if (!quiet && !string.IsNullOrEmpty(diagnostic.SourceLine))
            {
                // Escape markup brackets in source line
                string escapedLine = diagnostic.SourceLine.Replace("[", "[[").Replace("]", "]]");
                console.MarkupLine($"  {escapedLine}");
            }
        }
    }

    /// <summary>
    /// Formats all results as JSON.
    /// </summary>
    public static string FormatJson(
        List<(string Path, IReadOnlyList<Diagnostic> Diagnostics)> results,
        ValidationResultSummary summary)
    {
        var output = new JsonOutput
        {
            Summary = summary
        };

        foreach (var (path, diagnostics) in results)
        {
            var fileResult = new JsonFileResult
            {
                Path = path,
                Errors = new List<JsonErrorResult>()
            };

            foreach (var diagnostic in diagnostics)
            {
                fileResult.Errors.Add(new JsonErrorResult
                {
                    Code = diagnostic.Code.ToString(),
                    Message = diagnostic.Message,
                    Line = diagnostic.Location.Start.Line,
                    Column = diagnostic.Location.Start.Column,
                    EndLine = diagnostic.Location.End.Line,
                    EndColumn = diagnostic.Location.End.Column,
                    SourceLine = diagnostic.SourceLine,
                    Severity = diagnostic.Severity.ToString()
                });
            }

            output.Files.Add(fileResult);
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        return JsonSerializer.Serialize(output, options);
    }

    /// <summary>
    /// Formats a summary of validation results.
    /// </summary>
    public static void FormatSummary(ValidationResultSummary summary, IAnsiConsole console)
    {
        console.WriteLine($"Files processed: {summary.FilesProcessed}");
        console.WriteLine($"Files with errors: {summary.FilesWithErrors}");
        console.WriteLine($"Total errors: {summary.TotalErrors}");
        console.WriteLine($"Total warnings: {summary.TotalWarnings}");
    }
}
