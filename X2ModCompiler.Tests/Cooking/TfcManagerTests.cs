using Xunit;
using Microsoft.Extensions.Logging;
using NSubstitute;
using X2ModCompiler.Cooking;
using X2ModCompiler.Configuration;
using X2ModCompiler.Tracking;

namespace X2ModCompiler.Tests.Cooking;

/// <summary>
/// Tests for TfcManager extracted from ModAssetsCookStep.
/// </summary>
public class TfcManagerTests
{
    private readonly string _tempDir;
    private readonly BuildOptions _options;
    private readonly CookerOutputTracker _tracker;
    private readonly ILogger<TfcManager> _logger;

    public TfcManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);

        _options = new BuildOptions
        {
            ModName = "TestMod",
            ProjectRoot = Path.Combine(_tempDir, "Project"),
            SdkPath = Path.Combine(_tempDir, "Sdk"),
            GamePath = Path.Combine(_tempDir, "Game"),
            ModDestinationPath = Path.Combine(_tempDir, "Dest"),
            BuildCachePathOverride = Path.Combine(_tempDir, "Cache")
        };

        Directory.CreateDirectory(_options.ProjectRoot);
        Directory.CreateDirectory(_options.SdkPath);
        Directory.CreateDirectory(_options.GamePath);
        Directory.CreateDirectory(_options.CookerOutputPath);

        _tracker = new CookerOutputTracker();
        _logger = Substitute.For<ILogger<TfcManager>>();
    }

    [Fact]
    public void Constructor_CreatesInstance_WithValidParameters()
    {
        // Arrange & Act
        var manager = new TfcManager(_options, _tracker, _logger);

        // Assert
        Assert.NotNull(manager);
    }

    [Fact]
    public void GetTfcSuffix_ReturnsFormattedSuffix_WithModName()
    {
        // Arrange
        var manager = new TfcManager(_options, _tracker, _logger);

        // Act
        var suffix = manager.GetTfcSuffix();

        // Assert
        Assert.Equal("_TestMod_DLCTFC_XPACK_", suffix);
    }

    [Fact]
    public void GetOurTfcFiles_ReturnsEmptyArray_WhenNoTfcFilesExist()
    {
        // Arrange
        var manager = new TfcManager(_options, _tracker, _logger);

        // Act
        var files = manager.GetOurTfcFiles();

        // Assert
        Assert.Empty(files);
    }

    [Fact]
    public void GetOurTfcFiles_ReturnsMatchingTfcFiles_OnlyModSpecificFiles()
    {
        // Arrange
        var manager = new TfcManager(_options, _tracker, _logger);
        
        // Create TFC files with correct suffix
        var tfc1 = Path.Combine(_options.CookerOutputPath, "Texture1_TestMod_DLCTFC_XPACK_.tfc");
        var tfc2 = Path.Combine(_options.CookerOutputPath, "Texture2_TestMod_DLCTFC_XPACK_.tfc");
        File.WriteAllText(tfc1, "dummy content 1");
        File.WriteAllText(tfc2, "dummy content 2");
        
        // Create TFC file with wrong suffix (should be ignored)
        var wrongTfc = Path.Combine(_options.CookerOutputPath, "OtherMod_OtherMod_DLCTFC_XPACK_.tfc");
        File.WriteAllText(wrongTfc, "other content");

        // Act
        var files = manager.GetOurTfcFiles();

        // Assert
        Assert.Equal(2, files.Length);
        Assert.All(files, f => Assert.Contains("TestMod_DLCTFC_XPACK_", f.Name));
    }

    [Fact]
    public void GetTfcTrackerData_ReturnsNull_WhenTfcNotTracked()
    {
        // Arrange
        var manager = new TfcManager(_options, _tracker, _logger);
        _tracker.TfcFiles.Add(new TfcFileData 
        { 
            FullFileName = "TrackedTfc_TestMod_DLCTFC_XPACK_.tfc",
            OriginalSize = 1024,
            LastUpdatedUtc = DateTime.UtcNow.Ticks
        });

        // Act
        var data = manager.GetTfcTrackerData("UntrackedTfc_TestMod_DLCTFC_XPACK_.tfc");

        // Assert
        Assert.Null(data);
    }

    [Fact]
    public void GetTfcTrackerData_ReturnsData_WhenTfcIsTracked()
    {
        // Arrange
        var manager = new TfcManager(_options, _tracker, _logger);
        var expectedData = new TfcFileData 
        { 
            FullFileName = "TrackedTfc_TestMod_DLCTFC_XPACK_.tfc",
            OriginalSize = 1024,
            LastUpdatedUtc = DateTime.UtcNow.Ticks
        };
        _tracker.TfcFiles.Add(expectedData);

        // Act
        var data = manager.GetTfcTrackerData("TrackedTfc_TestMod_DLCTFC_XPACK_.tfc");

        // Assert
        Assert.NotNull(data);
        Assert.Equal(expectedData.FullFileName, data.FullFileName);
        Assert.Equal(expectedData.OriginalSize, data.OriginalSize);
    }

    [Fact]
    public void AddTfcToTracker_AddsNewTfcFile_WithCorrectMetadata()
    {
        // Arrange
        var manager = new TfcManager(_options, _tracker, _logger);
        var tfcPath = Path.Combine(_options.CookerOutputPath, "NewTfc_TestMod_DLCTFC_XPACK_.tfc");
        File.WriteAllText(tfcPath, "dummy content");
        var fileInfo = new FileInfo(tfcPath);

        // Act
        manager.AddTfcToTracker(fileInfo);

        // Assert
        var trackedData = _tracker.TfcFiles.FirstOrDefault(f => f.FullFileName == "NewTfc_TestMod_DLCTFC_XPACK_.tfc");
        Assert.NotNull(trackedData);
        Assert.Equal(fileInfo.Length, trackedData.OriginalSize);
        Assert.Equal(fileInfo.LastWriteTimeUtc.Ticks, trackedData.LastUpdatedUtc);
    }

    [Fact]
    public void UpdateTfcTimestamp_UpdatesExistingTfc_WithNewTimestamp()
    {
        // Arrange
        var manager = new TfcManager(_options, _tracker, _logger);
        var originalTicks = DateTime.UtcNow.AddHours(-1).Ticks;
        _tracker.TfcFiles.Add(new TfcFileData 
        { 
            FullFileName = "ExistingTfc_TestMod_DLCTFC_XPACK_.tfc",
            OriginalSize = 1024,
            LastUpdatedUtc = originalTicks
        });
        
        var tfcPath = Path.Combine(_options.CookerOutputPath, "ExistingTfc_TestMod_DLCTFC_XPACK_.tfc");
        File.WriteAllText(tfcPath, "dummy content");
        var fileInfo = new FileInfo(tfcPath);
        var newTicks = fileInfo.LastWriteTimeUtc.Ticks;

        // Act
        manager.UpdateTfcTimestamp(fileInfo);

        // Assert
        var trackedData = _tracker.TfcFiles.First(f => f.FullFileName == "ExistingTfc_TestMod_DLCTFC_XPACK_.tfc");
        Assert.Equal(newTicks, trackedData.LastUpdatedUtc);
        Assert.Equal(1024, trackedData.OriginalSize); // Original size unchanged
    }

    [Fact]
    public void CheckTfcGrowth_ReturnsEmptyList_WhenNoGrowthDetected()
    {
        // Arrange
        var manager = new TfcManager(_options, _tracker, _logger);
        var tfcPath = Path.Combine(_options.CookerOutputPath, "StableTfc_TestMod_DLCTFC_XPACK_.tfc");
        File.WriteAllText(tfcPath, "dummy content");
        var fileInfo = new FileInfo(tfcPath);
        
        _tracker.TfcFiles.Add(new TfcFileData 
        { 
            FullFileName = "StableTfc_TestMod_DLCTFC_XPACK_.tfc",
            OriginalSize = fileInfo.Length,
            LastUpdatedUtc = fileInfo.LastWriteTimeUtc.Ticks
        });

        // Act
        var growthEntries = manager.CheckTfcGrowth();

        // Assert
        Assert.Empty(growthEntries);
    }

    [Fact]
    public void CheckTfcGrowth_ReturnsGrowthEntries_WhenTfcFilesGrew()
    {
        // Arrange
        var manager = new TfcManager(_options, _tracker, _logger);
        var tfcPath = Path.Combine(_options.CookerOutputPath, "GrowingTfc_TestMod_DLCTFC_XPACK_.tfc");
        File.WriteAllText(tfcPath, "dummy content that is larger than before");
        var fileInfo = new FileInfo(tfcPath);
        
        // Track with smaller original size to simulate growth
        _tracker.TfcFiles.Add(new TfcFileData 
        { 
            FullFileName = "GrowingTfc_TestMod_DLCTFC_XPACK_.tfc",
            OriginalSize = 10, // Much smaller than current
            LastUpdatedUtc = fileInfo.LastWriteTimeUtc.Ticks
        });

        // Act
        var growthEntries = manager.CheckTfcGrowth();

        // Assert
        Assert.NotEmpty(growthEntries);
        var entry = growthEntries.First();
        Assert.Equal("GrowingTfc_TestMod_DLCTFC_XPACK_.tfc", entry.Name);
        Assert.Contains("x", entry.Increase); // Should contain multiplier like "4.50x"
    }

    [Fact]
    public void CleanTfcFiles_DeletesAllMatchingTfcFiles_FromSdkPath()
    {
        // Arrange
        var manager = new TfcManager(_options, _tracker, _logger);
        var sdkCookedPath = Path.Combine(_options.SdkPath, "XComGame", "Published", "CookedPCConsole");
        Directory.CreateDirectory(sdkCookedPath);
        
        // Create TFC files to be cleaned
        var tfc1 = Path.Combine(sdkCookedPath, "Texture1_TestMod_DLCTFC_XPACK_.tfc");
        var tfc2 = Path.Combine(sdkCookedPath, "Texture2_TestMod_DLCTFC_XPACK_.tfc");
        File.WriteAllText(tfc1, "content 1");
        File.WriteAllText(tfc2, "content 2");
        
        // Create TFC file that should NOT be cleaned (wrong mod)
        var otherTfc = Path.Combine(sdkCookedPath, "OtherMod_OtherMod_DLCTFC_XPACK_.tfc");
        File.WriteAllText(otherTfc, "other content");

        // Act
        manager.CleanTfcFiles(sdkCookedPath);

        // Assert
        Assert.False(File.Exists(tfc1));
        Assert.False(File.Exists(tfc2));
        Assert.True(File.Exists(otherTfc)); // Should not be deleted
    }

    [Fact]
    public void FormatGrowthReport_ReturnsFormattedTable_WithGrowthData()
    {
        // Arrange
        var manager = new TfcManager(_options, _tracker, _logger);
        var growthEntries = new List<(string Name, string OriginalSize, string CurrentSize, string Increase)>
        {
            ("Tfc1_TestMod_DLCTFC_XPACK_.tfc", "100 B", "500 B", "5.00x"),
            ("Tfc2_TestMod_DLCTFC_XPACK_.tfc", "1 KB", "3 KB", "3.00x")
        };

        // Act
        var report = manager.FormatGrowthReport(growthEntries);

        // Assert
        Assert.NotNull(report);
        Assert.Contains("Tfc1_TestMod_DLCTFC_XPACK_.tfc", report);
        Assert.Contains("5.00x", report);
    }

    private void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }
}
