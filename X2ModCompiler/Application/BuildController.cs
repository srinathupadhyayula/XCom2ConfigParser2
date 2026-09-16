using Kokuban;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;
using X2ModCompiler.Configuration;
using X2ModCompiler.Compilation;
using X2ModCompiler.Cooking;
using X2ModCompiler.Tracking;
using X2ModCompiler.Utilities;
using X2ModCompiler.Core.Validation;
using X2ModCompiler.Core.Configuration;
using X2ModCompiler.Exceptions;

namespace X2ModCompiler.Application;

/// <summary>
/// The central coordinator for the mod build process.
/// Manages the lifecycle of a build, including initialization, execution of pipeline steps, and final results reporting.
/// </summary>
public class BuildController
{
    private readonly BuildOptions _options;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<BuildController> _logger;
    private readonly BuildServices _services;

    public static int ConfigParserDelayMs { get; set; } = BuildConstants.ConfigParserDelayMs;

    /// <summary>
    /// Initializes a new instance of the <see cref="BuildController"/> class.
    /// </summary>
    /// <param name="options">The build options.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="services">The build services facade containing all required dependencies.</param>
    public BuildController(
        BuildOptions options,
        ILoggerFactory loggerFactory,
        BuildServices services)
    {
        _options = options;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<BuildController>();
        _services = services;
    }

    /// <summary>
    /// Executes the full mod build pipeline asynchronously.
    /// </summary>
    public async Task<BuildResult> InvokeBuildAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        string? originalIniContent = null;
        string? targetIniPath = null;
        bool needsIniRestoration = false;
        Steps.CompilationStep? compilationStep = null;

