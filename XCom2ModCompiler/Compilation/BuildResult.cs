namespace XCom2ModCompiler.Compilation;

/// <summary>
/// Represents the comprehensive result of a build operation.
/// </summary>
/// <param name="Success">Indicates whether the entire build process completed successfully.</param>
/// <param name="Duration">The total time taken for the build operation.</param>
/// <param name="OutputPaths">A list of absolute paths to the generated artifacts (e.g., .u files, shader caches).</param>
/// <param name="Errors">A list of error messages encountered during the build.</param>
/// <param name="Timings">A breakdown of the time spent in various phases of the build.</param>
public record BuildResult(
    bool Success,
    TimeSpan Duration,
    List<string> OutputPaths,
    List<string> Errors,
    List<TimingRecord> Timings);

/// <summary>
/// Represents a timing entry for a specific build phase.
/// </summary>
/// <param name="Description">A human-readable description of the phase (e.g., "Phase 1: Base Compilation").</param>
/// <param name="Seconds">The duration of the phase in fractional seconds.</param>
/// <param name="Share">A string representing the percentage of total build time this phase occupied.</param>
public record TimingRecord(
    string Description,
    double Seconds,
    string Share);

/// <summary>
/// Represents the result of a project cleanup operation.
/// </summary>
/// <param name="Success">Indicates whether the cleanup was successful.</param>
/// <param name="DeletedPaths">A list of paths that were successfully removed.</param>
/// <param name="Errors">A list of errors encountered during cleanup.</param>
public record CleanResult(
    bool Success,
    List<string> DeletedPaths,
    List<string> Errors);
