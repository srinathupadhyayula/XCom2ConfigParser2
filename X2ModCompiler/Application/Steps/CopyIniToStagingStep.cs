using Kokuban;
using Microsoft.Extensions.Logging;
using X2ModCompiler.Configuration;

namespace X2ModCompiler.Application.Steps;

/// <summary>
/// Copies the modified XComEngine.ini from the mod's Config folder to the staging directory.
/// This ensures the SDK reads the correct ModEditPackages during compilation.
/// 
/// This step must run AFTER PrepareIniStep (which modifies the INI) and BEFORE CompilationStep.
/// </summary>
public class CopyIniToStagingStep : IBuildStep
{
    private readonly ILogger<CopyIniToStagingStep> _logger;

    public CopyIniToStagingStep(ILogger<CopyIniToStagingStep> logger)
    {
        _logger = logger;
    }

    public string Name => "Copy INI to Staging";

    public async Task<bool> ExecuteAsync(BuildOptions options, CancellationToken ct)
    {
        // Find the modified XComEngine.ini in the mod's Config folder
        var modConfigPath = System.IO.Path.Combine(options.ModSrcRoot, "Config");
        if (!System.IO.Directory.Exists(modConfigPath))
        {
            _logger.LogInformation(Chalk.Gray["[INI] Mod Config folder not found - skipping INI copy to staging."]);
            return true;
        }

        var iniFiles = System.IO.Directory.GetFiles(modConfigPath, "XComEngine.ini", System.IO.SearchOption.AllDirectories);
        if (iniFiles.Length == 0)
        {
            _logger.LogInformation(Chalk.Gray["[INI] No XComEngine.ini found in mod Config - skipping copy to staging."]);
            return true;
        }

        // Use the first INI file found (should be the one PrepareIniStep modified)
        var sourceIni = iniFiles[0];
        var stagingIni = System.IO.Path.Combine(options.StagingPath, "Config", "XComEngine.ini");

        _logger.LogInformation(Chalk.Cyan[$"[INI] Copying modified INI to staging..."]);
        _logger.LogInformation(Chalk.Cyan[$"  Source: {sourceIni}"]);
        _logger.LogInformation(Chalk.Cyan[$"  Destination: {stagingIni}"]);

        // Ensure staging Config directory exists
        var stagingConfigDir = System.IO.Path.GetDirectoryName(stagingIni);
        if (stagingConfigDir != null && !System.IO.Directory.Exists(stagingConfigDir))
        {
            System.IO.Directory.CreateDirectory(stagingConfigDir);
        }

        // Copy the INI file (overwrite if exists)
        await System.Threading.Tasks.Task.Run(() => System.IO.File.Copy(sourceIni, stagingIni, overwrite: true), ct);

        _logger.LogInformation(Chalk.Green[$"[INI] Copied INI to staging."]);
        return true;
    }
}
