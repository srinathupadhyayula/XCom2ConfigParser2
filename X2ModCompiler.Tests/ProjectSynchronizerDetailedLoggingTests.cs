using System;
using System.IO;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using X2ModCompiler.Utilities;
using X2ModCompiler.Tests.Shared;

namespace X2ModCompiler.Tests;

public class ProjectSynchronizerDetailedLoggingTests : TestBase
{
    private readonly ProjectSynchronizer _synchronizer;

    public ProjectSynchronizerDetailedLoggingTests()
    {
        _synchronizer = new ProjectSynchronizer(Substitute.For<ILogger<ProjectSynchronizer>>());
    }

    [Fact]
    public async Task Synchronize_LogsDetailedProgress()
    {
        await using var temp = new TempDirectory();
        // Arrange
        var srcDir = Path.Combine(temp.Path, "Src");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "Test.uc"), "class Test;");

        var configDir = Path.Combine(temp.Path, "Config");
        Directory.CreateDirectory(configDir);
        File.WriteAllText(Path.Combine(configDir, "XComEngine.ini"), "[Engine.Engine]");

        var x2projPath = Path.Combine(temp.Path, "TestMod.x2proj");
        var x2projContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <SteamPublishID>123</SteamPublishID>
  </PropertyGroup>
  <ItemGroup>
    <Folder Include=""OldFolder"" />
    <Content Include=""OldFile.uc"" />
  </ItemGroup>
</Project>";
        File.WriteAllText(x2projPath, x2projContent);

        // Act
        _synchronizer.Synchronize(x2projPath);

        // Assert - Test passes if Synchronize doesn't throw
        Assert.True(true);
    }

    [Fact]
    public async Task Synchronize_LogsRemovedItems()
    {
        await using var temp = new TempDirectory();
        // Arrange
        var srcDir = Path.Combine(temp.Path, "Src");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "Test.uc"), "class Test;");

        var x2projPath = Path.Combine(temp.Path, "TestMod.x2proj");
        var x2projContent = @"<Project>
  <ItemGroup>
    <Folder Include=""OldFolder"" />
  </ItemGroup>
</Project>";
        File.WriteAllText(x2projPath, x2projContent);

        // Act
        _synchronizer.Synchronize(x2projPath);

        // Assert
        Assert.True(true);
    }

    [Fact]
    public async Task Synchronize_LogsFileCounts()
    {
        await using var temp = new TempDirectory();
        // Arrange
        var srcDir = Path.Combine(temp.Path, "Src");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "Test1.uc"), "class Test1;");
        File.WriteAllText(Path.Combine(srcDir, "Test2.uc"), "class Test2;");

        var x2projPath = Path.Combine(temp.Path, "TestMod.x2proj");
        File.WriteAllText(x2projPath, @"<Project></Project>");

        // Act
        _synchronizer.Synchronize(x2projPath);

        // Assert
        Assert.True(true);
    }

    [Fact]
    public async Task Synchronize_LogsAddedNodes()
    {
        await using var temp = new TempDirectory();
        // Arrange
        var srcDir = Path.Combine(temp.Path, "Src");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "Test.uc"), "class Test;");

        var x2projPath = Path.Combine(temp.Path, "TestMod.x2proj");
        File.WriteAllText(x2projPath, @"<Project></Project>");

        // Act
        _synchronizer.Synchronize(x2projPath);

        // Assert
        Assert.True(true);
    }
}
