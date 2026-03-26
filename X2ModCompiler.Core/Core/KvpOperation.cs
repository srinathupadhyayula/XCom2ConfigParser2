namespace X2ModCompiler.Core.Core;

/// <summary>
/// Specifies the operation type for a Key-Value Pair (KVP) directive, determined by the property name prefix.
/// These operations mirror the Unreal Engine 3 configuration inherited behavior.
/// </summary>
public enum KvpOperation
{
    /// <summary> Standard assignment: <c>Property=Value</c>. Overwrites existing values or adds a new one. </summary>
    Set = 0,

    /// <summary> Unique insertion: <c>+Property=Value</c>. Adds the value only if it doesn't already exist in the array. </summary>
    InsertUnique = 1,

    /// <summary> Non-unique insertion: <c>.Property=Value</c>. Adds the value to the array even if it already exists. </summary>
    Insert = 2,

    /// <summary> Removal: <c>-Property=Value</c>. Removes the specified value from the array. </summary>
    Remove = 3,

    /// <summary> Clear: <c>!Property</c>. Clears all values from the specified array property. </summary>
    Clear = 4
}
