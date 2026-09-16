using System.ComponentModel;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using X2ModCompiler.Application;
using X2ModCompiler.Compilation;
using X2ModCompiler.Configuration;
using X2ModCompiler.Core.Validation;
using X2ModCompiler.Cooking;
using X2ModCompiler.DependencyInjection;
using X2ModCompiler.Tracking;
using X2ModCompiler.Utilities;
using ZLogger;
using System.Text.Json;

namespace X2ModCompiler;

/// <summary>
/// The main entry point for the X2ModCompiler CLI application.
/// This class configures the command-line interface using Spectre.Console.Cli,
/// registering the build and clean commands.
/// </summary>
public class Program
{
    /// <summary>
    /// The main entry point of the application.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The application exit code (0 for success, non-zero for failure).</returns>
    public static async Task<int> Main(string[] args)
    {
        var app = new CommandApp();
        app.Configure(config =>
        {
            config.SetApplicationName(BuildConstants.ApplicationName);
            config.AddCommand<BuildCommand>(BuildConstants.BuildCommandName)
                  .WithDescription("Builds the specified mod.");
            config.AddCommand<ValidateCommand>(BuildConstants.ValidateCommandName)
                  .WithDescription("Validates configuration files for the specified mod.");
            config.AddCommand<CleanCommand>(BuildConstants.CleanCommandName)
                  .WithDescription("Cleans the build artifacts for the specified mod.");
        });

        return await app.RunAsync(args);
    }
}

/// <summary>
/// Defines the command-line settings and options for the build and clean commands.
/// These settings are mapped from CLI arguments using Spectre.Console.Cli.
/// </summary>
public class BuildSettings : CommandSettings
{
    /// <summary>
    /// Gets the name of the mod project to build.
    /// </summary>
    [CommandOption("--mod-name <MODNAME>")]
    [Description("The name of the mod.")]
    public string ModName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the absolute path to the root directory containing the mod's source code and configuration.
    /// </summary>
    [CommandOption("--src-directory <SRCDIRECTORY>")]
    [Description("The path that contains your mod's source.")]
    public string SrcDirectory { get; init; } = string.Empty;

    /// <summary>
    /// Gets the absolute path to the XCOM 2 SDK installation.
    /// </summary>
    [CommandOption("--sdk-path <SDKPATH>")]
    [Description("The path to your XCOM 2 SDK installation.")]
    public string SdkPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the absolute path to the XCOM 2 game installation.
    /// </summary>
    [CommandOption("--game-path <GAMEPATH>")]
    [Description("The path to your XCOM 2 Web installation.")]
    public string GamePath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the destination directory where the built mod artifacts should be deployed.
    /// </summary>
    [CommandOption("--mod-destination <MODDESTINATION>")]
    [Description("The destination directory for the built mod.")]
    public string ModDestinationPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the build configuration name (e.g., "default", "debug", "final_release").
    /// </summary>
    [CommandOption("--config <CONFIG>")]
    [Description("Build configuration (default or debug).")]
    [DefaultValue("default")]
    public string? Config { get; init; }

