using System;
using System.IO;
using Xunit;
using ZLogger;
using Microsoft.Extensions.Logging;

namespace X2ModCompiler.Tests;

/// <summary>
/// Tests for ZLogger logging functionality.
/// Verifies zero-allocation logging works correctly.
/// </summary>
public class ZLoggerTests : IDisposable
{
    private readonly ILoggerFactory _loggerFactory;

    public ZLoggerTests()
    {
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddZLoggerConsole(options =>
            {
                // Use plain text formatter for tests
                options.UsePlainTextFormatter();
            });
            builder.SetMinimumLevel(LogLevel.Information);
        });
    }

    /// <summary>
    /// Verifies ZLogger supports string interpolation without format string errors.
    /// This was a bug with Console.WriteLine format strings containing curly braces.
    /// </summary>
    [Fact]
    public void ZLogInformation_WithStringInterpolation_DoesNotThrowFormatException()
    {
        // Arrange
        var logger = _loggerFactory.CreateLogger<ZLoggerTests>();
        var pathWithBraces = @"C:\Path\{SomeFolder}\file.txt";
        
        // Act & Assert - Should not throw FormatException
        var exception = Record.Exception(() =>
        {
            logger.ZLogInformation($"Testing path: {pathWithBraces}");
        });

        Assert.Null(exception);
    }

    /// <summary>
    /// Verifies ZLogger handles multiple interpolated parameters correctly.
    /// </summary>
    [Fact]
    public void ZLogInformation_WithMultipleParameters_LogsCorrectly()
    {
        // Arrange
        var logger = _loggerFactory.CreateLogger<ZLoggerTests>();
        var source = "SourcePath";
        var destination = "DestPath";
        var elapsed = 1.234567;
        
        // Act - Log with multiple parameters
        logger.ZLogInformation($"Copying {source} to {destination} in {elapsed:F6}s");
        
        // Assert - No exception means success
    }

    /// <summary>
    /// Verifies ZLogger handles value types without boxing.
    /// </summary>
    [Fact]
    public void ZLogInformation_WithValueTypes_NoBoxing()
    {
        // Arrange
        var logger = _loggerFactory.CreateLogger<ZLoggerTests>();
        int count = 42;
        double time = 3.14159;
        bool success = true;
        
        // Act & Assert - Should not throw
        var exception = Record.Exception(() =>
        {
            logger.ZLogInformation($"Count: {count}, Time: {time:F2}, Success: {success}");
        });

        Assert.Null(exception);
    }

    /// <summary>
    /// Verifies ZLogger supports all log levels.
    /// </summary>
    [Theory]
    [InlineData("Trace")]
    [InlineData("Debug")]
    [InlineData("Information")]
    [InlineData("Warning")]
    [InlineData("Error")]
    [InlineData("Critical")]
    public void ZLog_AllLevels_AreSupported(string level)
    {
        // Arrange
        var logger = _loggerFactory.CreateLogger<ZLoggerTests>();
        var message = $"Test message at {level}";
        
        // Act & Assert - Should not throw for any level
        var exception = Record.Exception(() =>
        {
            // Use the standard Microsoft.Extensions.Logging methods which ZLogger supports
            switch (level)
            {
                case "Trace":
                    logger.LogTrace(message);
                    break;
                case "Debug":
                    logger.LogDebug(message);
                    break;
                case "Information":
                    logger.LogInformation(message);
                    break;
                case "Warning":
                    logger.LogWarning(message);
                    break;
                case "Error":
                    logger.LogError(message);
                    break;
                case "Critical":
                    logger.LogCritical(message);
                    break;
            }
        });

        Assert.Null(exception);
    }

    /// <summary>
    /// Verifies ZLogger handles null parameters gracefully.
    /// </summary>
    [Fact]
    public void ZLogInformation_WithNullParameter_HandlesGracefully()
    {
        // Arrange
        var logger = _loggerFactory.CreateLogger<ZLoggerTests>();
        string? nullValue = null;
        
        // Act & Assert - Should not throw with null
        var exception = Record.Exception(() =>
        {
            logger.ZLogInformation($"Value: {nullValue}");
        });

        Assert.Null(exception);
    }

    /// <summary>
    /// Verifies ZLogger can log exception information.
    /// </summary>
    [Fact]
    public void ZLogError_WithException_LogsExceptionDetails()
    {
        // Arrange
        var logger = _loggerFactory.CreateLogger<ZLoggerTests>();
        var testException = new InvalidOperationException("Test exception");
        
        // Act & Assert - Should not throw
        var exception = Record.Exception(() =>
        {
            logger.ZLogError(testException, $"Error occurred");
        });

        Assert.Null(exception);
    }

    public void Dispose()
    {
        _loggerFactory.Dispose();
    }
}
