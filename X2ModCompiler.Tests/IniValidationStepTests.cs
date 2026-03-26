using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using X2ModCompiler.Application.Steps;
using X2ModCompiler.Configuration;
using X2ModCompiler.Tests.Shared;
using Xunit;

namespace X2ModCompiler.Tests;

public class IniValidationStepTests : TestBase
{
    private readonly ILoggerFactory _loggerFactory;

    public IniValidationStepTests()
    {
        _loggerFactory = Substitute.For<ILoggerFactory>();
        _loggerFactory.CreateLogger<IniValidationStep>().Returns(Substitute.For<ILogger<IniValidationStep>>());
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsSuccess_WhenOnlyOneIniFileExists()
    {
        await using var temp = new TempDirectory();
        var options = new BuildOptions();
        options.IniRoots.Add(temp.Path);
        
        File.WriteAllText(Path.Combine(temp.Path, "XComEngine.ini"), "[Engine.ScriptPackages]\n+NonNativePackages=Test");

        var step = new IniValidationStep(_loggerFactory);
        var result = await step.ExecuteAsync(options, CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFailure_WhenSectionsAreSplitAcrossFiles()
    {
        await using var temp = new TempDirectory();
        var root1 = Path.Combine(temp.Path, "Root1");
        var root2 = Path.Combine(temp.Path, "Root2");
        Directory.CreateDirectory(root1);
        Directory.CreateDirectory(root2);

        File.WriteAllText(Path.Combine(root1, "XComEngine.ini"), "[Engine.ScriptPackages]\n+NonNativePackages=Test");
        File.WriteAllText(Path.Combine(root2, "XComEngine.ini"), "[UnrealEd.EditorEngine]\n+ModEditPackages=Test");

        var options = new BuildOptions();
        options.IniRoots.Add(root1);
        options.IniRoots.Add(root2);

        var step = new IniValidationStep(_loggerFactory);
        var result = await step.ExecuteAsync(options, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsSuccess_WhenAllSectionsAreInSameFile()
    {
        await using var temp = new TempDirectory();
        var root1 = Path.Combine(temp.Path, "Root1");
        var root2 = Path.Combine(temp.Path, "Root2");
        Directory.CreateDirectory(root1);
        Directory.CreateDirectory(root2);

        File.WriteAllText(Path.Combine(root1, "XComEngine.ini"), 
            "[Engine.ScriptPackages]\n+NonNativePackages=Test\n" +
            "[UnrealEd.EditorEngine]\n+ModEditPackages=Test\n" +
            "[X2ModCompiler.DependantPackages]\n+DependantPackages=Deps");
        
        File.WriteAllText(Path.Combine(root2, "XComEngine.ini"), "[SomeOtherSection]\nKey=Value");

        var options = new BuildOptions();
        options.IniRoots.Add(root1);
        options.IniRoots.Add(root2);

        var step = new IniValidationStep(_loggerFactory);
        var result = await step.ExecuteAsync(options, CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFailure_WhenSameSectionExistsInMultipleFiles()
    {
        await using var temp = new TempDirectory();
        var root1 = Path.Combine(temp.Path, "Root1");
        var root2 = Path.Combine(temp.Path, "Root2");
        Directory.CreateDirectory(root1);
        Directory.CreateDirectory(root2);

        File.WriteAllText(Path.Combine(root1, "XComEngine.ini"), "[Engine.ScriptPackages]\n+NonNativePackages=Test1");
        File.WriteAllText(Path.Combine(root2, "XComEngine.ini"), "[Engine.ScriptPackages]\n+NonNativePackages=Test2");

        var options = new BuildOptions();
        options.IniRoots.Add(root1);
        options.IniRoots.Add(root2);

        var step = new IniValidationStep(_loggerFactory);
        var result = await step.ExecuteAsync(options, CancellationToken.None);

        Assert.False(result);
    }
}
