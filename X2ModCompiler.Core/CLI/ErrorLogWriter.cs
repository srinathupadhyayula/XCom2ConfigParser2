using System.Text;
using X2ModCompiler.Core.Core;

namespace X2ModCompiler.Core.CLI;

/// <summary>
/// Writes error logs in a VSCode-friendly format with problem matcher support.
/// </summary>
public static class ErrorLogWriter
{
    /// <summary>
    /// Writes the error log to a file in categorized format.
    /// </summary>
    public static void Write(ErrorLog log, string outputPath)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("================================================================================");
        sb.AppendLine("                    UE3 Config Parser - Validation Report                       ");
        sb.AppendLine("================================================================================");
        sb.AppendLine();
        sb.AppendLine($"Generated: {log.GeneratedAt:yyyy-MM-dd HH:mm:ss UTC}");
        sb.AppendLine($"Project:   {log.ProjectRoot}");
        sb.AppendLine();
        
        // Summary
        sb.AppendLine("+------------------------------------------------------------------------------+");
        sb.AppendLine("| SUMMARY                                                                      |");
        sb.AppendLine("+------------------------------------------------------------------------------+");
        sb.AppendLine($"| Files Processed:     {log.Summary.FilesProcessed,5}                                             |");
        sb.AppendLine($"| Files with Errors:   {log.Summary.FilesWithErrors,5}                                             |");
        sb.AppendLine($"| Total Errors:        {log.TotalErrors,5}                                             |");
        sb.AppendLine($"| Total Warnings:      {log.TotalWarnings,5}                                             |");
        sb.AppendLine("+------------------------------------------------------------------------------+");
        sb.AppendLine();

        // Errors by category
        foreach (var category in log.ErrorsByCategory.OrderBy(kvp => kvp.Key))
        {
            var errorCode = category.Key;
            var entries = category.Value;
            
            // Skip empty categories
            if (entries.Count == 0) continue;

            // Category header
            string categoryTitle = $"{errorCode} ({entries.Count} {(entries.Count == 1 ? "occurrence" : "occurrences")})";
            sb.AppendLine(categoryTitle);
            sb.AppendLine(new string('-', categoryTitle.Length));
            sb.AppendLine();

            // Group by severity
            var errors = entries.Where(e => e.Severity == DiagnosticSeverity.Error).ToList();
            var warnings = entries.Where(e => e.Severity == DiagnosticSeverity.Warning).ToList();

            if (errors.Count > 0)
            {
                sb.AppendLine($"  Errors: {errors.Count}");
                foreach (var entry in errors)
                {
                    WriteEntry(sb, entry);
                }
                sb.AppendLine();
            }

            if (warnings.Count > 0)
            {
                sb.AppendLine($"  Warnings: {warnings.Count}");
                foreach (var entry in warnings)
                {
                    WriteEntry(sb, entry);
                }
                sb.AppendLine();
            }
        }

        // Footer
        sb.AppendLine("================================================================================");
        sb.AppendLine($"End of Report - {log.ErrorsByCategory.Count} categories, {log.TotalErrors} errors, {log.TotalWarnings} warnings");
        sb.AppendLine("================================================================================");

        File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
    }

    private static void WriteEntry(StringBuilder sb, ErrorLogEntry entry)
    {
        // VSCode problem matcher format: file(line,col): error code: message
        sb.AppendLine($"  [FILE] {entry.FilePath}({entry.Line},{entry.Column})");
        
        // Get symbol for severity
        string symbol = entry.Severity switch
        {
            DiagnosticSeverity.Error => "[ERROR]",
            DiagnosticSeverity.Warning => "[WARN]",
            DiagnosticSeverity.Info => "[INFO]",
            _ => "[?]"
        };

        sb.AppendLine($"         {symbol} [{entry.Code}] {entry.Message}");
        
        // Source line excerpt
        if (!string.IsNullOrEmpty(entry.SourceLine))
        {
            sb.AppendLine($"           | {entry.SourceLine.Trim()}");
        }
        sb.AppendLine();
    }

    /// <summary>
    /// Writes a compact JSON version of the log for machine processing.
    /// </summary>
    public static void WriteJson(ErrorLog log, string outputPath)
    {
        var options = new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        };
        
        var json = System.Text.Json.JsonSerializer.Serialize(log, options);
        File.WriteAllText(outputPath, json, Encoding.UTF8);
    }
}
