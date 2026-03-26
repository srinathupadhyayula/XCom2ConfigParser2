using Microsoft.Extensions.Logging;
using X2ModCompiler.Configuration;
using Kokuban;
using X2ModCompiler.Utilities;

namespace X2ModCompiler.Application.Steps;

/// <summary>
/// Cleans up compiled script packages (.u files) from the SDK and Game directories.
/// This step ensures that no stale compiled scripts interfere with subsequent builds or the game.
/// Mimics _CleanLeftoverScripts from build_common.ps1.
/// </summary>
public class ScriptCleanupStep : IBuildStep
{
    private readonly ILogger<ScriptCleanupStep> _logger;

    public ScriptCleanupStep(ILogger<ScriptCleanupStep> logger)
    {
        _logger = logger;
    }

    public string Name => "Script Cleanup";

    public async Task<bool> ExecuteAsync(BuildOptions options, CancellationToken ct)
    {
        _logger.LogInformation(LogColors.Info("Cleaning leftover script packages from SDK/Game directories..."));

        var packages = GetAllScriptPackages(options);
        
        foreach (var package in packages)
        {
            if (ct.IsCancellationRequested) return false;

            var paths = new[]
            {
                Path.Combine(options.SdkPath, "XComGame", "Script", $"{package}.u"),
                Path.Combine(options.SdkPath, "XComGame", "ScriptFinalRelease", $"{package}.u"),
                Path.Combine(options.GamePath, "XComGame", "Script", $"{package}.u")
            };

            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        _logger.LogDebug($"Deleting {path}...");
                        File.Delete(path);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to delete {path}: {ex.Message}");
                    }
                }
            }
        }

        _logger.LogInformation(LogColors.Success("Script cleanup complete."));
        return true;
    }

    private List<string> GetAllScriptPackages(BuildOptions options)
    {
        var packages = new List<string> { options.ModName };
        foreach (var dep in options.DependentPackages)
        {
            if (!packages.Contains(dep))
            {
                packages.Add(dep);
            }
        }
        return packages;
    }
}
