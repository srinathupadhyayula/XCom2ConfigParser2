using Spectre.Console;
using System.Text.Json;
using System.Text.Json.Serialization;
using XCom2ConfigParser2.Core;

namespace XCom2ConfigParser2.CLI;

/// <summary>
/// Defines the available output formats and verbosity levels for the CLI.
/// </summary>
public enum OutputMode
{
    /// <summary> Standard console output with full diagnostic details. </summary>
    Default,

    /// <summary> Machine-readable JSON output for all results and summary data. </summary>
    Json,

    /// <summary> Minimal console output, typically showing only file paths and error messages without source context. </summary>
    Quiet,

    /// <summary> Shows only the final statistical summary of the validation run. </summary>
    Summary
}

/// <summary>
/// Provides a high-level statistical summary of the entire validation session.
/// </summary>
public sealed class ValidationResultSummary
{
    /// <summary>Gets or sets the total number of files scanned.</summary>
    public int FilesProcessed { get; set; }

    /// <summary>Gets or sets the number of files that contained at least one error.</summary>
    public int FilesWithErrors { get; set; }

    /// <summary>Gets or sets the aggregate count of all error-level diagnostics.</summary>
    public int TotalErrors { get; set; }

    /// <summary>Gets or sets the aggregate count of all warning-level diagnostics.</summary>
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
/// Defines the root schema for JSON output, aggregating results across all files and providing a final summary.
/// </summary>
public sealed class JsonOutput
{
    /// <summary>Gets or sets the list of results for each processed file.</summary>
    [JsonPropertyName("files")]
    public List<JsonFileResult> Files { get; set; } = new();

    /// <summary>Gets or sets the overall validation summary.</summary>
    [JsonPropertyName("summary")]
    public ValidationResultSummary Summary { get; set; } = new();
}

/// <summary>
/// Provides static utilities for formatting and displaying validation results across different output modes.
/// </summary>
public static class OutputFormatter
{
    /// <summary>
    /// Displays diagnostics for a single file according to the specified <see cref="OutputMode"/>.
    /// </summary>
    /// <param name="filePath">The path of the file.</param>
    /// <param name="diagnostics">The list of diagnostics to display.</param>
    /// <param name="mode">The requested output mode.</param>
    /// <param name="console">The ANSI console instance for output.</param>
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
                // JSON is formatted at a higher level via FormatJson
                break;

            case OutputMode.Summary:
                // Summary is formatted at a higher level via FormatSummary
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
            console.MarkupLine($"[{color}]{Markup.Escape(filePath)}{diagnostic.Location}: {diagnostic.Code}: {Markup.Escape(diagnostic.Message)}[/]");

            if (!quiet && !string.IsNullOrEmpty(diagnostic.SourceLine))
            {
                // Escape markup brackets in source line
                string escapedLine = diagnostic.SourceLine.Replace("[", "[[").Replace("]", "]]");
                console.MarkupLine($"  {escapedLine}");
            }
        }
    }

    /// <summary>
    /// Serializes entire session results into a standardized JSON string.
    /// </summary>
    /// <param name="results">The collection of per-file results.</param>
    /// <param name="summary">The overall run summary.</param>
    /// <returns>A formatted JSON string.</returns>
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
    /// Displays a terminal-friendly summary of the validation session.
    /// </summary>
    /// <param name="summary">The summary data to display.</param>
    /// <param name="console">The ANSI console instance for output.</param>
    public static void FormatSummary(ValidationResultSummary summary, IAnsiConsole console)
    {
        console.WriteLine($"Files processed: {summary.FilesProcessed}");
        console.WriteLine($"Files with errors: {summary.FilesWithErrors}");
        console.WriteLine($"Total errors: {summary.TotalErrors}");
        console.WriteLine($"Total warnings: {summary.TotalWarnings}");
    }
}
