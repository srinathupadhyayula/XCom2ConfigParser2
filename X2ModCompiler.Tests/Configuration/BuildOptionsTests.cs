using Xunit;
using X2ModCompiler.Configuration;
using X2ModCompiler.Core.Configuration;

namespace X2ModCompiler.Tests.Configuration;

/// <summary>
/// Tests for BuildOptions class.
/// </summary>
public class BuildOptionsTests
{
    [Fact]
    public void BuildOptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new BuildOptions
        {
            ModName = "TestMod",
            ProjectRoot = "/test/project",
            SdkPath = "/test/sdk",
            GamePath = "/test/game",
            ModDestinationPath = "/test/dest"
        };

        // Assert - Default values
        Assert.False(options.Debug);
        Assert.False(options.FinalRelease);
        Assert.False(options.CompileOnly);
        Assert.Equal(CompilerLogLevel.Debug, options.LogVerbosity);
        Assert.False(options.TwoPassCompilation);
        Assert.False(options.ValidateConfig);
        Assert.Equal(-1, options.WorkshopId);
        Assert.Empty(options.IncludePaths);
        Assert.Empty(options.IniRoots);
        Assert.Empty(options.CleanMods);
        Assert.Empty(options.DependentPackages);
        Assert.Null(options.ContentOptionsJson);
        Assert.Empty(options.PreMakeHooks);
        Assert.Null(options.BuildCachePathOverride);
    }

    [Fact]
    public void BuildOptions_ModNameCanonical_RemovesSpacesAndSemicolons()
    {
        // Arrange
        var options = new BuildOptions
        {
            ModName = "Test Mod; Name",
            ProjectRoot = "/test",
            SdkPath = "/test/sdk",
            GamePath = "/test/game",
            ModDestinationPath = "/test/dest"
        };

        // Act
        var canonical = options.ModNameCanonical;

        // Assert
        Assert.Equal("TestModName", canonical);
    }

    [Fact]
    public void BuildOptions_ModSrcRoot_CombinesProjectRootAndModName()
    {
        // Arrange
        var options = new BuildOptions
        {
            ModName = "TestMod",
            ProjectRoot = "/test/project",
            SdkPath = "/test/sdk",
            GamePath = "/test/game",
            ModDestinationPath = "/test/dest"
        };

        // Act
        var modSrcRoot = options.ModSrcRoot;

        // Assert
        Assert.Contains("TestMod", modSrcRoot);
        Assert.Contains("project", modSrcRoot);
    }

    [Fact]
    public void BuildOptions_StagingPath_CombinesSdkPathAndModName()
    {
        // Arrange
        var options = new BuildOptions
        {
            ModName = "TestMod",
            ProjectRoot = "/test/project",
            SdkPath = "/test/sdk",
            GamePath = "/test/game",
            ModDestinationPath = "/test/dest"
        };

        // Act
        var stagingPath = options.StagingPath;

        // Assert
        Assert.Contains("TestMod", stagingPath);
        Assert.Contains("Mods", stagingPath);
    }

    [Fact]
    public void BuildOptions_FinalModPath_CombinesDestinationAndModName()
    {
        // Arrange
        var options = new BuildOptions
        {
            ModName = "TestMod",
            ProjectRoot = "/test/project",
            SdkPath = "/test/sdk",
            GamePath = "/test/game",
            ModDestinationPath = "/test/dest"
        };

        // Act
        var finalModPath = options.FinalModPath;

        // Assert
        Assert.Contains("TestMod", finalModPath);
        Assert.Contains("dest", finalModPath);
    }

    [Fact]
    public void BuildOptions_BuildCachePath_UsesOverride_WhenProvided()
    {
        // Arrange
        var options = new BuildOptions
        {
            ModName = "TestMod",
            ProjectRoot = "/test/project",
            SdkPath = "/test/sdk",
            GamePath = "/test/game",
            ModDestinationPath = "/test/dest",
            BuildCachePathOverride = "/custom/cache"
        };

        // Act
        var cachePath = options.BuildCachePath;

        // Assert
        Assert.Equal("/custom/cache", cachePath);
    }

    [Fact]
    public void BuildOptions_BuildCachePath_UsesDefault_WhenOverrideNotProvided()
    {
        // Arrange
        var options = new BuildOptions
        {
            ModName = "TestMod",
            ProjectRoot = "/test/project",
            SdkPath = "/test/sdk",
            GamePath = "/test/game",
            ModDestinationPath = "/test/dest"
        };

        // Act
        var cachePath = options.BuildCachePath;

        // Assert
        Assert.Contains("BuildCache", cachePath);
        Assert.Contains("project", cachePath);
    }

    [Fact]
    public void BuildOptions_CommandletPath_ReturnsCorrectPath()
    {
        // Arrange
        var options = new BuildOptions
        {
            ModName = "TestMod",
            ProjectRoot = "/test/project",
            SdkPath = "/test/sdk",
            GamePath = "/test/game",
            ModDestinationPath = "/test/dest"
        };

        // Act
        var commandletPath = options.CommandletPath;

        // Assert
        Assert.EndsWith("XComGame.com", commandletPath);
        Assert.Contains("binaries", commandletPath);
        Assert.Contains("Win64", commandletPath);
    }

    [Fact]
    public void BuildOptions_CookerOutputPath_ReturnsCorrectPath()
    {
        // Arrange
        var options = new BuildOptions
        {
            ModName = "TestMod",
            ProjectRoot = "/test/project",
            SdkPath = "/test/sdk",
            GamePath = "/test/game",
            ModDestinationPath = "/test/dest"
        };

        // Act
        var cookerPath = options.CookerOutputPath;

        // Assert
        Assert.EndsWith("CookedPCConsole", cookerPath);
        Assert.Contains("Published", cookerPath);
    }

    [Fact]
    public void BuildOptions_Validate_ReturnsErrors_WhenRequiredFieldsMissing()
    {
        // Arrange
        var options = new BuildOptions(); // All required fields empty

        // Act
        var errors = options.Validate();

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("ModName"));
        Assert.Contains(errors, e => e.Contains("ProjectRoot"));
        Assert.Contains(errors, e => e.Contains("SdkPath"));
        Assert.Contains(errors, e => e.Contains("GamePath"));
        Assert.Contains(errors, e => e.Contains("ModDestinationPath"));
    }

    [Fact]
    public void BuildOptions_Validate_ReturnsNoErrors_WhenAllFieldsPresent()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        
        try
        {
            var options = new BuildOptions
            {
                ModName = "TestMod",
                ProjectRoot = tempDir,
                SdkPath = tempDir,
                GamePath = tempDir,
                ModDestinationPath = tempDir
            };

            // Act
            var errors = options.Validate();

            // Assert
            Assert.Empty(errors);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void BuildOptions_SyncToParserSettings_UpdatesParserSettings()
    {
        // Arrange
        var options = new BuildOptions
        {
            ModName = "TestMod",
            ProjectRoot = "/test/project",
            SdkPath = "/test/sdk",
            GamePath = "/test/game",
            ModDestinationPath = "/test/dest",
            IncludePaths = { "/include1", "/include2" }
        };

        // Act
        options.SyncToParserSettings();

        // Assert
        Assert.Contains("TestMod", options.ParserSettings.LocalSrcRoot);
        Assert.Equal("/test/sdk", options.ParserSettings.SdkRoot);
        Assert.Equal(2, options.ParserSettings.ModsCompiledAgainst.Count);
    }
}
