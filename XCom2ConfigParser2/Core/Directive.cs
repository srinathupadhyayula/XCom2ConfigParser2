namespace XCom2ConfigParser2.Core;

/// <summary>
/// Represents a parsed directive (discriminated union).
/// </summary>
public readonly struct Directive
{
    public DirectiveType Type { get; }
    public SectionHeader? SectionHeader { get; }
    public Kvp? Kvp { get; }
    public Unknown? Unknown { get; }

    private Directive(DirectiveType type, SectionHeader? sectionHeader, Kvp? kvp, Unknown? unknown)
    {
        Type = type;
        SectionHeader = sectionHeader;
        Kvp = kvp;
        Unknown = unknown;
    }

    public static Directive Section(SectionHeader header) =>
        new(DirectiveType.SectionHeader, header, null, null);

    public static Directive KvpDirective(Kvp kvp) =>
        new(DirectiveType.Kvp, null, kvp, null);

    public static Directive UnknownDirective(Unknown unknown) =>
        new(DirectiveType.Unknown, null, null, unknown);
}
