using Spectre.Console;
using Spectre.Console.Cli;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using XCom2ConfigParser2.CLI;
using XCom2ConfigParser2.Configuration;
using XCom2ConfigParser2.Validation;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace XCom2ConfigParser2;

public sealed class Program
{
    public static async Task<int> Main(string[] args)
    {
        var app = new CommandApp<ParseCommand>();
        app.Configure(config =>
        {
            config.SetApplicationName("XCom2ConfigParser2");
            config.AddExample(new[] { "--settings", "\"D:\\MyMod\\.vscode\\settings.json\"" });
            config.AddExample(new[] { "\"D:\\MyMod\\Config\\\"" });
        });
        
        try
        {
            return await app.RunAsync(args);
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
            return 1;
        }
    }
}

public sealed class ParseCommandSettings : CommandSettings
{
    [CommandArgument(0, "[path]")]
    [Description("File or directory to process (overrides iniRoots from settings.json)")]
    public string? Path { get; init; }

    [CommandOption("-j|--json")]
    [Description("Output in JSON format")]
    public bool Json { get; init; }

    [CommandOption("-q|--quiet")]
    [Description("Suppress non-error output")]
    public bool Quiet { get; init; }

    [CommandOption("-s|--summary")]
    [Description("Show only summary")]
    public bool ShowSummary { get; init; }

    [CommandOption("-r|--recursive")]
    [Description("Process directories recursively")]
    [DefaultValue(true)]
    public bool Recursive { get; init; }

    [CommandOption("--no-recursive")]
    [Description("Disable recursive processing")]
    public bool NoRecursive { get; init; }

    [CommandOption("-p|--pattern")]
    [Description("Glob pattern for files (use **/*.ini for recursive search)")]
    public string? Pattern { get; init; }

    [CommandOption("-P|--project-root")]
    [Description("Path to project root directory containing .vscode/settings.json (auto-detected from current directory if not specified)")]
    public string? ProjectRoot { get; init; }

    [CommandOption("-S|--settings")]
    [Description("Path to .vscode/settings.json file (overrides --project-root)")]
    public string? SettingsPath { get; init; }

    [CommandOption("--no-struct-validation")]
    [Description("Disable struct member validation")]
    public bool NoStructValidation { get; init; }

    [CommandOption("--cache-dir")]
    [Description("Path to struct cache directory (overrides settings.json)")]
    public string? CacheDir { get; init; }

    [CommandOption("--force-reindex")]
    [Description("Force re-indexing of all structs (ignore cache)")]
    public bool ForceReindex { get; init; }

    [CommandOption("--log")]
    [Description("Generate categorized log file in cache directory")]
    public bool Log { get; init; }
}

