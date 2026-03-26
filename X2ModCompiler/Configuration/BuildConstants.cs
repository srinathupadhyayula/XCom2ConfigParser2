namespace X2ModCompiler.Configuration;

/// <summary>
/// Defines constant values used throughout the build system.
/// These constants replace magic numbers and provide a single source of truth for configuration values.
/// </summary>
public static class BuildConstants
{
    #region Timing Constants

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

    #endregion

    #region Retry Constants

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

    #endregion

    #region Application Constants

    /// <summary>
    /// The application name.
    /// </summary>
    public const string ApplicationName = "X2ModCompiler";

    /// <summary>
    /// The application version.
    /// </summary>
    public const string ApplicationVersion = "1.1.0";

    /// <summary>
    /// The build command name.
    /// </summary>
    public const string BuildCommandName = "build";

    /// <summary>
    /// The validate command name.
    /// </summary>
    public const string ValidateCommandName = "validate";

    /// <summary>
    /// The clean command name.
    /// </summary>
    public const string CleanCommandName = "clean";

    #endregion

    #region Build Header Messages

    /// <summary>
    /// Build in-progress header message format.
    /// </summary>
    public const string BuildInProgressHeader = "BUILDING {0}";

    /// <summary>
    /// Build success header message.
    /// </summary>
    public const string BuildSuccessHeader = "BUILD COMPLETED SUCCESSFULLY";

    /// <summary>
    /// Build failure header message.
    /// </summary>
    public const string BuildFailureHeader = "BUILD FAILED";

    /// <summary>
    /// Clean in-progress header message format.
    /// </summary>
    public const string CleanInProgressHeader = "CLEANING {0}";

    /// <summary>
    /// Clean success header message.
    /// </summary>
    public const string CleanSuccessHeader = "Clean completed successfully.";

    /// <summary>
    /// Clean failure header message.
    /// </summary>
    public const string CleanFailureHeader = "Clean failed.";

    /// <summary>
    /// Validation in-progress header message format.
    /// </summary>
    public const string ValidationInProgressHeader = "VALIDATING {0} CONFIGURATION";

    #endregion

    #region Path Constants

    /// <summary>
    /// Default build cache directory name.
    /// </summary>
    public const string BuildCacheDirectoryName = "BuildCache";

    /// <summary>
    /// Default config directory name.
    /// </summary>
    public const string ConfigDirectoryName = "Config";

    /// <summary>
    /// Default source directory name.
    /// </summary>
    public const string SrcDirectoryName = "Src";

    #endregion
}
