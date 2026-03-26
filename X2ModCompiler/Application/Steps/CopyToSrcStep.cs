using Kokuban;
using Microsoft.Extensions.Logging;
using X2ModCompiler.Configuration;

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

        // 1. Mirror SrcOrig to Src (reset SDK to clean state)
        _logger.LogInformation(Chalk.Cyan["Mirroring SrcOrig to Src..."]);
        await MirrorSrcOrigToSrcAsync(options.SdkPath, ct);
        _logger.LogInformation(Chalk.Green["Mirrored SrcOrig to Src."]);

        // 2. Copy dependency sources (IncludePaths)
        if (options.IncludePaths.Count > 0)
        {
            _logger.LogInformation(Chalk.Cyan["Copying dependency sources to Src..."]);
            foreach (var includePath in options.IncludePaths)
            {
                if (Directory.Exists(includePath))
                {
                    _logger.LogInformation($"  Including: {includePath}");
                    await CopySrcFolderAsync(includePath, devSrcRoot, ct);
                }
                else
                {
                    _logger.LogWarning($"  Include path does not exist: {includePath}");
                }
            }
            _logger.LogInformation(Chalk.Green["Copied dependency sources to Src."]);
        }

        // 3. Copy mod sources to Src
        var modSrcPath = Path.Combine(options.ModSrcRoot, "Src");
        if (Directory.Exists(modSrcPath))
        {
            _logger.LogInformation(Chalk.Cyan["Copying mod sources to Src..."]);
            await CopySrcFolderAsync(modSrcPath, devSrcRoot, ct);
            _logger.LogInformation(Chalk.Green["Copied mod sources to Src."]);
        }
        else
        {
            _logger.LogWarning(Chalk.Yellow[$"Mod source folder not found: {modSrcPath}"]);
        }

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
            _logger.LogWarning(Chalk.Yellow[$"SrcOrig folder not found: {srcOrigPath}"]);
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
            _logger.LogError(Chalk.Red[$"Robocopy failed with exit code {process.ExitCode}"]);
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
    private async Task CopySrcFolderAsync(string includeDir, string devSrcRoot, CancellationToken ct)
    {
        // Copy all files and folders recursively
        await CopyDirectoryRecursiveAsync(includeDir, devSrcRoot, ct);

        // Check for extra_globals.uci and append to Globals.uci
        var extraGlobalsFile = Path.Combine(includeDir, "extra_globals.uci");
        if (File.Exists(extraGlobalsFile))
        {
            var globalsPath = Path.Combine(devSrcRoot, "Core", "Globals.uci");
            
            _logger.LogInformation($"  Processing extra_globals.uci: {extraGlobalsFile}");
            
            // Append comment and contents to Globals.uci
            var extraContent = await File.ReadAllTextAsync(extraGlobalsFile, ct);
            var appendContent = $"// Macros included from {extraGlobalsFile}{Environment.NewLine}{extraContent}{Environment.NewLine}";
            
            await File.AppendAllTextAsync(globalsPath, appendContent, ct);
            _logger.LogInformation($"  Appended extra_globals.uci to {globalsPath}");
        }
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
