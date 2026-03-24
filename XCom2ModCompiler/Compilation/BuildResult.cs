namespace XCom2ModCompiler.Compilation;

public record BuildResult(
    bool Success,
    TimeSpan Duration,
    List<string> OutputPaths,
    List<string> Errors,
    List<TimingRecord> Timings);

public record TimingRecord(
    string Description,
    double Seconds,
    string Share);

public record CleanResult(
    bool Success,
    List<string> DeletedPaths,
    List<string> Errors);
