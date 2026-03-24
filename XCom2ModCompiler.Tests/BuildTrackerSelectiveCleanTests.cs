using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using XCom2ModCompiler.Configuration;
using XCom2ModCompiler.Tracking;

namespace XCom2ModCompiler.Tests;

public class BuildTrackerSelectiveCleanTests : IDisposable
{
    private readonly string _tempPath;
    private readonly Mock<ILogger<BuildTracker>> _loggerMock;
    private readonly BuildTracker _tracker;

    public BuildTrackerSelectiveCleanTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempPath);
        _loggerMock = new Mock<ILogger<BuildTracker>>();
        _tracker = new BuildTracker(_tempPath, _loggerMock.Object);
    }

    [Fact]
    public async Task ShouldRebuildAsync_ReturnsTrue_WhenBuildModeChanges()
    {
        // Arrange: Save debug fingerprint
        var fingerprint = new BuildFingerprint("debug", "hash123", DateTime.UtcNow, DateTime.UtcNow, new Dictionary<string, DateTime>());
        await _tracker.SaveFingerprintAsync(fingerprint);

        var options = new BuildOptions { Debug = false }; // release mode
        var globalsHash = "hash123";

        // Act
        var shouldRebuild = await _tracker.ShouldRebuildAsync(options, globalsHash);

        // Assert
        Assert.True(shouldRebuild);
    }

    [Fact]
    public async Task ShouldRebuildAsync_ReturnsTrue_WhenGlobalsHashChanges()
    {
        // Arrange
        var fingerprint = new BuildFingerprint("debug", "oldHash", DateTime.UtcNow, DateTime.UtcNow, new Dictionary<string, DateTime>());
        await _tracker.SaveFingerprintAsync(fingerprint);

        var options = new BuildOptions { Debug = true };
        var globalsHash = "newHash";

        // Act
        var shouldRebuild = await _tracker.ShouldRebuildAsync(options, globalsHash);

        // Assert
        Assert.True(shouldRebuild);
    }

    [Fact]
    public async Task ShouldRebuildAsync_ReturnsTrue_WhenCoreTimestampChanges()
    {
        // Arrange
        var oldCoreTime = DateTime.UtcNow.AddHours(-2);
        var newCoreTime = DateTime.UtcNow;
        
        var fingerprint = new BuildFingerprint("debug", "hash123", oldCoreTime, DateTime.UtcNow, new Dictionary<string, DateTime>());
        await _tracker.SaveFingerprintAsync(fingerprint);

        var options = new BuildOptions { Debug = true };
        var globalsHash = "hash123";

        // Act
        var shouldRebuild = await _tracker.ShouldRebuildAsync(options, globalsHash, newCoreTime);

        // Assert
        Assert.True(shouldRebuild);
    }

    [Fact]
    public async Task ShouldRebuildAsync_ReturnsFalse_WhenNothingChanged()
    {
        // Arrange
        var coreTime = DateTime.UtcNow;
        var fingerprint = new BuildFingerprint("debug", "hash123", coreTime, DateTime.UtcNow, new Dictionary<string, DateTime>());
        await _tracker.SaveFingerprintAsync(fingerprint);

        var options = new BuildOptions { Debug = true };
        var globalsHash = "hash123";

        // Act
        var shouldRebuild = await _tracker.ShouldRebuildAsync(options, globalsHash, coreTime);

        // Assert
        Assert.False(shouldRebuild);
    }

    [Fact]
    public async Task ShouldRebuildAsync_ReturnsTrue_WhenNoFingerprintExists()
    {
        // Arrange - no fingerprint saved
        var options = new BuildOptions { Debug = true };
        var globalsHash = "hash123";

        // Act
        var shouldRebuild = await _tracker.ShouldRebuildAsync(options, globalsHash);

        // Assert
        Assert.True(shouldRebuild); // First build should always rebuild
    }

    [Fact]
    public void ComputeFileHash_ReturnsConsistentHash()
    {
        // Arrange
        var testFile = Path.Combine(_tempPath, "test.txt");
        File.WriteAllText(testFile, "Test content for hashing");

        // Act
        var hash1 = BuildTracker.ComputeFileHash(testFile);
        var hash2 = BuildTracker.ComputeFileHash(testFile);

        // Assert
        Assert.Equal(hash1, hash2);
        Assert.NotEmpty(hash1);
    }

    [Fact]
    public void ComputeFileHash_ReturnsEmptyString_ForNonExistentFile()
    {
        // Arrange
        var nonExistentFile = Path.Combine(_tempPath, "DoesNotExist.txt");

        // Act
        var hash = BuildTracker.ComputeFileHash(nonExistentFile);

        // Assert
        Assert.Equal("", hash);
    }

    [Fact]
    public async Task GetSelectiveCleanPaths_ReturnsPathsToClean()
    {
        // Arrange
        var fingerprint = new BuildFingerprint("debug", "hash123", DateTime.UtcNow, DateTime.UtcNow, new Dictionary<string, DateTime>());
        await _tracker.SaveFingerprintAsync(fingerprint);

        var sdkPath = Path.Combine(_tempPath, "SDK");
        Directory.CreateDirectory(sdkPath);
        
        var scriptPath = Path.Combine(sdkPath, "XComGame", "Script");
        Directory.CreateDirectory(scriptPath);
        
        // Create test .u files
        File.WriteAllText(Path.Combine(scriptPath, "TestMod.u"), "test");
        File.WriteAllText(Path.Combine(scriptPath, "Dependency.u"), "test");

        var modScriptPackages = new[] { "TestMod", "Dependency" };

        // Act
        var pathsToClean = await _tracker.GetSelectiveCleanPathsAsync(sdkPath, modScriptPackages);

        // Assert
        Assert.Contains(Path.Combine(scriptPath, "TestMod.u"), pathsToClean);
        Assert.Contains(Path.Combine(scriptPath, "Dependency.u"), pathsToClean);
    }

    [Fact]
    public async Task GetSelectiveCleanPaths_ReturnsEmpty_WhenFilesDontExist()
    {
        // Arrange
        var fingerprint = new BuildFingerprint("debug", "hash123", DateTime.UtcNow, DateTime.UtcNow, new Dictionary<string, DateTime>());
        await _tracker.SaveFingerprintAsync(fingerprint);

        var sdkPath = Path.Combine(_tempPath, "SDK");
        Directory.CreateDirectory(sdkPath);

        var modScriptPackages = new[] { "NonExistent" };

        // Act
        var pathsToClean = await _tracker.GetSelectiveCleanPathsAsync(sdkPath, modScriptPackages);

        // Assert
        Assert.Empty(pathsToClean);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempPath))
        {
            Directory.Delete(_tempPath, true);
        }
    }
}
