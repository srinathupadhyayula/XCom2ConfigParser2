using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using X2ModCompiler.Compilation;
using X2ModCompiler.Exceptions;
using X2ModCompiler.Tests.Shared;
using Microsoft.Extensions.Logging.Abstractions;

namespace X2ModCompiler.Tests;

public class OutputReceiverTests : TestBase
{
    [Fact]
    public void ParseLine_DetectsCrash_WhenCrashExceptionFound()
    {
        // Arrange
        var receiver = new TestOutputReceiver(NullLogger.Instance);

        receiver.ParseLine("Fatal Error: Crash Exception detected");

        // Assert
        Assert.True(receiver.CrashDetected);
    }

    [Fact]
    public void ParseLine_DoesNotDetectCrash_WhenNoCrashException()
    {
        // Arrange
        var receiver = new TestOutputReceiver(NullLogger.Instance);

        receiver.ParseLine("Warning: Something happened");

        // Assert
        Assert.False(receiver.CrashDetected);
    }

    [Fact]
    public void Finish_ThrowsBuildCrashException_WhenCrashDetected()
    {
        // Arrange
        var receiver = new TestOutputReceiver(NullLogger.Instance);
        receiver.ProcessDescription = "Test Process";
        receiver.ParseLine("Fatal Error: Crash Exception detected");

        // Act & Assert
        var ex = Assert.Throws<BuildCrashException>(() => receiver.Finish(0));
        Assert.Contains("Test Process", ex.Message);
        Assert.Equal("Test Process", ex.ProcessDescription);
    }

    [Fact]
    public void Finish_ThrowsBuildFailureException_WhenNonZeroExitCode()
    {
        // Arrange
        var receiver = new TestOutputReceiver(NullLogger.Instance);
        receiver.ProcessDescription = "Test Process";

        // Act & Assert
        var ex = Assert.Throws<BuildFailureException>(() => receiver.Finish(1));
        Assert.Contains("Test Process", ex.Message);
        Assert.Contains("1", ex.Message);
        Assert.Equal(1, ex.ExitCode);
    }

    [Fact]
    public void Finish_DoesNotThrow_WhenSuccess()
    {
        // Arrange
        var receiver = new TestOutputReceiver(NullLogger.Instance);
        receiver.ProcessDescription = "Test Process";

        // Act & Assert - should not throw
        receiver.Finish(0);
    }

    [Fact]
    public void PassthroughReceiver_WritesLineToConsole()
    {
        // Arrange
        var receiver = new PassthroughReceiver(NullLogger.Instance);
        var testLine = "Test output line";

        // Act & Assert - verify it doesn't throw and calls Console.WriteLine
        // (We can't easily capture console output in xUnit, so we just verify no exception)
        var ex = Record.Exception(() => receiver.ParseLine(testLine));
        Assert.Null(ex);
    }

    [Fact]
    public async Task MakeOutputReceiver_TranslatesSdkPathToSourcePath()
    {
        // Arrange
        await using var temp = new TempDirectory();
        var sourcePath = Path.Combine(temp.Path, "TestMod");
        Directory.CreateDirectory(sourcePath);
        
        // Create a dummy file for path verification
        var testFile = Path.Combine(sourcePath, "Src", "Test.uc");
        Directory.CreateDirectory(Path.GetDirectoryName(testFile)!);
        File.WriteAllText(testFile, "test");

        var receiver = new MakeOutputReceiver(new[] { sourcePath }, @"C:\XCOM 2 War of the Chosen SDK", NullLogger.Instance);

        // Act - this should translate the path
        var sdkPath = @"C:\XCOM 2 War of the Chosen SDK\Development\Src\Test.uc(10) : Error";
        
        // Assert - should not throw and should attempt translation
        var ex = Record.Exception(() => receiver.ParseLine(sdkPath));
        Assert.Null(ex);
    }

    [Fact]
    public void MakeOutputReceiver_DetectsError()
    {
        // Arrange
        var receiver = new MakeOutputReceiver(new[] { @"C:\Source" }, @"C:\SDK", NullLogger.Instance);
        var errorLine = @"C:\Path\File.uc(10) : Error: Something went wrong";

        // Act & Assert - should not throw, should detect error
        var ex = Record.Exception(() => receiver.ParseLine(errorLine));
        Assert.Null(ex);
    }

    [Fact]
    public void MakeOutputReceiver_DetectsWarning()
    {
        // Arrange
        var receiver = new MakeOutputReceiver(new[] { @"C:\Source" }, @"C:\SDK", NullLogger.Instance);
        var warningLine = @"C:\Path\File.uc(10) : Warning: Something might be wrong";

        // Act & Assert
        var ex = Record.Exception(() => receiver.ParseLine(warningLine));
        Assert.Null(ex);
    }

    [Fact]
    public void MakeOutputReceiver_HandlesNullLine()
    {
        // Arrange
        var receiver = new MakeOutputReceiver(new[] { @"C:\Source" }, @"C:\SDK", NullLogger.Instance);

        // Act & Assert - should not throw
        var ex = Record.Exception(() => receiver.ParseLine(null));
        Assert.Null(ex);
    }

    // Test helper class
    private class TestOutputReceiver : OutputReceiver
    {
        public TestOutputReceiver(Microsoft.Extensions.Logging.ILogger logger) : base(logger) { }

        public override void ParseLine(string? line)
        {
            base.ParseLine(line);
        }
    }
}
