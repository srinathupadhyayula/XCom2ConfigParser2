using System.Text;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Kokuban;
using X2ModCompiler.Exceptions;

namespace X2ModCompiler.Utilities;

/// <summary>
/// Handles discovery and modification of XComEngine.ini files for two-pass compilation.
/// </summary>
public class IniHandler
{
    private readonly string _projectRoot;
    private readonly IEnumerable<string> _iniRoots;
    private readonly ILogger<IniHandler> _logger;
    private string? _targetFile;
    public string? TargetIniPath => _targetFile;

    /// <summary>
    /// Initializes a new instance of the <see cref="IniHandler"/> class.
    /// </summary>
    /// <param name="projectRoot">The absolute path to the mod project root.</param>
    /// <param name="iniRoots">A collection of directory paths to scan for XComEngine.ini files.</param>
    /// <param name="logger">The logger instance.</param>
    public IniHandler(string projectRoot, IEnumerable<string> iniRoots, ILogger<IniHandler> logger)
    {
        _projectRoot = projectRoot;
        _iniRoots = iniRoots;
        _logger = logger;
    }

    /// <summary>
    /// Searches for the target XComEngine.ini file within the configured INI roots.
    /// Uses a priority-based discovery mechanism:
    /// 1. Files containing [X2ModCompiler.DependantPackages] or [UnrealEd.EditorEngine] are prioritized.
    /// 2. If multiple candidates remain, files not containing "0Base" (mod-specific config) are preferred.
    /// </summary>
    /// <returns>The path to the target INI file, or null if not found.</returns>
    public string? FindTargetIni()
    {
        if (!string.IsNullOrEmpty(_targetFile)) return _targetFile;

        var candidates = new List<string>();
        foreach (var root in _iniRoots)
        {
            if (!Directory.Exists(root))
            {
                _logger.LogDebug(Chalk.Gray[$"  Skipping non-existent root: {root}"]);
                continue;
            }
            var files = Directory.GetFiles(root, "XComEngine.ini", SearchOption.AllDirectories);
            candidates.AddRange(files);
        }

        if (candidates.Count == 0)
        {
            _logger.LogWarning(Chalk.Yellow["[DEBUG] No XComEngine.ini files found in any roots."]);
            return null;
        }

        if (candidates.Count == 1)
        {
            _logger.LogInformation(Chalk.Cyan[$"[DEBUG] Found single candidate: {candidates[0]}"]);
            _targetFile = candidates[0];
            return _targetFile;
        }

        var targetSections = new[] { "[X2ModCompiler.DependantPackages]", "[UnrealEd.EditorEngine]" };
        _logger.LogInformation(Chalk.Cyan[$"[DEBUG] Multiple candidates ({candidates.Count}). Prioritizing based on sections..."]);
        
        var priorityFiles = candidates.Where(f => {
            bool found = FileContainsAnySection(f, targetSections);
            if (found) _logger.LogInformation(Chalk.Green[$"  + Priority match: {f}"]);
            return found;
        }).ToList();
        
        if (priorityFiles.Count == 1)
        {
            _targetFile = priorityFiles[0];
        }
        else if (priorityFiles.Count > 1)
        {
            _logger.LogInformation(Chalk.Cyan["  Multiple priority matches. Choosing first non-0Base file..."]);
            _targetFile = priorityFiles.OrderBy(f => f.Contains("0Base") ? 1 : 0).First();
        }
        else
        {
            _logger.LogInformation(Chalk.Cyan["  No priority matches. Choosing first non-0Base file from total candidates..."]);
            _targetFile = candidates.OrderBy(f => f.Contains("0Base") ? 1 : 0).First();
        }

        _logger.LogInformation(Chalk.Bold.Cyan[$"[DEBUG] Selected: {_targetFile}"]);
        return _targetFile;
    }

