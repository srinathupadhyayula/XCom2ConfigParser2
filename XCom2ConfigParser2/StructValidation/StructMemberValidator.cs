using XCom2ConfigParser2.Core;
using XCom2ConfigParser2.Parser;

namespace XCom2ConfigParser2.StructValidation;

/// <summary>
/// Validates struct member names against UnrealScript struct definitions.
/// Uses a two-phase resolution with session-scoped variable caching:
///   Phase 1: Variable type resolution (VariableTypeResolver → VariableCache)
///   Phase 2: Struct definition resolution (StructDefinitionResolver → StructCache)
/// </summary>
public sealed class StructMemberValidator
{
    private readonly VariableTypeResolver _varResolver;
    private readonly StructDefinitionResolver _structResolver;
    private readonly StructCache _cache;
    private readonly VariableCache _varCache;
    private readonly bool _enabled;

    public StructMemberValidator(Configuration.ParserSettings settings, bool enabled = true, ModSrcPathCache? modSrcCache = null)
    {
        _cache = new StructCache(settings.CachePath);
        // Clear negative cache entries from previous (possibly buggy) runs at startup
        _cache.ClearNegativeEntries();

        _varCache = new VariableCache();
        _varResolver = new VariableTypeResolver(settings, modSrcCache);
        _structResolver = new StructDefinitionResolver(settings, _cache, modSrcCache);
        _enabled = enabled;
    }

    /// <summary>
    /// Validates struct members in all KVP directives.
    /// </summary>
    public IReadOnlyList<Diagnostic> Validate(
        string text,
        List<Directive> directives,
        string filePath)
    {
        if (!_enabled) return Array.Empty<Diagnostic>();

        var diagnostics = new List<Diagnostic>();
        string? currentSection = null;

        foreach (var directive in directives)
        {
            if (directive.Type == DirectiveType.SectionHeader)
            {
                currentSection = directive.SectionHeader!.Value.GetObjectName(text);
                continue;
            }

            if (directive.Type != DirectiveType.Kvp || currentSection == null)
                continue;

            var kvp = directive.Kvp!.Value;
            string propertyName = kvp.GetPropertyName(text);

            // Strip array index suffix from property name
            propertyName = StripIndexSuffix(propertyName);

            // Phase 1: Get variable type — check session cache first to avoid re-parsing .uc files
            VariableTypeResolutionResult varTypeResult;
            if (!_varCache.TryGet(currentSection, propertyName, out var cachedVarResult))
            {
                varTypeResult = _varResolver.Resolve(currentSection, propertyName);
                _varCache.Set(currentSection, propertyName, varTypeResult);
            }
            else
            {
                varTypeResult = cachedVarResult;
            }

            if (!varTypeResult.Found || string.IsNullOrEmpty(varTypeResult.BaseType))
            {
                diagnostics.Add(CreateStructDefNotFoundWarning(
                    currentSection, propertyName, "Unknown type",
                    kvp.ValueSpan, text, varTypeResult.SearchedPaths));
                continue;
            }

            // Check for array prefix on non-array property
            bool isArrayType = varTypeResult.FullType?.EndsWith("[]") == true;
            bool hasPrefix = kvp.Operation != KvpOperation.Set;

            if (hasPrefix && !isArrayType)
            {
                diagnostics.Add(CreateArrayPrefixOnNonArrayError(
                    kvp.Operation, propertyName, varTypeResult.FullType,
                    kvp.IdentSpan, text, currentSection));
            }

            // Skip non-struct types (uses shared KnownPrimitives — no allocation)
            if (UnrealScriptParser.KnownPrimitives.Contains(varTypeResult.BaseType))
                continue;

            // Parse value — if it's a struct literal, validate its members
            PropValue? value = TryParseValue(kvp.ValueSpan, text);
            if (value is not StructValue structValue)
                continue;

            // Phase 2: Get struct definition
            var structResult = _structResolver.Resolve(varTypeResult.BaseType);
            if (!structResult.Found || structResult.StructDef == null)
            {
                diagnostics.Add(CreateStructDefNotFoundWarning(
                    currentSection, propertyName, varTypeResult.BaseType,
                    kvp.ValueSpan, text, structResult.SearchedPaths));
                continue;
            }

            // Validate field names
            diagnostics.AddRange(ValidateStructMembers(
                structResult.StructDef, structValue,
                kvp.ValueSpan, text, currentSection, propertyName));
        }

        return diagnostics;
    }

