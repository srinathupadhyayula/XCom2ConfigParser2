using System.Text;
using Microsoft.Extensions.Logging;
using XCom2ModCompiler.Configuration;
using XCom2ModCompiler.Utilities;

namespace XCom2ModCompiler.Compilation;

public class ScriptCompiler
{
    private readonly string _commandletPath;
    private readonly string _sdkPath;
    private readonly string _gamePath;
    private readonly ILogger<ScriptCompiler> _logger;
    private readonly IProcessRunner _runner;

    public ScriptCompiler(
        string commandletPath,
        string sdkPath,
        string gamePath,
        IProcessRunner runner,
        ILogger<ScriptCompiler> logger)
    {
        _commandletPath = commandletPath;
        _sdkPath = sdkPath;
        _gamePath = gamePath;
        _runner = runner;
        _logger = logger;
    }

    public virtual async Task<bool> CompileBaseAsync(
        BuildOptions options,
        OutputReceiver receiver,
        CancellationToken ct)
    {
        _logger.LogInformation("Compiling base packages...");
        
        // Pass 1: Final release build (if enabled)
        // build_common.ps1 parity: _RunMakeBase() does final_release pass first, then normal pass
        if (options.FinalRelease)
        {
            _logger.LogInformation("Compiling base packages (final_release)...");
            var finalReleaseArgs = "make -nopause -unattended -final_release";
            if (options.Debug) finalReleaseArgs += " -debug";

            var success = await InvokeCommandlet(finalReleaseArgs, receiver, "Base Compilation (Final Release)", ct);
            if (!success) return false;
        }

        // Pass 2: Normal build (always done, and required after final_release)
        // build_common.ps1 parity: "If we build in final release, we must build the normal scripts too"
        var args = "make -nopause -unattended";
        if (options.Debug) args += " -debug";
        return await InvokeCommandlet(args, receiver, "Base Compilation", ct);
    }

    public virtual async Task<bool> CompileModAsync(
        string modName,
        string stagingPath,
        BuildOptions options,
        OutputReceiver receiver,
        CancellationToken ct)
    {
        _logger.LogInformation($"Compiling mod: {modName}");
        // build_common.ps1 parity: Mod-pass uses make -nopause (WITHOUT -unattended)
        var args = $"make -nopause";
        
        if (options.Debug)
            args += " -debug";

        // Include mod argument for compiling a specific mod
        // Format: -mods <ModName> <StagingPath>
        args += $" -mods {modName} \"{stagingPath}\"";
        
        return await InvokeCommandlet(args, receiver, $"Mod Compilation ({modName})", ct);
    }

    private string BuildArguments(BuildOptions options)
    {
        var args = new StringBuilder("make -nopause -unattended");
        
        if (options.FinalRelease)
            args.Append(" -final_release");
        
        if (options.Debug)
            args.Append(" -debug");
        
        return args.ToString();
    }

    private async Task<bool> InvokeCommandlet(string args, OutputReceiver receiver, string description, CancellationToken ct)
    {
        receiver.ProcessDescription = description;
        int exitCode = await _runner.RunProcessWithSleepAsync(_commandletPath, args, receiver, sleepAtStartMs: 1000, sleepAtEndMs: 5000, ct: ct);

        return exitCode == 0;
    }
}
