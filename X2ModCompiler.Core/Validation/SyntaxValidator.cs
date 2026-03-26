using System.Text.RegularExpressions;
using X2ModCompiler.Core.Core;
using X2ModCompiler.Core.Parser;

namespace X2ModCompiler.Core.Validation;

/// <summary>
/// Validates directives against UE3 grammar rules.
/// </summary>
public interface IValidator
{
    IReadOnlyList<Diagnostic> Validate(string text, List<Directive> directives, string filePath);
}

/// <summary>
/// Simple syntax validator implementing UE3 grammar rules.
/// </summary>
public sealed class SyntaxValidator : IValidator
{
    // Property name: MyProp, MyProp[0], MyProp(1), MyProp[eStat_Will]
    private static readonly Regex PropertyNameRegex =
        new(@"^[A-Za-z][A-Za-z0-9_]*(?:\[[A-Za-z0-9_]*\]|\([A-Za-z0-9_]*\))?$");

    // Section object name: IDENT or IDENT IDENT
    private static readonly Regex ObjectNameRegex =
        new(@"^[A-Za-z][A-Za-z0-9_]*(?:[ \t.]+[A-Za-z][A-Za-z0-9_]*)?$");

    // Value literal (names/unquoted strings in config): allows dots, underscores, slashes, colons, hyphens, alphanumeric
    private static readonly Regex ValueLiteralRegex =
        new(@"^[A-Za-z0-9_][A-Za-z0-9_./:-]*$");

    // Boolean values (case-insensitive)
    private static readonly HashSet<string> Booleans =
        new(StringComparer.OrdinalIgnoreCase) { "true", "false" };

    // Number pattern (including f suffix and leading dots like .5f)
    private static readonly Regex NumberRegex = new(@"^-?(?:[0-9]+\.?[0-9]*|\.[0-9]+)[fF]?$");

    public IReadOnlyList<Diagnostic> Validate(string text, List<Directive> directives, string filePath)
    {
        var errors = new List<Diagnostic>();
        string? currentSection = null;

        foreach (var directive in directives)
        {
            switch (directive.Type)
            {
                case DirectiveType.SectionHeader:
                    var section = directive.SectionHeader!.Value;
                    errors.AddRange(ValidateSectionHeader(text, section));
                    currentSection = section.GetObjectName(text);
                    break;

                case DirectiveType.Kvp:
                    var kvp = directive.Kvp!.Value;
                    errors.AddRange(ValidateKvp(text, kvp, currentSection));
                    break;

                case DirectiveType.Unknown:
                    var unknown = directive.Unknown!.Value;
                    errors.Add(CreateOtherError(text, unknown));
                    break;
            }
        }

        return errors;
    }

    private IEnumerable<Diagnostic> ValidateSectionHeader(string text, SectionHeader section)
    {
        string objectName = section.GetObjectName(text);
        string fullText = section.Span.Extract(text);

        // Check for malformed header: space before ] or trailing space after ]
        bool isMalformed = false;

        // Check if there's whitespace before the closing bracket
        if (fullText.Length >= 2 && fullText.StartsWith("[") && fullText.EndsWith("]"))
        {
            string innerContent = fullText.Substring(1, fullText.Length - 2);
            if (innerContent.EndsWith(" ") || innerContent.EndsWith("\t"))
            {
                isMalformed = true;
            }
        }

        // Check for trailing space after ]
        if (fullText.EndsWith(" ") || fullText.EndsWith("\t"))
        {
            isMalformed = true;
        }

        if (isMalformed)
        {
            yield return CreateDiagnostic(
                ErrorCode.MalformedHeader,
                "Invalid header. The first character of a header line must be '[' and the last must be ']'.",
                section.Span,
                text);
            yield break;
        }

        // Validate identifier
        if (!ObjectNameRegex.IsMatch(objectName))
        {
            yield return CreateDiagnostic(
                ErrorCode.InvalidIdentifier,
                "Invalid identifier",
                section.ObjectNameSpan,
                text);
        }
    }

