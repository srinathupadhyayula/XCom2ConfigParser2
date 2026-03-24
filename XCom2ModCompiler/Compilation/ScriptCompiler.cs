using System.Text;
using Microsoft.Extensions.Logging;
using XCom2ModCompiler.Configuration;
using XCom2ModCompiler.Utilities;

namespace XCom2ModCompiler.Compilation;

/// <summary>
/// Provides a high-level interface for invoking the UnrealEd MakeCommandlet to compile XCOM 2 script packages.
/// This class encapsulates the logic for both base game package compilation and mod-specific source compilation.
/// </summary>
public class ScriptCompiler
{
    private readonly string _commandletPath;
    private readonly string _sdkPath;
    private readonly string _gamePath;
    private readonly ILogger<ScriptCompiler> _logger;
    private readonly IProcessRunner _runner;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScriptCompiler"/> class.
    /// </summary>
    /// <param name="commandletPath">The absolute path to the UnrealEd.exe executable.</param>
    /// <param name="sdkPath">The root path of the XCOM 2 SDK.</param>
    /// <param name="gamePath">The root path of the XCOM 2 game installation.</param>
    /// <param name="runner">The process runner used to execute the commandlet.</param>
    /// <param name="logger">The logger for diagnostic output.</param>
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

    /// <summary>
    /// Performs compilation of the base game packages.
    /// Optionally performs a final release pass followed by a standard pass to ensure binary consistency.
    /// </summary>
    /// <param name="options">The build options governing the compilation (e.g., FinalRelease, Debug).</param>
    /// <param name="receiver">The output receiver for processing commandlet console output.</param>
    /// <param name="ct">A cancellation token to abort the compilation.</param>
    /// <returns>A task representing the asynchronous operation, returning true if compilation succeeded.</returns>
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

    /// <summary>
    /// Performs compilation for a specific mod project.
    /// Uses the '-mods' argument to target the project source and output to a staging directory.
    /// </summary>
    /// <param name="modName">The name of the mod package to compile.</param>
    /// <param name="stagingPath">The path where the compiled .u binary should be placed.</param>
    /// <param name="options">The build options governing the compilation.</param>
    /// <param name="receiver">The output receiver for processing commandlet console output.</param>
    /// <param name="ct">A cancellation token to abort the compilation.</param>
    /// <returns>A task representing the asynchronous operation, returning true if compilation succeeded.</returns>
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

    /// <summary>
    /// Constructs the CLI arguments for the make commandlet based on build options.
    /// </summary>
    /// <param name="options">The build options to translate into arguments.</param>
    /// <returns>A string containing the formatted commandlet arguments.</returns>
    private string BuildArguments(BuildOptions options)
    {
        var args = new StringBuilder("make -nopause -unattended");
        
        if (options.FinalRelease)
            args.Append(" -final_release");
        
        if (options.Debug)
            args.Append(" -debug");
        
        return args.ToString();
    }

    /// <summary>
    /// Invokes the underlying process runner to execute the UnrealEd commandlet.
    /// Includes the necessary sleep intervals at project start and end to ensure file system stability.
    /// </summary>
    /// <param name="args">The command line arguments for the commandlet.</param>
    /// <param name="receiver">The output receiver for logging.</param>
    /// <param name="description">A human-readable description of the current operation.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True if the process exited with code 0; otherwise false.</returns>
    private async Task<bool> InvokeCommandlet(string args, OutputReceiver receiver, string description, CancellationToken ct)
    {
        receiver.ProcessDescription = description;
        int exitCode = await _runner.RunProcessWithSleepAsync(_commandletPath, args, receiver, sleepAtStartMs: 1000, sleepAtEndMs: 5000, ct: ct);

        return exitCode == 0;
    }
}