    /// <summary>
    /// Gets an array of additional source paths to include in the compilation process.
    /// </summary>
    [CommandOption("--include-src <PATH>")]
    [Description("Additional source paths to include. Can be specified multiple times.")]
    public string[] IncludePaths { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets a value indicating whether configuration validation should be performed before building.
    /// </summary>
    [CommandOption("--validate")]
    [Description("Validate configuration files before building.")]
    public bool PerformConfigValidation { get; init; }

    /// <summary>
    /// Gets a value indicating whether to only compile scripts (skipping cooking and deployment).
    /// </summary>
    [CommandOption("--compile-only")]
    [Description("Only compile scripts, skipping cooking and final deployment mirroring.")]
    public bool CompileOnly { get; init; }

    /// <summary>
    /// Gets a value indicating whether to use the two-pass compilation flow for linkage recovery.
    /// </summary>
    [CommandOption("--two-pass")]
    [Description("Use the two-pass compilation flow for linkage recovery.")]
    public bool TwoPassCompilation { get; init; }

    /// <summary>
    /// Gets an array of internal package names that this mod depends on (used for two-pass compilation).
    /// </summary>
    [CommandOption("--dependent-package <NAME>")]
    [Description("Internal package names that this mod depends on. Can be specified multiple times.")]
    public string[] DependentPackages { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Executes the build pipeline for a specified mod project.
/// This command initializes the build controller and orchestrates mirroring, compilation, cooking, and deployment.
/// </summary>
public class BuildCommand : AsyncCommand<BuildSettings>
{
    /// <summary>
    /// Executes the build process asynchronously.
    /// </summary>
    /// <param name="context">The command execution context.</param>
    /// <param name="settings">The build settings derived from CLI arguments.</param>
    /// <returns>The command exit code.</returns>
    public override async Task<int> ExecuteAsync(CommandContext context, BuildSettings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine($"[bold blue]{BuildConstants.ApplicationName} v{BuildConstants.ApplicationVersion} (with Config Validation)[/]");
        AnsiConsole.MarkupLine("[grey]XCOM 2 Mod Build System[/]\n");

        if (string.IsNullOrWhiteSpace(settings.SdkPath) || string.IsNullOrWhiteSpace(settings.GamePath))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] --sdk-path and --game-path are required parameters.");
            return 1;
        }

        var options = new BuildOptions
        {
            ModName = settings.ModName,
            ProjectRoot = settings.SrcDirectory,
            SdkPath = settings.SdkPath,
            GamePath = settings.GamePath,
            ModDestinationPath = settings.ModDestinationPath,
            Debug = settings.Config?.Equals("debug", StringComparison.OrdinalIgnoreCase) == true,
            ValidateConfig = settings.PerformConfigValidation,
            CompileOnly = settings.CompileOnly,
            TwoPassCompilation = settings.TwoPassCompilation
        };

        if (settings.DependentPackages != null)
        {
            foreach (var pkg in settings.DependentPackages)
            {
                options.DependentPackages.Add(pkg);
            }
        }

        if (settings.IncludePaths != null)
        {
            foreach (var path in settings.IncludePaths)
            {
                options.IncludePaths.Add(path);
            }
        }

        // Extract settings from .vscode/settings.json
        var settingsPath = Path.Combine(options.ProjectRoot, ".vscode", "settings.json");
        if (File.Exists(settingsPath))
        {
            try
            {
                var settingsJson = File.ReadAllText(settingsPath);
                
                // Extract IniRoots
                var iniRoots = SettingsJsonParser.ExtractIniRoots(settingsJson);
                foreach (var root in iniRoots)
                {
                    var absoluteRoot = Path.IsPathRooted(root) ? root : Path.Combine(options.ProjectRoot, root);
                    if (Directory.Exists(absoluteRoot))
                    {
                        options.IniRoots.Add(absoluteRoot);
                    }
                }

                // Extract and apply log verbosity from VSCode settings
                options.LogVerbosity = SettingsJsonParser.ExtractLogVerbosity(settingsJson);

                // Extract AllModsRoot
                var allModsRoot = SettingsJsonParser.ExtractAllModsRoot(settingsJson);
                if (!string.IsNullOrEmpty(allModsRoot))
                {
                    var absoluteAllModsRoot = Path.IsPathRooted(allModsRoot) ? allModsRoot : Path.Combine(options.ProjectRoot, allModsRoot);
                    if (Directory.Exists(absoluteAllModsRoot))
                    {
                        options.ParserSettings.AllModsRoot = absoluteAllModsRoot;
                    }
                }

                // Extract Highlander paths
                var communityHighlanderPath = SettingsJsonParser.ExtractCommunityHighlanderPath(settingsJson);
                if (!string.IsNullOrEmpty(communityHighlanderPath))
                {
                    var absolutePath = Path.IsPathRooted(communityHighlanderPath) ? communityHighlanderPath : Path.Combine(options.ProjectRoot, communityHighlanderPath);
                    if (Directory.Exists(absolutePath))
                    {
                        options.ParserSettings.CommunityHighlanderPath = absolutePath;
                    }
                }

                var alienHighlanderPath = SettingsJsonParser.ExtractAlienHighlanderPath(settingsJson);
                if (!string.IsNullOrEmpty(alienHighlanderPath))
                {
                    var absolutePath = Path.IsPathRooted(alienHighlanderPath) ? alienHighlanderPath : Path.Combine(options.ProjectRoot, alienHighlanderPath);
                    if (Directory.Exists(absolutePath))
                    {
                        options.ParserSettings.AlienHighlanderPath = absolutePath;
                    }
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[yellow]Warning:[/] Failed to parse .vscode/settings.json: {ex.Message}");
            }
        }

        // Use DI container for dependency injection
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddX2ModCompiler(options);
        var serviceProvider = services.BuildServiceProvider();

        var controller = serviceProvider.GetRequiredService<BuildController>();

        var result = await controller.InvokeBuildAsync();

        if (result.Success)
        {
            AnsiConsole.MarkupLine($"[green]Build completed successfully in {result.Duration.TotalSeconds:F2} seconds.[/]");
            return 0;
        }
        else
        {
            AnsiConsole.MarkupLine("[red]Build failed.[/]");
            foreach (var error in result.Errors)
            {
                AnsiConsole.MarkupLine($"[red]- {error}[/]");
            }
            return 1;
        }
    }
}

/// <summary>
/// Executes a standalone configuration validation.
/// </summary>
public class ValidateCommand : AsyncCommand<BuildSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, BuildSettings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine($"[bold blue]{BuildConstants.ApplicationName} v{BuildConstants.ApplicationVersion} - Config Validation[/]");

        if (string.IsNullOrWhiteSpace(settings.SdkPath))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] --sdk-path is required for validation.");
            return 1;
        }

        var options = new BuildOptions
        {
            ModName = settings.ModName,
            ProjectRoot = settings.SrcDirectory,
            SdkPath = settings.SdkPath,
            GamePath = settings.GamePath,
            ModDestinationPath = settings.ModDestinationPath
        };

        if (settings.IncludePaths != null)
        {
            foreach (var path in settings.IncludePaths)
            {
                options.IncludePaths.Add(path);
            }
        }

        // Extract IniRoots from .vscode/settings.json
        var vscSettingsPath = Path.Combine(options.ProjectRoot, ".vscode", "settings.json");
        if (File.Exists(vscSettingsPath))
        {
            try
            {
                var settingsJson = File.ReadAllText(vscSettingsPath);
                var iniRoots = SettingsJsonParser.ExtractIniRoots(settingsJson);
                foreach (var root in iniRoots)
                {
                    var absoluteRoot = Path.IsPathRooted(root) ? root : Path.Combine(options.ProjectRoot, root);
                    if (Directory.Exists(absoluteRoot))
                    {
                        options.IniRoots.Add(absoluteRoot);
                    }
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[yellow]Warning:[/] Failed to parse .vscode/settings.json: {ex.Message}");
            }
        }

        // Use DI container for dependency injection
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddX2ModCompiler(options);
        var serviceProvider = services.BuildServiceProvider();

        var controller = serviceProvider.GetRequiredService<BuildController>();

        var success = await controller.InvokeValidationAsync();

        return success ? 0 : 1;
    }
}

