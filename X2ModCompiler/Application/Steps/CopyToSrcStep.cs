using Kokuban;
using Microsoft.Extensions.Logging;
using X2ModCompiler.Configuration;
using X2ModCompiler.Utilities;

namespace X2ModCompiler.Application.Steps;

/// <summary>
/// Copies mod sources and dependencies to the SDK's Development\Src folder.
/// This step mimics _CopyToSrc() from build_common.ps1.
/// 
/// Key operations:
/// 1. Mirrors SDK's SrcOrig to Src (resets SDK to clean state)
/// 2. Copies dependency sources (IncludePaths) to Src
/// 3. Copies mod sources to Src
/// 4. Processes extra_globals.uci files (appends macros to Globals.uci)
/// </summary>
public class CopyToSrcStep : IBuildStep
{
    private readonly ILogger<CopyToSrcStep> _logger;

    public CopyToSrcStep(ILogger<CopyToSrcStep> logger)
    {
        _logger = logger;
    }

    public string Name => "Copy Sources to SDK";

    public async Task<bool> ExecuteAsync(BuildOptions options, CancellationToken ct)
    {
        var devSrcRoot = Path.Combine(options.SdkPath, "Development", "Src");

        _logger.LogInformation(LogColors.Separator);
        _logger.LogInformation(LogColors.Info("COPY TO SRC STEP - Detailed Logging"));
        _logger.LogInformation(LogColors.Separator);
        _logger.LogInformation(LogColors.Info($"SDK Path: {options.SdkPath}"));
        _logger.LogInformation(LogColors.Info($"Dev Src Root: {devSrcRoot}"));
        _logger.LogInformation(LogColors.Info($"Mod Src Root: {options.ModSrcRoot}"));
        _logger.LogInformation(LogColors.Info($"Include Paths Count: {options.IncludePaths.Count}"));
        
        foreach (var includePath in options.IncludePaths)
        {
            _logger.LogInformation(LogColors.PackageName(includePath));
        }
        _logger.LogInformation(LogColors.Separator);

        // 1. Mirror SrcOrig to Src (reset SDK to clean state)
        _logger.LogInformation(LogColors.Info("[Step 1] Mirroring SrcOrig to Src..."));
        await MirrorSrcOrigToSrcAsync(options.SdkPath, ct);
        _logger.LogInformation(LogColors.Success("[Step 1] Mirrored SrcOrig to Src."));

        // 2. Copy dependency sources (IncludePaths)
        if (options.IncludePaths.Count > 0)
        {
            _logger.LogInformation(LogColors.Info("[Step 2] Copying dependency sources to Src..."));
            foreach (var includePath in options.IncludePaths)
            {
                if (Directory.Exists(includePath))
                {
                    _logger.LogInformation(LogColors.Success($"  Including: {includePath}"));
                    var packageCount = await CopySrcFolderAsync(includePath, devSrcRoot, ct);
                    _logger.LogInformation(LogColors.Success($"    Copied {packageCount} package(s) from {includePath}"));
                }
                else
                {
                    _logger.LogWarning(LogColors.Warning($"  Include path does not exist: {includePath}"));
                }
            }
            _logger.LogInformation(LogColors.Success("[Step 2] Copied dependency sources to Src."));
        }
        else
        {
            _logger.LogInformation(LogColors.Debug("[Step 2] No IncludePaths specified - skipping dependency sources."));
        }

        // 3. Copy mod sources to Src
        var modSrcPath = Path.Combine(options.ModSrcRoot, "Src");
        _logger.LogInformation(LogColors.Info("[Step 3] Copying mod sources to Src..."));
        _logger.LogInformation(LogColors.Info($"  Mod Src Path: {modSrcPath}"));
        
        if (Directory.Exists(modSrcPath))
        {
            var packageCount = await CopySrcFolderAsync(modSrcPath, devSrcRoot, ct);
            _logger.LogInformation(LogColors.Success($"  Copied {packageCount} package(s) from {modSrcPath}"));

            // List all packages copied from mod Src
            var modPackages = Directory.GetDirectories(modSrcPath);
            _logger.LogInformation(LogColors.Info("  Mod packages copied:"));
            foreach (var pkg in modPackages)
            {
                _logger.LogInformation(LogColors.Info($"    - {Path.GetFileName(pkg)}"));
            }
        }
        else
        {
            _logger.LogWarning(LogColors.Warning($"  Mod source folder not found: {modSrcPath}"));
        }

        // 4. Final summary - list all packages in Development\Src
        _logger.LogInformation(LogColors.Info("[Step 4] Final package inventory in Development\\Src:"));
        if (Directory.Exists(devSrcRoot))
        {
            var allPackages = Directory.GetDirectories(devSrcRoot);
            _logger.LogInformation(LogColors.Info($"  Total packages: {allPackages.Length}"));
            foreach (var pkg in allPackages.OrderBy(p => Path.GetFileName(p)))
            {
                _logger.LogInformation(LogColors.Info($"    - {Path.GetFileName(pkg)}"));
            }
        }
        else
        {
            _logger.LogWarning(LogColors.Warning("  Development\\Src directory does not exist!"));
        }

        _logger.LogInformation(LogColors.Success("========================================"));
        _logger.LogInformation(LogColors.Success("COPY TO SRC STEP COMPLETE"));
        _logger.LogInformation(LogColors.Success("========================================"));

        return true;
    }

