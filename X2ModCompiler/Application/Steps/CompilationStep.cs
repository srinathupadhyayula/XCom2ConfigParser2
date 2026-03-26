using Microsoft.Extensions.Logging;
using X2ModCompiler.Compilation;
using X2ModCompiler.Utilities;
using X2ModCompiler.Exceptions;
using X2ModCompiler.Tracking;
using X2ModCompiler.Configuration;
using Kokuban;

namespace X2ModCompiler.Application.Steps;

/// <summary>
/// Orchestrates the UnrealScript compilation process, supporting both single-pass and complex two-pass linkage flows.
/// </summary>
public class CompilationStep : IBuildStep
{
    private readonly ScriptCompiler _compiler;
    private readonly IniHandler _iniHandler;
    private readonly OutputReceiver _receiver;
    private readonly BuildTracker _tracker;
    private readonly ScriptCleaner _cleaner;
    private readonly ILogger<CompilationStep> _logger;

    /// <summary>
    /// Gets a value indicating whether two-pass compilation was performed.
    /// This is used by BuildController to determine if INI restoration is needed.
    /// </summary>
    public bool UsedTwoPass { get; private set; }

    public CompilationStep(
        ScriptCompiler compiler,
        IniHandler iniHandler,
        OutputReceiver receiver,
        BuildTracker tracker,
        ScriptCleaner cleaner,
        ILogger<CompilationStep> logger)
    {
        _compiler = compiler;
        _iniHandler = iniHandler;
        _receiver = receiver;
        _tracker = tracker;
        _cleaner = cleaner;
        _logger = logger;
    }

    public string Name => "Script Compilation";

    public async Task<bool> ExecuteAsync(BuildOptions options, CancellationToken ct)
    {
        _logger.LogInformation(Chalk.Bold.Cyan[$">>> Starting compilation step for {options.ModNameCanonical}..."]);

        string? targetIni = _iniHandler.FindTargetIni();
        if (targetIni == null)
            throw new BuildFailureException(
                "Target XComEngine.ini for compilation detection/modification not found in project or fallback roots.",
                1);

        _logger.LogInformation(Chalk.Cyan[$"[DEBUG] Target Configuration File: {targetIni}"]);

        // Use dependent packages detected by PrepareIniStep (or from CLI/settings.json)
        var dependentPackages = options.DependentPackages;
        bool twoPassRequired = dependentPackages.Count > 0 || options.TwoPassCompilation;

        _logger.LogInformation(Chalk.Magenta[$"[DEBUG] Dependent Packages for Compilation: {dependentPackages.Count}"]);
        foreach (var pkg in dependentPackages)
        {
            _logger.LogInformation(Chalk.Gray[$"  -> {pkg}"]);
        }

        _logger.LogInformation(Chalk.Magenta[$"[DEBUG] Two-Pass Required: {twoPassRequired}"]);

        // Check and clean mod packages BEFORE deciding compilation path
        // This ensures cleanup happens regardless of single-pass or two-pass mode
        if (!await CheckAndCleanModPackagesAsync(options, ct))
            return false;

        if (twoPassRequired)
        {
            _logger.LogInformation(Chalk.Bold.Green["[MODE] Two-Pass compilation flow"]);
            if (dependentPackages.Count == 0 && options.DependentPackages.Count > 0)
            {
                _logger.LogInformation(Chalk.Blue["[DEBUG] Using CLI-provided dependant packages."]);
                dependentPackages = options.DependentPackages;
            }

            UsedTwoPass = true;
            return await ExecuteTwoPassAsync(options, targetIni, dependentPackages, ct);
        }
        else
        {
            _logger.LogInformation(Chalk.Bold.Yellow["[MODE] Single-Pass compilation flow"]);
            UsedTwoPass = false;
            return await ExecuteSinglePassAsync(options, ct);
        }
    }

    /// <summary>
    /// Checks if mod packages need cleanup before compilation and performs cleanup if needed.
    /// </summary>
    private async Task<bool> CheckAndCleanModPackagesAsync(BuildOptions options, CancellationToken ct)
    {
        var modScriptPackages = new[] { options.ModNameCanonical };

        // Check if any mod package source has been modified since last compilation
        bool needsCleanup = await _tracker.ShouldCleanModScriptsAsync(
            options.SdkPath,
            Path.Combine(options.ModSrcRoot, "Src"),
            modScriptPackages,
            ct);

        if (needsCleanup)
        {
            _logger.LogInformation(
                Chalk.Yellow["Mod source files modified - cleaning old .u files before compilation..."]);
            await _cleaner.CleanPackagesAsync(options.SdkPath, modScriptPackages, ct);
        }
        else
        {
            _logger.LogInformation(Chalk.Gray["No mod source changes detected - skipping cleanup."]);
        }

        return true;
    }

