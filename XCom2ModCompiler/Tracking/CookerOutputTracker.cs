namespace XCom2ModCompiler.Tracking;

public class CookerOutputTracker
{
    public List<TfcFileData> TfcFiles { get; set; } = new();
    public List<SfPackageData> SfPackages { get; set; } = new();
}

public class TfcFileData
{
    public string FullFileName { get; set; } = "";
    public long OriginalSize { get; set; }
    public long LastUpdatedUtc { get; set; } // Ticks
}

public class SfPackageData
{
    public string FullFileName { get; set; } = "";
    public long LastUpdatedUtc { get; set; } // Ticks
}
