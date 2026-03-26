using Microsoft.Extensions.Logging;
using X2ModCompiler.Configuration;

namespace X2ModCompiler.Compilation;

/// <summary>
/// Handles cleaning of compiled script packages (.u files) from the SDK and Game directories.
/// Used for selective cleanup before compilation when source files have been modified.
/// </summary>
public class ScriptCleaner
{
    private readonly ILogger<ScriptCleaner> _logger;

    public ScriptCleaner(ILogger<ScriptCleaner> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Cleans compiled .u files for the specified packages from the SDK script directories.
    /// </summary>
    /// <param name="sdkPath">The SDK path.</param>
    /// <param name="packageNames">List of package names to clean.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task CleanPackagesAsync(string sdkPath, IEnumerable<string> packageNames, CancellationToken ct = default)
    {
        var scriptDirs = new[]
        {
            Path.Combine(sdkPath, "XComGame", "Script"),
            Path.Combine(sdkPath, "XComGame", "ScriptFinalRelease")
        };

        foreach (var packageName in packageNames)
        {
            if (ct.IsCancellationRequested) return;

            foreach (var scriptDir in scriptDirs)
            {
                var packagePath = Path.Combine(scriptDir, $"{packageName}.u");
                if (File.Exists(packagePath))
                {
                    _logger.LogDebug($"Cleaning {packagePath}...");
                    try
                    {
                        File.Delete(packagePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to delete {packagePath}: {ex.Message}");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Cleans all mod script packages from SDK and Game directories.
    /// Mimics _CleanLeftoverScripts from build_common.ps1.
    /// </summary>
    /// <param name="options">Build options.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task CleanAllModPackagesAsync(BuildOptions options, CancellationToken ct = default)
    {
        var packages = GetAllScriptPackages(options);
        var allPaths = new List<string>();

        foreach (var packageName in packages)
        {
            allPaths.Add(Path.Combine(options.SdkPath, "XComGame", "Script", $"{packageName}.u"));
            allPaths.Add(Path.Combine(options.SdkPath, "XComGame", "ScriptFinalRelease", $"{packageName}.u"));
            allPaths.Add(Path.Combine(options.GamePath, "XComGame", "Script", $"{packageName}.u"));
        }

        foreach (var path in allPaths)
        {
            if (ct.IsCancellationRequested) return;

            if (File.Exists(path))
            {
                _logger.LogDebug($"Cleaning {path}...");
                try
                {
                    File.Delete(path);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to delete {path}: {ex.Message}");
                }
            }
        }
    }

    private List<string> GetAllScriptPackages(BuildOptions options)
    {
        var packages = new List<string> { options.ModNameCanonical };
        packages.AddRange(options.DependentPackages.Where(p => !packages.Contains(p)));
        return packages;
    }
}
