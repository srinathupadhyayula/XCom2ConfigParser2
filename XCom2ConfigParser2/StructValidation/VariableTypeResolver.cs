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
        // Parse section header: [PackageName.ClassName] or [ObjectName ClassName]
        if (!TryParseSectionName(sectionName, out var packageName, out var className, out var isPackageTargeted))
            return VariableTypeResolutionResult.NotFound(new[] { "Invalid section format" });

        string currentClassName = className;
        string currentPackageName = packageName;
        bool currentlyTargeted = isPackageTargeted;
        var allSearched = new List<string>();

        // Follow inheritance up to 10 levels deep
        for (int depth = 0; depth < 10; depth++)
        {
            // Locate current class
            var classResult = (currentlyTargeted && !string.IsNullOrEmpty(currentPackageName))
                ? _locator.Locate(currentPackageName, currentClassName)
                : _locator.Locate("", currentClassName);

            // Accumulate searched paths for better diagnostics
            foreach (var path in classResult.SearchedPaths)
            {
                if (!allSearched.Contains(path))
                    allSearched.Add(path);
            }

            if (!classResult.Found)
                break;

            // Parse variables in this file
            var vars = UnrealScriptParser.ParseConfigVariables(classResult.FilePath!);
            if (vars != null)
            {
                var decl = vars.FirstOrDefault(v =>
                    string.Equals(v.Name, propertyName, StringComparison.OrdinalIgnoreCase));

                if (decl != null)
                {
                    return VariableTypeResolutionResult.Success(
                        decl.BaseType, decl.TypeName, classResult.FilePath!, allSearched);
                }
            }

            // Crawl up to parent class
            var header = UnrealScriptParser.ParseClassHeader(classResult.FilePath!);
            if (header == null || string.IsNullOrEmpty(header.ParentName) ||
                string.Equals(header.ParentName, "Object", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(header.ParentName, "Actor", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            // Move to parent. We switch to global search (packageName="") because parents
            // are often in different packages (e.g. XComGame, Core, Engine).
            currentClassName = header.ParentName;
            currentlyTargeted = false;
        }

        return VariableTypeResolutionResult.NotFound(allSearched);
    }

    private static bool TryParseSectionName(string sectionName, out string packageName, out string className, out bool isPackageTargeted)
    {
        packageName = "";
        className = "";
        isPackageTargeted = false;

        // Common format in XCOM 2: [PackageName.ClassName] or [ObjectName ClassName]
        int separatorIndex = sectionName.LastIndexOfAny(new[] { '.', ' ', '\t' });
        if (separatorIndex <= 0 || separatorIndex >= sectionName.Length - 1)
        {
            // Case where its just [ClassName] (Global search)
            className = sectionName;
            return !string.IsNullOrEmpty(className);
        }

        char separator = sectionName[separatorIndex];
        isPackageTargeted = (separator == '.');

        packageName = sectionName.Substring(0, separatorIndex).TrimEnd();
        className = sectionName.Substring(separatorIndex + 1).TrimStart();
        return !string.IsNullOrEmpty(packageName) && !string.IsNullOrEmpty(className);
    }
}
