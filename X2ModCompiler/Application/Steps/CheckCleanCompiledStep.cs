using Kokuban;
using Microsoft.Extensions.Logging;
using X2ModCompiler.Configuration;
using X2ModCompiler.Tracking;
using X2ModCompiler.Compilation;

namespace X2ModCompiler.Application.Steps;

/// <summary>
/// Checks for changes in build environment (Globals.uci, Core.u timestamp, build mode)
/// and performs selective cleanup of compiled script packages if needed.
/// This step mimics _CheckCleanCompiled() from build_common.ps1.
/// 
/// Triggers for:
/// - Build mode switch (debug ↔ release)
/// - Changes to Globals.uci macros
/// - External rebuild of Core packages
/// </summary>
public class CheckCleanCompiledStep : IBuildStep
{
    private readonly BuildTracker _tracker;
    private readonly ScriptCleaner _cleaner;
    private readonly ILogger<CheckCleanCompiledStep> _logger;

    public CheckCleanCompiledStep(
        BuildTracker tracker,
        ScriptCleaner cleaner,
        ILogger<CheckCleanCompiledStep> logger)
    {
        _tracker = tracker;
        _cleaner = cleaner;
        _logger = logger;
    }

    public string Name => "Check Clean Compiled";

    public async Task<bool> ExecuteAsync(BuildOptions options, CancellationToken ct)
    {
        _logger.LogInformation(Chalk.Cyan["Verifying compiled script packages..."]);

        // Compute current state
        var globalsPath = Path.Combine(options.SdkPath, "XComGame", "Config", "Globals.uci");
        var globalsHash = BuildTracker.ComputeFileHash(globalsPath);
        
        var corePath = Path.Combine(options.SdkPath, "XComGame", "Script", "Core.u");
        DateTime? coreTimestamp = File.Exists(corePath) ? File.GetLastWriteTime(corePath) : null;

        // Check if rebuild is needed
        bool needsClean = await _tracker.ShouldRebuildAsync(options, globalsHash, coreTimestamp, ct);

        if (needsClean)
        {
            _logger.LogInformation(Chalk.Yellow["Selective cleaning of compiled scripts to avoid compiler error..."]);
            
            // Clean all mod packages from SDK/Game script directories
            await _cleaner.CleanAllModPackagesAsync(options, ct);
            
            _logger.LogInformation(Chalk.Green["Cleaned."]);
        }
        else
        {
            _logger.LogInformation(Chalk.Gray["No environment changes detected - skipping selective clean."]);
        }

        return true;
    }
}
