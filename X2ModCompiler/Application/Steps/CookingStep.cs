using Microsoft.Extensions.Logging;
using X2ModCompiler.Cooking;
using X2ModCompiler.Configuration;
using X2ModCompiler.Utilities;

namespace X2ModCompiler.Application.Steps;

/// <summary>
/// Executes the asset cooking process to package non-script resources for the mod.
/// </summary>
public class CookingStep : BuildStepBase
{
    private readonly AssetCooker _cooker;
    private readonly ContentOptions _contentOptions;

    public CookingStep(AssetCooker cooker, ContentOptions contentOptions, ILogger<CookingStep> logger)
        : base("Asset Cooking", logger)
    {
        _cooker = cooker;
        _contentOptions = contentOptions;
    }

    protected override async Task<bool> ExecuteStepAsync(BuildOptions options, CancellationToken ct)
    {
        _logger.LogInformation($"Cooking assets for {options.ModNameCanonical}...");
        return await _cooker.CookAsync(options.ModNameCanonical, options.StagingPath, _contentOptions, options, ct);
    }
}
