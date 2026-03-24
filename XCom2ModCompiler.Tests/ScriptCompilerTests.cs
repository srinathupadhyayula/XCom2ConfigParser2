using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using XCom2ModCompiler.Compilation;
using XCom2ModCompiler.Configuration;
using XCom2ModCompiler.Utilities;
using System.Collections.Generic;

namespace XCom2ModCompiler.Tests;

public class ScriptCompilerTests
{
    private readonly Mock<IProcessRunner> _processRunnerMock;
    private readonly Mock<ILogger<ScriptCompiler>> _loggerMock;
    private readonly Mock<OutputReceiver> _receiverMock;
    private readonly ScriptCompiler _compiler;

    public ScriptCompilerTests()
    {
        _processRunnerMock = new Mock<IProcessRunner>();
        _loggerMock = new Mock<ILogger<ScriptCompiler>>();
        
        _receiverMock = new Mock<OutputReceiver>();
        
        _compiler = new ScriptCompiler(
            @"C:\XCOM2SDK\Binaries\Win64\XComGame.com",
            @"C:\XCOM2SDK",
            @"C:\XCOM2Client",
            _processRunnerMock.Object, 
            _loggerMock.Object);
    }

    [Fact]
    public async Task CompileModAsync_InvokesMakeCommandletWithCorrectArguments()
    {
        var options = new BuildOptions
        {
            ModName = "MyMod",
            ProjectRoot = @"C:\Source",
            SdkPath = @"C:\XCOM2SDK",
            GamePath = @"C:\XCOM2Client",
            ModDestinationPath = @"D:\Dest",
            Debug = true
        };

        _processRunnerMock.Setup(r => r.RunProcessWithSleepAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<OutputReceiver>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync(0);

        bool result = await _compiler.CompileModAsync("MyMod", @"C:\Source\MyMod", options, _receiverMock.Object, CancellationToken.None);

        Assert.True(result);

        string expectedArgs = @"make -nopause -unattended -debug -mods MyMod ""C:\Source\MyMod""";

        _processRunnerMock.Verify(r => r.RunProcessWithSleepAsync(@"C:\XCOM2SDK\Binaries\Win64\XComGame.com", expectedArgs, It.IsAny<OutputReceiver>(), 1000, 2000, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CompileModAsync_ReturnsFalse_OnNonZeroExitCode()
    {
        var options = new BuildOptions
        {
            ModName = "MyMod",
            ProjectRoot = @"C:\Source",
            SdkPath = @"C:\XCOM2SDK",
            GamePath = @"C:\XCOM2Client",
            ModDestinationPath = @"D:\Dest"
        };

        _processRunnerMock.Setup(r => r.RunProcessWithSleepAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<OutputReceiver>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync(1);

        bool result = await _compiler.CompileModAsync("MyMod", @"C:\Source\MyMod", options, _receiverMock.Object, CancellationToken.None);

        Assert.False(result);
    }
}
