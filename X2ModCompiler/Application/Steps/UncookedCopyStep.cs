using Microsoft.Extensions.Logging;
using X2ModCompiler.Compilation;
using X2ModCompiler.Configuration;
using X2ModCompiler.Utilities;

namespace X2ModCompiler.Application.Steps;

/// <summary>
/// Ensures all missing uncooked dependency packages are copied from the SDK to the mod staging directory.
/// </summary>
public class UncookedCopyStep : IBuildStep
{
    private readonly MissingUncookedCopier _copier;
    private readonly ContentOptions _contentOptions;
    private readonly ILogger<UncookedCopyStep> _logger;

    public UncookedCopyStep(MissingUncookedCopier copier, ContentOptions contentOptions, ILogger<UncookedCopyStep> logger)
    {
        _copier = copier;
        _contentOptions = contentOptions;
        _logger = logger;
    }

    public string Name => "Uncooked Asset Copier";

    public async Task<bool> ExecuteAsync(BuildOptions options, CancellationToken ct)
    {
        _logger.LogInformation("Checking for missing uncooked packages...");
        
        // This mirrors BuildController.Step_CopyMissingUncooked logic
        // We need ContentOptions, which are usually loaded from ContentOptions.json
        var contentOptions = new ContentOptions(); // Simplified for now, should ideally be passed in
        
        await _copier.CopyMissingAsync(options, contentOptions, ct);
        return true;
    }
}
