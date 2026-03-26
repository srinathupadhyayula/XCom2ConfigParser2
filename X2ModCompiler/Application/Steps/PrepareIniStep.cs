using Kokuban;
using Microsoft.Extensions.Logging;
using X2ModCompiler.Configuration;
using X2ModCompiler.Utilities;

namespace X2ModCompiler.Application.Steps;

/// <summary>
/// Prepares the XComEngine.ini file for two-pass compilation by ensuring all required packages
/// are listed in ModEditPackages. This step only runs for two-pass builds.
/// 
/// For single-pass compilation: NO INI modification (pure build_common parity)
/// For two-pass compilation: INI is modified once before Phase 1, restored after Phase 2
/// </summary>
public class PrepareIniStep : IBuildStep
{
    private readonly IniHandler _iniHandler;
    private readonly ILogger<PrepareIniStep> _logger;

    public PrepareIniStep(IniHandler iniHandler, ILogger<PrepareIniStep> logger)
    {
        _iniHandler = iniHandler;
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

        // Check if two-pass is required
        var currentContent = await File.ReadAllTextAsync(targetIni, ct);
        var dependentPackages = _iniHandler.GetDependantPackages(currentContent);
        bool twoPassRequired = _iniHandler.IsTwoPassNeeded(currentContent) || options.TwoPassCompilation;

        if (!twoPassRequired)
        {
            _logger.LogInformation(Chalk.Gray["[INI] Single-pass compilation - NO INI modification (build_common parity)."]);
            return true;
        }

        // Two-pass: Prepare INI with all packages before Phase 1
        _logger.LogInformation(Chalk.Cyan["[INI] Two-pass detected - preparing INI for compilation..."]);
        
        var preparedContent = _iniHandler.PrepareModCompilationIni(currentContent, options.ModNameCanonical, dependentPackages);
        await File.WriteAllTextAsync(targetIni, preparedContent, ct);

        _logger.LogInformation(Chalk.Green["[INI] INI preparation complete. Ready for two-pass compilation."]);
        return true;
    }
}
