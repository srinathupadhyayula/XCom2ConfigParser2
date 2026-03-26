using Microsoft.Extensions.Logging;
using X2ModCompiler.Utilities;
using X2ModCompiler.Configuration;

namespace X2ModCompiler.Application.Steps;

/// <summary>
/// Mirrors the mod's source code and configuration files to a clean staging directory to prevent environment pollution.
/// </summary>
public class StagingStep : IBuildStep
{
    private readonly IFileMirrorParity _mirror;
    private readonly ILogger<StagingStep> _logger;

    public StagingStep(IFileMirrorParity mirror, ILogger<StagingStep> logger)
    {
        _mirror = mirror;
        _logger = logger;
    }

    public string Name => "Environment Staging";

    public async Task<bool> ExecuteAsync(BuildOptions options, CancellationToken ct)
    {
        _logger.LogInformation($"Staging mod source to {options.StagingPath}...");
        
        // Mirror Src and Config
        var srcRoot = options.ModSrcRoot;
        await _mirror.MirrorAsync(srcRoot, options.StagingPath, ct: ct);
        
        return true;
    }
}
