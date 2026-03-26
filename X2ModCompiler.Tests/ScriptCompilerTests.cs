using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using X2ModCompiler.Compilation;
using X2ModCompiler.Configuration;
using X2ModCompiler.Utilities;
using System.Collections.Generic;

namespace X2ModCompiler.Tests;

public class ScriptCompilerTests
{
    private readonly IProcessRunner _processRunner;
    private readonly ILogger<ScriptCompiler> _logger;
    private readonly OutputReceiver _receiver;
    private readonly ScriptCompiler _compiler;

    public ScriptCompilerTests()
    {
        _processRunner = Substitute.For<IProcessRunner>();
        _logger = Substitute.For<ILogger<ScriptCompiler>>();
        
        _receiver = Substitute.For<OutputReceiver>(Substitute.For<ILogger>());
        
        _compiler = new ScriptCompiler(
            @"C:\XCOM2SDK\Binaries\Win64\XComGame.com",
            @"C:\XCOM2SDK",
            @"C:\XCOM2Client",
            _processRunner, 
            _logger);
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

        _processRunner.RunProcessWithSleepAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<OutputReceiver>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                      .Returns(Task.FromResult(0));

        bool result = await _compiler.CompileModAsync("MyMod", @"C:\Source\MyMod", options, _receiver, TestContext.Current.CancellationToken);

        Assert.True(result);

        string expectedArgs = @"make -nopause -debug -mods MyMod ""C:\Source\MyMod"""; // No longer uses -unattended for mod pass

        await _processRunner.Received(1).RunProcessWithSleepAsync(Arg.Any<string>(), expectedArgs, Arg.Any<OutputReceiver>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
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

        _processRunner.RunProcessWithSleepAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<OutputReceiver>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                      .Returns(Task.FromResult(1));

        bool result = await _compiler.CompileModAsync("MyMod", @"C:\Source\MyMod", options, _receiver, TestContext.Current.CancellationToken);

        Assert.False(result);
    }
}
