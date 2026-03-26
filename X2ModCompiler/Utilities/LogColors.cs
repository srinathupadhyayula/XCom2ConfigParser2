using Kokuban;

namespace X2ModCompiler.Utilities;

/// <summary>
/// Provides standardized color formatting for logging output using Kokuban.
/// This class ensures consistent color usage across all logging components.
/// </summary>
/// <remarks>
/// Color palette:
/// <list type="bullet">
/// <item><description><see cref="Error"/> - Bold Red (critical failures)</description></item>
/// <item><description><see cref="Warning"/> - Yellow (non-fatal issues)</description></item>
/// <item><description><see cref="Info"/> - Cyan (normal operation messages)</description></item>
/// <item><description><see cref="Success"/> - Bold Green (successful operations)</description></item>
/// <item><description><see cref="Debug"/> - Gray (detailed diagnostic info)</description></item>
/// <item><description><see cref="StepHeader"/> - Bold Blue (build step headers)</description></item>
/// <item><description><see cref="PhaseHeader"/> - Bold Yellow (phase headers)</description></item>
/// <item><description><see cref="ModeIndicator"/> - Magenta (mode indicators)</description></item>
/// <item><description><see cref="PathInfo"/> - Gray (path information)</description></item>
/// <item><description><see cref="PackageName"/> - Gray (package names)</description></item>
/// </list>
/// </remarks>
public static class LogColors
{
    /// <summary>
    /// Formats an error message in bold red.
    /// Use for critical failures that stop execution.
    /// </summary>
    public static string Error(string message) => Chalk.Bold.Red[message].ToString()!;

    /// <summary>
    /// Formats a warning message in yellow.
    /// Use for non-fatal issues that don't stop execution.
    /// </summary>
    public static string Warning(string message) => Chalk.Yellow[message].ToString()!;

    /// <summary>
    /// Formats an informational message in cyan.
    /// Use for normal operation messages.
    /// </summary>
    public static string Info(string message) => Chalk.Cyan[message].ToString()!;

    /// <summary>
    /// Formats a success message in bold green.
    /// Use for successful operations.
    /// </summary>
    public static string Success(string message) => Chalk.Bold.Green[message].ToString()!;

    /// <summary>
    /// Formats a debug message in gray.
    /// Use for detailed diagnostic information.
    /// </summary>
    public static string Debug(string message) => Chalk.Gray[message].ToString()!;

    /// <summary>
    /// Formats a step header in bold blue.
    /// Use for build step headers (e.g., ">>> STARTING STEP: Compilation").
    /// </summary>
    public static string StepHeader(string stepName) => Chalk.Bold.Blue[$">>> STARTING STEP: {stepName}"].ToString()!;

    /// <summary>
    /// Formats a phase header in bold yellow.
    /// Use for phase headers (e.g., "PHASE 1: INITIAL COMPILATION").
    /// </summary>
    public static string PhaseHeader(string phaseName) => Chalk.Bold.Yellow[phaseName].ToString()!;

    /// <summary>
    /// Formats a mode indicator in magenta.
    /// Use for mode indicators (e.g., "[MODE] Two-Pass compilation").
    /// </summary>
    public static string ModeIndicator(string mode) => Chalk.Magenta[$"[MODE] {mode}"].ToString()!;

    /// <summary>
    /// Formats path information in gray.
    /// Use for displaying file paths.
    /// </summary>
    public static string PathInfo(string path) => Chalk.Gray[$"Target path: {path}"].ToString()!;

    /// <summary>
    /// Formats a package name in gray.
    /// Use for displaying package names in lists.
    /// </summary>
    public static string PackageName(string pkg) => Chalk.Gray[$"  -> {pkg}"].ToString()!;

    /// <summary>
    /// Formats a build header in bold cyan.
    /// Use for main build headers (e.g., "BUILDING ModName").
    /// </summary>
    public static string BuildHeader(string message) => Chalk.Bold.Cyan[$"  {message}"].ToString()!;

    /// <summary>
    /// Formats a success header in bold green.
    /// Use for completion headers (e.g., "BUILD COMPLETED SUCCESSFULLY").
    /// </summary>
    public static string SuccessHeader(string message) => Chalk.Bold.Green[$"#  {message}"].ToString()!;

    /// <summary>
    /// Formats an error header in bold red.
    /// Use for failure headers (e.g., "BUILD FAILED").
    /// </summary>
    public static string ErrorHeader(string message) => Chalk.Bold.Red[$"!  {message}"].ToString()!;

    /// <summary>
    /// Formats a separator line in bold cyan.
    /// Use for visual separation in console output.
    /// </summary>
    public static string Separator => Chalk.Bold.Cyan["================================================================================"].ToString()!;

    /// <summary>
    /// Formats a success separator in bold green.
    /// </summary>
    public static string SuccessSeparator => Chalk.Bold.Green["################################################################################"].ToString()!;

    /// <summary>
    /// Formats an error separator in bold red.
    /// </summary>
    public static string ErrorSeparator => Chalk.Bold.Red["!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!"].ToString()!;

    /// <summary>
    /// Formats a progress message in blue.
    /// Use for progress indicators (e.g., "Processing...", "Compiling...").
    /// </summary>
    public static string Progress(string message) => Chalk.Blue[message].ToString()!;

    /// <summary>
    /// Formats a success detail message in green with a checkmark.
    /// Use for detailed success messages (e.g., "✓ File compiled successfully").
    /// </summary>
    public static string SuccessDetail(string message) => Chalk.Green[$"  ✓ {message}"].ToString()!;

    /// <summary>
    /// Formats an error detail message in red with an X mark.
    /// Use for detailed error messages (e.g., "✗ Compilation failed").
    /// </summary>
    public static string ErrorDetail(string message) => Chalk.Red[$"  ✗ {message}"].ToString()!;

    /// <summary>
    /// Formats a timing message in gray with brackets.
    /// Use for timing information (e.g., "[12.345s] Operation completed").
    /// </summary>
    public static string Timing(string message) => Chalk.Gray[$"[{message}]"].ToString()!;

    /// <summary>
    /// Formats a configuration value in cyan.
    /// Use for displaying configuration values (e.g., "DebugMode: true").
    /// </summary>
    public static string ConfigValue(string value) => Chalk.Cyan[value].ToString()!;
}
