using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using XCom2ModCompiler.Utilities;
using Xunit;

namespace XCom2ModCompiler.Tests;

public class ProjectSynchronizerTests
{
    private readonly ProjectSynchronizer _synchronizer;

    public ProjectSynchronizerTests()
    {
        _synchronizer = new ProjectSynchronizer(new Mock<ILogger<ProjectSynchronizer>>().Object);
    }

    [Fact]
    public void Synchronize_RegeneratesItemGroup_Correctly()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        
        var srcDir = Path.Combine(tempDir, "Src");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "Test.uc"), "class Test;");
        
        var configDir = Path.Combine(tempDir, "Config");
        Directory.CreateDirectory(configDir);
        File.WriteAllText(Path.Combine(configDir, "XComEngine.ini"), "[Engine.Engine]");

        var x2projPath = Path.Combine(tempDir, "TestMod.x2proj");
        var x2projContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <SteamPublishID>123</SteamPublishID>
  </PropertyGroup>
  <ItemGroup>
    <None Include=""OldFile.uc"" />
  </ItemGroup>
</Project>";
        File.WriteAllText(x2projPath, x2projContent);

        try {
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
        finally {
            Directory.Delete(tempDir, true);
        }
    }
}
