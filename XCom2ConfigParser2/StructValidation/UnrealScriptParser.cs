using System.Text;
using System.Text.RegularExpressions;

namespace XCom2ConfigParser2.StructValidation;

/// <summary>
/// Provides a canonical, high-performance parser for UnrealScript (.uc) source files.
/// Centrally manages regex patterns and parsing logic for class headers, variable declarations, and struct definitions.
/// </summary>
/// <remarks>
/// This parser is designed to be encoding-resilient (supporting both UTF-8 and Windows-1252) and performance-focused 
/// (utilizing early-exit strategies and targeted section scanning).
/// </remarks>
public static class UnrealScriptParser
{
    // -------------------------------------------------------------------------
    // Shared primitive type set — used by StructField and StructMemberValidator
    // -------------------------------------------------------------------------

    /// <summary>
    /// A set of known UnrealScript primitive and built-in math types.
    /// Used during validation to distinguish between simple literals and complex struct-backed properties.
    /// </summary>
    public static readonly HashSet<string> KnownPrimitives = new(StringComparer.OrdinalIgnoreCase)
    {
        // Integer types
        "bool", "byte", "int", "int16", "int64", "qword",
        // Float types
        "float", "double",
        // String-like
        "string", "name",
        // Built-in math structs (treated as primitives for config purposes)
        "vector", "vector2d", "rotator", "quaternion", "matrix",
        "color", "linearcolor", "plane", "coords", "box", "sphere",
        // Misc
        "guid", "pointer", "object", "actor", "class",
    };

    // -------------------------------------------------------------------------
    // Regex: Config variable declarations
    // -------------------------------------------------------------------------
    // Matches var declarations that contain the 'config' modifier keyword.
    // Handles all known forms:
    //   var config int Count;
    //   var(Group) config int Count;
    //   var config(Group) int Count;
    //   var private config array<MyStruct> Items;
    //   var config MyStruct Items[];
    //   var config MyStruct[] Items;
    // Groups:
    //   1: inner type name if array<T>  (e.g. "MyStruct")
    //   2: type name if simple/Type[]   (e.g. "MyStruct" or "int")
    //   3: variable name
    // -------------------------------------------------------------------------
    private static readonly Regex ConfigVarRegex = new(
        @"(?m)^\s*" +
        @"var\s*(?:\([^)]*\))?\s*" +           // var or var(Group)
        @"(?:\w+\s+)*?" +                      // optional pre-config modifiers (lazy)
        @"config(?:\s*\([^)]*\))?" +           // required 'config' keyword, with optional (Group)
        @"(?:\s+\w+)*?\s+" +                   // optional post-config modifiers (lazy)
        @"(?:" +
            @"array\s*<\s*(\w+)\s*>" +         // Capture group 1: array<InnerType>
            @"|" +
            @"(\w+(?:\s*\[\s*\])?)" +          // Capture group 2: Type or Type[]
        @")" +
        @"\s+([^;]+)\s*;",                     // Capture group 3: variable name list (everything up to ;)
        RegexOptions.IgnoreCase);

    // -------------------------------------------------------------------------
    // Regex: Struct definitions
    // -------------------------------------------------------------------------
    // Matches: struct [modifiers...] StructName { ... }
    // Groups:
    //   1: struct name
    //   2: struct body
    // -------------------------------------------------------------------------
    private static readonly Regex StructRegex = new(
        @"struct\s+(?:\w+\s+)*?(\w+)\s*\{([^}]*)\}",
        RegexOptions.Singleline | RegexOptions.IgnoreCase);

    // -------------------------------------------------------------------------
    // Regex: Field declarations inside struct bodies
    // -------------------------------------------------------------------------
    // Handles: var Type Name; var Type[] Name; var array<Type> Name;
    // Also handles var(Group) and modifier keywords.
    // Groups:
    //   1: inner type name if array<T>
    //   2: type name if simple/Type[]
    //   3: field name
    // -------------------------------------------------------------------------
    private static readonly Regex StructFieldRegex = new(
        @"var\s*(?:\([^)]*\))?\s*" +           // var or var(Group)
        @"(?:\w+\s+)*?" +                      // optional modifiers (lazy)
        @"(?:" +
            @"array\s*<\s*(\w+)\s*>" +         // Capture group 1: array<InnerType>
            @"|" +
            @"(\w+(?:\s*\[\s*\])?)" +          // Capture group 2: Type or Type[]
        @")" +
        @"\s+([^;]+)\s*;",                     // Capture group 3: field name list
        RegexOptions.IgnoreCase);

    // Matches the start of the first function definition (signals end of var block)
    private static readonly Regex FunctionStartRegex = new(
        @"(?m)^\s*(?:(?:static|exec|simulated|reliable|unreliable|private|protected|public|final|native|event|delegate)\s+)*function\b",
        RegexOptions.IgnoreCase);

