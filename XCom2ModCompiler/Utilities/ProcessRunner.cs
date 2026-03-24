using System.Diagnostics;
using XCom2ModCompiler.Compilation;

namespace XCom2ModCompiler.Utilities;

public interface IProcessRunner
{
    Task<int> RunProcessAsync(string fileName, string arguments, Action<string?>? onOutput = null, CancellationToken ct = default);
    Task<int> RunProcessWithSleepAsync(string fileName, string arguments, OutputReceiver receiver, int sleepAtStartMs = 0, int sleepAtEndMs = 0, CancellationToken ct = default);
    void KillProcessTree(int pid);
}

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

        if (onOutput != null)
        {
            process.OutputDataReceived += (sender, e) => onOutput(e.Data);
            process.ErrorDataReceived += (sender, e) => onOutput(e.Data);
        }

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
