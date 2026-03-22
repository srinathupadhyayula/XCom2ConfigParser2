namespace XCom2ConfigParser2.Core;

/// <summary>
/// Represents the operation type for a KVP (Key-Value Pair) directive.
/// </summary>
public enum KvpOperation
{
    Set = 0,          // No prefix
    InsertUnique = 1, // + prefix
    Insert = 2,       // . prefix
    Remove = 3,       // - prefix
    Clear = 4         // ! prefix
}