    private async Task<bool> ExecuteTwoPassAsync(BuildOptions options, string targetIni,
        List<string> dependentPackages, CancellationToken ct)
    {
        // INI was already modified by PrepareIniStep - just compile

        // Pass 1
        _logger.LogInformation(Chalk.Bold.Yellow["PHASE 1: INITIAL COMPILATION (Building base and all mod packages)"]);
        bool phase1Success = await ExecuteCompilationPassAsync(options, 1, dependentPackages, ct);

        // Record timestamps after Phase 1 if .u files were generated (even if Phase 1 "failed" but created binaries)
        // The compiler may report failure due to linking errors but still produce valid .u files
        var binaryPath = Path.Combine(options.SdkPath, "XComGame", "Script", $"{options.ModNameCanonical}.u");
        if (File.Exists(binaryPath))
        {
            _logger.LogDebug("Recording mod package timestamps after Phase 1 (.u files generated)...");
            await _tracker.RecordModTimestampsAsync(
                Path.Combine(options.ModSrcRoot, "Src"),
                new[] { options.ModNameCanonical },
                ct);
        }

        if (!phase1Success)
        {
            // Note: ExecuteCompilationPassAsync handles the forced recovery check for Phase 1
            return false;
        }

        // Pass 2 - NO INI modification, just compile
        _logger.LogInformation(Chalk.Bold.Yellow["PHASE 2: FINAL LINKAGE (Resolving cross-package dependencies)"]);
        if (!await ExecuteCompilationPassAsync(options, 2, dependentPackages, ct))
        {
            return false;
        }

        return true;
    }

    private async Task<bool> ExecuteCompilationPassAsync(BuildOptions options, int passNumber, List<string> dependentPackages, CancellationToken ct)
    {
        // 1. Compile Base (Always needed for environment stability)
        if (!await _compiler.CompileBaseAsync(options, _receiver, ct)) return false;

        // 2. Compile Mod
        if (passNumber == 2) await Task.Delay(2000, ct); // Small delay to clear file handles

        // Compile only the main mod - dependent packages are specified in INI ModEditPackages
        string compileTarget = options.ModNameCanonical;
        bool success = false;
        try
        {
            success = await _compiler.CompileModAsync(compileTarget, options.StagingPath, options, _receiver, ct);
        }
        catch (Exception)
        {
            if (passNumber == 1) success = false;
            else throw;
        }

        // Phase 1 Forced Recovery logic:
        // Linking failure is expected in Phase 1 for Two-Pass mods.
        // If the .u package was successfully created, we proceed to Phase 2.
        if (!success && passNumber == 1)
        {
            var binaryPath = Path.Combine(options.SdkPath, "XComGame", "Script", $"{options.ModNameCanonical}.u");
            if (File.Exists(binaryPath))
            {
                _logger.LogInformation(
                    Chalk.Cyan["################################################################################"]);
                _logger.LogInformation(Chalk.Cyan[$"# [PHASE 1] EXPECTED dependency failure in: {compileTarget}"]);
                _logger.LogInformation(
                    Chalk.Cyan[$"# [PHASE 1] Binary '{options.ModNameCanonical}.u' was created successfully."]);
                _logger.LogInformation(
                    Chalk.Cyan["# Proceeding to Phase 2 for final linkage of dependent packages..."]);
                _logger.LogInformation(
                    Chalk.Cyan["################################################################################"]);
                return true;
            }
        }

        return success;
    }

    private async Task<bool> ExecuteSinglePassAsync(BuildOptions options, CancellationToken ct)
    {
        if (!await _compiler.CompileBaseAsync(options, _receiver, ct)) return false;

        // Compile main mod only - dependent packages are handled via ModEditPackages in INI
        // This matches build_common.ps1 behavior: -mods ModName StagingPath
        var compileTarget = options.ModNameCanonical;

        var success = await _compiler.CompileModAsync(compileTarget, options.StagingPath, options, _receiver, ct);

        // Record timestamps if .u files were generated (even if compilation reported failure due to linking errors)
        var binaryPath = Path.Combine(options.SdkPath, "XComGame", "Script", $"{options.ModNameCanonical}.u");
        if (File.Exists(binaryPath))
        {
            _logger.LogDebug("Recording mod package timestamps (.u files generated)...");
            await _tracker.RecordModTimestampsAsync(
                Path.Combine(options.ModSrcRoot, "Src"),
                new[] { options.ModNameCanonical },
                ct);
        }

        return success;
    }
}
