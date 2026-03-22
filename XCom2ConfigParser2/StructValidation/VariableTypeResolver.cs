namespace XCom2ConfigParser2.StructValidation;

/// <summary>
/// Finds config variable declarations in class files and extracts their types.
/// Delegates all parsing to <see cref="UnrealScriptParser"/>.
/// </summary>
public sealed class VariableTypeResolver
{
    private readonly ClassFileLocator _locator;

    public VariableTypeResolver(Configuration.ParserSettings settings, ModSrcPathCache? modSrcCache = null)
    {
        _locator = new ClassFileLocator(settings, modSrcCache);
    }

    public VariableTypeResolutionResult Resolve(string sectionName, string propertyName)
    {
        // Parse section header: [PackageName.ClassName]
        if (!TryParseSectionName(sectionName, out var packageName, out var className))
            return VariableTypeResolutionResult.NotFound(new[] { "Invalid section format" });

        // Locate class file
        var classFileResult = _locator.Locate(packageName, className);
        if (!classFileResult.Found)
            return VariableTypeResolutionResult.NotFound(classFileResult.SearchedPaths);

        // Parse only config variable declarations (stops at first 'function' keyword)
        var vars = UnrealScriptParser.ParseConfigVariables(classFileResult.FilePath!);
        if (vars == null)
            return VariableTypeResolutionResult.NotFound(
                new[] { $"Could not read file: {classFileResult.FilePath}" });

        // Case-insensitive lookup by variable name
        var decl = vars.FirstOrDefault(v =>
            string.Equals(v.Name, propertyName, StringComparison.OrdinalIgnoreCase));

        if (decl == null)
            return VariableTypeResolutionResult.NotFound(
                new[] { $"Config property '{propertyName}' not found in {classFileResult.FilePath}" });

        return VariableTypeResolutionResult.Success(
            decl.BaseType, decl.TypeName, classFileResult.FilePath!, classFileResult.SearchedPaths);
    }

    private static bool TryParseSectionName(string sectionName, out string packageName, out string className)
    {
        packageName = "";
        className = "";

        int dotIndex = sectionName.LastIndexOf('.');
        if (dotIndex <= 0 || dotIndex >= sectionName.Length - 1)
            return false;

        packageName = sectionName.Substring(0, dotIndex);
        className = sectionName.Substring(dotIndex + 1);
        return !string.IsNullOrEmpty(packageName) && !string.IsNullOrEmpty(className);
    }
}
