using Microsoft.Extensions.Logging;
using X2ModCompiler.Core.Core;
using X2ModCompiler.Core.Parser;
using X2ModCompiler.Core.StructValidation;

namespace X2ModCompiler.Core.Validation;

/// <summary>
/// Orchestrates the validation of configuration properties against UnrealScript class and struct definitions.
/// This validator performs a two-phase resolution:
/// <list type="number">
/// <item><description><b>Variable Resolution:</b> Maps a configuration property name to its declared type in a specific UnrealScript class.</description></item>
/// <item><description><b>Struct Resolution:</b> If the property is a struct, resolves its definition to verify that all nested members are valid.</description></item>
/// </list>
/// </summary>
public sealed class StructMemberValidator
{
    private readonly VariableTypeResolver _varResolver;
    private readonly StructDefinitionResolver _structResolver;
    private readonly StructCache _cache;
    private readonly VariableCache _varCache;
    private readonly bool _enabled;
    private readonly ILogger<StructMemberValidator> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StructMemberValidator"/> class.
    /// </summary>
    /// <param name="settings">The parser settings containing cache and search paths.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="enabled">Whether validation is active.</param>
    /// <param name="modSrcCache">Optional cache for mod source file locations.</param>
    public StructMemberValidator(
        Configuration.ParserSettings settings,
        ILoggerFactory loggerFactory,
        bool enabled = true,
        ModSrcPathCache? modSrcCache = null)
    {
        _cache = new StructCache(settings.CachePath);
        // Clear negative cache entries from previous (possibly buggy) runs at startup
        _cache.ClearNegativeEntries();

        _varCache = new VariableCache(settings.CachePath);
        _varCache.ClearNegativeEntries();
        _varResolver = new VariableTypeResolver(settings, modSrcCache);
        _structResolver = new StructDefinitionResolver(settings, _cache, loggerFactory, modSrcCache);
        _enabled = enabled;
        _logger = loggerFactory.CreateLogger<StructMemberValidator>();
    }

    /// <summary>
    /// Persists session-scoped variable resolution results to the disk cache.
    /// </summary>
    public void SaveCache()
    {
        _varCache.Save();
    }

    /// <summary>
    /// Validates a list of configuration directives within a specific file context.
    /// </summary>
    /// <param name="text">The raw text of the configuration file.</param>
    /// <param name="directives">The list of parsed directives to validate.</param>
    /// <param name="filePath">The path to the configuration file being validated.</param>
    /// <returns>A collection of <see cref="Diagnostic"/> objects representing identified errors and warnings.</returns>
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
            string propertyValueString = kvp.GetValue(text);
            PropValue? value = TryParseValue(propertyValueString);
            bool isStructLiteral = value is StructValue;
            bool hasArrayPrefix = kvp.Operation != KvpOperation.Set;

            string propertyName = kvp.GetPropertyName(text);
            propertyName = StripIndexSuffix(propertyName);

            // Phase 2: Resolve variable type
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

            // Case A: Variable resolution failed
            if (!varTypeResult.Found || string.IsNullOrEmpty(varTypeResult.BaseType))
            {
                // If value IS a struct literal but we can't find the definition, WARN.
                if (isStructLiteral)
                {
                    diagnostics.Add(CreateStructDefNotFoundWarning(
                        currentSection, propertyName, "Unknown type",
                        kvp.ValueSpan, text, varTypeResult.SearchedPaths));
                }
                continue;
            }

            // Case B: Variable resolution succeeded
            bool isArrayType = varTypeResult.FullType?.EndsWith("[]") == true;

            // Check for array prefix usage correctly
            if (hasArrayPrefix && !isArrayType)
            {
                diagnostics.Add(CreateArrayPrefixOnNonArrayWarning(
                    kvp.Operation, propertyName, varTypeResult.FullType,
                    kvp.IdentSpan, text, currentSection));
            }

            // If it's a struct property and we have a structural value, validate members
            if (isStructLiteral && value is StructValue structValue && !UnrealScriptParser.KnownPrimitives.Contains(varTypeResult.BaseType))
            {
                var structResult = _structResolver.Resolve(varTypeResult.BaseType);
                if (!structResult.Found || structResult.StructDef == null)
                {
                    diagnostics.Add(CreateStructDefNotFoundWarning(
                        currentSection, propertyName, varTypeResult.BaseType,
                        kvp.ValueSpan, text, structResult.SearchedPaths));
                    continue;
                }

                diagnostics.AddRange(ValidateStructMembers(
                    structResult.StructDef, structValue,
                    kvp.ValueSpan, text, currentSection, propertyName));
            }
        }

        return diagnostics;
    }

    /// <summary>
    /// Removes array index or parenthetical suffixes from a property name to get the base identifier.
    /// Example: "MyProperty[0]" -> "MyProperty", "MyProperty(0)" -> "MyProperty"
    /// </summary>
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

    private PropValue? TryParseValue(string value)
    {
        return StructParser.TryParse(value.Trim());
    }

    /// <summary>
    /// Compares members of a struct value literal against the fields defined in the resolved UnrealScript struct.
    /// </summary>
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

    /// <summary>
    /// Creates a diagnostic for an unrecognized struct member.
    /// </summary>
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

    /// <summary>
    /// Creates a warning diagnostic when a struct definition cannot be found in the script source.
    /// </summary>
    private Diagnostic CreateStructDefNotFoundWarning(
        string sectionName,
        string propertyName,
        string typeName,
        Span valueSpan,
        string text,
        IReadOnlyList<string> searchedPaths)
    {
        string pathsSummary;
        if (searchedPaths.Count <= 5)
        {
            pathsSummary = string.Join(", ", searchedPaths);
        }
        else
        {
            pathsSummary = string.Join(", ", searchedPaths.Take(5)) + $", ... and {searchedPaths.Count - 5} more. See the full report for details.";
        }

        string message = $"Could not resolve struct definition for [{sectionName}] " +
            $"property \"{propertyName}\" (type: {typeName}). Struct member validation skipped.\n" +
            $"  Searched: {pathsSummary}";

        return CreateDiagnostic(
            ErrorCode.StructDefNotFound,
            message,
            valueSpan,
            text,
            DiagnosticSeverity.Warning);
    }

    /// <summary>
    /// Creates a warning diagnostic for using array operations (+, -, ., !) on non-array properties.
    /// </summary>
    private Diagnostic CreateArrayPrefixOnNonArrayWarning(
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
            DiagnosticSeverity.Warning);
    }

    /// <summary>
    /// Factory method for creating detailed <see cref="Diagnostic"/> objects with line and column information.
    /// </summary>
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

    /// <summary>
    /// Extracts the full text of the source line containing the specified character position.
    /// </summary>
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
