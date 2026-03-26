using X2ModCompiler.Core.Validation;
using X2ModCompiler.Core.Parser;

namespace X2ModCompiler.Core.StructValidation;

/// <summary>
/// Resolves configuration property names to their corresponding UnrealScript variable declarations.
/// This resolver crawls the class inheritance hierarchy to find the original 'config' or 'globalconfig' 
/// variable definition, enabling type-safe validation of configuration values.
/// </summary>
public sealed class VariableTypeResolver
{
    private readonly ClassFileLocator _locator;

    /// <summary>
    /// Initializes a new instance of the <see cref="VariableTypeResolver"/> class.
    /// </summary>
    /// <param name="settings">The global parser settings.</param>
    /// <param name="modSrcCache">Optional cache for mod source file locations.</param>
    public VariableTypeResolver(Configuration.ParserSettings settings, ModSrcPathCache? modSrcCache = null)
    {
        _locator = new ClassFileLocator(settings, modSrcCache);
    }

    /// <summary>
    /// Attempts to resolve the type of a property within a specific configuration section.
    /// Traverses up the inheritance chain (e.g., XComGameState_Unit -> XComGameState_BaseObject -> Object)
    /// until a matching variable declaration is found or the search limit is reached.
    /// </summary>
    /// <param name="sectionName">The raw section header name (e.g., "XComGame.X2Ability_Grenadier").</param>
    /// <param name="propertyName">The name of the property to resolve.</param>
    /// <returns>A <see cref="VariableTypeResolutionResult"/> containing the resolution status and type information.</returns>
    public VariableTypeResolutionResult Resolve(string sectionName, string propertyName)
    {
        // Parse section header: [PackageName.ClassName] or [ObjectName ClassName]
        if (!TryParseSectionName(sectionName, out var packageName, out var className, out var isPackageTargeted))
            return VariableTypeResolutionResult.NotFound(new[] { "Invalid section format" });

        string currentClassName = className;
        string currentPackageName = packageName;
        bool currentlyTargeted = isPackageTargeted;
        var allSearched = new List<string>();

        // Follow inheritance up to 10 levels deep to prevent infinite loops in malformed script
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

            // Parse variables in this file using the unified parser
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

            // Crawl up to parent class by parsing the 'extends' clause
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

    /// <summary>
    /// Parses an XCOM 2 configuration section header into its constituent package and class names.
    /// Supports standard formats like [Package.Class] and [Object Class].
    /// </summary>
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
