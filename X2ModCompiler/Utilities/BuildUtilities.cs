using System.Globalization;

namespace X2ModCompiler.Utilities;

/// <summary>
/// Utility methods for formatting and output.
/// </summary>
public static class BuildUtilities
{
    private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

    /// <summary>
    /// Formats a file size in bytes to a human-readable string.
    /// </summary>
    public static string FormatFileSize(long size)
    {
        if (size > 1L * 1024 * 1024 * 1024 * 1024) // TB
            return $"{size / (1024.0 * 1024.0 * 1024.0 * 1024.0):0.00} TB";
        if (size > 1L * 1024 * 1024 * 1024) // GB
            return $"{size / (1024.0 * 1024.0 * 1024.0):0.00} GB";
        if (size > 1L * 1024 * 1024) // MB
            return $"{size / (1024.0 * 1024.0):0.00} MB";
        if (size > 1L * 1024) // KB
            return $"{size / 1024.0:0.00} kB";
        if (size > 0)
            return $"{size:0.00} B";
        return "";
    }

    /// <summary>
    /// Formats a TimeSpan to a human-readable string.
    /// </summary>
    public static string FormatElapsed(TimeSpan elapsed)
    {
        return $"{elapsed.TotalSeconds:0.00}s";
    }

    /// <summary>
    /// Plays the system asterisk sound for success.
    /// </summary>
    public static void PlaySuccessSound()
    {
        // Sound playback not supported in .NET Core console apps
        // Console.Beep(800, 200); // Alternative: simple beep
    }

    /// <summary>
    /// Plays the system hand sound for failure.
    /// </summary>
    public static void PlayFailureSound()
    {
        // Sound playback not supported in .NET Core console apps
        // Console.Beep(200, 300); // Alternative: simple beep
    }
}
