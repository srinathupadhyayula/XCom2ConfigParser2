using Xunit;
using Microsoft.Extensions.Logging;
using NSubstitute;
using X2ModCompiler.Cooking;
using X2ModCompiler.Configuration;
using X2ModCompiler.Utilities;
using X2ModCompiler.Tracking;
using X2ModCompiler.Exceptions;

namespace X2ModCompiler.Tests.Cooking;

/// <summary>
/// Tests for ModAssetsCookStep class.
/// </summary>
public class ModAssetsCookStepTests
{
    private readonly BuildOptions _options;
    private readonly ContentOptions _contentOptions;
    private readonly IProcessRunner _processRunner;
    private readonly IFileMirrorParity _mirror;
    private readonly BuildTracker _tracker;
    private readonly ILogger<ModAssetsCookStep> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public ModAssetsCookStepTests()
    {
        _options = new BuildOptions
        {
            ModName = "TestMod",
            ProjectRoot = Path.Combine(Path.GetTempPath(), "TestProject"),
            SdkPath = Path.Combine(Path.GetTempPath(), "TestSdk"),
            GamePath = Path.Combine(Path.GetTempPath(), "TestGame"),
            ModDestinationPath = Path.Combine(Path.GetTempPath(), "TestDest"),
            BuildCachePathOverride = Path.Combine(Path.GetTempPath(), "TestCache")
        };

        _contentOptions = new ContentOptions();
        _processRunner = Substitute.For<IProcessRunner>();
        _mirror = Substitute.For<IFileMirrorParity>();
        _tracker = Substitute.For<BuildTracker>("cachePath", Substitute.For<ILogger<BuildTracker>>());
        _logger = Substitute.For<ILogger<ModAssetsCookStep>>();
        _loggerFactory = Substitute.For<ILoggerFactory>();
        _loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger>());
    }

    [Fact]
    public void Constructor_CreatesInstance_WithValidParameters()
    {
        // Arrange & Act
        var step = new ModAssetsCookStep(
            _options,
            _contentOptions,
            "/staging",
            _processRunner,
            _mirror,
            _tracker,
            _loggerFactory,
            _logger);

        // Assert
        Assert.NotNull(step);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsTrue_WhenNoAssetsToCook()
    {
        // Arrange
        var step = new ModAssetsCookStep(
            _options,
            new ContentOptions(), // Empty content options
            "/staging",
            _processRunner,
            _mirror,
            _tracker,
            _loggerFactory,
            _logger);

        // Act
        var result = await step.ExecuteAsync(CancellationToken.None);

        // Assert
        Assert.True(result);
        // Note: Logging verification is complex with ZLogger's structured logging
    }
}

/// <summary>
/// Tests for SdkEnvironmentVerifier extracted from ModAssetsCookStep.
/// </summary>
public class SdkEnvironmentVerifierTests
{
    private readonly string _tempDir;
    private readonly BuildOptions _options;

    public SdkEnvironmentVerifierTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);

        _options = new BuildOptions
        {
            ModName = "TestMod",
            ProjectRoot = Path.Combine(_tempDir, "Project"),
            SdkPath = Path.Combine(_tempDir, "Sdk"),
            GamePath = Path.Combine(_tempDir, "Game"),
            ModDestinationPath = Path.Combine(_tempDir, "Dest")
        };

        Directory.CreateDirectory(_options.ProjectRoot);
        Directory.CreateDirectory(_options.SdkPath);
        Directory.CreateDirectory(_options.GamePath);
    }

    [Fact]
    public void VerifyContentForCookExists_ThrowsBuildFailureException_WhenDirectoryMissing()
    {
        // Arrange
        var verifier = new SdkEnvironmentVerifier(_options);

        // Act & Assert - Should throw BuildFailureException
        Assert.Throws<BuildFailureException>(() => verifier.VerifyContentForCookExists());
    }

    [Fact]
    public void VerifyContentForCookExists_DoesNotThrow_WhenDirectoryExists()
    {
        // Arrange
        var contentForCook = Path.Combine(_options.ModSrcRoot, "ContentForCook");
        Directory.CreateDirectory(contentForCook);
        var verifier = new SdkEnvironmentVerifier(_options);

        // Act & Assert - Should not throw
        var exception = Record.Exception(() => verifier.VerifyContentForCookExists());
        Assert.Null(exception);
    }

    [Fact]
    public void VerifySdkContentModsDirectoryEmpty_ThrowsException_WhenDirectoryNotEmpty()
    {
        // Arrange
        var sdkContentMods = Path.Combine(_options.SdkPath, "XComGame", "Content", "Mods", _options.ModNameCanonical);
        Directory.CreateDirectory(sdkContentMods);
        File.WriteAllText(Path.Combine(sdkContentMods, "test.txt"), "content");
        var verifier = new SdkEnvironmentVerifier(_options);

        // Act & Assert
        var exception = Assert.Throws<Exception>(() => verifier.VerifySdkContentModsDirectoryEmpty());
        Assert.Contains("in use", exception.Message);
    }

    [Fact]
    public void VerifySdkContentModsDirectoryEmpty_DoesNotThrow_WhenDirectoryEmpty()
    {
        // Arrange
        var sdkContentMods = Path.Combine(_options.SdkPath, "XComGame", "Content", "Mods", _options.ModNameCanonical);
        Directory.CreateDirectory(sdkContentMods);
        var verifier = new SdkEnvironmentVerifier(_options);

        // Act & Assert - Should not throw
        var exception = Record.Exception(() => verifier.VerifySdkContentModsDirectoryEmpty());
        Assert.Null(exception);
    }

    [Fact]
    public void VerifySdkContentModsDirectoryEmpty_DoesNotThrow_WhenDirectoryDoesNotExist()
    {
        // Arrange
        var verifier = new SdkEnvironmentVerifier(_options);

        // Act & Assert - Should not throw
        var exception = Record.Exception(() => verifier.VerifySdkContentModsDirectoryEmpty());
        Assert.Null(exception);
    }

    [Fact]
    public void VerifyShippedGpcdExists_ThrowsException_WhenFileMissing()
    {
        // Arrange
        var verifier = new SdkEnvironmentVerifier(_options);

        // Act & Assert
        var exception = Assert.Throws<Exception>(() => verifier.VerifyShippedGpcdExists());
        Assert.Contains("GlobalPersistentCookerData.upk", exception.Message);
    }

    [Fact]
    public void VerifyShippedGpcdExists_DoesNotThrow_WhenFileExists()
    {
        // Arrange
        var gpcdPath = Path.Combine(_options.SdkPath, "XComGame", "CookedPCConsole", "GlobalPersistentCookerData.upk");
        Directory.CreateDirectory(Path.GetDirectoryName(gpcdPath)!);
        File.WriteAllText(gpcdPath, "dummy content");
        var verifier = new SdkEnvironmentVerifier(_options);

        // Act & Assert - Should not throw
        var exception = Record.Exception(() => verifier.VerifyShippedGpcdExists());
        Assert.Null(exception);
    }

    private void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }
}
