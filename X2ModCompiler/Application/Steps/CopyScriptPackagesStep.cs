using Kokuban;
using Microsoft.Extensions.Logging;
using X2ModCompiler.Configuration;

namespace X2ModCompiler.Application.Steps;

/// <summary>
/// Copies compiled script packages (.u files) to the staging directory.
/// This step mimics _CopyScriptPackages() from build_common.ps1.
/// 
/// For Highlander mods (native packages): Copies cooked .upk files from Published\CookedPCConsole
/// For regular mods: Copies .u files from SDK\XComGame\Script
/// </summary>
public class CopyScriptPackagesStep : IBuildStep
{
    private readonly ILogger<CopyScriptPackagesStep> _logger;

    private static readonly string[] NativePackages =
    {
        "XComGame", "Core", "Engine", "GFxUI", "AkAudio", "GameFramework",
        "UnrealEd", "GFxUIEditor", "IpDrv", "OnlineSubsystemPC",
        "OnlineSubsystemLive", "OnlineSubsystemSteamworks", "OnlineSubsystemPSN"
    };

    public CopyScriptPackagesStep(ILogger<CopyScriptPackagesStep> logger)
    {
        _logger = logger;
    }

    public string Name => "Copy Script Packages";

    public async Task<bool> ExecuteAsync(BuildOptions options, CancellationToken ct)
    {
        _logger.LogInformation(Chalk.Cyan["Copying compiled script packages to staging..."]);

        var packagesToCopy = new List<string> { options.ModNameCanonical };
        packagesToCopy.AddRange(options.DependentPackages.Where(p => !packagesToCopy.Contains(p)));

        var isHighlander = packagesToCopy.Any(p => NativePackages.Contains(p, StringComparer.OrdinalIgnoreCase));
        var cookHighlander = isHighlander && !options.Debug;

        foreach (var packageName in packagesToCopy)
        {
            ct.ThrowIfCancellationRequested();

            if (cookHighlander && NativePackages.Contains(packageName, StringComparer.OrdinalIgnoreCase))
            {
                // Highlander cooking was performed - copy cooked .upk files
                var cookedUpk = Path.Combine(options.CookerOutputPath, $"{packageName}.upk");
                var cookedSize = Path.Combine(options.CookerOutputPath, $"{packageName}.upk.uncompressed_size");
                var stagingCookedDir = Path.Combine(options.StagingPath, "CookedPCConsole");

                if (!Directory.Exists(stagingCookedDir))
                {
                    Directory.CreateDirectory(stagingCookedDir);
                }

                if (File.Exists(cookedUpk))
                {
                    await Task.Run(() => File.Copy(cookedUpk, Path.Combine(stagingCookedDir, $"{packageName}.upk"), overwrite: true), ct);
                    _logger.LogDebug($"Copied: {cookedUpk}");

                    if (File.Exists(cookedSize))
                    {
                        await Task.Run(() => File.Copy(cookedSize, Path.Combine(stagingCookedDir, $"{packageName}.upk.uncompressed_size"), overwrite: true), ct);
                    }
                }
                else
                {
                    _logger.LogWarning(Chalk.Yellow[$"Highlander package not found: {cookedUpk}"]);
                }
            }
            else
            {
                // Regular mod package - copy .u file from SDK Script directory
                var packagePath = Path.Combine(options.SdkPath, "XComGame", "Script", $"{packageName}.u");
                var stagingScriptDir = Path.Combine(options.StagingPath, "Script");

                if (!Directory.Exists(stagingScriptDir))
                {
                    Directory.CreateDirectory(stagingScriptDir);
                }

                if (File.Exists(packagePath))
                {
                    await Task.Run(() => File.Copy(packagePath, Path.Combine(stagingScriptDir, $"{packageName}.u"), overwrite: true), ct);
                    _logger.LogDebug($"Copied: {packagePath}");
                }
                else
                {
                    _logger.LogWarning(Chalk.Yellow[$"Package not found (may be merged): {packageName}.u"]);
                }
            }
        }

        _logger.LogInformation(Chalk.Green["Copied compiled script packages to staging."]);
        return true;
    }
}
