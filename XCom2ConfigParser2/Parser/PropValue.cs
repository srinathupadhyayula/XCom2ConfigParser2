namespace XCom2ConfigParser2.Parser;

/// <summary>
/// Serves as the base class for all nodes in the property value Abstract Syntax Tree (AST).
/// Provides a unified interface for evaluating complex property values (structs, arrays, terminals).
/// </summary>
public abstract class PropValue
{
    /// <summary>Gets the specific type of this property value node.</summary>
    public abstract PropValueType Type { get; }
}

/// <summary>
/// Specifies the structural type of a property value node.
/// </summary>
public enum PropValueType
{
    /// <summary> A simple literal value (string, number, boolean). </summary>
    Terminal,

    /// <summary> A nested structure containing child property assignments. </summary>
    Struct,

    /// <summary> A list of nested property values. </summary>
    Array,

    /// <summary> An empty value representation, typically <c>()</c>. </summary>
    Empty
}
