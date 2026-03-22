namespace XCom2ConfigParser2.StructValidation;

/// <summary>
/// Result of variable type resolution.
/// </summary>
public sealed class VariableTypeResolutionResult
{
    public bool Found { get; }
    public string? BaseType { get; }
    public string? FullType { get; }
    public string? ClassFilePath { get; }
    public IReadOnlyList<string> SearchedPaths { get; }

    private VariableTypeResolutionResult(bool found, string? baseType, string? fullType, string? classFilePath, IReadOnlyList<string> searchedPaths)
    {
        Found = found;
        BaseType = baseType;
        FullType = fullType;
        ClassFilePath = classFilePath;
        SearchedPaths = searchedPaths;
    }

    public static VariableTypeResolutionResult Success(string baseType, string fullType, string classFile, IReadOnlyList<string> searched) =>
        new(true, baseType, fullType, classFile, searched);

    public static VariableTypeResolutionResult NotFound(IReadOnlyList<string> searchedPaths) =>
        new(false, null, null, null, searchedPaths);
}
