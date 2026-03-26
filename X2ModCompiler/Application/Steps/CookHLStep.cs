using Microsoft.Extensions.Logging;
using X2ModCompiler.Configuration;
using X2ModCompiler.Utilities;
using Kokuban;

namespace X2ModCompiler.Application.Steps;

/// <summary>
/// Handles cooking of native script packages (Highlander).
/// This step mimics the _RunCookHL logic in build_common.ps1.
/// </summary>
public class CookHLStep : IBuildStep
{
    private readonly ILogger<CookHLStep> _logger;
    private readonly IProcessRunner _runner;
    private readonly IFileMirrorParity _mirror;

    private static readonly string[] NativePackages =
    {
        "XComGame", "Core", "Engine", "GFxUI", "AkAudio", "GameFramework",
        "UnrealEd", "GFxUIEditor", "IpDrv", "OnlineSubsystemPC",
        "OnlineSubsystemLive", "OnlineSubsystemSteamworks", "OnlineSubsystemPSN"
    };

    public CookHLStep(ILogger<CookHLStep> logger, IProcessRunner runner, IFileMirrorParity mirror)
    {
        _logger = logger;
        _runner = runner;
        _mirror = mirror;
    }

    public string Name => "Highlander Cooking";

    public async Task<bool> ExecuteAsync(BuildOptions options, CancellationToken ct)
    {
        var modPackages = GetModScriptPackages(options);
        var containsNative = modPackages.Any(p => NativePackages.Contains(p, StringComparer.OrdinalIgnoreCase));

        if (!containsNative)
        {
            _logger.LogInformation(Chalk.Gray["No native script packages detected. Skipping Highlander cooking."]);
            return true;
        }

        if (options.Debug)
        {
            _logger.LogInformation(Chalk.Yellow["Skipping Highlander cooking for debug build."]);
            return true;
        }

        _logger.LogInformation(Chalk.Cyan["Starting Highlander cooking..."]);

        // 1. Ensure CookedPCConsole exists in SDK
        var sdkCookedPCConsole = Path.Combine(options.SdkPath, "XComGame", "Published", "CookedPCConsole");
        if (!Directory.Exists(sdkCookedPCConsole))
        {
            Directory.CreateDirectory(sdkCookedPCConsole);
        }

        // 2. Copy base files from game to SDK Published dir if missing
        var gameCookedPCConsole = Path.Combine(options.GamePath, "XComGame", "CookedPCConsole");
        var baseFilesToCopy = new[] { "GuidCache.upk", "GlobalPersistentCookerData.upk", "PersistentCookerShaderData.bin" };
        
        foreach (var file in baseFilesToCopy)
        {
            var destPath = Path.Combine(sdkCookedPCConsole, file);
            if (!File.Exists(destPath))
            {
                var srcPath = Path.Combine(gameCookedPCConsole, file);
                if (File.Exists(srcPath))
                {
                    _logger.LogInformation($"Copying {file} to SDK Published dir...");
                    File.Copy(srcPath, destPath);
                }
            }
        }

        // 3. Mirror TFCs (Texture File Caches) - Don't overwrite existing ones (/XO /XN /XC logic)
        _logger.LogInformation("Copying Texture File Caches...");
        // Robocopy /XO /XN /XC mirrors files only if they are newer or non-existent
        // ModernFileMirror's SyncAsync might need a way to support "no-overwrite exists"
        // For parity, we'll just copy if not exists for now, or use robocopy directly since we have runner
        var robocopyArgs = $"\"{gameCookedPCConsole}\" \"{sdkCookedPCConsole}\" *.tfc /NJH /XT /XN /XO";
        await _runner.RunProcessAsync("robocopy.exe", robocopyArgs, ct: ct);

        // 4. Invoke CookPackages
        var cookArgs = "cookpackages -platform=pcconsole -quickanddirty -modcook -sha -multilanguagecook=INT+FRA+ITA+DEU+RUS+POL+KOR+ESN -singlethread -nopause";
        _logger.LogInformation(Chalk.Cyan["Invoking CookPackages (Highlander)..."]);
        var cookResult = await _runner.RunProcessAsync(options.CommandletPath, cookArgs, ct: ct);
        if (cookResult != 0)
        {
            _logger.LogError("Highlander cooking failed.");
            return false;
        }

        // 5. Copy cooked packages to staging
        var stagingCookedDir = Path.Combine(options.StagingPath, "CookedPCConsole");
        if (!Directory.Exists(stagingCookedDir))
        {
            Directory.CreateDirectory(stagingCookedDir);
        }

        foreach (var pkg in modPackages)
        {
            if (NativePackages.Contains(pkg, StringComparer.OrdinalIgnoreCase))
            {
                var cookedFile = Path.Combine(sdkCookedPCConsole, $"{pkg}.upk");
                var uncompressedSizeFile = Path.Combine(sdkCookedPCConsole, $"{pkg}.upk.uncompressed_size");

                if (File.Exists(cookedFile))
                {
                    _logger.LogInformation($"Staging cooked package {pkg}...");
                    File.Copy(cookedFile, Path.Combine(stagingCookedDir, $"{pkg}.upk"), true);
                    if (File.Exists(uncompressedSizeFile))
                    {
                        File.Copy(uncompressedSizeFile, Path.Combine(stagingCookedDir, $"{pkg}.upk.uncompressed_size"), true);
                    }
                }
            }
        }

        _logger.LogInformation(Chalk.Green["Highlander cooking complete."]);
        return true;
    }

    private List<string> GetModScriptPackages(BuildOptions options)
    {
        var modPackages = new List<string>();
        var modSrcPath = Path.Combine(options.ProjectRoot, options.ModName, "Src");
        if (Directory.Exists(modSrcPath))
        {
            var dirs = Directory.GetDirectories(modSrcPath);
            foreach (var dir in dirs)
            {
                modPackages.Add(Path.GetFileName(dir));
            }
        }
        return modPackages;
    }
}
