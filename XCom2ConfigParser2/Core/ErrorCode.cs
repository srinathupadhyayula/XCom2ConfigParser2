namespace XCom2ConfigParser2.Core;

/// <summary>
/// Enumerates the possible error and warning codes produced during the configuration parsing and validation process.
/// </summary>
public enum ErrorCode
{
    /// <summary>The identifier contains invalid characters or does not follow XCOM 2 naming conventions.</summary>
    InvalidIdentifier,

    /// <summary>An array prefix (+, -, ., !) was used on a property that is not defined as an array.</summary>
    ArrayPrefixOnNonArray,

    /// <summary>The section header brackets are mismatched or contain invalid syntax.</summary>
    MalformedHeader,

    /// <summary>Detected whitespace after a backslash (\) multiline continuation sequence.</summary>
    SpaceAfterMultiline,

    /// <summary>Detected a C-style comment (//). INI files must use semicolons (;) for comments.</summary>
    SlashSlashComment,

    /// <summary>The property value does not match any known config data type (Float, Int, String, Struct, etc.).</summary>
    BadValue,

    /// <summary>A multiline continuation marker (\) was found at the end of the file.</summary>
    TrailingContinuation,

    /// <summary>Detected a syntax error within a complex struct value.</summary>
    StructParseError,

    /// <summary>A struct field name was used that does not exist in the resolved struct definition.</summary>
    InvalidStructMember,

    /// <summary>The definition for the referenced struct could not be resolved from script files.</summary>
    StructDefNotFound,

    /// <summary>An unclassified error occurred.</summary>
    Other
}
