using Kokuban;
using Microsoft.Extensions.Logging;
using X2ModCompiler.Configuration;
using X2ModCompiler.Utilities;

namespace X2ModCompiler.Application.Steps;

/// <summary>
/// Cleans additional mods before building. Mimics _CleanAdditional() from build_common.ps1.
/// </summary>
public class CleanAdditionalStep : BuildStepBase
{
    private readonly BuildOptions _options;

    public CleanAdditionalStep(BuildOptions options, ILogger<CleanAdditionalStep> logger)
        : base("Clean Additional Mods", logger)
    {
        _options = options;
    }

    protected override Task<bool> ExecuteStepAsync(BuildOptions options, CancellationToken ct)
    {
        _logger.LogInformation(LogColors.Info("Cleaning additional mods..."));

        foreach (var modName in _options.CleanMods)
        {
            var cleanDir = Path.Combine(options.SdkPath, "XComGame", "Mods", modName);
            if (Directory.Exists(cleanDir))
            {
                _logger.LogInformation($"  Cleaning: {modName}");
                Task.Run(() => Directory.Delete(cleanDir, recursive: true), ct);
            }
        }

        _logger.LogInformation(LogColors.Success("Cleaned additional mods."));
        return Task.FromResult(true);
    }
}
