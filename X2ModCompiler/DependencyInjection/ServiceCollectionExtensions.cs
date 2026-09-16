using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO;
using X2ModCompiler.Application;
using X2ModCompiler.Compilation;
using X2ModCompiler.Configuration;
using X2ModCompiler.Cooking;
using X2ModCompiler.Core.Configuration;
using X2ModCompiler.Core.Validation;
using X2ModCompiler.Tracking;
using X2ModCompiler.Utilities;
using ZLogger;

namespace X2ModCompiler.DependencyInjection;

/// <summary>
/// Extension methods for configuring X2ModCompiler services in a <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all X2ModCompiler services to the service collection with singleton lifetime.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="options">The build options to use for configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddX2ModCompiler(this IServiceCollection services, BuildOptions options)
    {
        // Register options
        services.AddSingleton(options);

        // Sync parser settings
        options.SyncToParserSettings();

        // Register logger factory
        services.AddSingleton<ILoggerFactory>(sp =>
        {
            var logLevel = (Microsoft.Extensions.Logging.LogLevel)options.LogVerbosity;
            // Use exe directory for log file (so it's alongside the exe)
            var exeDir = AppContext.BaseDirectory;
            var logPath = Path.Combine(exeDir, "build.log");
            return LoggerFactory.Create(builder =>
            {
                builder.AddZLoggerConsole();
                // File logger with plain text formatter (no ANSI escape codes)
                builder.AddZLoggerFile(logPath, options => options.UsePlainTextFormatter());
                builder.SetMinimumLevel(logLevel);
                // Ensure Information level is logged for all categories
                builder.AddFilter((category, level) => level >= logLevel);
            });
        });

        // Register generic logger
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

        // Register core validation services
        services.AddSingleton<IValidator>(_ => new SyntaxValidator());
        services.AddSingleton<ModSrcPathCache>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ModSrcPathCache>>();
            return new ModSrcPathCache(options.ParserSettings, logger);
        });
        services.AddSingleton<FileProcessor>(sp =>
        {
            var validator = sp.GetRequiredService<IValidator>();
            var modSrcCache = sp.GetRequiredService<ModSrcPathCache>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            return new FileProcessor(validator, options.ParserSettings, structValidationEnabled: true, loggerFactory, modSrcCache);
        });

        // Register infrastructure services
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IFileMirrorParity>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ModernFileMirror>>();
            return new ModernFileMirror(logger);
        });

        // Register build services
        services.AddSingleton<BuildTracker>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<BuildTracker>>();
            return new BuildTracker(options.BuildCachePath, logger);
        });

        services.AddSingleton<ScriptCompiler>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ScriptCompiler>>();
            var runner = sp.GetRequiredService<IProcessRunner>();
            return new ScriptCompiler(options.CommandletPath, options.SdkPath, options.GamePath, runner, logger);
        });

        services.AddSingleton<AssetCooker>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<AssetCooker>>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var runner = sp.GetRequiredService<IProcessRunner>();
            var mirror = sp.GetRequiredService<IFileMirrorParity>();
            var tracker = sp.GetRequiredService<BuildTracker>();
            return new AssetCooker(options.SdkPath, options.GamePath, options.BuildCachePath, runner, mirror, tracker, loggerFactory, logger);
        });

        services.AddSingleton<ShaderPrecompiler>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ShaderPrecompiler>>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var runner = sp.GetRequiredService<IProcessRunner>();
            var mirror = sp.GetRequiredService<IFileMirrorParity>();
            return new ShaderPrecompiler(runner, mirror, loggerFactory, logger);
        });

        services.AddSingleton<MissingUncookedCopier>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<MissingUncookedCopier>>();
            var mirror = sp.GetRequiredService<IFileMirrorParity>();
            return new MissingUncookedCopier(mirror, logger);
        });

        services.AddSingleton<ProjectSynchronizer>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ProjectSynchronizer>>();
            return new ProjectSynchronizer(logger);
        });

        services.AddSingleton<ScriptCleaner>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ScriptCleaner>>();
            return new ScriptCleaner(logger);
        });

        // Register BuildServices facade
        services.AddSingleton<BuildServices>(sp => new BuildServices(
            sp.GetRequiredService<BuildTracker>(),
            sp.GetRequiredService<ScriptCompiler>(),
            sp.GetRequiredService<AssetCooker>(),
            sp.GetRequiredService<IFileMirrorParity>(),
            sp.GetRequiredService<IProcessRunner>(),
            sp.GetRequiredService<ShaderPrecompiler>(),
            sp.GetRequiredService<MissingUncookedCopier>(),
            sp.GetRequiredService<ProjectSynchronizer>(),
            sp.GetRequiredService<FileProcessor>(),
            sp.GetRequiredService<ScriptCleaner>()));

        // Register controller
        services.AddSingleton<BuildController>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var services = sp.GetRequiredService<BuildServices>();

            return new BuildController(
                options,
                loggerFactory,
                services);
        });

        return services;
    }
}
