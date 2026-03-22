namespace XCom2ConfigParser2.StructValidation;

/// <summary>
/// Represents a field in a UnrealScript struct definition.
/// </summary>
public sealed class StructField
{
    public string Name { get; set; } = "";
    public string TypeName { get; set; } = "";

    /// <summary>
    /// Gets whether this field is a struct type (not a known primitive).
    /// Uses the shared <see cref="UnrealScriptParser.KnownPrimitives"/> set.
    /// </summary>
    public bool IsStruct => !UnrealScriptParser.KnownPrimitives.Contains(BaseType);

    /// <summary>
    /// Gets the base type name without any array suffix.
    /// e.g., "SDLReplacement[]" → "SDLReplacement"
    /// </summary>
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
