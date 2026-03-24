namespace XCom2ConfigParser2.Core;

/// <summary>
/// Represents a Key-Value Pair directive: [op]property=value
/// </summary>
public readonly struct Kvp
{
    public Span Span { get; }           // Full "prop=value" span
    public Span IdentSpan { get; }      // Property name span
    public Span ValueSpan { get; }      // Value span in original source
    public string MergedValue { get; }  // Cleaned value from merged line
    public KvpOperation Operation { get; }

    public Kvp(Span span, Span identSpan, Span valueSpan, string mergedValue, KvpOperation operation)
    {
        Span = span;
        IdentSpan = identSpan;
        ValueSpan = valueSpan;
        MergedValue = mergedValue;
        Operation = operation;
    }

    public string GetPropertyName(string text) => IdentSpan.Extract(text);
    public string GetValue(string text) => MergedValue ?? ValueSpan.Extract(text);
}
