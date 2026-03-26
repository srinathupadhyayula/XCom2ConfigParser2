using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ZLogger;
using X2ModCompiler.Exceptions;
using X2ModCompiler.Utilities;

namespace X2ModCompiler.Compilation;

public abstract class OutputReceiver
{
    protected readonly ILogger _logger;
    public bool CrashDetected { get; protected set; }
    public string ProcessDescription { get; set; } = "";

    protected OutputReceiver(ILogger logger)
    {
        _logger = logger;
    }

    public virtual void ParseLine(string? line)
    {
        if (line != null && line.Contains("Crash", StringComparison.OrdinalIgnoreCase) && line.Contains("Exception", StringComparison.OrdinalIgnoreCase))
        {
            CrashDetected = true;
        }
    }

    public virtual void Finish(int exitCode)
    {
        if (CrashDetected)
            throw new BuildCrashException(ProcessDescription);

        if (exitCode != 0)
            throw new BuildFailureException(ProcessDescription, exitCode);
    }
}

/// <summary>
/// Passes all output directly to logger with immediate flush for live streaming.
/// </summary>
public class PassthroughReceiver : OutputReceiver
{
    public PassthroughReceiver(ILogger logger) : base(logger) { }

    public override void ParseLine(string? line)
    {
        base.ParseLine(line);
        if (line != null)
        {
            _logger.ZLogInformation($"{line}");
        }
    }
}

/// <summary>
/// Buffers all output and only displays it on failure.
/// Used for CookPackages which produces verbose output.
/// </summary>
public class BufferingReceiver : OutputReceiver
{
    private readonly List<string> _logLines = new();

    public BufferingReceiver(ILogger logger) : base(logger) { }

    public override void ParseLine(string? line)
    {
        base.ParseLine(line);
        if (line != null)
        {
            _logLines.Add(line);
        }
    }

    public override void Finish(int exitCode)
    {
        if (exitCode != 0 || CrashDetected)
        {
            // Display all buffered lines on failure
            foreach (var line in _logLines)
            {
                _logger.ZLogInformation($"{line}");
            }
        }
        base.Finish(exitCode);
    }
}

/// <summary>
/// Filters spam from asset cooking output.
/// Collapses repetitive "GFx movie package" lines into a single indicator.
/// </summary>
public class ModcookReceiver : OutputReceiver
{
    private bool _lastLineWasAdding = false;

    public ModcookReceiver(ILogger logger) : base(logger) { }

    public override void ParseLine(string? line)
    {
        base.ParseLine(line);
        
        if (line == null) return;

        var permitLine = true;

        if (line.StartsWith("GFx movie package", StringComparison.OrdinalIgnoreCase))
        {
            permitLine = false;
            if (!_lastLineWasAdding)
            {
                _logger.ZLogInformation($"[GFx movie packages ...]");
            }
            _lastLineWasAdding = true;
        }
        else
        {
            _lastLineWasAdding = false;
        }

        if (permitLine)
        {
            _logger.ZLogInformation($"{line}");
        }
    }
}

/// <summary>
/// Handles make commandlet output with path translation and color coding.
/// Translates SDK paths to source paths for error messages.
/// Deduplicates consecutive identical lines (UE3 commandlet quirk).
/// </summary>
public class MakeOutputReceiver : OutputReceiver
{
    private readonly string[] _reversePaths;
    private const string SdkPathPattern = @"(?i)\\XCOM 2 War of the Chosen SDK\\";
    private string? _lastLineWritten = null;

    public MakeOutputReceiver(string[] reversePaths, ILogger logger) : base(logger)
    {
        // Since later paths overwrite earlier files, check paths in reverse order
        _reversePaths = reversePaths.Reverse().ToArray();
    }

    public override void ParseLine(string? line)
    {
        base.ParseLine(line);

        if (line is null) return;

        // Check for crash
        if (line.Contains("Crash Detected", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("(filename not found)", StringComparison.OrdinalIgnoreCase))
        {
            CrashDetected = true;
        }

        // Deduplicate consecutive identical lines (UE3 commandlet echoes output)
        if (line == _lastLineWritten)
        {
            return;
        }
        _lastLineWritten = line;

        // Path translation for errors/warnings
        if (ContainsErrorOrWarning(line))
        {
            var translated = TranslatePath(line);
            WriteColoredOutput(translated, isError: line.Contains("Error", StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            WriteColoredOutput(line, isError: false);
        }
    }

    private bool ContainsErrorOrWarning(string line)
    {
        return line.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("Warning", StringComparison.OrdinalIgnoreCase);
    }

    private void WriteColoredOutput(string line, bool isError)
    {
        // Summary pattern check
        var summaryPattern = @"^(Success|Failure) - ([0-9]+) error\(s\), ([0-9]+) warning\(s\) \(([0-9]+) Unique Errors, ([0-9]+) Unique Warnings\)";
        var summaryMatch = Regex.Match(line, summaryPattern);
        
        if (isError)
        {
            _logger.ZLogInformation($"{line}");
        }
        else if (line.Contains("Warning", StringComparison.OrdinalIgnoreCase))
        {
            _logger.ZLogWarning($"{line}");
        }
        else if (summaryMatch.Success)
        {
            var numErr = int.Parse(summaryMatch.Groups[2].Value);
            if (numErr > 0)
                _logger.ZLogError($"{line}");
            else if (int.Parse(summaryMatch.Groups[3].Value) > 0)
                _logger.ZLogWarning($"{line}");
            else
                _logger.ZLogInformation($"{line}");
        }
        else
        {
            _logger.ZLogInformation($"{line}");
        }
    }

    private string TranslatePath(string line)
    {
        // Pattern: path(line) : message
        var match = Regex.Match(line, @"^(.*)\((\d+)\)\s*:\s*(.*)$");
        if (!match.Success) return line;

        var origPath = match.Groups[1].Value;

        // Find actual file in reverse path order
        foreach (var checkPath in _reversePaths)
        {
            try
            {
                // Create regex pattern from SDK path
                var pattern = Regex.Escape($"{Path.Combine(checkPath, "Development", "Src")}");
                var testPath = Regex.Replace(origPath, pattern, checkPath, RegexOptions.IgnoreCase);
                
                if (File.Exists(testPath))
                {
                    var fullPath = Path.GetFullPath(testPath);
                    return line.Replace(origPath, fullPath);
                }
            }
            catch
            {
                // Ignore translation failures
            }
        }

        return line;
    }
}
