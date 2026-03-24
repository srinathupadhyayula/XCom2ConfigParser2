using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using XCom2ModCompiler.Application;
using XCom2ModCompiler.Compilation;
using XCom2ModCompiler.Configuration;
using XCom2ModCompiler.Cooking;
using XCom2ModCompiler.Tracking;
using XCom2ModCompiler.Utilities;
using ZLogger;

namespace XCom2ModCompiler;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var app = new CommandApp();
        app.Configure(config =>
        {
            config.SetApplicationName("XCom2ModCompiler");
            config.AddCommand<BuildCommand>("build")
                  .WithDescription("Builds the specified mod.");
            config.AddCommand<CleanCommand>("clean")
                  .WithDescription("Cleans the build artifacts for the specified mod.");
        });

        return await app.RunAsync(args);
    }
}

public class BuildSettings : CommandSettings
{
    [CommandOption("--mod-name <MODNAME>")]
    [Description("The name of the mod.")]
    public string ModName { get; init; } = string.Empty;

    [CommandOption("--src-directory <SRCDIRECTORY>")]
    [Description("The path that contains your mod's source.")]
    public string SrcDirectory { get; init; } = string.Empty;

    [CommandOption("--sdk-path <SDKPATH>")]
    [Description("The path to your XCOM 2 SDK installation.")]
    public string SdkPath { get; init; } = string.Empty;

    [CommandOption("--game-path <GAMEPATH>")]
    [Description("The path to your XCOM 2 Web installation.")]
    public string GamePath { get; init; } = string.Empty;

    [CommandOption("--mod-destination <MODDESTINATION>")]
    [Description("The destination directory for the built mod.")]
    public string ModDestinationPath { get; init; } = string.Empty;

    [CommandOption("--config <CONFIG>")]
    [Description("Build configuration (default or debug).")]
    [DefaultValue("default")]
    public string? Config { get; init; }

    [CommandOption("--include-src <PATH>")]
    [Description("Additional source paths to include. Can be specified multiple times.")]
    public string[] IncludePaths { get; init; } = Array.Empty<string>();
}

public class BuildCommand : AsyncCommand<BuildSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, BuildSettings settings)
    {
        AnsiConsole.MarkupLine("[bold blue]XCom2ModCompiler v1.0.0[/]");
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
            Debug = settings.Config?.Equals("debug", StringComparison.OrdinalIgnoreCase) == true
        };

        if (settings.IncludePaths != null)
        {
            foreach (var path in settings.IncludePaths)
            {
                options.IncludePaths.Add(path);
            }
        }

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddZLoggerConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var logger = loggerFactory.CreateLogger<BuildController>();
        var runner = new ProcessRunner();
        var mirror = new RobocopyFileMirror(runner);

        var compiler = new ScriptCompiler(options.CommandletPath, options.SdkPath, options.GamePath, runner, loggerFactory.CreateLogger<ScriptCompiler>());
        var tracker = new BuildTracker(options.BuildCachePath, loggerFactory.CreateLogger<BuildTracker>());
        var cooker = new AssetCooker(options.SdkPath, options.GamePath, options.BuildCachePath, runner, mirror, tracker, loggerFactory, loggerFactory.CreateLogger<AssetCooker>());
        var shaderPrecompiler = new ShaderPrecompiler(runner, mirror, loggerFactory.CreateLogger<ShaderPrecompiler>());
        var missingUncookedCopier = new MissingUncookedCopier(mirror, loggerFactory.CreateLogger<MissingUncookedCopier>());
        var projectSynchronizer = new ProjectSynchronizer(loggerFactory.CreateLogger<ProjectSynchronizer>());

        var controller = new BuildController(options, logger, tracker, compiler, cooker, mirror, runner, shaderPrecompiler, missingUncookedCopier, projectSynchronizer);

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

public class CleanCommand : AsyncCommand<BuildSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, BuildSettings settings)
    {
        var options = new BuildOptions
        {
            ModName = settings.ModName,
            ProjectRoot = settings.SrcDirectory,
            SdkPath = settings.SdkPath,
            GamePath = settings.GamePath,
            ModDestinationPath = settings.ModDestinationPath
        };

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddZLoggerConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var runner = new ProcessRunner();
        var mirror = new RobocopyFileMirror(runner);
        var compiler = new ScriptCompiler(options.CommandletPath, options.SdkPath, options.GamePath, runner, loggerFactory.CreateLogger<ScriptCompiler>());
        var tracker = new BuildTracker(options.BuildCachePath, loggerFactory.CreateLogger<BuildTracker>());
        var cooker = new AssetCooker(options.SdkPath, options.GamePath, options.BuildCachePath, runner, mirror, tracker, loggerFactory, loggerFactory.CreateLogger<AssetCooker>());
        var shaderPrecompiler = new ShaderPrecompiler(runner, mirror, loggerFactory.CreateLogger<ShaderPrecompiler>());
        var missingUncookedCopier = new MissingUncookedCopier(mirror, loggerFactory.CreateLogger<MissingUncookedCopier>());
        var projectSynchronizer = new ProjectSynchronizer(loggerFactory.CreateLogger<ProjectSynchronizer>());

        var controller = new BuildController(options, loggerFactory.CreateLogger<BuildController>(), tracker, compiler, cooker, mirror, runner, shaderPrecompiler, missingUncookedCopier, projectSynchronizer);

        var result = await controller.InvokeCleanAsync();
        if (result.Success)
        {
            AnsiConsole.MarkupLine("[green]Clean completed successfully.[/]");
            return 0;
        }

        AnsiConsole.MarkupLine("[red]Clean failed.[/]");
        return 1;
    }
}
