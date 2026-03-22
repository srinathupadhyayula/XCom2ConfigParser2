namespace XCom2ConfigParser2.Core;

/// <summary>
/// Error codes for diagnostics.
/// </summary>
public enum ErrorCode
{
    InvalidIdentifier,       // Invalid property/section name
    ArrayPrefixOnNonArray,   // +, -, ., ! used on non-array property
    MalformedHeader,         // Section header syntax error
    SpaceAfterMultiline,     // Space/tab after \\ continuation
    SlashSlashComment,       // // comment style (should use ;)
    BadValue,                // Value doesn't match any valid type
    TrailingContinuation,    // \\ at end of file
    StructParseError,        // Struct syntax error
    InvalidStructMember,     // Struct field name not in definition
    StructDefNotFound,       // Struct definition could not be resolved
    Other                    // Unknown line not matching any pattern
}