public sealed class ParseCommand : AsyncCommand<ParseCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ParseCommandSettings cmdSettings, CancellationToken cancellationToken)
    {
        var console = AnsiConsole.Console;

        bool recursive = cmdSettings.Recursive && !cmdSettings.NoRecursive;
        string pattern = string.IsNullOrEmpty(cmdSettings.Pattern) 
            ? (recursive ? "**/*.ini" : "*.ini") 
            : cmdSettings.Pattern;

        // Determine project root and settings location
        string settingsFile;
        string effectiveProjectRoot;
        
        if (!string.IsNullOrEmpty(cmdSettings.SettingsPath))
        {
            if (!File.Exists(cmdSettings.SettingsPath))
            {
                console.MarkupLine($"[red]Error: Settings file not found: {cmdSettings.SettingsPath}[/]");
                return 4;
            }
            settingsFile = Path.GetFullPath(cmdSettings.SettingsPath);
            effectiveProjectRoot = Path.GetDirectoryName(Path.GetDirectoryName(settingsFile)) ?? Directory.GetCurrentDirectory();
        }
        else if (!string.IsNullOrEmpty(cmdSettings.ProjectRoot))
        {
            effectiveProjectRoot = cmdSettings.ProjectRoot;
            settingsFile = Path.Combine(effectiveProjectRoot, ".vscode", "settings.json");
        }
        else
        {
            effectiveProjectRoot = Directory.GetCurrentDirectory();
            settingsFile = Path.Combine(effectiveProjectRoot, ".vscode", "settings.json");
        }

        // Load settings from .vscode/settings.json
        var settingsLoader = new SettingsLoader(effectiveProjectRoot);
        var settings = settingsLoader.Load();

        bool hasSettingsFile = File.Exists(settingsFile);
        
        if (!hasSettingsFile && string.IsNullOrEmpty(cmdSettings.Path))
        {
            console.MarkupLine("[red]Error: No .vscode/settings.json found and no input path specified.[/]");
            console.WriteLine();
            console.WriteLine("Please either:");
            console.WriteLine("  1. Create a .vscode/settings.json file with configuration, OR");
            console.WriteLine("  2. Provide a path argument to validate specific files, OR");
            console.WriteLine("  3. Use --settings <path> to specify settings.json location, OR");
            console.WriteLine("  4. Use --project-root <path> to specify project root directory");
            return 4;
        }

        if (hasSettingsFile && settings.IniRoots.Count == 0)
        {
            if (settings.HasJsonParseError)
            {
                console.MarkupLine("[red]Error: .vscode/settings.json found but has JSON syntax errors.[/]");
                console.MarkupLine($"JSON Error: [yellow]{settings.JsonParseErrorMessage}[/]");
            }
            else
            {
                console.MarkupLine("[yellow]Warning: .vscode/settings.json found but 'xcom.configParser.iniRoots' is not configured.[/]");
                console.MarkupLine("No .ini files will be validated. Please add iniRoots to your settings.json.");
            }
            return settings.HasJsonParseError ? 4 : 0;
        }

        if (!string.IsNullOrEmpty(cmdSettings.CacheDir))
        {
            settings.CachePath = cmdSettings.CacheDir;
        }

        var modSrcCache = new XCom2ConfigParser2.StructValidation.ModSrcPathCache(settings);
        var structCache = new XCom2ConfigParser2.StructValidation.StructCache(settings.CachePath);
        
        if (cmdSettings.ForceReindex)
        {
            structCache.Clear();
            if (!cmdSettings.Quiet)
                console.MarkupLine($"[dim]Cleared struct cache at: {settings.CachePath}[/]");
        }

        if (!cmdSettings.NoStructValidation)
        {
            XCom2ConfigParser2.StructValidation.IndexingResult? indexResult = null;

            if (cmdSettings.Quiet || cmdSettings.Json)
            {
                var indexer = new XCom2ConfigParser2.StructValidation.StructIndexer(settings, structCache, modSrcCache);
                indexResult = indexer.IndexAll(new Progress<XCom2ConfigParser2.StructValidation.IndexingProgress>());
            }
            else
            {
                await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .StartAsync("Indexing struct definitions...", async ctx => 
                    {
                        var indexer = new XCom2ConfigParser2.StructValidation.StructIndexer(settings, structCache, modSrcCache);
                        var indexProgress = new Progress<XCom2ConfigParser2.StructValidation.IndexingProgress>(p =>
                        {
                            ctx.Status($"Indexing structs... [green]{p.FilesProcessed}[/] files scanned: [blue]{System.IO.Path.GetFileName(p.CurrentFile)}[/]");
                        });
                        
                        indexResult = await Task.Run(() => indexer.IndexAll(indexProgress));
                    });
                
                console.MarkupLine($"[green]Indexing complete:[/] {indexResult!.StructsFound} structs found in {indexResult.FilesScanned} files.");
                if (indexResult.Errors.Count > 0)
                {
                    foreach (var err in indexResult.Errors)
                        console.MarkupLine($"[yellow]Warning during indexing:[/] {err}");
                }
            }
        }

        var processor = new FileProcessor(
            new SimpleSyntaxValidator(),
            settings,
            !cmdSettings.NoStructValidation,
            modSrcCache);

        var files = new List<string>();
        
        if (!string.IsNullOrEmpty(cmdSettings.Path))
        {
            if (File.Exists(cmdSettings.Path))
            {
                files.Add(cmdSettings.Path);
            }
            else if (Directory.Exists(cmdSettings.Path))
            {
                files.AddRange(FindFiles(cmdSettings.Path, recursive, pattern));
            }
            else
            {
                console.MarkupLine($"[red]Error: Path '{cmdSettings.Path}' does not exist.[/]");
                return 2;
            }
        }
        else
        {
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var iniRoot in settings.IniRoots)
            {
                if (Directory.Exists(iniRoot))
                {
                    var rootFiles = FindFiles(iniRoot, recursive, pattern);
                    foreach (var file in rootFiles)
                    {
                        string fullPath = Path.GetFullPath(file);
                        if (seenPaths.Add(fullPath))
                        {
                            files.Add(file);
                        }
                    }
                }
                else
                {
                    console.MarkupLine($"[yellow]Warning: iniRoot '{iniRoot}' does not exist.[/]");
                }
            }
        }

        if (files.Count == 0)
        {
            console.MarkupLine("[yellow]No .ini files found to validate.[/]");
            return 0;
        }

        ErrorLog? errorLog = cmdSettings.Log ? new ErrorLog 
        { 
            ProjectRoot = effectiveProjectRoot,
            Summary = new ValidationResultSummary()
        } : null;

        var results = new List<(string Path, IReadOnlyList<Core.Diagnostic> Diagnostics)>();
        var summary = new ValidationResultSummary();
        var outputMode = cmdSettings.Json ? OutputMode.Json : cmdSettings.ShowSummary ? OutputMode.Summary : cmdSettings.Quiet ? OutputMode.Quiet : OutputMode.Default;

        foreach (var file in files)
        {
            var result = processor.ProcessFile(file);
            results.Add((file, result.Diagnostics));

            summary.FilesProcessed++;
            if (result.HasErrors)
            {
                summary.FilesWithErrors++;
                summary.TotalErrors += result.ErrorCount;
            }
            summary.TotalWarnings += result.WarningCount;

            if (errorLog != null)
            {
                errorLog.Summary.FilesProcessed++;
                if (result.HasErrors)
                {
                    errorLog.Summary.FilesWithErrors++;
                }
                foreach (var diagnostic in result.Diagnostics)
                {
                    errorLog.Add(diagnostic, file);
                }
            }

            if (outputMode == OutputMode.Default || outputMode == OutputMode.Quiet)
            {
                OutputFormatter.Format(file, result.Diagnostics, outputMode, console);
            }
        }

        if (errorLog != null && !string.IsNullOrEmpty(settings.CachePath))
        {
            try
            {
                Directory.CreateDirectory(settings.CachePath);
                
                string logPath = Path.Combine(settings.CachePath, "validation-report.txt");
                ErrorLogWriter.Write(errorLog, logPath);
                if (outputMode != OutputMode.Json && outputMode != OutputMode.Quiet)
                {
                    console.WriteLine();
                    console.MarkupLine($"📋 Validation report written to: [blue]{logPath}[/]");
                }
                
                string jsonLogPath = Path.Combine(settings.CachePath, "validation-report.json");
                ErrorLogWriter.WriteJson(errorLog, jsonLogPath);
                if (outputMode != OutputMode.Json && outputMode != OutputMode.Quiet)
                {
                    console.MarkupLine($"📄 JSON report written to: [blue]{jsonLogPath}[/]");
                }
            }
            catch (Exception ex)
            {
                console.MarkupLine($"[yellow]Warning: Failed to write log file:[/] {ex.Message}");
            }
        }

        if (outputMode == OutputMode.Json)
        {
            var jsonOutput = OutputFormatter.FormatJson(results, summary);
            Console.WriteLine(jsonOutput); // Use standard console to avoid markup parsing
        }
        else if (outputMode == OutputMode.Summary)
        {
            OutputFormatter.FormatSummary(summary, console);
        }

        return summary.TotalErrors > 0 ? 1 : 0;
    }

    private static List<string> FindFiles(string path, bool recursive, string pattern)
    {
        var files = new List<string>();

        if (File.Exists(path))
        {
            files.Add(Path.GetFullPath(path));
            return files;
        }

        if (!Directory.Exists(path))
        {
            return files;
        }

        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        matcher.AddInclude(pattern);

        var directoryInfo = new DirectoryInfoWrapper(new DirectoryInfo(path));
        var globResult = matcher.Execute(directoryInfo);

        if (globResult.HasMatches)
        {
            foreach (var file in globResult.Files)
            {
                files.Add(Path.GetFullPath(Path.Combine(path, file.Path)));
            }
        }

        return files;
    }
}
