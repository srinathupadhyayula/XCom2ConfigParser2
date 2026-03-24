namespace XCom2ConfigParser2.Core;

/// <summary>
/// Defines the severity levels for diagnostics reported during the configuration parsing and validation process.
/// </summary>
public enum DiagnosticSeverity
{
    /// <summary> Indicates a critical failure that prevents correct parsing or violates engine rules. </summary>
    Error,

    /// <summary> Indicates a potential issue or ambiguity that should be reviewed but does not halt the process. </summary>
    Warning,

    /// <summary> Provides navigational or contextual information for the user. </summary>
    Info
}
