namespace X2ModCompiler.Core.Core;

/// <summary>
/// A discriminated union representing a single parsed configuration directive.
/// A directive can be a section header, a key-value pair (KVP), or an unknown/unparseable line.
/// </summary>
public readonly struct Directive
{
    /// <summary>
    /// Gets the category of this directive.
    /// </summary>
    public DirectiveType Type { get; }

    /// <summary>
    /// Gets the section header data if <see cref="Type"/> is <see cref="DirectiveType.SectionHeader"/>.
    /// </summary>
    public SectionHeader? SectionHeader { get; }

    /// <summary>
    /// Gets the key-value pair data if <see cref="Type"/> is <see cref="DirectiveType.Kvp"/>.
    /// </summary>
    public Kvp? Kvp { get; }

    /// <summary>
    /// Gets the raw data for unparseable lines if <see cref="Type"/> is <see cref="DirectiveType.Unknown"/>.
    /// </summary>
    public Unknown? Unknown { get; }

    private Directive(DirectiveType type, SectionHeader? sectionHeader, Kvp? kvp, Unknown? unknown)
    {
        Type = type;
        SectionHeader = sectionHeader;
        Kvp = kvp;
        Unknown = unknown;
    }

    /// <summary>
    /// Creates a new section header directive.
    /// </summary>
    /// <param name="header">The parsed section header.</param>
    /// <returns>A new <see cref="Directive"/> of type <see cref="DirectiveType.SectionHeader"/>.</returns>
    public static Directive Section(SectionHeader header) =>
        new(DirectiveType.SectionHeader, header, null, null);

    /// <summary>
    /// Creates a new key-value pair directive.
    /// </summary>
    /// <param name="kvp">The parsed KVP.</param>
    /// <returns>A new <see cref="Directive"/> of type <see cref="DirectiveType.Kvp"/>.</returns>
    public static Directive KvpDirective(Kvp kvp) =>
        new(DirectiveType.Kvp, null, kvp, null);

    /// <summary>
    /// Creates a new unknown directive for unparseable or unrecognized lines.
    /// </summary>
    /// <param name="unknown">The raw data from the unknown line.</param>
    /// <returns>A new <see cref="Directive"/> of type <see cref="DirectiveType.Unknown"/>.</returns>
    public static Directive UnknownDirective(Unknown unknown) =>
        new(DirectiveType.Unknown, null, null, unknown);
}
