using System.Text;
using XCom2ConfigParser2.Core;
using XCom2ConfigParser2.Parser;
using XCom2ConfigParser2.StructValidation;
using XCom2ConfigParser2.Validation;

namespace XCom2ConfigParser2.CLI;

/// <summary>
/// Encapsulates the results of validating a single configuration file, including any identified diagnostics.
/// </summary>
public sealed class FileProcessingResult
{
    /// <summary>Gets or sets the absolute path of the processed file.</summary>
    public string FilePath { get; set; } = "";

    /// <summary>Gets or sets the collection of diagnostics (errors, warnings) found during processing.</summary>
    public IReadOnlyList<Diagnostic> Diagnostics { get; set; } = Array.Empty<Diagnostic>();

    /// <summary>Gets a value indicating whether any diagnostics with <see cref="DiagnosticSeverity.Error"/> were found.</summary>
    public bool HasErrors => Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);

    /// <summary>Gets a value indicating whether any diagnostics with <see cref="DiagnosticSeverity.Warning"/> were found.</summary>
    public bool HasWarnings => Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Warning);

    /// <summary>Gets the total count of error-level diagnostics.</summary>
    public int ErrorCount => Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);

    /// <summary>Gets the total count of warning-level diagnostics.</summary>
    public int WarningCount => Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning);
}

/// <summary>
/// Orchestrates the multi-stage validation pipeline for configuration files.
/// </summary>
/// <remarks>
/// The pipeline includes:
/// <list type="number">
/// <item><description>UTF-8 health checks and line merging.</description></item>
/// <item><description>Tokenization via <see cref="DirectiveTokenizer"/>.</description></item>
/// <item><description>Syntax validation via <see cref="IValidator"/>.</description></item>
/// <item><description>Contextual struct member validation via <see cref="StructMemberValidator"/> (if enabled).</description></item>
/// </list>
/// </remarks>
public sealed class FileProcessor
{
    private readonly IValidator _syntaxValidator;
    private readonly StructMemberValidator? _structValidator;
    private readonly bool _structValidationEnabled;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileProcessor"/> class.
    /// </summary>
    /// <param name="syntaxValidator">The validator responsible for basic syntax rules.</param>
    /// <param name="settings">The global parser settings.</param>
    /// <param name="structValidationEnabled">Whether to enable deep struct member validation.</param>
    /// <param name="modSrcCache">Optional cache for mod source paths, required for struct resolution.</param>
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
    /// Persists all underlying diagnostic and metadata caches (e.g., <see cref="VariableCache"/>) to disk.
    /// </summary>
    public void SaveCaches()
    {
        _structValidator?.SaveCache();
    }

    /// <summary>
    /// Executes the full validation pipeline on the specified configuration file.
    /// </summary>
    /// <param name="filePath">The absolute path to the .ini file to validate.</param>
    /// <returns>A <see cref="FileProcessingResult"/> containing the outcome and any identified diagnostics.</returns>
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
        foreach (var (lineText, firstSpan, lastSpan, hasTrailingContinuation) in mergedLines)
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
