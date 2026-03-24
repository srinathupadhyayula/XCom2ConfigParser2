namespace XCom2ConfigParser2.Parser;

/// <summary>
/// Represents an empty value indicator in the AST, specifically an empty set of parentheses <c>()</c>.
/// Often used in array clear or resetting scenarios.
/// </summary>
public sealed class EmptyValue : PropValue
{
    /// <inheritdoc />
    public override PropValueType Type => PropValueType.Empty;
}
