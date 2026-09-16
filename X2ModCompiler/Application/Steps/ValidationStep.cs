using Microsoft.Extensions.Logging;
using X2ModCompiler.Configuration;
using X2ModCompiler.Core.Validation;
using Kokuban;
using X2ModCompiler.Core.Core;
using X2ModCompiler.Utilities;
using System.Collections.Generic;
using System.IO;

namespace X2ModCompiler.Application.Steps;

/// <summary>
/// Executes configuration validation and consistency checks using the XCom2ConfigParser2 engine.
/// </summary>
public class ValidationStep : BuildStepBase
{
    private readonly FileProcessor _fileProcessor;

    public ValidationStep(FileProcessor fileProcessor, ILogger<ValidationStep> logger)
        : base("Config Validation", logger)
    {
        _fileProcessor = fileProcessor;
    }

    protected override async Task<bool> ExecuteStepAsync(BuildOptions options, CancellationToken ct)
    {
        _logger.LogInformation("Performing configuration validation...");

        var iniFiles = DiscoverIniFiles(options);
        foreach (var f in iniFiles)
        {
        }
        int errorCount = 0;
        int warningCount = 0;

        foreach (var file in iniFiles)
        {
            var result = _fileProcessor.ProcessFile(file);

            foreach (var diag in result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
            {
                var formattedMessage = FormatDiagnostic(file, diag);
                _logger.LogError(LogColors.Error(formattedMessage));
                errorCount++;
            }

            foreach (var diag in result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning))
            {
                var formattedMessage = FormatDiagnostic(file, diag);
                _logger.LogWarning(LogColors.Warning(formattedMessage));
                warningCount++;
            }

            if (!result.HasErrors && !result.HasWarnings)
            {
                _logger.LogTrace($"Validated {Path.GetFileName(file)}");
            }
        }

        // Store counts in options for BuildController to read
        options.ConfigValidationErrors = errorCount;
        options.ConfigValidationWarnings = warningCount;

        // Log summary with color
        if (errorCount > 0)
        {
            _logger.LogError(LogColors.Error($"Validation completed with {errorCount} error(s) and {warningCount} warning(s)."));
            return false;
        }
        else if (warningCount > 0)
        {
            _logger.LogWarning(LogColors.Warning($"Validation completed with {warningCount} warning(s)."));
            return true;
        }
        else
        {
            _logger.LogInformation(LogColors.Success("Validation completed successfully."));
            return true;
        }
    }

    /// <summary>
    /// Discovers .ini files within the configured INI roots only.
    /// This ensures the config parser does not scan outside the main mod's Config directory,
    /// preventing reference directories (e.g. reference/) from being parsed.
    /// </summary>
    private static List<string> DiscoverIniFiles(BuildOptions options)
    {
        var files = new List<string>();
        foreach (var root in options.IniRoots)
        {
            if (Directory.Exists(root))
            {
                files.AddRange(Directory.GetFiles(root, "*.ini", SearchOption.AllDirectories));
            }
        }
        return files.Distinct().ToList();
    }

    /// <summary>
    /// Formats a diagnostic for VS Code clickable output.
    /// Format: file:line:column: severity: message
    /// </summary>
    private static string FormatDiagnostic(string filePath, Diagnostic diag)
    {
        var severityStr = diag.Severity switch
        {
            DiagnosticSeverity.Error => "error",
            DiagnosticSeverity.Warning => "warning",
            DiagnosticSeverity.Info => "info",
            _ => "info"
        };

        return $"{filePath}:{diag.Location.Start.Line}:{diag.Location.Start.Column}: {severityStr}: {diag.Code}: {diag.Message}";
    }
}
