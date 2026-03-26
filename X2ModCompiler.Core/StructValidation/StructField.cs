using MemoryPack;
using X2ModCompiler.Core.Parser;

namespace X2ModCompiler.Core.StructValidation;

/// <summary>
/// Represents a metadata model for a field within an UnrealScript struct, specifically for serialization and caching.
/// </summary>
[MemoryPackable]
public sealed partial class StructField
{
    /// <summary>Gets or sets the name of the struct field.</summary>
    [MemoryPackOrder(0)]
    public string Name { get; set; } = "";

    /// <summary>Gets or sets the full type name of the field (e.g., "int", "Vector", "SDLReplacement[]").</summary>
    [MemoryPackOrder(1)]
    public string TypeName { get; set; } = "";

    /// <summary>
    /// Gets a value indicating whether this field is a custom struct type (as opposed to a known primitive).
    /// </summary>
    /// <remarks>
    /// Utilizes the global <see cref="UnrealScriptParser.KnownPrimitives"/> registry for classification.
    /// </remarks>
    [MemoryPackIgnore]
    public bool IsStruct => !UnrealScriptParser.KnownPrimitives.Contains(BaseType);

    /// <summary>
    /// Gets the base type name by stripping any array brackets (e.g., <c>"SDLReplacement[]"</c> becomes <c>"SDLReplacement"</c>).
    /// </summary>
    [MemoryPackIgnore]
    public string BaseType
    {
        get
        {
            if (string.IsNullOrEmpty(TypeName)) return "";
            int bracketIndex = TypeName.IndexOf('[');
            return bracketIndex >= 0 ? TypeName.Substring(0, bracketIndex) : TypeName;
        }
    }
}
