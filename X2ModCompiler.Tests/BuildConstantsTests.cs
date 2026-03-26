using Xunit;
using X2ModCompiler.Configuration;

namespace X2ModCompiler.Tests;

public class BuildConstantsTests
{
    [Fact]
    public void CommandletStartDelayMs_HasReasonableValue()
    {
        // Assert
        Assert.Equal(1000, BuildConstants.CommandletStartDelayMs);
        Assert.True(BuildConstants.CommandletStartDelayMs >= 0);
    }

    [Fact]
    public void CommandletEndDelayMs_HasReasonableValue()
    {
        // Assert
        Assert.Equal(5000, BuildConstants.CommandletEndDelayMs);
        Assert.True(BuildConstants.CommandletEndDelayMs >= 0);
    }

    [Fact]
    public void FileHandleClearDelayMs_HasReasonableValue()
    {
        // Assert
        Assert.Equal(2000, BuildConstants.FileHandleClearDelayMs);
        Assert.True(BuildConstants.FileHandleClearDelayMs >= 0);
    }

    [Fact]
    public void ConfigParserDelayMs_HasReasonableValue()
    {
        // Assert
        Assert.Equal(1000, BuildConstants.ConfigParserDelayMs);
        Assert.True(BuildConstants.ConfigParserDelayMs >= 0);
    }

    [Fact]
    public void MaxRetryAttempts_HasReasonableValue()
    {
        // Assert
        Assert.Equal(5, BuildConstants.MaxRetryAttempts);
        Assert.True(BuildConstants.MaxRetryAttempts > 0);
    }

    [Fact]
    public void InitialRetryDelayMs_HasReasonableValue()
    {
        // Assert
        Assert.Equal(200, BuildConstants.InitialRetryDelayMs);
        Assert.True(BuildConstants.InitialRetryDelayMs > 0);
    }

    [Fact]
    public void ProcessExitTimeoutMs_HasReasonableValue()
    {
        // Assert
        Assert.Equal(5000, BuildConstants.ProcessExitTimeoutMs);
        Assert.True(BuildConstants.ProcessExitTimeoutMs > 0);
    }
}
