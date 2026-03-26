using Microsoft.Extensions.Logging;
using X2ModCompiler.Configuration;
using X2ModCompiler.Tracking;
using X2ModCompiler.Utilities;

namespace X2ModCompiler.Cooking;

/// <summary>
/// Manages TFC (Texture File Cache) files for the asset cooking pipeline.
/// Handles TFC file discovery, tracking, growth detection, and cleanup.
/// </summary>
public class TfcManager
{
    private readonly BuildOptions _options;
    private readonly CookerOutputTracker _tracker;
    private readonly ILogger<TfcManager> _logger;
    private readonly string _tfcSuffix;

    /// <summary>
    /// Initializes a new instance of the TfcManager class.
    /// </summary>
    /// <param name="options">The build options containing mod configuration.</param>
    /// <param name="tracker">The cooker output tracker for TFC metadata.</param>
    /// <param name="logger">The logger for diagnostic output.</param>
    public TfcManager(BuildOptions options, CookerOutputTracker tracker, ILogger<TfcManager> logger)
    {
        _options = options;
        _tracker = tracker;
        _logger = logger;
        _tfcSuffix = $"_{_options.ModNameCanonical}_DLCTFC_XPACK_";
    }

    /// <summary>
    /// Gets the TFC file suffix for the current mod.
    /// </summary>
    /// <returns>The TFC suffix string (e.g., "_TestMod_DLCTFC_XPACK_").</returns>
    public string GetTfcSuffix() => _tfcSuffix;

    /// <summary>
    /// Retrieves all TFC files generated for the current mod in the cooker output directory.
    /// </summary>
    /// <returns>An array of FileInfo objects for the mod's TFC files.</returns>
    public FileInfo[] GetOurTfcFiles()
    {
        return Directory.GetFiles(_options.CookerOutputPath, $"*{_tfcSuffix}.tfc")
            .Select(f => new FileInfo(f))
            .ToArray();
    }

    /// <summary>
    /// Retrieves tracking metadata for a specific TFC file.
    /// </summary>
    /// <param name="fullFileName">The full filename of the TFC file.</param>
    /// <returns>The TfcFileData if tracked, otherwise null.</returns>
    public TfcFileData? GetTfcTrackerData(string fullFileName)
    {
        return _tracker.TfcFiles.FirstOrDefault(f => f.FullFileName == fullFileName);
    }

    /// <summary>
    /// Adds a new TFC file to the tracker with current metadata.
    /// </summary>
    /// <param name="fileInfo">The FileInfo of the TFC file to track.</param>
    public void AddTfcToTracker(FileInfo fileInfo)
    {
        _tracker.TfcFiles.Add(new TfcFileData
        {
            FullFileName = fileInfo.Name,
            OriginalSize = fileInfo.Length,
            LastUpdatedUtc = fileInfo.LastWriteTimeUtc.Ticks
        });
    }

    /// <summary>
    /// Updates the timestamp of an existing tracked TFC file.
    /// </summary>
    /// <param name="fileInfo">The FileInfo of the TFC file to update.</param>
    public void UpdateTfcTimestamp(FileInfo fileInfo)
    {
        var trackedData = GetTfcTrackerData(fileInfo.Name);
        if (trackedData != null)
        {
            trackedData.LastUpdatedUtc = fileInfo.LastWriteTimeUtc.Ticks;
        }
    }

    /// <summary>
    /// Checks for TFC file growth compared to original tracked sizes.
    /// </summary>
    /// <returns>A list of growth entries for TFC files that have grown.</returns>
    public List<(string Name, string OriginalSize, string CurrentSize, string Increase)> CheckTfcGrowth()
    {
        var growthEntries = new List<(string, string, string, string)>();
        var tfcs = GetOurTfcFiles();

        foreach (var file in tfcs)
        {
            var trackedFileData = GetTfcTrackerData(file.Name);
            if (trackedFileData == null) continue;
            if (file.Length == trackedFileData.OriginalSize) continue;

            var increase = (double)file.Length / trackedFileData.OriginalSize;
            growthEntries.Add((
                file.Name,
                BuildUtilities.FormatFileSize(trackedFileData.OriginalSize),
                BuildUtilities.FormatFileSize(file.Length),
                $"{increase:F2}x"
            ));
        }

        return growthEntries;
    }

    /// <summary>
    /// Cleans mod-specific TFC files from the SDK cooker output directory.
    /// </summary>
    /// <param name="sdkCookedPath">The path to the SDK's cooked PC console directory.</param>
    public void CleanTfcFiles(string sdkCookedPath)
    {
        var tfcFiles = Directory.GetFiles(sdkCookedPath, $"*{_tfcSuffix}.tfc");
        foreach (var tfc in tfcFiles)
        {
            File.Delete(tfc);
        }
    }

    /// <summary>
    /// Formats a growth report table from growth entries.
    /// </summary>
    /// <param name="growthEntries">The list of growth entries to format.</param>
    /// <returns>A formatted table string.</returns>
    public string FormatGrowthReport(List<(string Name, string OriginalSize, string CurrentSize, string Increase)> growthEntries)
    {
        return TableFormatter.Format(growthEntries.Select(e => new
        {
            Name = e.Name,
            OriginalSize = e.OriginalSize,
            CurrentSize = e.CurrentSize,
            Increase = e.Increase
        }).ToList());
    }
}
