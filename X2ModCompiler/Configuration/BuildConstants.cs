namespace X2ModCompiler.Configuration;

/// <summary>
/// Defines constant values used throughout the build system.
/// These constants replace magic numbers and provide a single source of truth for configuration values.
/// </summary>
public static class BuildConstants
{
    /// <summary>
    /// Delay in milliseconds before starting the UnrealEd commandlet.
    /// This allows the file system to stabilize before compilation begins.
    /// </summary>
    public const int CommandletStartDelayMs = 1000;

    /// <summary>
    /// Delay in milliseconds after the UnrealEd commandlet completes.
    /// This ensures file handles are released before subsequent operations.
    /// </summary>
    public const int CommandletEndDelayMs = 5000;

    /// <summary>
    /// Delay in milliseconds between compilation passes to clear file handles.
    /// Used in two-pass compilation flow.
    /// </summary>
    public const int FileHandleClearDelayMs = 2000;

    /// <summary>
    /// Delay in milliseconds for config parser operations.
    /// </summary>
    public const int ConfigParserDelayMs = 1000;

    /// <summary>
    /// Maximum number of retry attempts for transient I/O operations.
    /// </summary>
    public const int MaxRetryAttempts = 5;

    /// <summary>
    /// Initial delay in milliseconds for retry operations.
    /// This delay is multiplied by 2 for each subsequent retry (exponential backoff).
    /// </summary>
    public const int InitialRetryDelayMs = 200;

    /// <summary>
    /// Timeout in milliseconds for process exit operations.
    /// </summary>
    public const int ProcessExitTimeoutMs = 5000;
}
