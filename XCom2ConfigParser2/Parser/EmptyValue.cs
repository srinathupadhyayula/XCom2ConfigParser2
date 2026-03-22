namespace XCom2ConfigParser2.Parser;

/// <summary>
/// Represents an empty value: ()
/// </summary>
public sealed class EmptyValue : PropValue
{
    public override PropValueType Type => PropValueType.Empty;
}
