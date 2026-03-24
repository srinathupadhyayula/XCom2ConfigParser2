using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using XCom2ModCompiler.Utilities;
using System;

namespace XCom2ModCompiler.Tests;

public class ProcessRunnerTests
{
    [Fact]
    public async Task RunProcessAsync_InvokesProcessAndReturnsExitCode()
    {
        var loggerMock = new Mock<ILogger<ProcessRunner>>();
        var runner = new ProcessRunner();

        // Windows command processor 'cmd' /c exit 42
        int exitCode = await runner.RunProcessAsync("cmd.exe", "/c exit 42", null, CancellationToken.None);

        Assert.Equal(42, exitCode);
    }

    [Fact]
    public async Task RunProcessAsync_ThrowsOperationCanceledException_WhenCancelled()
    {
        var loggerMock = new Mock<ILogger<ProcessRunner>>();
        var runner = new ProcessRunner();
        using var cts = new CancellationTokenSource();
        
        // Command that waits
        var runTask = runner.RunProcessAsync("ping.exe", "127.0.0.1 -n 5", null, cts.Token);
        
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);
    }
}
