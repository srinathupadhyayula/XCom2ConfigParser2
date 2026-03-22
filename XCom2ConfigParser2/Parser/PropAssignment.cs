namespace XCom2ConfigParser2.Parser;

/// <summary>
/// Represents a property assignment within a struct: PropName[index]=Value
/// </summary>
public readonly struct PropAssignment
{
    public string Name { get; }
    public uint? Index { get; }  // For Prop[0] syntax
    public PropValue Value { get; }

    public PropAssignment(string name, uint? index, PropValue value)
    {
        Name = name;
        Index = index;
        Value = value;
    }
}
