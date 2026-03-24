using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using XCom2ModCompiler.Tracking;
using XCom2ModCompiler.Configuration;

namespace XCom2ModCompiler.Tests;

public class BuildTrackerTests : IDisposable
{
    private readonly string _tempPath;
    private readonly Mock<ILogger<BuildTracker>> _loggerMock;
    private readonly BuildTracker _tracker;

    public BuildTrackerTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempPath);
        _loggerMock = new Mock<ILogger<BuildTracker>>();
        _tracker = new BuildTracker(_tempPath, _loggerMock.Object);
    }

    [Fact]
    public async Task LoadFingerprintAsync_ReturnsEmptyFingerprint_WhenNoFileExists()
    {
        var fp = await _tracker.LoadFingerprintAsync();
        Assert.Equal("", fp.BuildMode);
        Assert.Equal("", fp.GlobalsHash);
        Assert.Equal(DateTime.MinValue, fp.CoreTimestamp);
    }

    [Fact]
    public async Task SaveAndLoadFingerprintAsync_ReturnsSavedFingerprint()
    {
        var originalFp = new BuildFingerprint("debug", "abc123hash", new DateTime(2025, 1, 1), new DateTime(2025, 1, 2), new Dictionary<string, DateTime>());
        
        await _tracker.SaveFingerprintAsync(originalFp);
        var loadedFp = await _tracker.LoadFingerprintAsync();
        
        Assert.Equal("debug", loadedFp.BuildMode);
        Assert.Equal("abc123hash", loadedFp.GlobalsHash);
        Assert.Equal(originalFp.CoreTimestamp, loadedFp.CoreTimestamp);
    }

    [Fact]
    public async Task HasConfigurationChangedAsync_ReturnsTrue_WhenBuildModeChanges()
    {
        await _tracker.SaveFingerprintAsync(new BuildFingerprint("debug", "hash", DateTime.UtcNow, DateTime.UtcNow, new Dictionary<string, DateTime>()));
        
        var options = new BuildOptions { Debug = false }; // "release"
        var changed = await _tracker.HasConfigurationChangedAsync(options, "hash");
        
        Assert.True(changed);
    }

    [Fact]
    public async Task HasConfigurationChangedAsync_ReturnsTrue_WhenGlobalsHashChanges()
    {
        await _tracker.SaveFingerprintAsync(new BuildFingerprint("debug", "oldhash", DateTime.UtcNow, DateTime.UtcNow, new Dictionary<string, DateTime>()));
        
        var options = new BuildOptions { Debug = true };
        var changed = await _tracker.HasConfigurationChangedAsync(options, "newhash");
        
        Assert.True(changed);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempPath))
        {
            Directory.Delete(_tempPath, true);
        }
    }
}
