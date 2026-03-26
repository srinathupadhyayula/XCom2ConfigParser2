using Microsoft.Extensions.Logging;
using X2ModCompiler.Configuration;
using X2ModCompiler.Utilities;

namespace X2ModCompiler.Application.Steps;

/// <summary>
/// Generates the standard .XComMod metadata file in the staging directory, which describes the mod for the launcher.
/// </summary>
public class MetadataStep : BuildStepBase
{
    public MetadataStep(ILogger<MetadataStep> logger)
        : base("Metadata Generation", logger)
    {
    }

    protected override async Task<bool> ExecuteStepAsync(BuildOptions options, CancellationToken ct)
    {
        _logger.LogInformation("Generating .XComMod metadata...");

        var xcomModPath = Path.Combine(options.StagingPath, $"{options.ModNameCanonical}.XComMod");

        // Ensure staging directory exists (in case previous steps were skipped or mocked)
        if (!Directory.Exists(options.StagingPath))
        {
            Directory.CreateDirectory(options.StagingPath);
        }

        var content = $"[mod]{Environment.NewLine}publishedFileId=-1{Environment.NewLine}Title={options.ModName}{Environment.NewLine}Description={Environment.NewLine}RequiresXPACK=true";

        await File.WriteAllTextAsync(xcomModPath, content, ct);

        return true;
    }
}
