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
        if (options.FinalRelease)
        {
            _logger.LogInformation("Compiling base packages (baseline)...");
            // Pass 1: Baseline (Normal build)
            // build_common.ps1 parity: Final release needs a normal baseline first
            var baselineArgs = "make -nopause -unattended";
            if (options.Debug) baselineArgs += " -debug";
            
            var success = await InvokeCommandlet(baselineArgs, receiver, "Base Compilation (Baseline)", ct);
            if (!success) return false;
        }

        _logger.LogInformation("Compiling base packages...");
        var args = BuildArguments(options);
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
        int exitCode = await _runner.RunProcessWithSleepAsync(_commandletPath, args, receiver, sleepAtStartMs: 1000, sleepAtEndMs: 2000, ct: ct);

        return exitCode == 0;
    }
}
