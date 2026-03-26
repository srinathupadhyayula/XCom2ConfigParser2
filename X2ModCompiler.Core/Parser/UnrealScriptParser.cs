using System.Collections.Frozen;
using System.Text;
using System.Text.RegularExpressions;

namespace X2ModCompiler.Core.Parser;

/// <summary>
/// Provides a canonical, high-performance parser for UnrealScript (.uc) source files.
/// </summary>
public static partial class UnrealScriptParser
{
    public static readonly FrozenSet<string> KnownPrimitives = FrozenSet.ToFrozenSet(new[]
    {
        "bool", "byte", "int", "int16", "int64", "qword",
        "float", "double",
        "string", "name",
        "vector", "vector2d", "rotator", "quaternion", "matrix",
        "color", "linearcolor", "plane", "coords", "box", "sphere",
        "guid", "pointer", "object", "actor", "class",
    }, StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex(@"(?m)^\s*var\s*(?:\([^)]*\))?\s*(?:\w+\s+)*?config(?:\s*\([^)]*\))?(?:\s+\w+)*?\s+(?:array\s*<\s*(\w+)\s*>|(\w+(?:\s*\[\s*\])?))\s+([^;]+)\s*;", RegexOptions.IgnoreCase)]
    private static partial Regex ConfigVarRegex();

    [GeneratedRegex(@"struct\s+(?:\w+\s+)*?(\w+)\s*\{([^}]*)\}", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex StructRegex();

    [GeneratedRegex(@"var\s*(?:\([^)]*\))?\s*(?:\w+\s+)*?(?:array\s*<\s*(\w+)\s*>|(\w+(?:\s*\[\s*\])?))\s+([^;]+)\s*;", RegexOptions.IgnoreCase)]
    private static partial Regex StructFieldRegex();

    [GeneratedRegex(@"(?m)^\s*(?:(?:static|exec|simulated|reliable|unreliable|private|protected|public|final|native|event|delegate)\s+)*function\b", RegexOptions.IgnoreCase)]
    private static partial Regex FunctionStartRegex();

    [GeneratedRegex(@"class\s+(\w+)\s+extends\s+(\w+)", RegexOptions.IgnoreCase)]
    private static partial Regex ClassHeaderRegex();

    public static ClassHeader? ParseClassHeader(string filePath)
    {
        string? content = TryReadFile(filePath);
        if (content == null) return null;

        var m = ClassHeaderRegex().Match(content);
        if (!m.Success) return null;

        return new ClassHeader
        {
            Name = m.Groups[1].Value,
            ParentName = m.Groups[2].Value
        };
    }

    public static List<ConfigVarDecl>? ParseConfigVariables(string filePath)
    {
        string? content = TryReadFile(filePath);
        if (content == null)
            return null;

        string declSection = GetDeclarationSection(content);
        var results = new List<ConfigVarDecl>();

        foreach (Match m in ConfigVarRegex().Matches(declSection))
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

    public static List<StructDef> ParseStructs(string filePath)
    {
        string? content = TryReadFile(filePath);
        if (content == null)
            return new List<StructDef>();

        return ParseStructsFromContent(content);
    }

    public static List<StructDef> ParseStructsFromContent(string content)
    {
        var results = new List<StructDef>();

        foreach (Match structMatch in StructRegex().Matches(content))
        {
            string structName = structMatch.Groups[1].Value;
            string body = structMatch.Groups[2].Value;
            var fields = ParseStructFieldsFromBody(body);
            results.Add(new StructDef { Name = structName, Fields = fields });
        }

        return results;
    }

    public static StructDef? FindStruct(string filePath, string structName)
    {
        string? content = TryReadFile(filePath);
        if (content == null)
            return null;

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

    public static bool FileContainsStruct(string filePath, string structName)
    {
        string escapedName = Regex.Escape(structName);
        var quickPattern = new Regex(
            $@"struct\s+(?:\w+\s+)*?{escapedName}\s*(\{{)?",
            RegexOptions.IgnoreCase);

        try
        {
            string? content = TryReadFile(filePath);
            if (content == null) return false;

            return quickPattern.IsMatch(content);
        }
        catch
        {
            return false;
        }
    }

    public static string GetDeclarationSection(string content)
    {
        var m = FunctionStartRegex().Match(content);
        return m.Success ? content.Substring(0, m.Index) : content;
    }

    public static string? TryReadFile(string filePath)
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            int start = 0;
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                start = 3;

            try
            {
                return Encoding.UTF8.GetString(bytes, start, bytes.Length - start);
            }
            catch (DecoderFallbackException)
            {
                var enc = Encoding.GetEncoding(1252);
                return enc.GetString(bytes, start, bytes.Length - start);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static List<StructFieldDecl> ParseStructFieldsFromBody(string body)
    {
        var fields = new List<StructFieldDecl>();
        var fieldRegex = StructFieldRegex();

        foreach (Match m in fieldRegex.Matches(body))
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

public sealed class ClassHeader
{
    public string Name { get; init; } = "";
    public string ParentName { get; init; } = "";
}

public sealed class ConfigVarDecl
{
    public string Name { get; init; } = "";
    public string TypeName { get; init; } = "";
    public string BaseType { get; init; } = "";
    public bool IsArray { get; init; }
}

public sealed class StructDef
{
    public string Name { get; init; } = "";
    public List<StructFieldDecl> Fields { get; init; } = new();
}

public sealed class StructFieldDecl
{
    public string Name { get; init; } = "";
    public string TypeName { get; init; } = "";
    public string BaseType { get; init; } = "";
    public bool IsArray { get; init; }
}
