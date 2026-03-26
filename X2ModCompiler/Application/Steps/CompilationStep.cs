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
public class CompilationStep : BuildStepBase
{
    private readonly ScriptCompiler _compiler;
    private readonly OutputReceiver _receiver;

    /// <summary>
    /// Gets a value indicating whether two-pass compilation was performed.
    /// This is used by BuildController to determine if INI restoration is needed.
    /// </summary>
    public bool UsedTwoPass { get; private set; }

    public CompilationStep(
        ScriptCompiler compiler,
        OutputReceiver receiver,
        ILogger<CompilationStep> logger)
        : base("Script Compilation", logger)
    {
        _compiler = compiler;
        _receiver = receiver;
    }

    protected override async Task<bool> ExecuteStepAsync(BuildOptions options, CancellationToken ct)
    {
        _logger.LogInformation(LogColors.Info($">>> Starting compilation step for {options.ModNameCanonical}..."));

        // Use dependent packages detected by PrepareIniStep (or from CLI/settings.json)
        var dependentPackages = options.DependentPackages;
        bool twoPassRequired = dependentPackages.Count > 0 || options.TwoPassCompilation;

        _logger.LogInformation(LogColors.Info($"[DEBUG] Dependent Packages for Compilation: {dependentPackages.Count}"));
        foreach (var pkg in dependentPackages)
        {
            _logger.LogInformation(LogColors.Debug($"  -> {pkg}"));
        }

        _logger.LogInformation(LogColors.Info($"[DEBUG] Two-Pass Required: {twoPassRequired}"));

        if (twoPassRequired)
        {
            _logger.LogInformation(LogColors.PhaseHeader("[MODE] Two-Pass compilation flow"));

            UsedTwoPass = true;
            return await ExecuteTwoPassAsync(options, dependentPackages, ct);
        }
        else
        {
            _logger.LogInformation(LogColors.PhaseHeader("[MODE] Single-Pass compilation flow (build_common parity)"));
            UsedTwoPass = false;
            return await ExecuteSinglePassAsync(options, ct);
        }
    }

    private async Task<bool> ExecuteTwoPassAsync(BuildOptions options,
        List<string> dependentPackages, CancellationToken ct)
    {
        // INI was already modified by PrepareIniStep - just compile

        // Pass 1
        _logger.LogInformation(LogColors.PhaseHeader("PHASE 1: INITIAL COMPILATION (Building base and all mod packages)"));
        bool phase1Success = await ExecuteCompilationPassAsync(options, 1, dependentPackages, ct);

        // Determine if Phase 2 should run based on Phase 1 output
        bool shouldRunPhase2 = ShouldRunPhase2(phase1Success, dependentPackages, options);

        if (!shouldRunPhase2)
        {
            _logger.LogError(LogColors.Error("Phase 1 failed with non-linkage errors - aborting Phase 2."));
            return false;
        }

        // Pass 2 - NO INI modification, just compile
        _logger.LogInformation(LogColors.PhaseHeader("PHASE 2: FINAL LINKAGE (Resolving cross-package dependencies)"));
        if (!await ExecuteCompilationPassAsync(options, 2, dependentPackages, ct))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Determines if Phase 2 should run based on Phase 1 compilation results.
    /// Phase 2 runs ONLY if:
    /// - Phase 1 succeeded, OR
    /// - Phase 1 failed with linkage errors (dependent package not found) AND:
    ///   - All dependent packages compiled (seen in output)
    ///   - Main mod .u was generated (linkage failure, not compilation failure)
    /// </summary>
    private bool ShouldRunPhase2(bool phase1Success, List<string> dependentPackages, BuildOptions options)
    {
        // If Phase 1 succeeded, no need for Phase 2 (but we still run it for completeness)
        if (phase1Success)
        {
            _logger.LogInformation(LogColors.Debug("Phase 1 succeeded - running Phase 2 for completeness."));
            return true;
        }

        // Phase 1 failed - check if it's a linkage failure (expected for two-pass)
        // Condition 1: Look for the specific linkage error pattern with .u file mentioned
        var linkageErrorFound = _receiver.OutputContains("Could not load existing package file") && 
                                _receiver.OutputContains($"{options.ModNameCanonical}.u");
        
        // Condition 2: All dependent packages were compiled (seen in output)
        var dependentPackagesCompiled = dependentPackages.All(pkg => 
            _receiver.OutputContains($"--------------------{pkg}"));
        
        // Condition 3: Main mod .u was generated (proves compilation succeeded, only linkage failed)
        var mainModBinaryExists = File.Exists(Path.Combine(options.SdkPath, "XComGame", "Script", $"{options.ModNameCanonical}.u"));
        
        if (linkageErrorFound && dependentPackagesCompiled && mainModBinaryExists)
        {
            _logger.LogInformation(LogColors.Info("Linkage error detected - Phase 2 will resolve cross-package dependencies."));
            return true;
        }

        // Other errors (syntax, missing sources, etc.) - do NOT run Phase 2
        _logger.LogWarning(LogColors.Warning("Phase 1 failed with non-linkage errors."));
        if (!linkageErrorFound)
            _logger.LogWarning(LogColors.Debug("  - No linkage error message found."));
        if (!dependentPackagesCompiled)
            _logger.LogWarning(LogColors.Debug("  - Not all dependent packages were compiled."));
        if (!mainModBinaryExists)
            _logger.LogWarning(LogColors.Debug("  - Main mod .u file was not generated."));
        
        return false;
    }

    private async Task<bool> ExecuteCompilationPassAsync(BuildOptions options, int passNumber, List<string> dependentPackages, CancellationToken ct)
    {
        // 1. Compile Base (Always needed for environment stability)
        if (!await _compiler.CompileBaseAsync(options, _receiver, ct)) return false;

        // 2. Compile Mod
        if (passNumber == 2) await Task.Delay(BuildConstants.FileHandleClearDelayMs, ct); // Small delay to clear file handles

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
                _logger.LogInformation(LogColors.Separator);
                _logger.LogInformation(LogColors.Info($"# [PHASE 1] EXPECTED dependency failure in: {compileTarget}"));
                _logger.LogInformation(LogColors.Info($"# [PHASE 1] Binary '{options.ModNameCanonical}.u' was created successfully."));
                _logger.LogInformation(LogColors.Info("# Proceeding to Phase 2 for final linkage of dependent packages..."));
                _logger.LogInformation(LogColors.Separator);
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

        return success;
    }
}
