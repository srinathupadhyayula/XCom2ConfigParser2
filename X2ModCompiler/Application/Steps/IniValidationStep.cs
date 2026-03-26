using Kokuban;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using X2ModCompiler.Configuration;
using X2ModCompiler.Utilities;

namespace X2ModCompiler.Application.Steps;

/// <summary>
/// Validates that critical XComEngine.ini sections are consolidated into a single file.
/// This prevents configuration fragmentation that can lead to subtle build errors.
/// </summary>
public class IniValidationStep : BuildStepBase
{
    public IniValidationStep(ILoggerFactory loggerFactory)
        : base("INI Consolidation Validation", loggerFactory.CreateLogger<IniValidationStep>())
    {
    }

    protected override async Task<bool> ExecuteStepAsync(BuildOptions options, CancellationToken ct)
    {
        _logger.LogInformation(LogColors.Info("Validating INI consolidation across roots..."));

        var iniFiles = DiscoverIniFiles(options);
        if (iniFiles.Count <= 1)
        {
            _logger.LogInformation(LogColors.Debug($"Found {iniFiles.Count} XComEngine.ini files. Validation skipped (consolidation inherent)."));
            return true;
        }

        var sectionMap = new Dictionary<string, List<string>>();
        string[] criticalSections = { "[Engine.ScriptPackages]", "[UnrealEd.EditorEngine]", "[X2ModCompiler.DependantPackages]" };

        foreach (var file in iniFiles)
        {
            try
            {
                var content = await File.ReadAllLinesAsync(file, ct);
                foreach (var line in content)
                {
                    var trimmed = line.Trim();
                    foreach (var section in criticalSections)
                    {
                        if (trimmed.Equals(section, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!sectionMap.ContainsKey(section)) sectionMap[section] = new List<string>();
                            sectionMap[section].Add(file);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(LogColors.Error($"Failed to read INI file {file}: {ex.Message}"));
                return false;
            }
        }

        // Check if sections are fragmented
        var uniqueFiles = sectionMap.Values.SelectMany(x => x).Distinct().ToList();
        
        if (uniqueFiles.Count > 1)
        {
            _logger.LogError(LogColors.Error("Critical sections are spread across multiple XComEngine.ini files."));
            _logger.LogError(LogColors.Error("Please consolidate them into a single file to ensure reliable building."));
            
            foreach (var file in uniqueFiles)
            {
                _logger.LogWarning(LogColors.Warning($"  -> {file}"));
            }
            
            return false;
        }

        _logger.LogInformation(LogColors.Success("INI consolidation validation passed."));
        return true;
    }

    private List<string> DiscoverIniFiles(BuildOptions options)
    {
        var files = new List<string>();
        foreach (var root in options.IniRoots)
        {
            if (Directory.Exists(root))
            {
                var found = Directory.GetFiles(root, "XComEngine.ini", SearchOption.AllDirectories);
                files.AddRange(found);
            }
        }
        return files.Distinct().ToList();
    }
}
