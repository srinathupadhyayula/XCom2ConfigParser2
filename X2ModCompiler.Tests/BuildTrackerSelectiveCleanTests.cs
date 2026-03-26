using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using X2ModCompiler.Configuration;
using X2ModCompiler.Tracking;
using X2ModCompiler.Tests.Shared;

namespace X2ModCompiler.Tests;

public class BuildTrackerSelectiveCleanTests : TestBase
{
    private readonly ILogger<BuildTracker> _logger;
    private readonly BuildTracker _tracker;

    public BuildTrackerSelectiveCleanTests()
    {
        _logger = Substitute.For<ILogger<BuildTracker>>();
        // Using a dummy path for the constructor since tests will use TempDirectory
        _tracker = new BuildTracker("tempPath", _logger);
    }

    [Fact]
    public async Task ShouldRebuildAsync_ReturnsTrue_WhenBuildModeChanges()
    {
        await using var temp = new TempDirectory();
        var tracker = new BuildTracker(temp.Path, _logger);

        // Arrange: Save debug fingerprint
        var fingerprint = new BuildFingerprint("debug", "hash123", DateTime.UtcNow, DateTime.UtcNow, new Dictionary<string, DateTime>(), new Dictionary<string, DateTime>());
        await tracker.SaveFingerprintAsync(fingerprint, TestContext.Current.CancellationToken);

        var options = new BuildOptions { Debug = false }; // release mode
        var globalsHash = "hash123";

        // Act
        var shouldRebuild = await tracker.ShouldRebuildAsync(options, globalsHash, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(shouldRebuild);
    }

    [Fact]
    public async Task ShouldRebuildAsync_ReturnsTrue_WhenGlobalsHashChanges()
    {
        await using var temp = new TempDirectory();
        var tracker = new BuildTracker(temp.Path, _logger);

        // Arrange
        var fingerprint = new BuildFingerprint("debug", "oldHash", DateTime.UtcNow, DateTime.UtcNow, new Dictionary<string, DateTime>(), new Dictionary<string, DateTime>());
        await tracker.SaveFingerprintAsync(fingerprint, TestContext.Current.CancellationToken);

        var options = new BuildOptions { Debug = true };
        var globalsHash = "newHash";

        // Act
        var shouldRebuild = await tracker.ShouldRebuildAsync(options, globalsHash, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(shouldRebuild);
    }

    [Fact]
    public async Task ShouldRebuildAsync_ReturnsTrue_WhenCoreTimestampChanges()
    {
        await using var temp = new TempDirectory();
        var tracker = new BuildTracker(temp.Path, _logger);

        // Arrange
        var oldCoreTime = DateTime.UtcNow.AddHours(-2);
        var newCoreTime = DateTime.UtcNow;
        
        var fingerprint = new BuildFingerprint("debug", "hash123", oldCoreTime, DateTime.UtcNow, new Dictionary<string, DateTime>(), new Dictionary<string, DateTime>());
        await tracker.SaveFingerprintAsync(fingerprint, TestContext.Current.CancellationToken);

        var options = new BuildOptions { Debug = true };
        var globalsHash = "hash123";

        // Act
        var shouldRebuild = await tracker.ShouldRebuildAsync(options, globalsHash, newCoreTime, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(shouldRebuild);
    }

    [Fact]
    public async Task ShouldRebuildAsync_ReturnsFalse_WhenNothingChanged()
    {
        await using var temp = new TempDirectory();
        var tracker = new BuildTracker(temp.Path, _logger);

        // Arrange
        var coreTime = DateTime.UtcNow;
        var fingerprint = new BuildFingerprint("debug", "hash123", coreTime, DateTime.UtcNow, new Dictionary<string, DateTime>(), new Dictionary<string, DateTime>());
        await tracker.SaveFingerprintAsync(fingerprint, TestContext.Current.CancellationToken);

        var options = new BuildOptions { Debug = true };
        var globalsHash = "hash123";

        // Act
        var shouldRebuild = await tracker.ShouldRebuildAsync(options, globalsHash, coreTime, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(shouldRebuild);
    }

    [Fact]
    public async Task ShouldRebuildAsync_ReturnsTrue_WhenNoFingerprintExists()
    {
        await using var temp = new TempDirectory();
        var tracker = new BuildTracker(temp.Path, _logger);

        // Arrange - no fingerprint saved
        var options = new BuildOptions { Debug = true };
        var globalsHash = "hash123";

        // Act
        var shouldRebuild = await tracker.ShouldRebuildAsync(options, globalsHash, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(shouldRebuild); // First build should always rebuild
    }

    [Fact]
    public async Task ComputeFileHash_ReturnsConsistentHash()
    {
        await using var temp = new TempDirectory();
        // Act & Assert
        // Re-implementing logic with TempDirectory logic
        var testFile = Path.Combine(temp.Path, "test.txt");
        File.WriteAllText(testFile, "Test content for hashing");

        var hash1 = BuildTracker.ComputeFileHash(testFile);
        var hash2 = BuildTracker.ComputeFileHash(testFile);

        Assert.Equal(hash1, hash2);
        Assert.NotEmpty(hash1);
    }

    [Fact]
    public async Task ComputeFileHash_ReturnsEmptyString_ForNonExistentFile()
    {
        await using var temp = new TempDirectory();
        // Arrange
        var nonExistentFile = Path.Combine(temp.Path, "DoesNotExist.txt");

        // Act
        var hash = BuildTracker.ComputeFileHash(nonExistentFile);

        // Assert
        Assert.Equal("", hash);
    }

    [Fact]
    public async Task GetSelectiveCleanPaths_ReturnsPathsToClean()
    {
        await using var temp = new TempDirectory();
        var tracker = new BuildTracker(temp.Path, _logger);

        // Arrange
        var fingerprint = new BuildFingerprint("debug", "hash123", DateTime.UtcNow, DateTime.UtcNow, new Dictionary<string, DateTime>(), new Dictionary<string, DateTime>());
        await tracker.SaveFingerprintAsync(fingerprint, TestContext.Current.CancellationToken);

        var sdkPath = Path.Combine(temp.Path, "SDK");
        Directory.CreateDirectory(sdkPath);
        
        var scriptPath = Path.Combine(sdkPath, "XComGame", "Script");
        Directory.CreateDirectory(scriptPath);
        
        // Create test .u files
        File.WriteAllText(Path.Combine(scriptPath, "TestMod.u"), "test");
        File.WriteAllText(Path.Combine(scriptPath, "Dependency.u"), "test");

        var modScriptPackages = new[] { "TestMod", "Dependency" };

        // Act
        var pathsToClean = await tracker.GetSelectiveCleanPathsAsync(sdkPath, modScriptPackages, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(Path.Combine(scriptPath, "TestMod.u"), pathsToClean);
        Assert.Contains(Path.Combine(scriptPath, "Dependency.u"), pathsToClean);
    }

    [Fact]
    public async Task GetSelectiveCleanPaths_ReturnsEmpty_WhenFilesDontExist()
    {
        await using var temp = new TempDirectory();
        var tracker = new BuildTracker(temp.Path, _logger);

        // Arrange
        var fingerprint = new BuildFingerprint("debug", "hash123", DateTime.UtcNow, DateTime.UtcNow, new Dictionary<string, DateTime>(), new Dictionary<string, DateTime>());
        await tracker.SaveFingerprintAsync(fingerprint, TestContext.Current.CancellationToken);

        var sdkPath = Path.Combine(temp.Path, "SDK");
        Directory.CreateDirectory(sdkPath);

        var modScriptPackages = new[] { "NonExistent" };

        // Act
        var pathsToClean = await tracker.GetSelectiveCleanPathsAsync(sdkPath, modScriptPackages, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(pathsToClean);
    }
}
