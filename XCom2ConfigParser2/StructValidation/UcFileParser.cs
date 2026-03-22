namespace XCom2ConfigParser2.StructValidation;

/// <summary>
/// Parses UnrealScript (.uc) files to extract struct definitions.
/// Delegates entirely to <see cref="UnrealScriptParser"/> for consistency.
/// </summary>
public static class UcFileParser
{
    /// <summary>
    /// Parses a .uc file and returns all struct definitions found.
    /// </summary>
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
