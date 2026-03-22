namespace XCom2ConfigParser2.Parser;

/// <summary>
/// Represents a struct value: (Prop1=Value1, Prop2=Value2)
/// </summary>
public sealed class StructValue : PropValue
{
    public List<PropAssignment> Children { get; }
    public override PropValueType Type => PropValueType.Struct;

    public StructValue()
    {
        Children = new List<PropAssignment>();
    }

    public StructValue(List<PropAssignment> children)
    {
        Children = children;
    }
}
