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
    private readonly BuildTracker _tracker;
    private readonly ScriptCompiler _compiler;
    private readonly AssetCooker _cooker;
    private readonly IFileMirrorParity _mirror;
    private readonly IProcessRunner _processRunner;
    private readonly ShaderPrecompiler _shaderPrecompiler;
    private readonly MissingUncookedCopier _missingUncookedCopier;
    private readonly ProjectSynchronizer _projectSynchronizer;
    private readonly FileProcessor _fileProcessor;
    private readonly ScriptCleaner _scriptCleaner;

    public static int ConfigParserDelayMs { get; set; } = 1000;

    public BuildController(
        BuildOptions options,
        ILoggerFactory loggerFactory,
        BuildTracker tracker,
        ScriptCompiler compiler,
        AssetCooker cooker,
        IFileMirrorParity mirror,
        IProcessRunner processRunner,
        ShaderPrecompiler shaderPrecompiler,
        MissingUncookedCopier missingUncookedCopier,
        ProjectSynchronizer projectSynchronizer,
        FileProcessor fileProcessor,
        ScriptCleaner scriptCleaner)
    {
        _options = options;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<BuildController>();
        _tracker = tracker;
        _compiler = compiler;
        _cooker = cooker;
        _mirror = mirror;
        _processRunner = processRunner;
        _shaderPrecompiler = shaderPrecompiler;
        _missingUncookedCopier = missingUncookedCopier;
        _projectSynchronizer = projectSynchronizer;
        _fileProcessor = fileProcessor;
        _scriptCleaner = scriptCleaner;
    }

    /// <summary>
    /// Executes the full mod build pipeline asynchronously.
    /// </summary>
    public async Task<BuildResult> InvokeBuildAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var outputPaths = new List<string>();
        string? originalIniContent = null;
        string? targetIniPath = null;
        bool needsIniRestoration = false;

        try
        {
            PrintInfoHeader($"BUILDING {_options.ModName}");

            // Priority roots: 1. Mod's Config folder (for detection) 2. SDK's Config folder (as fallback)
            var iniRoots = _options.IniRoots.Count > 0 ? _options.IniRoots : new List<string> { Path.Combine(_options.ProjectRoot, "Config"), Path.Combine(_options.SdkPath, "XComGame", "Config") };
            var iniHandler = new IniHandler(_options.ProjectRoot, iniRoots, _loggerFactory.CreateLogger<IniHandler>());

            // Identify the target INI file for modification (Mod's INI file ONLY, per user requirement)
            // Note: INI modification ONLY happens for two-pass compilation
            targetIniPath = iniHandler.FindTargetIni();
            
            // Check if two-pass compilation will be needed before backing up INI
            bool willNeedIniModification = false;
            if (targetIniPath != null && File.Exists(targetIniPath))
            {
                var currentContent = await File.ReadAllTextAsync(targetIniPath, ct);
                willNeedIniModification = iniHandler.IsTwoPassNeeded(currentContent) || _options.TwoPassCompilation;
                
                // Only backup INI if two-pass compilation is required
                if (willNeedIniModification)
                {
                    originalIniContent = await File.ReadAllTextAsync(targetIniPath, ct);
                    _logger.LogInformation(Chalk.Gray[$"INI backup created for two-pass compilation: {Path.GetFileName(targetIniPath)}"]);
                    _logger.LogInformation(Chalk.Gray[$"Target path: {targetIniPath}"]);
                }
                else
                {
                    _logger.LogInformation(Chalk.Gray[$"Single-pass compilation - NO INI modification (build_common parity)."]);
                }
            }

            var pipeline = new BuildPipeline(_loggerFactory.CreateLogger<BuildPipeline>());

            // 0. Early Validation
            pipeline.AddStep(new Steps.IniValidationStep(_loggerFactory));

            // 1. Copy Mod to SDK Staging (mimics _CopyModToSdk from build_common.ps1)
            if (!_options.ValidateConfig)
            {
                pipeline.AddStep(new Steps.CopyModToSdkStep(_loggerFactory.CreateLogger<Steps.CopyModToSdkStep>()));
            }

            // 2. Convert Localization (UTF-8 → UTF-16, mimics _ConvertLocalization)
            if (!_options.ValidateConfig)
            {
                pipeline.AddStep(new Steps.LocalizationStep(_loggerFactory.CreateLogger<Steps.LocalizationStep>()));
            }

            // 3. Prepare INI for compilation (ensure ModEditPackages includes dependents)
            if (!_options.ValidateConfig)
            {
                pipeline.AddStep(new Steps.PrepareIniStep(iniHandler, _loggerFactory.CreateLogger<Steps.PrepareIniStep>()));
            }

            // 4. Copy Sources to SDK (before compilation - mimics _CopyToSrc from build_common.ps1)
            // This copies SrcOrig → Src, dependencies → Src, and mod sources → Src
            if (!_options.ValidateConfig)
            {
                pipeline.AddStep(new Steps.CopyToSrcStep(_loggerFactory.CreateLogger<Steps.CopyToSrcStep>()));
            }

            // 5. Check Clean Compiled (after CopyToSrc, before compilation - mimics _CheckCleanCompiled)
            // Checks for Globals.uci changes, Core.u rebuild, and build mode switches
            if (!_options.ValidateConfig)
            {
                pipeline.AddStep(new Steps.CheckCleanCompiledStep(_tracker, _scriptCleaner, _loggerFactory.CreateLogger<Steps.CheckCleanCompiledStep>()));
            }

            // 6. Script Compilation
            Steps.CompilationStep? compilationStep = null;
            if (!_options.ValidateConfig)
            {
                var receiver = new MakeOutputReceiver(new[] { _options.ModSrcRoot }.Concat(_options.IncludePaths).ToArray(), _loggerFactory.CreateLogger<MakeOutputReceiver>());
                compilationStep = new Steps.CompilationStep(_compiler, iniHandler, receiver, _tracker, _scriptCleaner, _loggerFactory.CreateLogger<Steps.CompilationStep>());
                pipeline.AddStep(compilationStep);
            }

            // 6. Copy Script Packages to Staging (mimics _CopyScriptPackages)
            if (!_options.ValidateConfig && !_options.CompileOnly)
            {
                pipeline.AddStep(new Steps.CopyScriptPackagesStep(_loggerFactory.CreateLogger<Steps.CopyScriptPackagesStep>()));
            }

            // 7. Asset Processing
            // Note: Shader step MUST happen before cooking - precompiler gets confused by inlined materials
            if (!(_options.CompileOnly || _options.ValidateConfig))
            {
                var contentOptions = LoadContentOptions();
                pipeline.AddStep(new Steps.ShaderStep(_shaderPrecompiler, _loggerFactory.CreateLogger<Steps.ShaderStep>()));
                pipeline.AddStep(new Steps.CookingStep(_cooker, contentOptions, _loggerFactory.CreateLogger<Steps.CookingStep>()));
                pipeline.AddStep(new Steps.UncookedCopyStep(_missingUncookedCopier, contentOptions, _loggerFactory.CreateLogger<Steps.UncookedCopyStep>()));
            }

            // 8. Final Copy to Game Directory (mimics _FinalCopy)
            if (!(_options.CompileOnly || _options.ValidateConfig))
            {
                pipeline.AddStep(new Steps.FinalCopyStep(_loggerFactory.CreateLogger<Steps.FinalCopyStep>()));
            }

            // 9. Script Cleanup (after successful build, mimics _CleanLeftoverScripts from build_common.ps1)
            if (!(_options.CompileOnly || _options.ValidateConfig) && !_options.Debug)
            {
                pipeline.AddStep(new Steps.ScriptCleanupStep(_loggerFactory.CreateLogger<Steps.ScriptCleanupStep>()));
            }

            // 10. Optional Validation
            if (_options.ValidateConfig)
            {
                pipeline.AddStep(new Steps.ValidationStep(_fileProcessor, _loggerFactory.CreateLogger<Steps.ValidationStep>()));
            }

            _logger.LogDebug($"Build pipeline initialized with {pipeline.GetStepCount()} steps.");

            var timings = await pipeline.ExecuteAsync(_options, ct);

            // Determine if INI restoration is needed (for both single-pass and two-pass)
            // Single-pass: INI was modified by PrepareIniStep
            // Two-pass: INI was modified by CompilationStep
            needsIniRestoration = willNeedIniModification || (compilationStep?.UsedTwoPass == true);

            sw.Stop();
            bool success = timings.All(t => t.Status == "SUCCESS");
            
            if (success)
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

                await _tracker.SaveFingerprintAsync(fingerprint, ct);

                // Record Core timestamp after successful build (mod package timestamps are recorded by CompilationStep)
                await _tracker.RecordCoreTimestampAsync(_options.SdkPath, ct);

                PrintSuccessHeader("BUILD COMPLETED SUCCESSFULLY");
            }
            else PrintErrorHeader("BUILD FAILED");

            var errors = timings.Where(t => t.Status == "FAILED").Select(t => $"{t.Description}: {t.ErrorMessage}").ToList();

            return new BuildResult(
                success,
                sw.Elapsed,
                outputPaths,
                errors,
                timings);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(Chalk.Bold.Red["Build pipeline aborted due to unhandled exception"]);
            _logger.LogError(ex.Message);
            return new BuildResult(
                false, 
                sw.Elapsed, 
                outputPaths, 
                new List<string> { ex.Message }, 
                new List<BuildTimingRecord>());
        }
        finally
        {
            // INI Restoration: Only for two-pass builds where INI was modified
            // Single-pass builds have NO INI modification (build_common parity)
            if (needsIniRestoration && originalIniContent != null && targetIniPath != null)
            {
                _logger.LogInformation(Chalk.Cyan[$"Restoring {Path.GetFileName(targetIniPath)} to original state..."]);
                try
                {
                    await File.WriteAllTextAsync(targetIniPath, originalIniContent, ct);
                    _logger.LogInformation(Chalk.Green[$"{Path.GetFileName(targetIniPath)} restoration complete."]);
                }
                catch (Exception ex)
                {
                    _logger.LogError(Chalk.Red[$"Failed to restore {Path.GetFileName(targetIniPath)}: {ex.Message}"]);
                }
            }
        }
    }

    /// <summary>
    /// Executes a standalone cleanup operation.
    /// </summary>
    public async Task<bool> InvokeCleanAsync(CancellationToken ct = default)
    {
        try
        {
            PrintInfoHeader($"CLEANING {_options.ModName}");
            
            // 1. Clean build cache (all steps)
            await _mirror.DeleteAsync(_options.BuildCachePath, true, ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(Chalk.Red[$"Cleanup failed: {ex.Message}"]);
            return false;
        }
    }

    /// <summary>
    /// Executes a standalone configuration validation pass.
    /// </summary>
    public async Task<bool> InvokeValidationAsync(CancellationToken ct = default)
    {
        PrintInfoHeader($"VALIDATING {_options.ModName} CONFIGURATION");

        var pipeline = new BuildPipeline(_loggerFactory.CreateLogger<BuildPipeline>());

        // 0. Early Validation
        pipeline.AddStep(new Steps.IniValidationStep(_loggerFactory));

        // 1. Config Validation
        pipeline.AddStep(new Steps.ValidationStep(_fileProcessor, _loggerFactory.CreateLogger<Steps.ValidationStep>()));
        var results = await pipeline.ExecuteAsync(_options, ct);
        return results.All(r => r.Status == "SUCCESS");
    }

    private void PrintInfoHeader(string message)
    {
        Console.WriteLine();
        Console.WriteLine(Chalk.Bold.Cyan["================================================================================"]);
        Console.WriteLine(Chalk.Bold.Cyan[$"  {message}"]);
        Console.WriteLine(Chalk.Bold.Cyan["================================================================================"]);
    }

    private void PrintSuccessHeader(string message)
    {
        Console.WriteLine();
        Console.WriteLine(Chalk.Bold.Green["################################################################################"]);
        Console.WriteLine(Chalk.Bold.Green[$"#  {message}"]);
        Console.WriteLine(Chalk.Bold.Green["################################################################################"]);
    }

    private void PrintErrorHeader(string message)
    {
        Console.WriteLine();
        Console.WriteLine(Chalk.Bold.Red["!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!"]);
        Console.WriteLine(Chalk.Bold.Red[$"!  {message}"]);
        Console.WriteLine(Chalk.Bold.Red["!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!"]);
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
            _logger.LogWarning(Chalk.Yellow[$"Failed to load content options from {optionsPath}. Using defaults."]);
            return new ContentOptions();
        }
    }
}
