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
        _logger.LogInformation(LogColors.Separator);
        _logger.LogInformation(LogColors.Info("PREPARE INI STEP - Detailed Logging"));
        _logger.LogInformation(LogColors.Separator);

        var targetIni = _iniHandler.FindTargetIni();
        if (targetIni == null)
        {
            _logger.LogWarning(LogColors.Warning("[INI] No target INI found - skipping preparation."));
            return true;
        }

        _logger.LogInformation(LogColors.Info($"Target INI: {targetIni}"));

        // Check if two-pass is required BEFORE modifying INI
        var currentContent = await File.ReadAllTextAsync(targetIni, ct);
        var dependentPackages = _iniHandler.GetDependantPackages(currentContent);
        bool twoPassRequired = _iniHandler.IsTwoPassNeeded(currentContent) || options.TwoPassCompilation;

        _logger.LogInformation(LogColors.Info($"[X2ModCompiler.DependantPackages] section content:"));
        foreach (var pkg in dependentPackages)
        {
            _logger.LogInformation(LogColors.PackageName(pkg));
        }

        _logger.LogInformation(LogColors.Info($"Two-pass required: {twoPassRequired}"));
        _logger.LogInformation(LogColors.Info($"options.TwoPassCompilation: {options.TwoPassCompilation}"));
        _logger.LogInformation(LogColors.Info($"options.DependentPackages (from CLI/settings): {string.Join(", ", options.DependentPackages)}"));

        // SINGLE-PASS PATH: Do NOTHING - this matches build_common.ps1 exactly
        if (!twoPassRequired)
        {
            _logger.LogInformation(LogColors.Debug("[INI] Single-pass compilation - NO INI modification (build_common parity)."));
            _logger.LogInformation(LogColors.Debug("[INI] User's existing ModEditPackages will be used as-is."));
            _logger.LogInformation(LogColors.SuccessSeparator);
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
                    _logger.LogInformation(LogColors.Success($"Added {pkg} to options.DependentPackages"));
                }
            }
            _logger.LogInformation(LogColors.Info($"[INI] Detected {dependentPackages.Count} dependent packages from INI for two-pass compilation."));
        }

        // Two-pass: Prepare INI with all packages before Phase 1
        _logger.LogInformation(LogColors.Info("[INI] Two-pass detected - preparing INI for compilation..."));

        _logger.LogInformation(LogColors.Info("[INI] BEFORE modification - ModEditPackages:"));
        var beforeModEditPackages = ExtractModEditPackages(currentContent);
        foreach (var line in beforeModEditPackages)
        {
            _logger.LogInformation(LogColors.Info($"  {line}"));
        }
        
        var preparedContent = _iniHandler.PrepareModCompilationIni(currentContent, options.ModNameCanonical, dependentPackages);
        await File.WriteAllTextAsync(targetIni, preparedContent, ct);
        
        // Force flush to ensure SDK sees the changes
        using (var stream = File.Open(targetIni, FileMode.Open, FileAccess.Write, FileShare.None))
        {
            await stream.FlushAsync(ct);
        }
        
        _logger.LogInformation(LogColors.Info("[INI] AFTER modification - ModEditPackages:"));
        var afterModEditPackages = ExtractModEditPackages(preparedContent);
        foreach (var line in afterModEditPackages)
        {
            _logger.LogInformation(LogColors.Info($"  {line}"));
        }

        _logger.LogInformation(LogColors.Success("[INI] INI preparation complete. Ready for two-pass compilation."));
        _logger.LogInformation(LogColors.SuccessSeparator);
        return true;
    }

    private List<string> ExtractModEditPackages(string iniContent)
    {
        var result = new List<string>();
        var lines = iniContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        bool inEngineSection = false;
        
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Equals("[UnrealEd.EditorEngine]", StringComparison.OrdinalIgnoreCase))
            {
                inEngineSection = true;
                continue;
            }
            if (inEngineSection)
            {
                if (trimmed.StartsWith("[")) break;
                if (trimmed.Contains("ModEditPackages"))
                {
                    result.Add(trimmed);
                }
            }
        }
        
        return result;
    }
}
