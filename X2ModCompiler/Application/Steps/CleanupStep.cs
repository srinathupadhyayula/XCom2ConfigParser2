using System.IO;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using X2ModCompiler.Cooking;
using X2ModCompiler.Tracking;
using X2ModCompiler.Configuration;
using ZLogger;

namespace X2ModCompiler.Application.Steps;

/// <summary>
/// Prepares the build environment by cleaning up previous build artifacts.
/// Note: Selective clean of script packages is now handled by CompilationStep based on mod package timestamps.
/// The SDK handles its own incremental compilation cleanup.
/// </summary>
public class CleanupStep : IBuildStep
{
    private readonly AssetCooker _cooker;
    private readonly BuildTracker _tracker;
    private readonly ILogger<CleanupStep> _logger;

    public CleanupStep(AssetCooker cooker, BuildTracker tracker, ILogger<CleanupStep> logger)
    {
        _cooker = cooker;
        _tracker = tracker;
        _logger = logger;
    }

    public string Name => "Environment Cleanup";

    public async Task<bool> ExecuteAsync(BuildOptions options, CancellationToken ct)
    {
        _logger.LogInformation("Cleaning build artifacts...");

        // Clean cooked assets from previous builds
        await _cooker.CleanAsync(options.ModNameCanonical, ct);

        // Note: We no longer do selective clean of script packages here.
        // The CompilationStep checks mod package timestamps and cleans only when needed.
        // The SDK handles its own incremental compilation cleanup.

        return true;
    }
}