    private string StripIndexSuffix(string propertyName)
    {
        int bracketPos = propertyName.IndexOf('[');
        if (bracketPos >= 0)
            return propertyName.Substring(0, bracketPos);

        int parenPos = propertyName.IndexOf('(');
        if (parenPos >= 0)
            return propertyName.Substring(0, parenPos);

        return propertyName;
    }

    private PropValue? TryParseValue(Span valueSpan, string text)
    {
        string value = valueSpan.Extract(text).Trim();
        return StructParser.TryParse(value);
    }

    private IEnumerable<Diagnostic> ValidateStructMembers(
        CachedStructDef structDef,
        StructValue structValue,
        Span valueSpan,
        string text,
        string sectionName,
        string propertyName)
    {
        var validFieldNames = new HashSet<string>(structDef.Fields.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);

        foreach (var child in structValue.Children)
        {
            if (!validFieldNames.Contains(child.Name))
            {
                yield return CreateInvalidStructMemberError(
                    child.Name, structDef, sectionName, propertyName,
                    valueSpan, text);
            }
        }
    }

    private Diagnostic CreateInvalidStructMemberError(
        string invalidMember,
        CachedStructDef structDef,
        string sectionName,
        string propertyName,
        Span valueSpan,
        string text)
    {
        string message = $"Unknown struct member \"{invalidMember}\" in " +
            $"[{sectionName}] property \"{propertyName}\".\n" +
            $"  Valid members: {string.Join(", ", structDef.Fields.Select(f => f.Name))}\n" +
            $"  Struct defined in: {structDef.SourceFile}";

        return CreateDiagnostic(
            ErrorCode.InvalidStructMember,
            message,
            valueSpan,
            text,
            DiagnosticSeverity.Error);
    }

    private Diagnostic CreateStructDefNotFoundWarning(
        string sectionName,
        string propertyName,
        string typeName,
        Span valueSpan,
        string text,
        IReadOnlyList<string> searchedPaths)
    {
        string message = $"Could not resolve struct definition for [{sectionName}] " +
            $"property \"{propertyName}\" (type: {typeName}). Struct member validation skipped.\n" +
            $"  Searched: {string.Join(", ", searchedPaths)}";

        return CreateDiagnostic(
            ErrorCode.StructDefNotFound,
            message,
            valueSpan,
            text,
            DiagnosticSeverity.Warning);
    }

    private Diagnostic CreateArrayPrefixOnNonArrayError(
        KvpOperation operation,
        string propertyName,
        string? propertyType,
        Span span,
        string text,
        string sectionName)
    {
        string prefixStr = operation switch
        {
            KvpOperation.InsertUnique => "+",
            KvpOperation.Insert => ".",
            KvpOperation.Remove => "-",
            KvpOperation.Clear => "!",
            _ => ""
        };

        string message = $"Array prefix '{prefixStr}' used on non-array property \"{propertyName}\"";
        if (!string.IsNullOrEmpty(propertyType))
            message += $" (type: {propertyType})";
        message += $".\n  In [{sectionName}]\n  Note: Only array properties can use +, -, ., ! prefixes";

        return CreateDiagnostic(
            ErrorCode.ArrayPrefixOnNonArray,
            message,
            span,
            text,
            DiagnosticSeverity.Error);
    }

    private Diagnostic CreateDiagnostic(
        ErrorCode code,
        string message,
        Span span,
        string text,
        DiagnosticSeverity severity)
    {
        int safeStart = Math.Max(0, Math.Min(span.Start, text.Length));
        int safeEnd = Math.Max(safeStart, Math.Min(span.End, text.Length));

        int startLine = 1, startCol = 1;
        int endLine = 1, endCol = 1;

        for (int i = 0; i < safeStart; i++)
        {
            if (text[i] == '\n') { startLine++; startCol = 1; }
            else if (text[i] != '\r') startCol++;
        }

        endLine = startLine;
        endCol = startCol;
        for (int i = safeStart; i < safeEnd; i++)
        {
            if (text[i] == '\n') { endLine++; endCol = 1; }
            else if (text[i] != '\r') endCol++;
        }

        string sourceLine = GetSourceLine(text, safeStart);

        return new Diagnostic(
            code,
            message,
            new SpanWithLocation(new Span(safeStart, safeEnd), new SourceLocation(startLine, startCol), new SourceLocation(endLine, endCol)),
            sourceLine,
            severity);
    }

    private string GetSourceLine(string text, int position)
    {
        int start = position;
        int end = position;

        while (start > 0 && text[start - 1] != '\n' && text[start - 1] != '\r')
            start--;

        while (end < text.Length && text[end] != '\n' && text[end] != '\r')
            end++;

        return text.Substring(start, end - start).TrimEnd('\r', '\n');
    }
}
