using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using NSubstitute;
using X2ModCompiler.Utilities;
using Xunit;
using X2ModCompiler.Tests.Shared;

namespace X2ModCompiler.Tests;

public class ProjectSynchronizerTests : TestBase
{
    private readonly ProjectSynchronizer _synchronizer;

    public ProjectSynchronizerTests()
    {
        _synchronizer = new ProjectSynchronizer(Substitute.For<ILogger<ProjectSynchronizer>>());
    }

    [Fact]
    public async Task Synchronize_RegeneratesItemGroup_Correctly()
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
    <None Include=""OldFile.uc"" />
  </ItemGroup>
</Project>";
        File.WriteAllText(x2projPath, x2projContent);

        // Act
        _synchronizer.Synchronize(x2projPath);

        // Assert
        var doc = XDocument.Load(x2projPath);
        XNamespace ns = doc.Root!.GetDefaultNamespace();
        
        var folders = doc.Descendants(ns + "Folder").Select(f => f.Attribute("Include")?.Value).ToList();
        var contents = doc.Descendants(ns + "Content").Select(c => c.Attribute("Include")?.Value).ToList();

        Assert.Contains("Src", folders);
        Assert.Contains("Config", folders);
        Assert.Contains(@"Src\Test.uc", contents);
        Assert.Contains(@"Config\XComEngine.ini", contents);
        
        // Should not contain the old file
        Assert.DoesNotContain("OldFile.uc", contents);
    }
}
