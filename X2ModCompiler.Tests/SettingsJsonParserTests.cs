using Xunit;
using X2ModCompiler.Utilities;
using System.Text.Json;

namespace X2ModCompiler.Tests;

public class SettingsJsonParserTests
{
    [Fact]
    public void ExtractIniRoots_ExtractsRoots_WhenValidJsonWithIniRoots()
    {
        // Arrange
        var json = """
        {
            "X2ModCompiler.iniRoots": [
                "Config",
                "Config/0Base",
                "Sdk/Config"
            ]
        }
        """;

        // Act
        var roots = SettingsJsonParser.ExtractIniRoots(json);

        // Assert
        Assert.Collection(roots,
            r => Assert.Equal("Config", r),
            r => Assert.Equal("Config/0Base", r),
            r => Assert.Equal("Sdk/Config", r));
    }

    [Fact]
    public void ExtractIniRoots_ReturnsEmptyList_WhenIniRootsPropertyMissing()
    {
        // Arrange
        var json = """
        {
            "other.property": "value"
        }
        """;

        // Act
        var roots = SettingsJsonParser.ExtractIniRoots(json);

        // Assert
        Assert.Empty(roots);
    }

    [Fact]
    public void ExtractIniRoots_ReturnsEmptyList_WhenIniRootsIsEmptyArray()
    {
        // Arrange
        var json = """
        {
            "X2ModCompiler.iniRoots": []
        }
        """;

        // Act
        var roots = SettingsJsonParser.ExtractIniRoots(json);

        // Assert
        Assert.Empty(roots);
    }

    [Fact]
    public void ExtractIniRoots_SkipsNullValues_WhenArrayContainsNulls()
    {
        // Arrange
        var json = """
        {
            "X2ModCompiler.iniRoots": [
                "Config",
                null,
                "Sdk/Config"
            ]
        }
        """;

        // Act
        var roots = SettingsJsonParser.ExtractIniRoots(json);

        // Assert
        Assert.Collection(roots,
            r => Assert.Equal("Config", r),
            r => Assert.Equal("Sdk/Config", r));
    }

    [Fact]
    public void ExtractIniRoots_SkipsEmptyStrings_WhenArrayContainsEmptyStrings()
    {
        // Arrange
        var json = """
        {
            "X2ModCompiler.iniRoots": [
                "Config",
                "",
                "Sdk/Config"
            ]
        }
        """;

        // Act
        var roots = SettingsJsonParser.ExtractIniRoots(json);

        // Assert
        Assert.Collection(roots,
            r => Assert.Equal("Config", r),
            r => Assert.Equal("Sdk/Config", r));
    }

    [Fact]
    public void ExtractIniRoots_ReturnsEmptyList_WhenInvalidJson()
    {
        // Arrange
        var json = "{ invalid json }";

        // Act
        var roots = SettingsJsonParser.ExtractIniRoots(json);

        // Assert
        Assert.Empty(roots);
    }

    [Fact]
    public void ExtractIniRoots_HandlesTrailingCommas_WhenJsonHasTrailingCommas()
    {
        // Arrange
        var json = """
        {
            "X2ModCompiler.iniRoots": [
                "Config",
                "Sdk/Config",
            ]
        }
        """;

        // Act
        var roots = SettingsJsonParser.ExtractIniRoots(json);

        // Assert
        Assert.Collection(roots,
            r => Assert.Equal("Config", r),
            r => Assert.Equal("Sdk/Config", r));
    }

    [Fact]
    public void ExtractIniRoots_HandlesComments_WhenJsonHasComments()
    {
        // Arrange
        var json = """
        {
            // This is a comment
            "X2ModCompiler.iniRoots": [
                "Config" // inline comment
            ]
        }
        """;

        // Act
        var roots = SettingsJsonParser.ExtractIniRoots(json);

        // Assert
        Assert.Collection(roots,
            r => Assert.Equal("Config", r));
    }

    [Fact]
    public void ExtractIniRoots_ExtractsRoots_WhenUsingJsonDocument()
    {
        // Arrange
        var json = """
        {
            "X2ModCompiler.iniRoots": [
                "Config",
                "Sdk/Config"
            ]
        }
        """;
        using var doc = JsonDocument.Parse(json);

        // Act
        var roots = SettingsJsonParser.ExtractIniRoots(doc);

        // Assert
        Assert.Collection(roots,
            r => Assert.Equal("Config", r),
            r => Assert.Equal("Sdk/Config", r));
    }

    [Fact]
    public void ExtractIniRoots_ReturnsEmptyList_WhenJsonDocumentHasNoIniRoots()
    {
        // Arrange
        var json = """
        {
            "other.property": "value"
        }
        """;
        using var doc = JsonDocument.Parse(json);

        // Act
        var roots = SettingsJsonParser.ExtractIniRoots(doc);

        // Assert
        Assert.Empty(roots);
    }
}
