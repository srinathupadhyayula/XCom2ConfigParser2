using Kokuban;
using Microsoft.Extensions.Logging;
using X2ModCompiler.Configuration;
using X2ModCompiler.Utilities;

namespace X2ModCompiler.Application.Steps;

/// <summary>
/// Copies the staged mod from SDK to the final mod destination.
/// This step mimics _FinalCopy() from build_common.ps1.
/// 
/// Uses robocopy to mirror the staging directory to the final destination,
/// then removes the staging directory to complete the move operation.
/// </summary>
public class FinalCopyStep : IBuildStep
{
    private readonly ILogger<FinalCopyStep> _logger;

    public FinalCopyStep(ILogger<FinalCopyStep> logger)
    {
        _logger = logger;
    }

    public string Name => "Final Copy";

    public async Task<bool> ExecuteAsync(BuildOptions options, CancellationToken ct)
    {
        _logger.LogInformation(LogColors.Info("Copying built mod to final destination..."));

        // Ensure destination directory exists
        if (!Directory.Exists(options.FinalModPath))
        {
            Directory.CreateDirectory(options.FinalModPath);
        }

        // Use robocopy to mirror staging to final destination
        // /S /E /COPY:DAT /PURGE /MIR /NP /R:1000000 /W:30
        var robocopyArgs = $"\"{options.StagingPath}\" \"{options.FinalModPath}\" *.* /S /E /COPY:DAT /PURGE /MIR /NP /R:1000000 /W:30";

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
            _logger.LogError(LogColors.Error($"Robocopy failed with exit code {process.ExitCode}"));
            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogError(error);
            }
            return false;
        }

        // Verify destination was created properly before removing source
        if (Directory.Exists(options.FinalModPath))
        {
            var sourceItems = Directory.GetFiles(options.StagingPath, "*.*", SearchOption.AllDirectories).Length +
                             Directory.GetDirectories(options.StagingPath, "*", SearchOption.AllDirectories).Length;
            var destItems = Directory.GetFiles(options.FinalModPath, "*.*", SearchOption.AllDirectories).Length +
                           Directory.GetDirectories(options.FinalModPath, "*", SearchOption.AllDirectories).Length;

            if (destItems > 0 && destItems >= sourceItems)
            {
                _logger.LogInformation($"Removing staging directory: {options.StagingPath}");
                try
                {
                    Directory.Delete(options.StagingPath, recursive: true);
                    _logger.LogInformation(LogColors.Success("Source staging directory removed."));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(LogColors.Warning($"Failed to remove staging directory: {ex.Message}"));
                }
            }
            else
            {
                _logger.LogWarning(LogColors.Warning("Destination has fewer items than source - preserving staging directory for safety."));
            }
        }
        else
        {
            _logger.LogError(LogColors.Error("Destination directory was not created - preserving staging directory for safety."));
            return false;
        }

        _logger.LogInformation(LogColors.Success($"Copied built mod to {options.FinalModPath}"));
        return true;
    }
}
