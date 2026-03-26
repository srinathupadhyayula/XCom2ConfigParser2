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
    private readonly OutputReceiver _receiver;
    private readonly ILogger<CompilationStep> _logger;

    /// <summary>
    /// Gets a value indicating whether two-pass compilation was performed.
    /// This is used by BuildController to determine if INI restoration is needed.
    /// </summary>
    public bool UsedTwoPass { get; private set; }

    public CompilationStep(
        ScriptCompiler compiler,
        OutputReceiver receiver,
        ILogger<CompilationStep> logger)
    {
        _compiler = compiler;
        _receiver = receiver;
        _logger = logger;
    }

    public string Name => "Script Compilation";

    public async Task<bool> ExecuteAsync(BuildOptions options, CancellationToken ct)
    {
        _logger.LogInformation(Chalk.Bold.Cyan[$">>> Starting compilation step for {options.ModNameCanonical}..."]);

        // Use dependent packages detected by PrepareIniStep (or from CLI/settings.json)
        var dependentPackages = options.DependentPackages;
        bool twoPassRequired = dependentPackages.Count > 0 || options.TwoPassCompilation;

        _logger.LogInformation(Chalk.Magenta[$"[DEBUG] Dependent Packages for Compilation: {dependentPackages.Count}"]);
        foreach (var pkg in dependentPackages)
        {
            _logger.LogInformation(Chalk.Gray[$"  -> {pkg}"]);
        }

        _logger.LogInformation(Chalk.Magenta[$"[DEBUG] Two-Pass Required: {twoPassRequired}"]);

        if (twoPassRequired)
        {
            _logger.LogInformation(Chalk.Bold.Green["[MODE] Two-Pass compilation flow"]);

            UsedTwoPass = true;
            return await ExecuteTwoPassAsync(options, dependentPackages, ct);
        }
        else
        {
            _logger.LogInformation(Chalk.Bold.Yellow["[MODE] Single-Pass compilation flow (build_common parity)"]);
            UsedTwoPass = false;
            return await ExecuteSinglePassAsync(options, ct);
        }
    }

    private async Task<bool> ExecuteTwoPassAsync(BuildOptions options,
        List<string> dependentPackages, CancellationToken ct)
    {
        // INI was already modified by PrepareIniStep - just compile

        // Pass 1
        _logger.LogInformation(Chalk.Bold.Yellow["PHASE 1: INITIAL COMPILATION (Building base and all mod packages)"]);
        bool phase1Success = await ExecuteCompilationPassAsync(options, 1, dependentPackages, ct);

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

        return success;
    }
}
