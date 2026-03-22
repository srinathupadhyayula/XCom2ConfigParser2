namespace XCom2ConfigParser2.Core;

/// <summary>
/// Represents a Key-Value Pair directive: [op]property=value
/// </summary>
public readonly struct Kvp
{
    public Span Span { get; }           // Full "prop=value" span
    public Span IdentSpan { get; }      // Property name span
    public Span ValueSpan { get; }      // Value span
    public KvpOperation Operation { get; }

    public Kvp(Span span, Span identSpan, Span valueSpan, KvpOperation operation)
    {
        Span = span;
        IdentSpan = identSpan;
        ValueSpan = valueSpan;
        Operation = operation;
    }

    public string GetPropertyName(string text) => IdentSpan.Extract(text);
    public string GetValue(string text) => ValueSpan.Extract(text);
}