    private static bool FileContainsAnySection(string filePath, string[] sections)
    {
        foreach (var line in File.ReadLines(filePath))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            // Strip trailing comments
            if (trimmed.Contains(";")) trimmed = trimmed.Split(';')[0].Trim();
            if (trimmed.Contains("#")) trimmed = trimmed.Split('#')[0].Trim();
            if (trimmed.Contains("'")) trimmed = trimmed.Split('\'')[0].Trim();

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
    /// Prepares the INI content for the staging phase by removing the two-pass trigger section
    /// and ensuring the main mod and its dependencies are correctly seated in the editor engine settings.
    /// </summary>
    /// <param name="originalContent">The raw content of the source INI.</param>
    /// <param name="mainModName">The canonical name of the main mod.</param>
    /// <param name="dependantPackages">An optional list of dependent mod packages.</param>
    /// <returns>The modified INI content string.</returns>
    public string PrepareStagedIni(string originalContent, string mainModName, List<string>? dependantPackages = null)
    {
        _logger.LogInformation(Chalk.Cyan["[INI] Preparing staged configuration..."]);
        var lines = originalContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();
        RemoveSection(lines, "[X2ModCompiler.DependantPackages]");
        EnsurePackagesInEngineSection(lines, mainModName, dependantPackages);
        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Prepares the INI for mod compilation.
    /// This is used for BOTH passes in the two-pass system to ensure environment stability.
    /// Environment stability prevents Unreal from deleting binaries or re-compiling unnecessarily between runs.
    /// </summary>
    /// <param name="originalContent">The current content of the INI.</param>
    /// <param name="mainModName">The canonical name of the main mod.</param>
    /// <param name="dependantPackages">The list of dependent mods to include in ModEditPackages.</param>
    /// <returns>The modified INI content.</returns>
    public string PrepareModCompilationIni(string originalContent, string mainModName, List<string>? dependantPackages)
    {
        _logger.LogInformation(Chalk.Cyan[$"[INI] Injecting mod packages into [UnrealEd.EditorEngine]: {mainModName}{(dependantPackages?.Count > 0 ? (", " + string.Join(", ", dependantPackages)) : "")}"]);
        var lines = originalContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();
        RemoveSection(lines, "[X2ModCompiler.DependantPackages]");
        EnsurePackagesInEngineSection(lines, mainModName, dependantPackages);
        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Alias for <see cref="PrepareModCompilationIni"/> used during Phase 1 of a two-pass build.
    /// </summary>
    public string PreparePass1Ini(string originalContent, string mainModName, List<string> dependantPackages)
    {
        return PrepareModCompilationIni(originalContent, mainModName, dependantPackages);
    }

    /// <summary>
    /// Alias for <see cref="PrepareModCompilationIni"/> used during Phase 2 of a two-pass build.
    /// </summary>
    public string PreparePass2Ini(string originalContent, string mainModName, List<string> dependantPackages)
    {
        return PrepareModCompilationIni(originalContent, mainModName, dependantPackages);
    }

    /// <summary>
    /// Gets the absolute paths to the .u binary files for the specified mod and its dependents in the SDK Script folder.
    /// </summary>
    public List<string> GetModBinaryPaths(string sdkPath, string mainModName, List<string> dependantPackages)
    {
        var paths = new List<string>();
        var scriptDir = Path.Combine(sdkPath, "XComGame", "Script");
        
        paths.Add(Path.Combine(scriptDir, $"{mainModName}.u"));
        foreach (var dep in dependantPackages)
        {
            paths.Add(Path.Combine(scriptDir, $"{dep}.u"));
        }
        
        return paths;
    }

    /// <summary>
    /// Removes a section from the INI file based on its header.
    /// </summary>
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

            if (sectionStartIndex != -1 && (trimmed.StartsWith("[") || i == lines.Count - 1))
            {
                sectionEndIndex = i;
                if (i == lines.Count - 1 && !trimmed.StartsWith("[")) sectionEndIndex = lines.Count;
                break;
            }
        }
        if (sectionStartIndex != -1)
        {
            if (sectionEndIndex == -1) sectionEndIndex = lines.Count;
            lines.RemoveRange(sectionStartIndex, sectionEndIndex - sectionStartIndex);
        }
    }

    /// <summary>
    /// Ensures that the specified main mod and its dependencies are correctly ordered within the [UnrealEd.EditorEngine] section.
    /// This method enforces a specific "Main Mod Last" or "Dependencies after Main Mod" order depending on pass context.
    /// </summary>
    /// <param name="lines">The list of INI lines to modify.</param>
    /// <param name="mainModName">The canonical name of the main mod.</param>
    /// <param name="dependantPackages">The optional list of dependent mod packages.</param>
    private void EnsurePackagesInEngineSection(List<string> lines, string mainModName, List<string>? dependantPackages = null)
    {
        int engineSectionIndex = FindLastSection(lines, "[UnrealEd.EditorEngine]");
        if (engineSectionIndex == -1)
        {
            _logger.LogInformation(Chalk.Gray["  [INI] [UnrealEd.EditorEngine] section not found. Creating it at the end of the file."]);
            lines.Add("");
            lines.Add("[UnrealEd.EditorEngine]");
            engineSectionIndex = lines.Count - 1;
        }

        _logger.LogInformation(Chalk.Cyan[$"  [INI] Found [UnrealEd.EditorEngine] at line {engineSectionIndex + 1}"]);
        _logger.LogInformation(Chalk.Cyan[$"  [INI] Current ModEditPackages in INI:"]);
        for (int i = engineSectionIndex + 1; i < lines.Count && !lines[i].Trim().StartsWith("["); i++)
        {
            if (lines[i].Contains("ModEditPackages"))
                _logger.LogInformation(Chalk.Cyan[$"    {lines[i].Trim()}"]);
        }

        // 1. ALWAYS remove existing entries for main mod and dependants to ensure they are moved to the END
        int mainModIndex = FindPackageInSection(lines, engineSectionIndex, mainModName);
        if (mainModIndex != -1)
        {
            _logger.LogInformation(Chalk.Gray[$"  [INI] Removing existing entry for {mainModName} to move it to the end."]);
            lines.RemoveAt(mainModIndex);
            // Re-find the section index as it might have moved
            engineSectionIndex = FindLastSection(lines, "[UnrealEd.EditorEngine]");
        }

        if (dependantPackages != null && dependantPackages.Count > 0)
        {
            _logger.LogInformation(Chalk.Gray[$"  [INI] Removing existing entries for {dependantPackages.Count} dependents to move them after main mod."]);
            RemovePackagesFromSection(lines, engineSectionIndex, dependantPackages);
            // Re-find the section index as it might have moved
            engineSectionIndex = FindLastSection(lines, "[UnrealEd.EditorEngine]");
        }

        // 2. Insert main mod at the end of the section
        int insertPoint = FindSectionEnd(lines, engineSectionIndex);
        _logger.LogInformation(Chalk.Green[$"  [INI] Injecting {mainModName} at the end of [UnrealEd.EditorEngine] (Line {insertPoint + 1})"]);
        lines.Insert(insertPoint, $"+ModEditPackages={mainModName}");
        mainModIndex = insertPoint;

        // 3. Append dependants after main mod
        if (dependantPackages != null && dependantPackages.Count > 0)
        {
             insertPoint = mainModIndex + 1;
             foreach(var dep in dependantPackages)
             {
                 _logger.LogInformation(Chalk.Green[$"  [INI] Injecting dependent {dep} after main mod (Line {insertPoint + 1})"]);
                 lines.Insert(insertPoint, $"+ModEditPackages={dep}");
                 insertPoint++;
             }
        }
        
        _logger.LogInformation(Chalk.Cyan[$"  [INI] Final ModEditPackages in INI:"]);
        for (int i = engineSectionIndex + 1; i < lines.Count && !lines[i].Trim().StartsWith("["); i++)
        {
            if (lines[i].Contains("ModEditPackages"))
                _logger.LogInformation(Chalk.Cyan[$"    {lines[i].Trim()}"]);
        }
    }

    /// <summary>
    /// Finds the line index of the last occurrence of the specified section header.
    /// </summary>
    private int FindLastSection(List<string> lines, string sectionHeader)
    {
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.Contains(";")) trimmed = trimmed.Split(';')[0].Trim();
            if (trimmed.Contains("#")) trimmed = trimmed.Split('#')[0].Trim();
            if (trimmed.Contains("'")) trimmed = trimmed.Split('\'')[0].Trim();

            if (string.Equals(trimmed, sectionHeader, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Finds the index of the line where the next section starts, or the end of the file.
    /// </summary>
    private int FindSectionEnd(List<string> lines, int sectionStartIndex)
    {
        for (int i = sectionStartIndex + 1; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.StartsWith("[")) return i;
        }
        return lines.Count;
    }

    /// <summary>
    /// Searches for a '+ModEditPackages' or 'ModEditPackages' entry within a section.
    /// </summary>
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

    /// <summary>
    /// Removes multiple packages from a section if they exist.
    /// </summary>
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

    /// <summary>
    /// Appends a list of packages to the end of the last [UnrealEd.EditorEngine] section.
    /// </summary>
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

    /// <summary>
    /// Parses the [X2ModCompiler.DependantPackages] section to identify mods that require two-pass linkage.
    /// </summary>
    /// <param name="content">The INI content to parse.</param>
    /// <returns>A list of dependent mod package names.</returns>
    public List<string> GetDependantPackages(string content)
    {
        var packages = new List<string>();
        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        bool inSection = false;
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            // Handle comments: semicolon, hash, and apostrophe (UnrealScript/INI style)
            if (trimmed.StartsWith(";") || trimmed.StartsWith("#") || trimmed.StartsWith("'")) continue;

            // Strip trailing comments for header/key detection
            var linePart = trimmed;
            if (linePart.Contains(";")) linePart = linePart.Split(';')[0].Trim();
            if (linePart.Contains("#")) linePart = linePart.Split('#')[0].Trim();
            if (linePart.Contains("'")) linePart = linePart.Split('\'')[0].Trim();
            
            if (linePart.StartsWith("[X2ModCompiler.DependantPackages]", StringComparison.OrdinalIgnoreCase))
            {
                inSection = true;
                continue;
            }
            if (inSection)
            {
                if (linePart.StartsWith("[")) break;
                if (linePart.StartsWith("+", StringComparison.OrdinalIgnoreCase) && linePart.Contains("="))
                {
                    var parts = linePart.Split('=');
                    if (parts.Length > 1)
                    {
                        var package = parts[1].Trim();
                        // Strip trailing comments (already done via linePart above, but for safety)
                        if (package.Contains(";")) package = package.Split(';')[0].Trim();
                        if (package.Contains("#")) package = package.Split('#')[0].Trim();
                        if (package.Contains("'")) package = package.Split('\'')[0].Trim();
                        
                        if (!string.IsNullOrEmpty(package)) packages.Add(package);
                    }
                }
            }
        }
        return packages;
    }

    /// <summary>
    /// Determines if a two-pass compilation flow is required based on the presence of dependent packages.
    /// </summary>
    /// <param name="content">The INI content to analyze.</param>
    /// <returns>True if a two-pass build is necessary; otherwise false.</returns>
    public bool IsTwoPassNeeded(string content) => GetDependantPackages(content).Any();
}
