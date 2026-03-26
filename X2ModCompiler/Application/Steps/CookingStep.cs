using Microsoft.Extensions.Logging;
using X2ModCompiler.Cooking;
using X2ModCompiler.Configuration;

namespace X2ModCompiler.Application.Steps;

/// <summary>
/// Executes the asset cooking process to package non-script resources for the mod.
/// </summary>
public class CookingStep : IBuildStep
{
    private readonly AssetCooker _cooker;
    private readonly ContentOptions _contentOptions;
    private readonly ILogger<CookingStep> _logger;

    public CookingStep(AssetCooker cooker, ContentOptions contentOptions, ILogger<CookingStep> logger)
    {
        _cooker = cooker;
        _contentOptions = contentOptions;
        _logger = logger;
    }

    public string Name => "Asset Cooking";

    public async Task<bool> ExecuteAsync(BuildOptions options, CancellationToken ct)
    {
        _logger.LogInformation($"Cooking assets for {options.ModNameCanonical}...");
        return await _cooker.CookAsync(options.ModNameCanonical, options.StagingPath, _contentOptions, options, ct);
    }
}
