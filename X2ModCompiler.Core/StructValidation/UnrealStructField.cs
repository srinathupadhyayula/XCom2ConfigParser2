using MemoryPack;

namespace X2ModCompiler.Core.StructValidation;

/// <summary>
/// Represents a simple name-type pair for a single field within an <see cref="UnrealStructDef"/>.
/// </summary>
[MemoryPackable]
public readonly partial struct UnrealStructField
{
    /// <summary>Gets the name of the field.</summary>
    [MemoryPackOrder(0)]
    public string Name { get; }

    /// <summary>Gets the full type name of the field.</summary>
    [MemoryPackOrder(1)]
    public string TypeName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnrealStructField"/> struct.
    /// </summary>
    /// <param name="name">The name of the field.</param>
    /// <param name="typeName">The full type name of the field.</param>
    public UnrealStructField(string name, string typeName)
    {
        Name = name;
        TypeName = typeName;
    }
}
