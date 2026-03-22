namespace XCom2ConfigParser2.Core;

/// <summary>
/// Represents a section header directive: [ObjectName]
/// </summary>
public readonly struct SectionHeader
{
    public Span Span { get; }            // Full "[Name]" span
    public Span ObjectNameSpan { get; }  // "Name" span (without brackets)

    public SectionHeader(Span span, Span objectNameSpan)
    {
        Span = span;
        ObjectNameSpan = objectNameSpan;
    }

    public string GetObjectName(string text) => ObjectNameSpan.Extract(text);
}
