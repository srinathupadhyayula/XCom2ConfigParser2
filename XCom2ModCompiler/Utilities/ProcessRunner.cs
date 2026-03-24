using System.Diagnostics;
using XCom2ModCompiler.Compilation;

namespace XCom2ModCompiler.Utilities;

/// <summary>
/// Defines a contract for executing external processes and commandlets with output redirection.
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Executes a process asynchronously and captures its standard output/error via a callback.
    /// </summary>
    /// <param name="fileName">The executable file to run.</param>
    /// <param name="arguments">The command-line arguments.</param>
    /// <param name="onOutput">An optional callback invoked for each line of output.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The exit code of the process.</returns>
    Task<int> RunProcessAsync(string fileName, string arguments, Action<string?>? onOutput = null, CancellationToken ct = default);

    /// <summary>
    /// Executes a process asynchronously, redirecting output to an <see cref="OutputReceiver"/>.
    /// Includes optional sleep intervals at the start and end of the process execution.
    /// </summary>
    /// <param name="fileName">The executable file to run.</param>
    /// <param name="arguments">The command-line arguments.</param>
    /// <param name="receiver">The receiver for processing commandlet output.</param>
    /// <param name="sleepAtStartMs">Milliseconds to wait before starting the process.</param>
    /// <param name="sleepAtEndMs">Milliseconds to wait after the process exists.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The exit code of the process.</returns>
    Task<int> RunProcessWithSleepAsync(string fileName, string arguments, OutputReceiver receiver, int sleepAtStartMs = 0, int sleepAtEndMs = 0, CancellationToken ct = default);

    /// <summary>
    /// Forcefully terminates a process and all its child processes.
    /// </summary>
    /// <param name="pid">The process ID of the root process.</param>
    void KillProcessTree(int pid);
}

/// <summary>
/// Implements <see cref="IProcessRunner"/> using the native <see cref="Process"/> class.
/// Provides robust output redirection to avoid buffer deadlocks.
/// </summary>
public class ProcessRunner : IProcessRunner
{
    public async Task<int> RunProcessAsync(string fileName, string arguments, Action<string?>? onOutput = null, CancellationToken ct = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };

        // Always attach handlers to consume output and prevent buffer deadlock,
        // even if we don't have a handler to process the data.
        // This is critical: if the buffer fills up and no one is reading, the process will hang.
        process.OutputDataReceived += (sender, e) => 
        {
            if (onOutput != null && e.Data != null)
            {
                onOutput(e.Data);
            }
        };
        process.ErrorDataReceived += (sender, e) => 
        {
            if (onOutput != null && e.Data != null)
            {
                onOutput(e.Data);
            }
        };

        process.Start();

        // Always begin reading to avoid blocking the process if the buffer fills up,
        // even if we don't have a handler to process the data.
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(ct);
        }
        catch (TaskCanceledException)
        {
            KillProcessTree(process.Id);
            throw;
        }

        return process.ExitCode;
    }

    /// <summary>
    /// Runs a process with output handling. Simple and reliable.
    /// </summary>
    public async Task<int> RunProcessWithSleepAsync(
        string fileName,
        string arguments,
        OutputReceiver receiver,
        int sleepAtStartMs = 0,
        int sleepAtEndMs = 0,
        CancellationToken ct = default)
    {
        // Sleep at the start if specified
        if (sleepAtStartMs > 0)
        {
            Console.WriteLine($"Waiting for {sleepAtStartMs / 1000.0} seconds...");
            await Task.Delay(sleepAtStartMs, ct);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };

        // ALL output goes through receiver to avoid duplicates
        // The receiver handles both regular output and errors
        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                receiver.ParseLine(e.Data);
            }
        };

        // stderr also goes through receiver (make commandlet writes errors there)
        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                receiver.ParseLine(e.Data);  // Same receiver, no duplicate
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(ct);
        }
        catch (TaskCanceledException)
        {
            KillProcessTree(process.Id);
            throw;
        }

        var exitCode = process.ExitCode;
        receiver.Finish(exitCode);

        // Sleep at the end if specified
        if (sleepAtEndMs > 0)
        {
            Console.WriteLine($"Waiting for {sleepAtEndMs / 1000.0} seconds...");
            await Task.Delay(sleepAtEndMs, ct);
        }

        return exitCode;
    }

    public void KillProcessTree(int pid)
    {
        ProcessExtensions.KillProcessTree(pid);
    }
}
