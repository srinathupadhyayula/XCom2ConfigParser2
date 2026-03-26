using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using X2ModCompiler.Tracking;
using X2ModCompiler.Tests.Shared;

namespace X2ModCompiler.Tests;

public class MacroValidatorTests : TestBase
{
    private readonly ILogger<MacroValidator> _logger;
    private readonly MacroValidator _validator;

    public MacroValidatorTests()
    {
        _logger = Substitute.For<ILogger<MacroValidator>>();
        _validator = new MacroValidator(_logger);
    }

    [Fact]
    public async Task ParseMacroFile_ParsesValidMacroDefinitions()
    {
        await using var temp = new TempDirectory();
        // Arrange
        var macroFile = Path.Combine(temp.Path, "Globals.uci");
        var content = @"
`define MY_MACRO 1
`define ANOTHER_MACRO value
// Comment
class MyClass;
";
        File.WriteAllText(macroFile, content);

        // Act
        _validator.ParseMacroFile(macroFile);
        var result = _validator.Validate();

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ParseMacroFile_DetectsImplicitRedefinition()
    {
        await using var temp = new TempDirectory();
        // Arrange
        var file1 = Path.Combine(temp.Path, "File1.uci");
        var file2 = Path.Combine(temp.Path, "File2.uci");
        
        File.WriteAllText(file1, "`define MY_MACRO 1");
        File.WriteAllText(file2, "`define MY_MACRO 2");

        // Act
        _validator.ParseMacroFile(file1);
        _validator.ParseMacroFile(file2);
        var result = _validator.Validate();

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Contains("MY_MACRO", result.Errors[0].Message);
        Assert.Contains("redefined", result.Errors[0].Message);
    }

    [Fact]
    public async Task Validate_AllowsExplicitRedefine()
    {
        await using var temp = new TempDirectory();
        // Arrange
        var file1 = Path.Combine(temp.Path, "File1.uci");
        var file2 = Path.Combine(temp.Path, "File2.uci");
        
        File.WriteAllText(file1, "`define MY_MACRO 1");
        File.WriteAllText(file2, "`define MY_MACRO 2");

        // Act
        _validator.ParseMacroFile(file1);
        _validator.ParseMacroFile(file2);
        _validator.AllowRedefine("MY_MACRO");
        var result = _validator.Validate();

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ParseMacroFile_HandlesMissingFile()
    {
        await using var temp = new TempDirectory();
        // Arrange
        var nonExistentFile = Path.Combine(temp.Path, "NonExistent.uci");

        // Act & Assert - should not throw
        var ex = Record.Exception(() => _validator.ParseMacroFile(nonExistentFile));
        Assert.Null(ex);
    }

    [Fact]
    public async Task ParseMacroFile_IgnoresComments()
    {
        await using var temp = new TempDirectory();
        // Arrange
        var macroFile = Path.Combine(temp.Path, "Globals.uci");
        var content = @"
// `define COMMENTED_OUT 1
`define REAL_MACRO 2
";
        File.WriteAllText(macroFile, content);

        // Act
        _validator.ParseMacroFile(macroFile);
        var result = _validator.Validate();

        // Assert
        Assert.True(result.IsValid);
        // Only REAL_MACRO should be parsed
    }

    [Fact]
    public async Task ParseMacroFile_HandlesMultipleMacrosInSingleFile()
    {
        await using var temp = new TempDirectory();
        // Arrange
        var macroFile = Path.Combine(temp.Path, "Globals.uci");
        var content = @"
`define MACRO_A 1
`define MACRO_B 2
`define MACRO_C 3
";
        File.WriteAllText(macroFile, content);

        // Act
        _validator.ParseMacroFile(macroFile);
        var result = _validator.Validate();

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void MacroDefinition_HasCorrectProperties()
    {
        // Arrange
        var definition = new MacroDefinition("TEST_MACRO", "TestFile.uci", 42, false);

        // Assert
        Assert.Equal("TEST_MACRO", definition.Name);
        Assert.Equal("TestFile.uci", definition.FilePath);
        Assert.Equal(42, definition.LineNumber);
        Assert.False(definition.IsExplicitRedefine);
    }

    [Fact]
    public void MacroError_HasCorrectProperties()
    {
        // Arrange
        var error = new MacroError("TEST_MACRO", "TestFile.uci", 42, "Test message");

        // Assert
        Assert.Equal("TEST_MACRO", error.MacroName);
        Assert.Equal("TestFile.uci", error.FilePath);
        Assert.Equal(42, error.LineNumber);
        Assert.Equal("Test message", error.Message);
    }

    [Fact]
    public void ValidationResult_HasCorrectProperties()
    {
        // Arrange
        var errors = new[] { new MacroError("TEST", "File.uci", 1, "Error") };
        var result = new ValidationResult(false, errors);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
    }

    [Fact]
    public async Task ParseMacroFile_CaseInsensitiveComparison()
    {
        await using var temp = new TempDirectory();
        // Arrange
        var file1 = Path.Combine(temp.Path, "File1.uci");
        var file2 = Path.Combine(temp.Path, "File2.uci");
        
        File.WriteAllText(file1, "`define MY_MACRO 1");
        File.WriteAllText(file2, "`define my_macro 2"); // Different case

        // Act
        _validator.ParseMacroFile(file1);
        _validator.ParseMacroFile(file2);
        var result = _validator.Validate();

        // Assert - should detect as redefinition (case insensitive)
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
    }
}
