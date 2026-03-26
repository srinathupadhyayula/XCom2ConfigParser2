namespace X2ModCompiler.Exceptions;

/// <summary>
/// Base exception for X2ModCompiler build operations.
/// </summary>
public abstract class BuildException : Exception
{
    protected BuildException(string message) : base(message) { }
    protected BuildException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Exception thrown when a build process crashes.
/// </summary>
public class BuildCrashException : BuildException
{
    public string ProcessDescription { get; }
    
    public BuildCrashException(string processDescr)
        : base($"Crash detected while {processDescr}")
    {
        ProcessDescription = processDescr;
    }
}

/// <summary>
/// Exception thrown when a build process fails.
/// </summary>
public class BuildFailureException : BuildException
{
    public int ExitCode { get; }
    
    public BuildFailureException(string processDescr, int exitCode)
        : base($"Failed {processDescr} (exit code {exitCode})")
    {
        ExitCode = exitCode;
    }
}

/// <summary>
/// Exception thrown when build configuration is invalid.
/// </summary>
public class BuildConfigurationException : BuildException
{
    public string ConfigurationKey { get; }
    
    public BuildConfigurationException(string key, string message)
        : base($"Configuration error for '{key}': {message}")
    {
        ConfigurationKey = key;
    }
}

/// <summary>
/// Exception thrown when a path is invalid or not found.
/// </summary>
public class BuildPathException : BuildException
{
    public string Path { get; }
    
    public BuildPathException(string path, string reason)
        : base($"Path '{path}' is invalid: {reason}")
    {
        Path = path;
    }
}
