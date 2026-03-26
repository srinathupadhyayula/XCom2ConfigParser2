namespace X2ModCompiler.Core.Validation;

/// <summary>
/// Result of class file location.
/// </summary>
public sealed class ClassFileResult
{
    public bool Found { get; }
    public string? FilePath { get; }
    public string Source { get; }
    public IReadOnlyList<string> SearchedPaths { get; }

    private ClassFileResult(bool found, string? filePath, string source, IReadOnlyList<string> searchedPaths)
    {
        Found = found;
        FilePath = filePath;
        Source = source;
        SearchedPaths = searchedPaths;
    }

    public static ClassFileResult Success(string filePath, string source) =>
        new(true, filePath, source, new[] { source });

    public static ClassFileResult NotFound(IReadOnlyList<string> searchedPaths) =>
        new(false, null, "", searchedPaths);
}
