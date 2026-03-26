using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using X2ModCompiler.Utilities;
using Microsoft.Extensions.Logging.Abstractions;

namespace X2ModCompiler.Tests;

public class ProcessRunnerTests
{
    [Fact]
    public async Task RunProcessAsync_InvokesProcessAndReturnsExitCode()
    {
        var runner = new ProcessRunner(NullLogger<ProcessRunner>.Instance);

        // Windows command processor 'cmd' /c exit 42
        int exitCode = await runner.RunProcessAsync("cmd.exe", "/c exit 42", null, CancellationToken.None);

        Assert.Equal(42, exitCode);
    }

    [Fact]
    public async Task RunProcessAsync_ThrowsOperationCanceledException_WhenCancelled()
    {
        var runner = new ProcessRunner(NullLogger<ProcessRunner>.Instance);
        using var cts = new CancellationTokenSource();
        
        // Command that waits (ping loopback)
        var runTask = runner.RunProcessAsync("ping.exe", "127.0.0.1 -n 5", null, cts.Token);
        
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await runTask);
    }
}
