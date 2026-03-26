using Microsoft.Extensions.Logging;
using X2ModCompiler.Configuration;
using X2ModCompiler.Core.Validation;
using X2ModCompiler.Core.Core;

namespace X2ModCompiler.Application.Steps;

/// <summary>
/// Executes configuration validation and consistency checks using the XCom2ConfigParser2 engine.
/// </summary>
public class ValidationStep : IBuildStep
{
    private readonly FileProcessor _fileProcessor;
    private readonly ILogger<ValidationStep> _logger;

    public ValidationStep(FileProcessor fileProcessor, ILogger<ValidationStep> logger)
    {
        _fileProcessor = fileProcessor;
        _logger = logger;
    }

    public string Name => "Config Validation";

    public async Task<bool> ExecuteAsync(BuildOptions options, CancellationToken ct)
    {
        _logger.LogInformation("Performing configuration validation...");
        
        var iniFiles = Directory.GetFiles(options.ProjectRoot, "*.ini", SearchOption.AllDirectories);
        int errorCount = 0;
        
        foreach (var file in iniFiles)
        {
            var result = _fileProcessor.ProcessFile(file);
            if (result.HasErrors)
            {
                foreach (var diag in result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
                {
                    _logger.LogError($"Validation Error in {Path.GetFileName(file)}: {diag.Message}");
                }
                errorCount += result.ErrorCount;
            }
        }

        if (errorCount > 0)
        {
            _logger.LogError($"Validation failed with {errorCount} errors.");
            return false;
        }

        _logger.LogInformation("Validation completed successfully.");
        return await Task.FromResult(true);
    }
}
