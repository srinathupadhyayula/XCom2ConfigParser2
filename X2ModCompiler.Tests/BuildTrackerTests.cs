using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using X2ModCompiler.Tracking;
using X2ModCompiler.Configuration;
using X2ModCompiler.Tests.Shared;

namespace X2ModCompiler.Tests;

public class BuildTrackerTests : TestBase
{
    private readonly ILogger<BuildTracker> _logger;

    public BuildTrackerTests()
    {
        _logger = Substitute.For<ILogger<BuildTracker>>();
    }

    [Fact]
    public async Task LoadFingerprintAsync_ReturnsEmptyFingerprint_WhenNoFileExists()
    {
        await using var temp = new TempDirectory();
        var tracker = new BuildTracker(temp.Path, _logger);
        
        var fp = await tracker.LoadFingerprintAsync(TestContext.Current.CancellationToken);
        Assert.Equal("", fp.BuildMode);
        Assert.Equal("", fp.GlobalsHash);
        Assert.Equal(DateTime.MinValue, fp.CoreTimestamp);
    }

    [Fact]
    public async Task SaveAndLoadFingerprintAsync_ReturnsSavedFingerprint()
    {
        await using var temp = new TempDirectory();
        var tracker = new BuildTracker(temp.Path, _logger);

        var originalFp = new BuildFingerprint("debug", "abc123hash", new DateTime(2025, 1, 1), new DateTime(2025, 1, 2), new Dictionary<string, DateTime>(), new Dictionary<string, DateTime>());

        await tracker.SaveFingerprintAsync(originalFp, TestContext.Current.CancellationToken);
        var loadedFp = await tracker.LoadFingerprintAsync(TestContext.Current.CancellationToken);

        Assert.Equal("debug", loadedFp.BuildMode);
        Assert.Equal("abc123hash", loadedFp.GlobalsHash);
        Assert.Equal(originalFp.CoreTimestamp, loadedFp.CoreTimestamp);
    }

    [Fact]
    public async Task HasConfigurationChangedAsync_ReturnsTrue_WhenBuildModeChanges()
    {
        await using var temp = new TempDirectory();
        var tracker = new BuildTracker(temp.Path, _logger);

        await tracker.SaveFingerprintAsync(new BuildFingerprint("debug", "hash", DateTime.UtcNow, DateTime.UtcNow, new Dictionary<string, DateTime>(), new Dictionary<string, DateTime>()), TestContext.Current.CancellationToken);

        var options = new BuildOptions { Debug = false }; // "release"
        var changed = await tracker.HasConfigurationChangedAsync(options, "hash", TestContext.Current.CancellationToken);

        Assert.True(changed);
    }

    [Fact]
    public async Task HasConfigurationChangedAsync_ReturnsTrue_WhenGlobalsHashChanges()
    {
        await using var temp = new TempDirectory();
        var tracker = new BuildTracker(temp.Path, _logger);

        await tracker.SaveFingerprintAsync(new BuildFingerprint("debug", "oldhash", DateTime.UtcNow, DateTime.UtcNow, new Dictionary<string, DateTime>(), new Dictionary<string, DateTime>()), TestContext.Current.CancellationToken);

        var options = new BuildOptions { Debug = true };
        var changed = await tracker.HasConfigurationChangedAsync(options, "newhash", TestContext.Current.CancellationToken);

        Assert.True(changed);
    }
}
