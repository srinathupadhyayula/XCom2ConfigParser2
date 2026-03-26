namespace X2ModCompiler.Core.Core;

/// <summary>
/// Represents a Key-Value Pair (KVP) directive in an XCOM 2 configuration file.
/// Format: <c>[Operation]Property=Value</c>
/// </summary>
public readonly struct Kvp
{
    /// <summary>Gets the full span of the directive, including the property name, operator, and value.</summary>
    public Span Span { get; }

    /// <summary>Gets the span of the property identifier (name).</summary>
    public Span IdentSpan { get; }

    /// <summary>Gets the span of the value as it appears in the original source text.</summary>
    public Span ValueSpan { get; }

    /// <summary>Gets the cleaned value string, potentially reconstructed from multiline continuations.</summary>
    public string MergedValue { get; }

    /// <summary>Gets the operation type (e.g., Set, Insert, Remove) based on the property prefix.</summary>
    public KvpOperation Operation { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Kvp"/> struct.
    /// </summary>
    public Kvp(Span span, Span identSpan, Span valueSpan, string mergedValue, KvpOperation operation)
    {
        Span = span;
        IdentSpan = identSpan;
        ValueSpan = valueSpan;
        MergedValue = mergedValue;
        Operation = operation;
    }

    /// <summary> Extracts the property name from the source text. </summary>
    public string GetPropertyName(string text) => IdentSpan.Extract(text);

    /// <summary> Gets the property value, prioritizing the cleaned merged value over the raw source span. </summary>
    public string GetValue(string text) => MergedValue ?? ValueSpan.Extract(text);
}
