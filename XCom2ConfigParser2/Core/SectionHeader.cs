namespace XCom2ConfigParser2.Core;

/// <summary>
/// Represents a section header directive in a configuration file (e.g., <c>[XComGame.X2Ability_Grenadier]</c>).
/// </summary>
public readonly struct SectionHeader
{
    /// <summary>Gets the full span of the header, including the square brackets.</summary>
    public Span Span { get; }

    /// <summary>Gets the span of the object/class name inside the brackets.</summary>
    public Span ObjectNameSpan { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SectionHeader"/> struct.
    /// </summary>
    public SectionHeader(Span span, Span objectNameSpan)
    {
        Span = span;
        ObjectNameSpan = objectNameSpan;
    }

    /// <summary> Extracts the object/class name from the source text. </summary>
    public string GetObjectName(string text) => ObjectNameSpan.Extract(text);
}
