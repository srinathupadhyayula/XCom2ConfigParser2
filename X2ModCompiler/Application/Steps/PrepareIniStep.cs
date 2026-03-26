using Kokuban;
using Microsoft.Extensions.Logging;
using X2ModCompiler.Configuration;
using X2ModCompiler.Utilities;

namespace X2ModCompiler.Application.Steps;

/// <summary>
/// Prepares the XComEngine.ini file for two-pass compilation ONLY.
/// 
/// SINGLE-PASS (build_common.ps1 parity):
///   - NO INI modification whatsoever
///   - User's existing ModEditPackages are used as-is
///   - This matches build_common.ps1 behavior exactly
/// 
/// TWO-PASS (C# enhancement):
///   - Modify INI once before Phase 1
///   - Restore INI once after Phase 2
///   - Total: 2 INI operations
/// 
/// Also detects two-pass requirement and stores it in BuildOptions for CompilationStep.
/// </summary>
public class PrepareIniStep : IBuildStep
{
    private readonly IniHandler _iniHandler;
    private readonly BuildOptions _options;
    private readonly ILogger<PrepareIniStep> _logger;

    public PrepareIniStep(IniHandler iniHandler, BuildOptions options, ILogger<PrepareIniStep> logger)
    {
        _iniHandler = iniHandler;
        _options = options;
        _logger = logger;
    }

    public string Name => "Prepare INI";

    public async Task<bool> ExecuteAsync(BuildOptions options, CancellationToken ct)
    {
        var targetIni = _iniHandler.FindTargetIni();
        if (targetIni == null)
        {
            _logger.LogWarning(Chalk.Yellow["[INI] No target INI found - skipping preparation."]);
            return true;
        }

        // Check if two-pass is required BEFORE modifying INI
        var currentContent = await File.ReadAllTextAsync(targetIni, ct);
        var dependentPackages = _iniHandler.GetDependantPackages(currentContent);
        bool twoPassRequired = _iniHandler.IsTwoPassNeeded(currentContent) || options.TwoPassCompilation;

        // SINGLE-PASS PATH: Do NOTHING - this matches build_common.ps1 exactly
        if (!twoPassRequired)
        {
            _logger.LogInformation(Chalk.Gray["[INI] Single-pass compilation - NO INI modification (build_common parity)."]);
            _logger.LogInformation(Chalk.Gray["[INI] User's existing ModEditPackages will be used as-is."]);
            return true;
        }

        // TWO-PASS PATH: Store detected dependent packages for CompilationStep
        if (dependentPackages.Count > 0)
        {
            // Add detected dependent packages to options so CompilationStep can use them
            foreach (var pkg in dependentPackages)
            {
                if (!options.DependentPackages.Contains(pkg))
                {
                    options.DependentPackages.Add(pkg);
                }
            }
            _logger.LogInformation(Chalk.Cyan[$"[INI] Detected {dependentPackages.Count} dependent packages from INI for two-pass compilation."]);
        }

        // Two-pass: Prepare INI with all packages before Phase 1
        _logger.LogInformation(Chalk.Cyan["[INI] Two-pass detected - preparing INI for compilation..."]);
        
        var preparedContent = _iniHandler.PrepareModCompilationIni(currentContent, options.ModNameCanonical, dependentPackages);
        await File.WriteAllTextAsync(targetIni, preparedContent, ct);

        _logger.LogInformation(Chalk.Green["[INI] INI preparation complete. Ready for two-pass compilation."]);
        return true;
    }
}
