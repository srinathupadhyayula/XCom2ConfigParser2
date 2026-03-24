namespace XCom2ConfigParser2.Core;

/// <summary>
/// Specifies the type of configuration directive identified during the initial line splitting phase.
/// </summary>
public enum DirectiveType
{
    /// <summary> A section header enclosed in square brackets (e.g., [SectionName]). </summary>
    SectionHeader,

    /// <summary> A key-value pair property assignment (e.g., Property=Value). </summary>
    Kvp,

    /// <summary> A line that could not be categorized or represents unrecognized content. </summary>
    Unknown
}
