namespace X2ModCompiler.Core.Core;

/// <summary>
/// Represents a human-readable position within a source file, consisting of a line and column number.
/// </summary>
public readonly struct SourceLocation
{
    /// <summary>Gets the 1-based line number.</summary>
    public int Line { get; }

    /// <summary>Gets the 1-based column number.</summary>
    public int Column { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SourceLocation"/> struct.
    /// </summary>
    public SourceLocation(int line, int column)
    {
        Line = line;
        Column = column;
    }

    /// <summary> Returns a human-readable representation of the location in (Line,Column) format. </summary>
    public override string ToString() => $"({Line},{Column})";
}
