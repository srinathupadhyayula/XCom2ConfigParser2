using System.Text;
using System.Text.RegularExpressions;

namespace XCom2ConfigParser2.StructValidation;

/// <summary>
/// Canonical, unified parser for UnrealScript (.uc) files.
/// Provides source-of-truth regex patterns and helpers for extracting
/// config variable declarations and struct definitions from .uc source.
/// All other parsers (VariableTypeResolver, StructDefinitionResolver, UcFileParser)
/// delegate to this class.
/// </summary>
public static class UnrealScriptParser
{
    // -------------------------------------------------------------------------
    // Shared primitive type set — used by StructField and StructMemberValidator
    // -------------------------------------------------------------------------

    /// <summary>
    /// Set of known UnrealScript primitive (non-struct) type names.
    /// Types NOT in this set are treated as potential struct types.
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
    /// Parses the class name and parent class name from the file header.
    /// returns null if the header cannot be found or parsed.
    /// </summary>
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
    /// Reads a .uc file, extracts only the variable declaration section (up to
    /// the first function definition), and returns all 'var config' declarations.
    /// Returns null if the file cannot be read.
    /// </summary>
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
    /// Parses all struct definitions from a .uc file.
    /// Returns an empty list on read or parse error (never throws).
    /// </summary>
    public static List<StructDef> ParseStructs(string filePath)
    {
        string? content = TryReadFile(filePath);
        if (content == null)
            return new List<StructDef>();

        return ParseStructsFromContent(content);
    }

    /// <summary>
    /// Parses struct definitions from already-loaded content string.
    /// Useful when the caller has already read the file.
    /// </summary>
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
    /// Finds a single named struct definition in a .uc file.
    /// Returns null if not found or file unreadable.
    /// Uses early-exit line-by-line reading to avoid reading the full file
    /// when the struct is near the top.
    /// </summary>
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
    /// Checks whether a file contains a struct definition with a given name,
    /// without parsing the full body. Uses <see cref="File.ReadLines"/> for
    /// streaming read — exits as soon as the match is found.
    /// Returns false on any read/permission error.
    /// </summary>
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
    /// Returns the portion of source content before the first function definition.
    /// This limits regex scanning to the variable declaration section only,
    /// preventing false matches inside function bodies.
    /// </summary>
    public static string GetDeclarationSection(string content)
    {
        var m = FunctionStartRegex.Match(content);
        return m.Success ? content.Substring(0, m.Index) : content;
    }

    /// <summary>
    /// Attempts to read a file as UTF-8. If that throws a decoding error,
    /// falls back to Windows-1252 (common encoding for older UE3 source files).
    /// Returns null on IO error (file not found, permission denied, etc.).
    /// </summary>
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
