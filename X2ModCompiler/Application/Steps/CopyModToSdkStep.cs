using Kokuban;
using Microsoft.Extensions.Logging;
using X2ModCompiler.Configuration;

namespace X2ModCompiler.Application.Steps;

/// <summary>
/// Copies the mod project to the SDK staging directory.
/// This step mimics _CopyModToSdk() from build_common.ps1.
/// 
/// Key operations:
/// 1. Mirror mod project to SDK\XComGame\Mods\ModName\ (excluding .x2proj and ContentForCook)
/// 2. Create Script subdirectory for compiled packages
/// 3. Write XComMod metadata file
/// 4. Create CookedPCConsole directory for Highlander mods
/// </summary>
public class CopyModToSdkStep : IBuildStep
{
    private readonly ILogger<CopyModToSdkStep> _logger;

    public CopyModToSdkStep(ILogger<CopyModToSdkStep> logger)
    {
        _logger = logger;
    }

    public string Name => "Copy Mod to SDK";

    public async Task<bool> ExecuteAsync(BuildOptions options, CancellationToken ct)
    {
        _logger.LogInformation(Chalk.Cyan["Copying mod project to staging..."]);

        // Exclude .x2proj and content options JSON from copy
        var excludePatterns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "*.x2proj",
            string.IsNullOrEmpty(options.ContentOptionsJson) ? "" : options.ContentOptionsJson
        };

        // Mirror mod project to staging
        await MirrorModToSdkAsync(options, excludePatterns, ct);

        // Create Script directory for compiled packages
        var scriptDir = Path.Combine(options.StagingPath, "Script");
        if (!Directory.Exists(scriptDir))
        {
            Directory.CreateDirectory(scriptDir);
            _logger.LogDebug($"Created {scriptDir}");
        }

        // Write XComMod metadata
        await WriteXComModMetadataAsync(options, ct);

        // Create CookedPCConsole directory for Highlander mods (if needed)
        // This will be determined later based on package analysis

        _logger.LogInformation(Chalk.Green["Copied mod project to staging."]);
        return true;
    }

    /// <summary>
    /// Mirrors the mod project directory to the SDK staging location.
    /// Uses robocopy with the same arguments as build_common.ps1.
    /// </summary>
    private async Task MirrorModToSdkAsync(BuildOptions options, HashSet<string> excludePatterns, CancellationToken ct)
    {
        // Ensure staging directory exists
        if (!Directory.Exists(options.StagingPath))
        {
            Directory.CreateDirectory(options.StagingPath);
        }

        // Build robocopy arguments
        // /S /E /COPY:DAT /PURGE /MIR /NP /R:1000000 /W:30
        var excludeArgs = string.Join(" ", excludePatterns.Where(p => !string.IsNullOrEmpty(p)).Select(p => $"/XF \"{p}\""));
        var robocopyArgs = $"\"{options.ModSrcRoot}\" \"{options.StagingPath}\" *.* /S /E /COPY:DAT /PURGE /MIR /NP /R:1000000 /W:30 {excludeArgs} /XD \"ContentForCook\"";

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "robocopy.exe",
            Arguments = robocopyArgs,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new System.Diagnostics.Process { StartInfo = startInfo };
        process.Start();

        // Read output to prevent buffer deadlock
        var output = await process.StandardOutput.ReadToEndAsync(ct);
        var error = await process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);

        // Robocopy exit codes: 0-7 generally indicate success
        if (process.ExitCode > 7)
        {
            _logger.LogError(Chalk.Red[$"Robocopy failed with exit code {process.ExitCode}"]);
            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogError(error);
            }
            throw new Exception($"Robocopy failed with exit code {process.ExitCode}");
        }
    }

    /// <summary>
    /// Writes the XComMod metadata file.
    /// Mimics the PowerShell logic for writing mod metadata.
    /// </summary>
    private async Task WriteXComModMetadataAsync(BuildOptions options, CancellationToken ct)
    {
        var xcomModPath = Path.Combine(options.StagingPath, $"{options.ModNameCanonical}.XComMod");

        // Read metadata from .x2proj if available
        var x2projPath = Path.Combine(options.ModSrcRoot, $"{options.ModNameCanonical}.x2proj");
        string title = options.ModNameCanonical;
        string description = "";
        long publishedId = options.WorkshopId;

        if (File.Exists(x2projPath))
        {
            try
            {
                var xml = System.Xml.Linq.XDocument.Load(x2projPath);
                var propertyGroup = xml.Descendants("PropertyGroup").FirstOrDefault();
                if (propertyGroup != null)
                {
                    var nameElement = propertyGroup.Element("Name");
                    var descElement = propertyGroup.Element("Description");
                    var steamIdElement = propertyGroup.Element("SteamPublishID");

                    if (nameElement != null && !string.IsNullOrEmpty(nameElement.Value))
                        title = nameElement.Value;
                    if (descElement != null && !string.IsNullOrEmpty(descElement.Value))
                        description = descElement.Value;
                    if (steamIdElement != null && long.TryParse(steamIdElement.Value, out var id) && id > 0)
                        publishedId = id;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(Chalk.Yellow[$"Failed to read metadata from .x2proj: {ex.Message}"]);
            }
        }

        // Override with CLI-provided workshop ID if specified
        if (options.WorkshopId > 0)
        {
            publishedId = options.WorkshopId;
        }

        // Write XComMod file
        var content = $"[mod]{Environment.NewLine}publishedFileId={publishedId}{Environment.NewLine}Title={title}{Environment.NewLine}Description={description}{Environment.NewLine}RequiresXPACK=true";
        await File.WriteAllTextAsync(xcomModPath, content, ct);

        _logger.LogDebug($"Written XComMod metadata: {xcomModPath}");
    }
}
