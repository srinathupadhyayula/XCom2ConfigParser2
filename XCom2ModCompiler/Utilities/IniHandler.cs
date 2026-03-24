using System.Text;
using System.Collections.Generic;
using XCom2ModCompiler.Exceptions;

namespace XCom2ModCompiler.Utilities;

/// <summary>
/// Handles discovery and modification of XComEngine.ini files for two-pass compilation.
/// </summary>
public class IniHandler
{
    private readonly string _projectRoot;
    private readonly IEnumerable<string> _iniRoots;
    private string? _targetFile;
    public string? TargetIniPath => _targetFile;

    public IniHandler(string projectRoot, IEnumerable<string> iniRoots)
    {
        _projectRoot = projectRoot;
        _iniRoots = iniRoots;
    }

    /// <summary>
    /// Discovers the single XComEngine.ini that contains the compiler sections.
    /// Throws BuildConfigurationException if multiple files contain these sections.
    /// </summary>
    public string FindTargetIni()
    {
        if (_targetFile != null) return _targetFile;

        var candidates = new List<string>();
        var targetSections = new[] { "[Engine.ScriptPackages]", "[UnrealEd.EditorEngine]", "[X2Compiler.DependantPackages]" };

        foreach (var root in _iniRoots)
        {
            if (!Directory.Exists(root)) continue;

            var files = Directory.GetFiles(root, "XComEngine.ini", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                if (FileContainsAnySection(file, targetSections))
                {
                    candidates.Add(file);
                }
            }
        }

        if (candidates.Count == 0)
        {
            return "";
        }

        if (candidates.Count > 1)
        {
            var message = "Multiple XComEngine.ini files contain compiler sections. Please consolidate them into a single file:\n" +
                          string.Join("\n", candidates);
            throw new BuildConfigurationException("XComEngine.ini", message);
        }

        _targetFile = candidates[0];
        return _targetFile;
    }

    private static bool FileContainsAnySection(string filePath, string[] sections)
    {
        foreach (var line in File.ReadLines(filePath))
        {
            var trimmed = line.Trim();
            foreach (var section in sections)
            {
                if (string.Equals(trimmed, section, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Prepares the INI content for staging. 
    /// Ensures the main mod is included in ModEditPackages and (optionally) adds dependent packages.
    /// </summary>
    public string PrepareStagedIni(string originalContent, string mainModName, List<string>? dependantPackages = null)
    {
        var lines = originalContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();
        
        // 1. Remove the [X2Compiler.DependantPackages] section entirely from staged version
        int depStartIndex = -1;
        int depEndIndex = -1;
        for (int i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (string.Equals(trimmed, "[X2Compiler.DependantPackages]", StringComparison.OrdinalIgnoreCase))
            {
                depStartIndex = i;
                continue;
            }

            if (depStartIndex != -1 && trimmed.StartsWith("["))
            {
                depEndIndex = i;
                break;
            }
        }
        if (depStartIndex != -1)
        {
            if (depEndIndex == -1) depEndIndex = lines.Count;
            lines.RemoveRange(depStartIndex, depEndIndex - depStartIndex);
        }

        // 2. Ensure [UnrealEd.EditorEngine] exists and contains the mod packages
        int engineSectionIndex = -1;
        for (int i = 0; i < lines.Count; i++)
        {
            if (string.Equals(lines[i].Trim(), "[UnrealEd.EditorEngine]", StringComparison.OrdinalIgnoreCase))
            {
                engineSectionIndex = i;
                break;
            }
        }

        if (engineSectionIndex == -1)
        {
            // Section missing? Add it at the end
            lines.Add("");
            lines.Add("[UnrealEd.EditorEngine]");
            engineSectionIndex = lines.Count - 1;
        }

        // Remove any existing mainMod entry to ensure we can place it at the absolute end
        for (int i = engineSectionIndex + 1; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.StartsWith("[")) break;
            if (trimmed.Contains($"={mainModName}", StringComparison.OrdinalIgnoreCase))
            {
                lines.RemoveAt(i);
                i--; 
            }
        }

        // If we have dependant packages to add, remove them too (to avoid duplicates)
        if (dependantPackages != null)
        {
            foreach (var dep in dependantPackages)
            {
                for (int i = engineSectionIndex + 1; i < lines.Count; i++)
                {
                    var trimmed = lines[i].Trim();
                    if (trimmed.StartsWith("[")) break;
                    if (trimmed.Contains($"={dep}", StringComparison.OrdinalIgnoreCase))
                    {
                        lines.RemoveAt(i);
                        i--;
                    }
                }
            }
        }

        // Find insertion point (end of section)
        int insertAt = -1;
        for (int i = engineSectionIndex + 1; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.StartsWith("["))
            {
                insertAt = i;
                break;
            }
        }
        if (insertAt == -1) insertAt = lines.Count;

        var toAdd = new List<string>();
        // 1. Dependent packages (if any)
        if (dependantPackages != null)
        {
            foreach (var dep in dependantPackages)
            {
                toAdd.Add($"+ModEditPackages={dep}");
            }
        }
        // 2. Main mod is ALWAYS included and ALWAYS last
        toAdd.Add($"+ModEditPackages={mainModName}");

        lines.InsertRange(insertAt, toAdd);

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Checks if the two-pass strategy is needed (i.e. if DependantPackages section exists and has entries).
    /// </summary>
    public bool IsTwoPassNeeded(string content)
    {
        return GetDependantPackages(content).Any();
    }

    /// <summary>
    /// Gets the list of package names from the [X2Compiler.DependantPackages] section.
    /// </summary>
    public List<string> GetDependantPackages(string content)
    {
        var packages = new List<string>();
        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        bool inSection = false;
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.Equals(trimmed, "[X2Compiler.DependantPackages]", StringComparison.OrdinalIgnoreCase))
            {
                inSection = true;
                continue;
            }
            if (inSection)
            {
                if (trimmed.StartsWith("[")) break;
                if (trimmed.StartsWith("+", StringComparison.OrdinalIgnoreCase) && trimmed.Contains("="))
                {
                    var package = trimmed.Split('=')[1].Trim();
                    if (!string.IsNullOrEmpty(package))
                    {
                        packages.Add(package);
                    }
                }
            }
        }
        return packages;
    }
}