        try
        {
            PrintInfoHeader(string.Format(BuildConstants.BuildInProgressHeader, _options.ModName));

            // Initialize build context
            (var iniHandler, originalIniContent, targetIniPath) = await InitializeBuildAsync(ct);

            // Execute the pipeline
            (var timings, var execCompilationStep) = await ExecutePipelineAsync(iniHandler, ct);
            compilationStep = execCompilationStep;

            // Finalize and create result
            needsIniRestoration = await FinalizeBuildAsync(timings, compilationStep, ct);

            sw.Stop();
            bool success = timings.All(t => t.Status == "SUCCESS");

            if (success)
            {
                await RecordBuildFingerprintAsync(ct);
                await _services.Tracker.RecordCoreTimestampAsync(_options.SdkPath, ct);
                PrintSuccessHeader(BuildConstants.BuildSuccessHeader);
            }
            else PrintErrorHeader(BuildConstants.BuildFailureHeader);

            var errors = timings.Where(t => t.Status == "FAILED").Select(t => $"{t.Description}: {t.ErrorMessage}").ToList();

            return new BuildResult(
                success,
                sw.Elapsed,
                new List<string>(),
                errors,
                timings);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(LogColors.Error("Build pipeline aborted due to unhandled exception"));
            _logger.LogError(ex.Message);
            return new BuildResult(
                false,
                sw.Elapsed,
                new List<string>(),
                new List<string> { ex.Message },
                new List<BuildTimingRecord>());
        }
        finally
        {
            // INI Restoration
            if (needsIniRestoration && originalIniContent != null && targetIniPath != null)
            {
                await RestoreIniAsync(targetIniPath, originalIniContent, ct);
            }
        }
    }

    /// <summary>
    /// Initializes the build context including INI handler and backup.
    /// </summary>
    private async Task<(IniHandler handler, string? originalIniContent, string? targetIniPath)> InitializeBuildAsync(CancellationToken ct)
    {
        string? originalIniContent = null;
        
        // Priority roots: 1. Mod's Config folder (for detection) 2. SDK's Config folder (as fallback)
        var iniRoots = _options.IniRoots.Count > 0 ? _options.IniRoots : new List<string> { Path.Combine(_options.ProjectRoot, "Config"), Path.Combine(_options.SdkPath, "XComGame", "Config") };
        var iniHandler = new IniHandler(_options.ProjectRoot, iniRoots, _loggerFactory.CreateLogger<IniHandler>());

        // Identify the target INI file for modification
        var targetIniPath = iniHandler.FindTargetIni();

        // Check if two-pass compilation will be needed and backup INI if required
        if (targetIniPath != null && File.Exists(targetIniPath))
        {
            var currentContent = await File.ReadAllTextAsync(targetIniPath, ct);
            var willNeedIniModification = iniHandler.IsTwoPassNeeded(currentContent) || _options.TwoPassCompilation;

            if (willNeedIniModification)
            {
                originalIniContent = await File.ReadAllTextAsync(targetIniPath, ct);
                _logger.LogInformation(LogColors.Debug($"INI backup created for two-pass compilation: {Path.GetFileName(targetIniPath)}"));
                _logger.LogInformation(LogColors.PathInfo(targetIniPath));
            }
            else
            {
                _logger.LogInformation(LogColors.Debug("Single-pass compilation - NO INI modification (build_common parity)."));
            }
        }

        return (iniHandler, originalIniContent, targetIniPath);
    }

    /// <summary>
    /// Executes the build pipeline with all steps.
    /// </summary>
    private async Task<(List<BuildTimingRecord> timings, Steps.CompilationStep? compilationStep)> ExecutePipelineAsync(IniHandler iniHandler, CancellationToken ct)
    {
        var pipeline = new BuildPipeline(_loggerFactory.CreateLogger<BuildPipeline>());
        Steps.CompilationStep? compilationStep = null;

        // ============================================================
        // BUILD PIPELINE - Exact parity with build_common.ps1
        // ============================================================

        // 0. Config Validation (NON-FATAL) - runs first when enabled, then build continues
        if (_options.ValidateConfig && !_options.CompileOnly)
        {
            pipeline.AddStep(new Steps.ValidationStep(_services.FileProcessor, _loggerFactory.CreateLogger<Steps.ValidationStep>()));
        }

        // 1. Prepare INI (TWO-PASS ONLY)
        pipeline.AddStep(new Steps.PrepareIniStep(iniHandler, _options, _loggerFactory.CreateLogger<Steps.PrepareIniStep>()));

        // 2. Regenerate ItemGroup
        pipeline.AddStep(new Steps.ProjectSyncStep(_services.ProjectSynchronizer, _loggerFactory.CreateLogger<Steps.ProjectSyncStep>()));

        // 3. Clean Additional Mods
        if (_options.CleanMods.Count > 0)
        {
            pipeline.AddStep(new Steps.CleanAdditionalStep(_options, _loggerFactory.CreateLogger<Steps.CleanAdditionalStep>()));
        }

        // 4. Copy Mod to SDK
        pipeline.AddStep(new Steps.CopyModToSdkStep(_loggerFactory.CreateLogger<Steps.CopyModToSdkStep>()));

        // 5. Convert Localization
        pipeline.AddStep(new Steps.LocalizationStep(_loggerFactory.CreateLogger<Steps.LocalizationStep>()));

        // 6. Copy Sources to SDK
        pipeline.AddStep(new Steps.CopyToSrcStep(_loggerFactory.CreateLogger<Steps.CopyToSrcStep>()));

        // 7. Run Pre-Make Hooks
        if (_options.PreMakeHooks.Count > 0)
        {
            pipeline.AddStep(new Steps.PreMakeHooksStep(_options, _loggerFactory.CreateLogger<Steps.PreMakeHooksStep>()));
        }

        // 8. Check Clean Compiled
        pipeline.AddStep(new Steps.CheckCleanCompiledStep(_services.Tracker, _services.ScriptCleaner, _loggerFactory.CreateLogger<Steps.CheckCleanCompiledStep>()));

        // 9. Script Compilation
        {
            var receiver = new MakeOutputReceiver(new[] { _options.ModSrcRoot }.Concat(_options.IncludePaths).ToArray(), _options.SdkPath, _loggerFactory.CreateLogger<MakeOutputReceiver>());
            compilationStep = new Steps.CompilationStep(_services.Compiler, receiver, _loggerFactory.CreateLogger<Steps.CompilationStep>());
            pipeline.AddStep(compilationStep);
        }

        // 10. Copy Script Packages
        if (!_options.CompileOnly)
        {
            pipeline.AddStep(new Steps.CopyScriptPackagesStep(_loggerFactory.CreateLogger<Steps.CopyScriptPackagesStep>()));
        }

        // 11. Asset Processing
        if (!_options.CompileOnly)
        {
            var contentOptions = LoadContentOptions();
            pipeline.AddStep(new Steps.ShaderStep(_services.ShaderPrecompiler, _loggerFactory.CreateLogger<Steps.ShaderStep>()));
            pipeline.AddStep(new Steps.CookingStep(_services.Cooker, contentOptions, _loggerFactory.CreateLogger<Steps.CookingStep>()));
            pipeline.AddStep(new Steps.UncookedCopyStep(_services.MissingUncookedCopier, contentOptions, _loggerFactory.CreateLogger<Steps.UncookedCopyStep>()));
        }

        // 12. Final Copy
        if (!_options.CompileOnly)
        {
            pipeline.AddStep(new Steps.FinalCopyStep(_loggerFactory.CreateLogger<Steps.FinalCopyStep>()));
        }

        // 13. Script Cleanup
        if (!_options.CompileOnly && !_options.Debug)
        {
            pipeline.AddStep(new Steps.ScriptCleanupStep(_loggerFactory.CreateLogger<Steps.ScriptCleanupStep>()));
        }

        _logger.LogDebug($"Build pipeline initialized with {pipeline.GetStepCount()} steps.");

        var timings = await pipeline.ExecuteAsync(_options, ct);
        return (timings, compilationStep);
    }

    /// <summary>
    /// Finalizes the build including fingerprint recording and INI restoration flag.
    /// </summary>
    private async Task<bool> FinalizeBuildAsync(List<BuildTimingRecord> timings, Steps.CompilationStep? compilationStep, CancellationToken ct)
    {
        // Determine if INI restoration is needed
        return compilationStep?.UsedTwoPass == true;
    }

    /// <summary>
    /// Records the build fingerprint after successful completion.
    /// </summary>
    private async Task RecordBuildFingerprintAsync(CancellationToken ct)
    {
        var globalsHash = BuildTracker.ComputeFileHash(Path.Combine(_options.SdkPath, "XComGame", "Config", "Globals.uci"));
        var coreTimestamp = File.Exists(Path.Combine(_options.SdkPath, "XComGame", "Script", "Core.u"))
            ? File.GetLastWriteTime(Path.Combine(_options.SdkPath, "XComGame", "Script", "Core.u"))
            : DateTime.MinValue;

        var fingerprint = new BuildFingerprint(
            _options.Debug ? "debug" : "release",
            globalsHash,
            coreTimestamp,
            DateTime.UtcNow,
            new Dictionary<string, DateTime>(),
            new Dictionary<string, DateTime>()
        );

        await _services.Tracker.SaveFingerprintAsync(fingerprint, ct);
    }

    /// <summary>
    /// Restores the INI file to its original state.
    /// </summary>
    private async Task RestoreIniAsync(string targetIniPath, string originalIniContent, CancellationToken ct)
    {
        _logger.LogInformation(LogColors.Info($"Restoring {Path.GetFileName(targetIniPath)} to original state..."));
        try
        {
            await File.WriteAllTextAsync(targetIniPath, originalIniContent, ct);
            _logger.LogInformation(LogColors.Success($"{Path.GetFileName(targetIniPath)} restoration complete."));
        }
        catch (Exception ex)
        {
            _logger.LogError(LogColors.Error($"Failed to restore {Path.GetFileName(targetIniPath)}: {ex.Message}"));
        }
    }

    /// <summary>
    /// Executes a standalone cleanup operation.
    /// </summary>
    public async Task<bool> InvokeCleanAsync(CancellationToken ct = default)
    {
        try
        {
            PrintInfoHeader(string.Format(BuildConstants.CleanInProgressHeader, _options.ModName));
            
            // 1. Clean build cache (all steps)
            await _services.Mirror.DeleteAsync(_options.BuildCachePath, true, ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(LogColors.Error($"Cleanup failed: {ex.Message}"));
            return false;
        }
    }

    /// <summary>
    /// Executes a standalone configuration validation pass.
    /// </summary>
    public async Task<bool> InvokeValidationAsync(CancellationToken ct = default)
    {
        PrintInfoHeader(string.Format(BuildConstants.ValidationInProgressHeader, _options.ModName));

        var pipeline = new BuildPipeline(_loggerFactory.CreateLogger<BuildPipeline>());

        // 0. Early Validation
        pipeline.AddStep(new Steps.IniValidationStep(_loggerFactory));

        // 1. Config Validation
        pipeline.AddStep(new Steps.ValidationStep(_services.FileProcessor, _loggerFactory.CreateLogger<Steps.ValidationStep>()));
        var results = await pipeline.ExecuteAsync(_options, ct);
        return results.All(r => r.Status == "SUCCESS");
    }

    private void PrintInfoHeader(string message)
    {
        _logger.LogInformation("");
        _logger.LogInformation(LogColors.Separator);
        _logger.LogInformation(LogColors.BuildHeader(message));
        _logger.LogInformation(LogColors.Separator);
    }

    private void PrintSuccessHeader(string message)
    {
        _logger.LogInformation("");
        _logger.LogInformation(LogColors.SuccessSeparator);
        _logger.LogInformation(LogColors.SuccessHeader(message));
        _logger.LogInformation(LogColors.SuccessSeparator);
    }

    private void PrintErrorHeader(string message)
    {
        _logger.LogInformation("");
        _logger.LogInformation(LogColors.ErrorSeparator);
        _logger.LogInformation(LogColors.ErrorHeader(message));
        _logger.LogInformation(LogColors.ErrorSeparator);
    }

    private ContentOptions LoadContentOptions()
    {
        if (string.IsNullOrEmpty(_options.ContentOptionsJson)) return new ContentOptions();
        
        var optionsPath = Path.Combine(_options.ModSrcRoot, _options.ContentOptionsJson);
        if (!File.Exists(optionsPath)) return new ContentOptions();
        
        try
        {
            var json = File.ReadAllText(optionsPath);
            return JsonSerializer.Deserialize<ContentOptions>(json) ?? new ContentOptions();
        }
        catch
        {
            _logger.LogWarning(LogColors.Warning($"Failed to load content options from {optionsPath}. Using defaults."));
            return new ContentOptions();
        }
    }
}
