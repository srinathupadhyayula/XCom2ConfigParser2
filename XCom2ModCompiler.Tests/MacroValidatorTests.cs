using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using XCom2ModCompiler.Tracking;

namespace XCom2ModCompiler.Tests;

public class MacroValidatorTests : IDisposable
{
    private readonly Mock<ILogger<MacroValidator>> _loggerMock;
    private readonly MacroValidator _validator;
    private readonly string _tempPath;

    public MacroValidatorTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempPath);
        _loggerMock = new Mock<ILogger<MacroValidator>>();
        _validator = new MacroValidator(_loggerMock.Object);
    }

    [Fact]
    public void ParseMacroFile_ParsesValidMacroDefinitions()
    {
        // Arrange
        var macroFile = Path.Combine(_tempPath, "Globals.uci");
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
    public void ParseMacroFile_DetectsImplicitRedefinition()
    {
        // Arrange
        var file1 = Path.Combine(_tempPath, "File1.uci");
        var file2 = Path.Combine(_tempPath, "File2.uci");
        
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
    public void Validate_AllowsExplicitRedefine()
    {
        // Arrange
        var file1 = Path.Combine(_tempPath, "File1.uci");
        var file2 = Path.Combine(_tempPath, "File2.uci");
        
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
    public void ParseMacroFile_HandlesMissingFile()
    {
        // Arrange
        var nonExistentFile = Path.Combine(_tempPath, "NonExistent.uci");

        // Act & Assert - should not throw
        var ex = Record.Exception(() => _validator.ParseMacroFile(nonExistentFile));
        Assert.Null(ex);
    }

    [Fact]
    public void ParseMacroFile_IgnoresComments()
    {
        // Arrange
        var macroFile = Path.Combine(_tempPath, "Globals.uci");
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
    public void ParseMacroFile_HandlesMultipleMacrosInSingleFile()
    {
        // Arrange
        var macroFile = Path.Combine(_tempPath, "Globals.uci");
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
    public void ParseMacroFile_CaseInsensitiveComparison()
    {
        // Arrange
        var file1 = Path.Combine(_tempPath, "File1.uci");
        var file2 = Path.Combine(_tempPath, "File2.uci");
        
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

    public void Dispose()
    {
        if (Directory.Exists(_tempPath))
        {
            Directory.Delete(_tempPath, true);
        }
    }
}
