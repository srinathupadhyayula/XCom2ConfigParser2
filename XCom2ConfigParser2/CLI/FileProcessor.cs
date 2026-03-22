using System.Text;
using XCom2ConfigParser2.Core;
using XCom2ConfigParser2.Parser;
using XCom2ConfigParser2.StructValidation;
using XCom2ConfigParser2.Validation;

namespace XCom2ConfigParser2.CLI;

/// <summary>
/// Result of processing a single file.
/// </summary>
public sealed class FileProcessingResult
{
    public string FilePath { get; set; } = "";
    public IReadOnlyList<Diagnostic> Diagnostics { get; set; } = Array.Empty<Diagnostic>();
    public bool HasErrors => Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
    public bool HasWarnings => Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Warning);
    public int ErrorCount => Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
    public int WarningCount => Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning);
}

/// <summary>
/// Processes files through the validation pipeline.
/// </summary>
public sealed class FileProcessor
{
    private readonly IValidator _syntaxValidator;
    private readonly StructMemberValidator? _structValidator;
    private readonly bool _structValidationEnabled;

    public FileProcessor(
        IValidator syntaxValidator,
        Configuration.ParserSettings settings,
        bool structValidationEnabled,
        ModSrcPathCache? modSrcCache = null)
    {
        _syntaxValidator = syntaxValidator;
        _structValidationEnabled = structValidationEnabled;
        _structValidator = structValidationEnabled
            ? new StructMemberValidator(settings, modSrcCache: modSrcCache)
            : null;
    }

    /// <summary>
    /// Saves all underlying caches (e.g., VariableCache) to disk.
    /// </summary>
    public void SaveCaches()
    {
        _structValidator?.SaveCache();
    }

    /// <summary>
    /// Processes a single file through the validation pipeline.
    /// </summary>
    public FileProcessingResult ProcessFile(string filePath)
    {
        var result = new FileProcessingResult { FilePath = filePath };
        var allDiagnostics = new List<Diagnostic>();

        // Read file
        string content;
        try
        {
            content = ReadFile(filePath);
        }
        catch (Exception ex)
        {
            allDiagnostics.Add(CreateIoError(ex, filePath));
            result.Diagnostics = allDiagnostics;
            return result;
        }

        // Handle empty files
        if (string.IsNullOrEmpty(content))
        {
            result.Diagnostics = allDiagnostics;
            return result;
        }

        // Split into lines
        var lines = LineSplitter.Split(content);
        var mergedLines = LineSplitter.GetMergedLines(content, lines);

        // Check for space after continuation
        foreach (var (lineText, firstSpan, hasTrailingContinuation) in mergedLines)
        {
            if (hasTrailingContinuation)
            {
                allDiagnostics.Add(CreateTrailingContinuationError(firstSpan, content));
            }
        }

        // Tokenize directives
        var directives = DirectiveTokenizer.Tokenize(content, mergedLines);

        // Syntax validation
        var syntaxErrors = _syntaxValidator.Validate(content, directives, filePath);
        allDiagnostics.AddRange(syntaxErrors);

        // Struct member validation
        if (_structValidationEnabled && _structValidator != null)
        {
            var structErrors = _structValidator.Validate(content, directives, filePath);
            allDiagnostics.AddRange(structErrors);
        }

        result.Diagnostics = allDiagnostics;
        return result;
    }

    private string ReadFile(string filePath)
    {
        // Check UTF-8 encoding
        var bytes = File.ReadAllBytes(filePath);

        // Strip BOM if present
        int start = 0;
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            start = 3;

        // Validate UTF-8
        try
        {
            return Encoding.UTF8.GetString(bytes, start, bytes.Length - start);
        }
        catch (ArgumentException)
        {
            throw new IOException("Invalid UTF-8 encoding");
        }
    }

    private Diagnostic CreateIoError(Exception ex, string filePath)
    {
        return new Diagnostic(
            ErrorCode.Other,
            ex.Message,
            new SpanWithLocation(0, 0, 1, 1, 1, 1),
            "",
            DiagnosticSeverity.Error);
    }

    private Diagnostic CreateTrailingContinuationError(LineSpan lineSpan, string content)
    {
        return new Diagnostic(
            ErrorCode.TrailingContinuation,
            "Trailing \\\\ without following line",
            new SpanWithLocation(
                lineSpan.End - 2, lineSpan.End,
                lineSpan.LineNumber, GetColumn(content, lineSpan.End - 2),
                lineSpan.LineNumber, GetColumn(content, lineSpan.End)),
            lineSpan.Extract(content),
            DiagnosticSeverity.Error);
    }

    private int GetColumn(string text, int position)
    {
        int col = 1;
        for (int i = 0; i < position && i < text.Length; i++)
        {
            if (text[i] == '\n')
                col = 1;
            else if (text[i] != '\r')
                col++;
        }
        return col;
    }
}
