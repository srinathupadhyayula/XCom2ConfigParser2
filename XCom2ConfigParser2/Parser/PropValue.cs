namespace XCom2ConfigParser2.Parser;

/// <summary>
/// Base class for property value AST nodes.
/// </summary>
public abstract class PropValue
{
    public abstract PropValueType Type { get; }
}

/// <summary>
/// Type of property value.
/// </summary>
public enum PropValueType
{
    Terminal,
    Struct,
    Array,
    Empty
}
