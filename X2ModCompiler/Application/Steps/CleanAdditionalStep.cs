using Kokuban;
using Microsoft.Extensions.Logging;
using X2ModCompiler.Configuration;

namespace X2ModCompiler.Application.Steps;

/// <summary>
/// Cleans additional mods before building. Mimics _CleanAdditional() from build_common.ps1.
/// </summary>
public class CleanAdditionalStep : IBuildStep
{
    private readonly BuildOptions _options;
    private readonly ILogger<CleanAdditionalStep> _logger;

    public CleanAdditionalStep(BuildOptions options, ILogger<CleanAdditionalStep> logger)
    {
        _options = options;
        _logger = logger;
    }

    public string Name => "Clean Additional Mods";

    public async Task<bool> ExecuteAsync(BuildOptions options, CancellationToken ct)
    {
        _logger.LogInformation(Chalk.Cyan["Cleaning additional mods..."]);

        foreach (var modName in _options.CleanMods)
        {
            var cleanDir = Path.Combine(options.SdkPath, "XComGame", "Mods", modName);
            if (Directory.Exists(cleanDir))
            {
                _logger.LogInformation($"  Cleaning: {modName}");
                await Task.Run(() => Directory.Delete(cleanDir, recursive: true), ct);
            }
        }

        _logger.LogInformation(Chalk.Green["Cleaned additional mods."]);
        return true;
    }
}