    private IEnumerable<Diagnostic> ValidateKvp(string text, Kvp kvp, string? currentSection)
    {
        string propertyName = kvp.GetPropertyName(text);
        string value = kvp.GetValue(text).Trim();

        // Validate property name
        if (!PropertyNameRegex.IsMatch(propertyName))
        {
            yield return CreateDiagnostic(
                ErrorCode.InvalidIdentifier,
                "Invalid identifier",
                kvp.IdentSpan,
                text);
        }

        // Check for array prefix on non-array property
        if (kvp.Operation != KvpOperation.Set)
        {
            // We don't know the variable type at this point - that's handled by StructMemberValidator
            // But we can flag it as a warning that prefix usage should be verified
            // The actual ArrayPrefixOnNonArray error is emitted by StructMemberValidator when it knows the type
        }

        // Validate value
        var valueErrors = ValidateValue(text, kvp.ValueSpan, value);
        foreach (var error in valueErrors)
            yield return error;
    }

    private IEnumerable<Diagnostic> ValidateValue(string text, Span valueSpan, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            yield return CreateDiagnostic(
                ErrorCode.BadValue,
                "Bad Value",
                valueSpan,
                text);
            yield break;
        }

        // Check for trailing continuation
        if (value.EndsWith("\\\\"))
        {
            yield return CreateDiagnostic(
                ErrorCode.TrailingContinuation,
                "Trailing \\\\ without following line",
                valueSpan,
                text);
            yield break;
        }

        // Try matching known types
        if (Booleans.Contains(value))
            yield break;

        if (NumberRegex.IsMatch(value))
            yield break;

        if (IsPossibleNameLiteral(value))
            yield break;

        if (value.StartsWith("\"") && value.EndsWith("\"") && value.Length >= 2)
            yield break; // String literal

        if (value.StartsWith("("))
        {
            var structErrors = ValidateStructOrArray(text, valueSpan, value);
            foreach (var error in structErrors)
                yield return error;
            yield break;
        }

        yield return CreateDiagnostic(
            ErrorCode.BadValue,
            "Bad Value",
            valueSpan,
            text);
    }

    private IEnumerable<Diagnostic> ValidateStructOrArray(string text, Span valueSpan, string value)
    {
        try
        {
            StructParser.Parse(value);
            return Array.Empty<Diagnostic>();
        }
        catch (ParseException ex)
        {
            int errorPos = valueSpan.Start + ex.Position;
            var diagnostic = CreateDiagnostic(
                ErrorCode.StructParseError,
                ex.Message,
                new Span(errorPos, errorPos + 1),
                text);
            return new[] { diagnostic };
        }
    }

    private bool IsPossibleNameLiteral(string value)
    {
        return ValueLiteralRegex.IsMatch(value);
    }

    private Diagnostic CreateDiagnostic(
        ErrorCode code,
        string message,
        Span span,
        string text,
        DiagnosticSeverity severity = DiagnosticSeverity.Error)
    {
        // Clamp span to valid range
        int safeStart = Math.Max(0, Math.Min(span.Start, text.Length));
        int safeEnd = Math.Max(safeStart, Math.Min(span.End, text.Length));

        // Calculate line/column positions
        int startLine = 1, startCol = 1;
        int endLine = 1, endCol = 1;

        for (int i = 0; i < safeStart; i++)
        {
            if (text[i] == '\n')
            {
                startLine++;
                startCol = 1;
            }
            else if (text[i] != '\r')
            {
                startCol++;
            }
        }

        endLine = startLine;
        endCol = startCol;
        for (int i = safeStart; i < safeEnd; i++)
        {
            if (text[i] == '\n')
            {
                endLine++;
                endCol = 1;
            }
            else if (text[i] != '\r')
            {
                endCol++;
            }
        }

        string sourceLine = GetSourceLine(text, safeStart);

        return new Diagnostic(
            code,
            message,
            new SpanWithLocation(new Span(safeStart, safeEnd), new SourceLocation(startLine, startCol),
                new SourceLocation(endLine, endCol)),
            sourceLine,
            severity);
    }

    private Diagnostic CreateOtherError(string text, Unknown unknown)
    {
        return CreateDiagnostic(
            ErrorCode.Other,
            "Invalid config directive",
            unknown.Span,
            text);
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
