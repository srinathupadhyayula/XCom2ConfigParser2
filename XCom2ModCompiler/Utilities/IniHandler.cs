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

    public string? FindTargetIni()
    {
        if (!string.IsNullOrEmpty(_targetFile)) return _targetFile;

        var candidates = new List<string>();
        foreach (var root in _iniRoots)
        {
            if (!Directory.Exists(root)) continue;
            var files = Directory.GetFiles(root, "XComEngine.ini", SearchOption.AllDirectories);
            candidates.AddRange(files);
        }

        if (candidates.Count == 0) return null;
        if (candidates.Count == 1)
        {
            _targetFile = candidates[0];
            return _targetFile;
        }

        var targetSections = new[] { "[X2ModCompiler.DependantPackages]", "[UnrealEd.EditorEngine]" };
        var priorityFiles = candidates.Where(f => FileContainsAnySection(f, targetSections)).ToList();
        
        if (priorityFiles.Count == 1)
        {
            _targetFile = priorityFiles[0];
        }
        else
        {
            _targetFile = candidates.OrderBy(f => f.Contains("0Base") ? 1 : 0).First();
        }

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

    public string PrepareStagedIni(string originalContent, string mainModName, List<string>? dependantPackages = null)
    {
        var lines = originalContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();
        RemoveSection(lines, "[X2ModCompiler.DependantPackages]");
        EnsurePackagesInEngineSection(lines, mainModName, dependantPackages);
        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Prepares the INI for mod compilation.
    /// This is used for BOTH passes in the two-pass system to ensure environment stability.
    /// </summary>
    public string PrepareModCompilationIni(string originalContent, string mainModName, List<string> dependantPackages)
    {
        var lines = originalContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();
        RemoveSection(lines, "[X2ModCompiler.DependantPackages]");
        EnsurePackagesInEngineSection(lines, mainModName, dependantPackages);
        return string.Join(Environment.NewLine, lines);
    }

    public string PreparePass1Ini(string originalContent, string mainModName, List<string> dependantPackages)
    {
        return PrepareModCompilationIni(originalContent, mainModName, dependantPackages);
    }

    public string PreparePass2Ini(string originalContent, string mainModName, List<string> dependantPackages)
    {
        return PrepareModCompilationIni(originalContent, mainModName, dependantPackages);
    }

    private void RemoveSection(List<string> lines, string sectionHeader)
    {
        int sectionStartIndex = -1;
        int sectionEndIndex = -1;
        for (int i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (string.Equals(trimmed, sectionHeader, StringComparison.OrdinalIgnoreCase))
            {
                sectionStartIndex = i;
                continue;
            }

            if (sectionStartIndex != -1 && trimmed.StartsWith("["))
            {
                sectionEndIndex = i;
                break;
            }
        }
        if (sectionStartIndex != -1)
        {
            if (sectionEndIndex == -1) sectionEndIndex = lines.Count;
            lines.RemoveRange(sectionStartIndex, sectionEndIndex - sectionStartIndex);
        }
    }

    private void EnsurePackagesInEngineSection(List<string> lines, string mainModName, List<string>? dependantPackages = null)
    {
        int engineSectionIndex = FindLastSection(lines, "[UnrealEd.EditorEngine]");
        if (engineSectionIndex == -1)
        {
            lines.Add("");
            lines.Add("[UnrealEd.EditorEngine]");
            engineSectionIndex = lines.Count - 1;
        }

        // 1. Find stable position of main mod
        int mainModIndex = FindPackageInSection(lines, engineSectionIndex, mainModName);
        
        // 2. Remove dependants to re-seat them after main mod
        if (dependantPackages != null)
        {
            RemovePackagesFromSection(lines, engineSectionIndex, dependantPackages);
            // Re-find mainModIndex in case it moved
            mainModIndex = FindPackageInSection(lines, engineSectionIndex, mainModName);
        }

        // 3. Insert/Move main mod if needed
        if (mainModIndex == -1)
        {
            int insertPoint = FindSectionEnd(lines, engineSectionIndex);
            lines.Insert(insertPoint, $"+ModEditPackages={mainModName}");
            mainModIndex = insertPoint;
        }

        // 4. Append dependants after main mod
        if (dependantPackages != null && dependantPackages.Count > 0)
        {
             int insertPoint = mainModIndex + 1;
             foreach(var dep in dependantPackages)
             {
                 lines.Insert(insertPoint, $"+ModEditPackages={dep}");
                 insertPoint++;
             }
        }
    }

    private int FindLastSection(List<string> lines, string sectionHeader)
    {
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.Contains(";")) trimmed = trimmed.Split(';')[0].Trim();
            if (trimmed.Contains("#")) trimmed = trimmed.Split('#')[0].Trim();

            if (string.Equals(trimmed, sectionHeader, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return -1;
    }

    private int FindSectionEnd(List<string> lines, int sectionStartIndex)
    {
        for (int i = sectionStartIndex + 1; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.StartsWith("[")) return i;
        }
        return lines.Count;
    }

    private int FindPackageInSection(List<string> lines, int sectionStartIndex, string packageName)
    {
        for (int i = sectionStartIndex + 1; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.StartsWith("[")) break;

            if (trimmed.Contains($"={packageName}", StringComparison.OrdinalIgnoreCase))
            {
                var index = trimmed.IndexOf($"={packageName}", StringComparison.OrdinalIgnoreCase);
                var entryValue = trimmed.Substring(index + 1).Trim();
                if (entryValue.Contains(";")) entryValue = entryValue.Split(';')[0].Trim();
                if (entryValue.Contains("#")) entryValue = entryValue.Split('#')[0].Trim();

                if (string.Equals(entryValue, packageName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
        }
        return -1;
    }

    private void RemovePackagesFromSection(List<string> lines, int sectionStartIndex, List<string> packagesToRemove)
    {
        for (int i = sectionStartIndex + 1; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.StartsWith("[")) break;

            foreach (var pkg in packagesToRemove)
            {
                if (trimmed.Contains($"={pkg}", StringComparison.OrdinalIgnoreCase))
                {
                    var index = trimmed.IndexOf($"={pkg}", StringComparison.OrdinalIgnoreCase);
                    var entryValue = trimmed.Substring(index + 1).Trim();
                    if (entryValue.Contains(";")) entryValue = entryValue.Split(';')[0].Trim();
                    if (entryValue.Contains("#")) entryValue = entryValue.Split('#')[0].Trim();

                    if (string.Equals(entryValue, pkg, StringComparison.OrdinalIgnoreCase))
                    {
                        lines.RemoveAt(i);
                        i--;
                        break;
                    }
                }
            }
        }
    }

    private void AppendPackagesToLastEngineSection(List<string> lines, List<string> packages)
    {
        int lastEngineSectionIndex = FindLastSection(lines, "[UnrealEd.EditorEngine]");
        if (lastEngineSectionIndex == -1) return;

        int insertAt = FindSectionEnd(lines, lastEngineSectionIndex);

        foreach (var pkg in packages)
        {
            lines.Insert(insertAt, $"+ModEditPackages={pkg}");
            insertAt++;
        }
    }

    public List<string> GetDependantPackages(string content)
    {
        var packages = new List<string>();
        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        bool inSection = false;
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";") || trimmed.StartsWith("#")) continue;
            
            if (trimmed.StartsWith("[X2ModCompiler.DependantPackages]", StringComparison.OrdinalIgnoreCase))
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
                    if (!string.IsNullOrEmpty(package)) packages.Add(package);
                }
            }
        }
        return packages;
    }

    public bool IsTwoPassNeeded(string content) => GetDependantPackages(content).Any();
}
