using Microsoft.Extensions.Logging;

namespace X2ModCompiler.Core.Configuration;

/// <summary>
/// Defines the available log verbosity levels for the X2ModCompiler.
/// This enum maps directly to Microsoft.Extensions.Logging.LogLevel for seamless integration.
/// </summary>
/// <remarks>
/// <para>This enum provides configuration-friendly names that map to the standard 
/// <see cref="Microsoft.Extensions.Logging.LogLevel"/> values used by ZLogger and 
/// Microsoft.Extensions.Logging.</para>
/// <para>Values:</para>
/// <list type="bullet">
/// <item><description><see cref="Trace"/> (0) - Most detailed logging, includes all trace information</description></item>
/// <item><description><see cref="Debug"/> (1) - Detailed diagnostic information for debugging</description></item>
/// <item><description><see cref="Information"/> (2) - Standard operational information</description></item>
/// <item><description><see cref="Warning"/> (3) - Only warnings and errors are logged</description></item>
/// <item><description><see cref="Error"/> (4) - Only errors are logged</description></item>
/// <item><description><see cref="Critical"/> (5) - Only critical errors are logged</description></item>
/// <item><description><see cref="None"/> (6) - Logging is disabled</description></item>
/// </list>
/// </remarks>
public enum CompilerLogLevel
{
    /// <summary>Most detailed logging, includes all trace information.</summary>
    Trace = LogLevel.Trace,

    /// <summary>Detailed diagnostic information for debugging.</summary>
    Debug = LogLevel.Debug,

    /// <summary>Standard operational information.</summary>
    Information = LogLevel.Information,

    /// <summary>Only warnings and errors are logged.</summary>
    Warning = LogLevel.Warning,

    /// <summary>Only errors are logged.</summary>
    Error = LogLevel.Error,

    /// <summary>Only critical errors are logged.</summary>
    Critical = LogLevel.Critical,

    /// <summary>Logging is disabled.</summary>
    None = LogLevel.None
}
