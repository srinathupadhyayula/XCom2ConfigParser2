using Microsoft.Extensions.Logging;
using X2ModCompiler.Configuration;
using X2ModCompiler.Utilities;

namespace X2ModCompiler.Application.Steps;

/// <summary>
/// Mirrors the cooked and compiled artifacts to the local mod folder for final deployment.
/// </summary>
public class MirrorStep : BuildStepBase
{
    private readonly IFileMirrorParity _mirror;

    public MirrorStep(IFileMirrorParity mirror, ILogger<MirrorStep> logger)
        : base("File Mirroring", logger)
    {
        _mirror = mirror;
    }

    protected override async Task<bool> ExecuteStepAsync(BuildOptions options, CancellationToken ct)
    {
        _logger.LogInformation($"Mirroring build artifacts to {options.FinalModPath}...");
        // 1. Mirror staging to destination
        _logger.LogInformation($"Synchronizing {options.StagingPath} -> {options.FinalModPath}");
        await _mirror.MirrorAsync(options.StagingPath, options.FinalModPath, ct: ct);
        return true;
    }
}
