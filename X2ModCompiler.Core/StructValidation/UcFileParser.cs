using X2ModCompiler.Core.Parser;

namespace X2ModCompiler.Core.StructValidation;

/// <summary>
/// Provides high-level methods for parsing UnrealScript (.uc) files to extract struct definitions.
/// This parser is designed to extract metadata required for configuration validation, 
/// delegating the core parsing logic to the shared <see cref="UnrealScriptParser"/>.
/// </summary>
public static class UcFileParser
{
    /// <summary>
    /// Parses the specified .uc file and maps its struct definitions to a list of <see cref="UnrealStructDef"/> objects.
    /// </summary>
    /// <param name="filePath">The absolute path to the .uc file to parse.</param>
    /// <param name="packageName">The name of the package containing the file.</param>
    /// <param name="className">The name of the UnrealScript class defined in the file.</param>
    /// <returns>A list of <see cref="UnrealStructDef"/> objects representing the discovered structs.</returns>
    public static List<UnrealStructDef> ParseFile(string filePath, string packageName, string className)
    {
        var structDefs = UnrealScriptParser.ParseStructs(filePath);

        return structDefs.Select(s => new UnrealStructDef(
            s.Name,
            className,
            packageName,
            filePath,
            s.Fields.Select(f => new UnrealStructField(f.Name, f.TypeName)).ToList()
        )).ToList();
    }
}