    // -------------------------------------------------------------------------
    // Regex: Class header (name and extends)
    // -------------------------------------------------------------------------
    private static readonly Regex ClassHeaderRegex = new(
        @"class\s+(\w+)\s+extends\s+(\w+)",
        RegexOptions.IgnoreCase);

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Parses the class header (ClassName and ParentClass) from the specified file.
    /// </summary>
    /// <param name="filePath">The absolute path to the .uc file.</param>
    /// <returns>A <see cref="ClassHeader"/> if successful; otherwise, <c>null</c>.</returns>
    public static ClassHeader? ParseClassHeader(string filePath)
    {
        string? content = TryReadFile(filePath);
        if (content == null) return null;

        var m = ClassHeaderRegex.Match(content);
        if (!m.Success) return null;

        return new ClassHeader
        {
            Name = m.Groups[1].Value,
            ParentName = m.Groups[2].Value
        };
    }

    /// <summary>
    /// Extracts all variable declarations marked with the <c>config</c> keyword from a .uc file.
    /// </summary>
    /// <param name="filePath">The absolute path to the .uc file.</param>
    /// <returns>A list of <see cref="ConfigVarDecl"/> objects, or <c>null</c> if the file remains unreadable.</returns>
    public static List<ConfigVarDecl>? ParseConfigVariables(string filePath)
    {
        string? content = TryReadFile(filePath);
        if (content == null)
            return null;

        string declSection = GetDeclarationSection(content);
        var results = new List<ConfigVarDecl>();

        foreach (Match m in ConfigVarRegex.Matches(declSection))
        {
            string innerArrayType = m.Groups[1].Value;  // array<T>
            string simpleType = m.Groups[2].Value;      // T or T[]
            string nameList = m.Groups[3].Value;        // a, b, c

            string baseType = !string.IsNullOrEmpty(innerArrayType) ? innerArrayType : simpleType;
            bool isArrayFromType = !string.IsNullOrEmpty(innerArrayType) || m.Groups[2].Value.Contains('[');

            foreach (var rawName in nameList.Split(','))
            {
                string namePart = rawName.Trim();
                if (string.IsNullOrEmpty(namePart)) continue;

                bool isArray = isArrayFromType || namePart.Contains('[');
                string cleanName = namePart.Split('[')[0].Trim();
                
                // Clean baseType if it has brackets
                string cleanBaseType = baseType.Split('[')[0].Trim();

                results.Add(new ConfigVarDecl
                {
                    Name = cleanName,
                    TypeName = isArray ? cleanBaseType + "[]" : cleanBaseType,
                    BaseType = cleanBaseType,
                    IsArray = isArray,
                });
            }
        }

        return results;
    }

    /// <summary>
    /// Extracts all struct definitions from the specified .uc file.
    /// </summary>
    /// <param name="filePath">The absolute path to the .uc file.</param>
    /// <returns>A list of identified <see cref="StructDef"/> objects.</returns>
    public static List<StructDef> ParseStructs(string filePath)
    {
        string? content = TryReadFile(filePath);
        if (content == null)
            return new List<StructDef>();

        return ParseStructsFromContent(content);
    }

    /// <summary>
    /// Extracts struct definitions from a raw content string.
    /// </summary>
    /// <param name="content">The raw source text to scan.</param>
    /// <returns>A list of identified <see cref="StructDef"/> objects.</returns>
    public static List<StructDef> ParseStructsFromContent(string content)
    {
        var results = new List<StructDef>();

        foreach (Match structMatch in StructRegex.Matches(content))
        {
            string structName = structMatch.Groups[1].Value;
            string body = structMatch.Groups[2].Value;
            var fields = ParseStructFieldsFromBody(body);
            results.Add(new StructDef { Name = structName, Fields = fields });
        }

        return results;
    }

