using System.Text.Json.Serialization;

namespace XCom2ConfigParser2.StructValidation;

/// <summary>
/// Result of variable type resolution.
/// </summary>
/// <summary>
/// Encapsulates the outcome of an attempt to resolve the type of a configuration variable.
/// </summary>
public sealed class VariableTypeResolutionResult
{
    /// <summary>Gets a value indicating whether the variable was successfully resolved.</summary>
    public bool Found { get; }

    /// <summary>Gets the base type name (e.g., "Vector" or "int"), or <c>null</c> if not found.</summary>
    public string? BaseType { get; }

    /// <summary>Gets the full type string including array markers (e.g., "Vector" or "int[]"), or <c>null</c> if not found.</summary>
    public string? FullType { get; }

    /// <summary>Gets the absolute path to the .uc file where the declaration was found, or <c>null</c> if not found.</summary>
    public string? ClassFilePath { get; }

    /// <summary>Gets the log of file system locations that were searched during resolution.</summary>
    public IReadOnlyList<string> SearchedPaths { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="VariableTypeResolutionResult"/> class.
    /// Primarily for JSON deserialization via <see cref="VariableCache"/>.
    /// </summary>
    [JsonConstructor]
    public VariableTypeResolutionResult(bool found, string? baseType, string? fullType, string? classFilePath, IReadOnlyList<string> searchedPaths)
    {
        Found = found;
        BaseType = baseType;
        FullType = fullType;
        ClassFilePath = classFilePath;
        SearchedPaths = searchedPaths;
    }

    /// <summary> Creates a successful resolution result. </summary>
    public static VariableTypeResolutionResult Success(string baseType, string fullType, string classFile, IReadOnlyList<string> searched) =>
        new(true, baseType, fullType, classFile, searched);

    /// <summary> Creates a failed resolution result. </summary>
    public static VariableTypeResolutionResult NotFound(IReadOnlyList<string> searchedPaths) =>
        new(false, null, null, null, searchedPaths);
}