/// <summary>
/// Cleans the build artifacts and cache for a specified mod project.
/// This command removes temporary staging areas, compiled script binaries, and tracking fingerprints.
/// </summary>
public class CleanCommand : AsyncCommand<BuildSettings>
{
    /// <summary>
    /// Executes the clean process asynchronously.
    /// </summary>
    /// <param name="context">The command execution context.</param>
    /// <param name="settings">The build settings derived from CLI arguments.</param>
    /// <returns>The command exit code.</returns>
    public override async Task<int> ExecuteAsync(CommandContext context, BuildSettings settings, CancellationToken cancellationToken)
    {
        var options = new BuildOptions
        {
            ModName = settings.ModName,
            ProjectRoot = settings.SrcDirectory,
            SdkPath = settings.SdkPath,
            GamePath = settings.GamePath,
            ModDestinationPath = settings.ModDestinationPath
        };

        // Use DI container for dependency injection
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddX2ModCompiler(options);
        var serviceProvider = services.BuildServiceProvider();

        var controller = serviceProvider.GetRequiredService<BuildController>();

        var success = await controller.InvokeCleanAsync(cancellationToken);
        if (success)
        {
            AnsiConsole.MarkupLine("[green]Clean completed successfully.[/]");
            return 0;
        }

        AnsiConsole.MarkupLine("[red]Clean failed.[/]");
        return 1;
    }
}