    /// <summary>
    /// Searches for a specific struct definition by name in the given file.
    /// </summary>
    /// <param name="filePath">The absolute path to the .uc file.</param>
    /// <param name="structName">The case-insensitive name of the struct.</param>
    /// <returns>A <see cref="StructDef"/> if found; otherwise, <c>null</c>.</returns>
    public static StructDef? FindStruct(string filePath, string structName)
    {
        string? content = TryReadFile(filePath);
        if (content == null)
            return null;

        // Build a targeted pattern for this specific struct name
        var pattern = new Regex(
            $@"struct\s+(?:\w+\s+)*?{Regex.Escape(structName)}\s*\{{([^}}]*)\}}",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        var m = pattern.Match(content);
        if (!m.Success)
            return null;

        string body = m.Groups[1].Value;
        return new StructDef
        {
            Name = structName,
            Fields = ParseStructFieldsFromBody(body),
        };
    }

    /// <summary>
    /// Performs a lightweight scan to determine if a file contains a specific struct definition.
    /// </summary>
    /// <param name="filePath">The absolute path to the .uc file.</param>
    /// <param name="structName">The case-insensitive name of the struct.</param>
    /// <returns><c>true</c> if the struct declaration is found; otherwise, <c>false</c>.</returns>
    public static bool FileContainsStruct(string filePath, string structName)
    {
        // Pre-compile a lightweight pattern — match struct declaration with optional opening brace
        // that may appear on the same OR next line:
        //   struct MyStruct {          ← brace same line
        //   struct native MyStruct     ← brace may be on next line
        string escapedName = Regex.Escape(structName);
        var quickPattern = new Regex(
            $@"struct\s+(?:\w+\s+)*?{escapedName}\s*(\{{)?",
            RegexOptions.IgnoreCase);

        try
        {
            string? content = TryReadFile(filePath);
            if (content == null) return false;

            // Use the full content so multi-line splits don't fool us
            // but we use a lightweight pattern (no body parsing needed)
            return quickPattern.IsMatch(content);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Truncates source content at the first function definition to isolate variable declarations.
    /// </summary>
    /// <param name="content">The full source content of the .uc file.</param>
    /// <returns>The portion of the content containing only variable declarations, or the full content if no function is found.</returns>
    public static string GetDeclarationSection(string content)
    {
        var m = FunctionStartRegex.Match(content);
        return m.Success ? content.Substring(0, m.Index) : content;
    }

    /// <summary>
    /// Buffers the specified file into memory, attempting UTF-8 before falling back to Windows-1252.
    /// </summary>
    /// <param name="filePath">The path to the file to read.</param>
    /// <returns>The file content as a string, or <c>null</c> if reading fails.</returns>
    public static string? TryReadFile(string filePath)
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(filePath);

            // Strip UTF-8 BOM if present
            int start = 0;
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                start = 3;

            // Try UTF-8
            try
            {
                // Note: Standard UTF8.GetString will not throw on invalid sequences unless a custom decoder is used.
                // However, we can check for common invalid patterns if we wanted to be stricter.
                return Encoding.UTF8.GetString(bytes, start, bytes.Length - start);
            }
            catch (DecoderFallbackException)
            {
                // Fall back to Windows-1252
                var enc = Encoding.GetEncoding(1252);
                return enc.GetString(bytes, start, bytes.Length - start);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static List<StructFieldDecl> ParseStructFieldsFromBody(string body)
    {
        var fields = new List<StructFieldDecl>();

        foreach (Match m in StructFieldRegex.Matches(body))
        {
            string innerArrayType = m.Groups[1].Value;
            string simpleType = m.Groups[2].Value;
            string nameList = m.Groups[3].Value;

            string baseType = !string.IsNullOrEmpty(innerArrayType) ? innerArrayType : simpleType;
            bool isArrayFromType = !string.IsNullOrEmpty(innerArrayType) || m.Groups[2].Value.Contains('[');

            foreach (var rawName in nameList.Split(','))
            {
                string namePart = rawName.Trim();
                if (string.IsNullOrEmpty(namePart)) continue;

                bool isArray = isArrayFromType || namePart.Contains('[');
                string cleanName = namePart.Split('[')[0].Trim();
                
                // Clean baseType
                string cleanBaseType = baseType.Split('[')[0].Trim();

                fields.Add(new StructFieldDecl
                {
                    Name = cleanName,
                    TypeName = isArray ? cleanBaseType + "[]" : cleanBaseType,
                    BaseType = cleanBaseType,
                    IsArray = isArray,
                });
            }
        }

        return fields;
    }
}

// -------------------------------------------------------------------------
// Data types returned by UnrealScriptParser
/// <summary>Parsed class header information.</summary>
public sealed class ClassHeader
{
    public string Name { get; init; } = "";
    public string ParentName { get; init; } = "";
}

// -------------------------------------------------------------------------

/// <summary>A parsed config variable declaration from a .uc class file.</summary>
public sealed class ConfigVarDecl
{
    public string Name { get; init; } = "";
    public string TypeName { get; init; } = "";   // e.g. "MyStruct[]" or "int"
    public string BaseType { get; init; } = "";   // e.g. "MyStruct" or "int"
    public bool IsArray { get; init; }
}

/// <summary>A parsed struct definition from a .uc file.</summary>
public sealed class StructDef
{
    public string Name { get; init; } = "";
    public List<StructFieldDecl> Fields { get; init; } = new();
}

/// <summary>A parsed field inside a struct definition.</summary>
public sealed class StructFieldDecl
{
    public string Name { get; init; } = "";
    public string TypeName { get; init; } = "";
    public string BaseType { get; init; } = "";
    public bool IsArray { get; init; }
}
