namespace XCom2ConfigParser2.Parser;

/// <summary>
/// Represents an array value: (Item1, Item2, Item3)
/// </summary>
public sealed class ArrayValue : PropValue
{
    public List<PropValue> Elements { get; }
    public override PropValueType Type => PropValueType.Array;

    public ArrayValue()
    {
        Elements = new List<PropValue>();
    }

    public ArrayValue(List<PropValue> elements)
    {
        Elements = elements;
    }
}
