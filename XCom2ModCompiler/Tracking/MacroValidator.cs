using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace XCom2ModCompiler.Tracking;

public class MacroValidator
{
    private readonly ILogger<MacroValidator> _logger;
    private readonly Dictionary<string, List<MacroDefinition>> _definitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _allowedRedefines = new(StringComparer.OrdinalIgnoreCase);

    public MacroValidator(ILogger<MacroValidator> logger)
    {
        _logger = logger;
    }

    public void ParseMacroFile(string filePath)
    {
        if (!File.Exists(filePath)) return;

        var lines = File.ReadAllLines(filePath);
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            var match = Regex.Match(line, @"^`define\s+([A-Za-z0-9_]+)");
            if (match.Success)
            {
                var macroName = match.Groups[1].Value;
                if (!_definitions.ContainsKey(macroName))
                {
                    _definitions[macroName] = new List<MacroDefinition>();
                }
                _definitions[macroName].Add(new MacroDefinition(macroName, filePath, i + 1, false));
            }
        }
    }

    public ValidationResult Validate()
    {
        var errors = new List<MacroError>();

        foreach (var kvp in _definitions)
        {
            if (kvp.Value.Count > 1 && !_allowedRedefines.Contains(kvp.Key))
            {
                var firstDef = kvp.Value[0];
                var redef = kvp.Value[1];
                var error = new MacroError(kvp.Key, redef.FilePath, redef.LineNumber,
                    $"Macro '{kvp.Key}' redefined. Originally defined in {firstDef.FilePath}:{firstDef.LineNumber}");
                errors.Add(error);
                
                // Log detailed error with help text (mimics PowerShell detailed output)
                _logger.LogError("Implicit redefinition of macro {MacroName}", kvp.Key);
                _logger.LogError("    Note: Previously defined at {File}:{Line}", firstDef.FilePath, firstDef.LineNumber);
                _logger.LogError("    Note: Implicitly redefined at {File}:{Line}", redef.FilePath, redef.LineNumber);
                _logger.LogError("    Help: Rename the macro, or add ``// X2MBC-Redefine`` above to explicitly redefine and silence this warning.");
            }
        }

        return new ValidationResult(errors.Count == 0, errors);
    }

    public void AllowRedefine(string macroName)
    {
        _allowedRedefines.Add(macroName);
    }
}

public record MacroDefinition(string Name, string FilePath, int LineNumber, bool IsExplicitRedefine);
public record ValidationResult(bool IsValid, IReadOnlyList<MacroError> Errors);
public record MacroError(string MacroName, string FilePath, int LineNumber, string Message);