    /// <summary>
    /// Mirrors the SDK's SrcOrig folder to Src, resetting the SDK to a clean state.
    /// Uses robocopy with the same arguments as build_common.ps1.
    /// </summary>
    private async Task MirrorSrcOrigToSrcAsync(string sdkPath, CancellationToken ct)
    {
        var srcOrigPath = Path.Combine(sdkPath, "Development", "SrcOrig");
        var devSrcRoot = Path.Combine(sdkPath, "Development", "Src");

        if (!Directory.Exists(srcOrigPath))
        {
            _logger.LogWarning(LogColors.Warning($"SrcOrig folder not found: {srcOrigPath}"));
            return;
        }

        // Ensure destination exists
        if (!Directory.Exists(devSrcRoot))
        {
            Directory.CreateDirectory(devSrcRoot);
        }

        // Robocopy arguments matching build_common.ps1 $global:def_robocopy_args
        // /S /E /COPY:DAT /PURGE /MIR /NP /R:1000000 /W:30
        // But we only want .uc and .uci files
        var robocopyArgs = $"\"{srcOrigPath}\" \"{devSrcRoot}\" *.uc *.uci /S /E /COPY:DAT /PURGE /MIR /NP /R:1000000 /W:30";
        
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
        }
    }

    /// <summary>
    /// Copies the contents of a source folder to the SDK's Development\Src folder.
    /// Mimics _CopySrcFolder() from build_common.ps1.
    /// </summary>
    /// <returns>Number of packages copied</returns>
    private async Task<int> CopySrcFolderAsync(string includeDir, string devSrcRoot, CancellationToken ct)
    {
        _logger.LogInformation(LogColors.Info($"  CopySrcFolder: {includeDir} → {devSrcRoot}"));
        
        // Copy all files and folders recursively
        await CopyDirectoryRecursiveAsync(includeDir, devSrcRoot, ct);
        
        // Count packages copied
        var packageCount = Directory.GetDirectories(includeDir).Length;

        // Check for extra_globals.uci and append to Globals.uci
        var extraGlobalsFile = Path.Combine(includeDir, "extra_globals.uci");
        if (File.Exists(extraGlobalsFile))
        {
            var globalsPath = Path.Combine(devSrcRoot, "Core", "Globals.uci");
            
            _logger.LogInformation(LogColors.Info($"    Processing extra_globals.uci: {extraGlobalsFile}"));
            _logger.LogInformation(LogColors.Info($"    Appending to: {globalsPath}"));
            
            // Append comment and contents to Globals.uci
            var extraContent = await File.ReadAllTextAsync(extraGlobalsFile, ct);
            var appendContent = $"// Macros included from {extraGlobalsFile}{Environment.NewLine}{extraContent}{Environment.NewLine}";
            
            await File.AppendAllTextAsync(globalsPath, appendContent, ct);
            _logger.LogInformation(LogColors.Success("    Appended extra_globals.uci to Globals.uci"));
        }
        
        return packageCount;
    }

    /// <summary>
    /// Recursively copies a directory, preserving structure.
    /// </summary>
    private async Task CopyDirectoryRecursiveAsync(string sourceDir, string destDir, CancellationToken ct)
    {
        // Create destination directory
        if (!Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
        }

        // Copy files
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            ct.ThrowIfCancellationRequested();
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            // Use Task.Run for synchronous Copy to avoid blocking
            await Task.Run(() => File.Copy(file, destFile, overwrite: true), ct);
        }

        // Copy subdirectories
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            ct.ThrowIfCancellationRequested();
            var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            await CopyDirectoryRecursiveAsync(dir, destSubDir, ct);
        }
    }
}
