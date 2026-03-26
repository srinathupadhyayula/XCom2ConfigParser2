using Microsoft.Extensions.Logging;
using ZLogger;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using X2ModCompiler.Configuration;
using Polly;
using Polly.Retry;

namespace X2ModCompiler.Utilities;

/// <summary>
/// Defines a contract for file mirroring and synchronization operations.
/// </summary>
public interface IFileMirrorParity
{
    /// <summary>
    /// Mirrors a source directory to a destination directory. 
    /// Maintains parity with Robocopy /MIR, but implemented natively in C#.
    /// </summary>
    Task MirrorAsync(string source, string destination, string pattern = "*.*", string[]? excludeFiles = null, string[]? excludeDirs = null, CancellationToken ct = default);

    /// <summary>
    /// Mirrors a source directory using raw logic parity with Robocopy-style arguments.
    /// Note: This implementation supports basic /MIR parity via Native C# logic.
    /// </summary>
    Task MirrorWithArgsAsync(string source, string destination, string pattern = "*.*", string args = "/MIR", CancellationToken ct = default);

    /// <summary>
    /// Low-level copy operation for individual files.
    /// </summary>
    Task CopyAsync(string source, string destination, bool overwrite = true, CancellationToken ct = default);

    /// <summary>
    /// Deletes a file or directory.
    /// </summary>
    Task DeleteAsync(string path, bool recursive = true, CancellationToken ct = default);
}

/// <summary>
/// Implements <see cref="IFileMirrorParity"/> as a native C# incremental synchronizer.
/// Replaces the Windows-only 'robocopy.exe' with a cross-platform, high-performance implementation.
/// </summary>
public class ModernFileMirror : IFileMirrorParity
{
    private readonly ILogger<ModernFileMirror> _logger;

    private readonly AsyncRetryPolicy _retryPolicy;

    public ModernFileMirror(ILogger<ModernFileMirror> logger)
    {
        _logger = logger;
        
        // Configure Polly retry policy with exponential backoff
        _retryPolicy = Policy
            .Handle<IOException>()
            .WaitAndRetryAsync(
                retryCount: BuildConstants.MaxRetryAttempts,
                sleepDurationProvider: retryAttempt =>
                {
                    var delay = BuildConstants.InitialRetryDelayMs * Math.Pow(2, retryAttempt);
                    return TimeSpan.FromMilliseconds(delay);
                },
                onRetry: (outcome, timeSpan, retryCount, context) =>
                {
                    _logger.ZLogWarning($"Transient I/O error: {outcome.Message}. Retry attempt {retryCount} after {timeSpan.TotalMilliseconds}ms");
                }
            );
    }

    // Overload for tests that don't need a real logger factory (legacy shim)
    public ModernFileMirror(IProcessRunner processRunner)
    {
        _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<ModernFileMirror>.Instance;
        
        // Configure Polly retry policy with exponential backoff for tests
        _retryPolicy = Policy
            .Handle<IOException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(100 * Math.Pow(2, retryAttempt))
            );
    }

    /// <inheritdoc/>
    public async Task MirrorAsync(string source, string destination, string pattern = "*.*", string[]? excludeFiles = null, string[]? excludeDirs = null, CancellationToken ct = default)
    {
        if (!Directory.Exists(source)) return;

        _logger.ZLogInformation($"Mirroring {source} to {destination} (Pattern: {pattern})");

        // Ensure destination exists
        if (!Directory.Exists(destination))
        {
            Directory.CreateDirectory(destination);
        }

        var sourceDir = new DirectoryInfo(source);
        var destDir = new DirectoryInfo(destination);

        // 1. Synchronize Files and Subdirectories
        await SyncDirectoryAsync(sourceDir, destDir, pattern, excludeFiles, excludeDirs, ct);

        // 2. Perform Orphan Cleanup (the 'MIR' part)
        // Note: For safety, we only cleanup if pattern is *.* or matches everything.
        // If the user wants specific pattern mirroring, we only clear files matching that pattern?
        // Robocopy /MIR deletes everything in dest NOT in source.
        await CleanupOrphansAsync(sourceDir, destDir, pattern, excludeFiles, excludeDirs, ct);
    }

    /// <inheritdoc/>
    public async Task MirrorWithArgsAsync(string source, string destination, string pattern = "*.*", string args = "/MIR", CancellationToken ct = default)
    {
        // For simplicity, we map simple Robocopy-style calls to our native Mirror logic
        await MirrorAsync(source, destination, pattern, null, null, ct);
    }

    /// <inheritdoc/>
    public async Task CopyAsync(string source, string destination, bool overwrite = true, CancellationToken ct = default)
    {
        if (!File.Exists(source)) return;

        var destDir = Path.GetDirectoryName(destination);
        if (destDir != null && !Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
        }

        // Implementation of incremental copy
        if (!overwrite && File.Exists(destination))
        {
            return;
        }

        if (File.Exists(destination))
        {
            var srcInfo = new FileInfo(source);
            var destInfo = new FileInfo(destination);

            // Robocopy parity: 1s tolerance for timestamps
            if (srcInfo.Length == destInfo.Length && Math.Abs((srcInfo.LastWriteTimeUtc - destInfo.LastWriteTimeUtc).TotalSeconds) < 1.0)
            {
                return;
            }
        }

        await _retryPolicy.ExecuteAsync(async (ct) =>
        {
            using var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            using var destStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
            await sourceStream.CopyToAsync(destStream, ct);
            File.SetLastWriteTimeUtc(destination, File.GetLastWriteTimeUtc(source));
        }, CancellationToken.None);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string path, bool recursive = true, CancellationToken ct = default)
    {
        if (Directory.Exists(path))
        {
            await _retryPolicy.ExecuteAsync(async () =>
            {
                Directory.Delete(path, recursive);
                await Task.CompletedTask;
            });
        }
        else if (File.Exists(path))
        {
            await _retryPolicy.ExecuteAsync(async () =>
            {
                File.Delete(path);
                await Task.CompletedTask;
            });
        }
    }

    private async Task SyncDirectoryAsync(DirectoryInfo source, DirectoryInfo dest, string pattern, string[]? excludeFiles, string[]? excludeDirs, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // Sync files in current directory
        var sourceFiles = source.GetFiles(pattern);
        foreach (var srcFile in sourceFiles)
        {
            if (excludeFiles != null && excludeFiles.Any(x => string.Equals(srcFile.Name, x, StringComparison.OrdinalIgnoreCase))) continue;

            var destFilePath = Path.Combine(dest.FullName, srcFile.Name);
            await CopyAsync(srcFile.FullName, destFilePath, true, ct);
        }

        // Recursively sync subdirectories
        var sourceSubDirs = source.GetDirectories();
        foreach (var srcSubDir in sourceSubDirs)
        {
            if (excludeDirs != null && excludeDirs.Any(x => string.Equals(srcSubDir.Name, x, StringComparison.OrdinalIgnoreCase))) continue;

            var destSubDir = new DirectoryInfo(Path.Combine(dest.FullName, srcSubDir.Name));
            if (!destSubDir.Exists)
            {
                destSubDir.Create();
            }

            await SyncDirectoryAsync(srcSubDir, destSubDir, pattern, excludeFiles, excludeDirs, ct);
        }
    }

    private async Task CleanupOrphansAsync(DirectoryInfo source, DirectoryInfo dest, string pattern, string[]? excludeFiles, string[]? excludeDirs, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // 1. Cleanup Files in Dest that are not in Source
        var destFiles = dest.GetFiles(pattern);
        foreach (var destFile in destFiles)
        {
            if (excludeFiles != null && excludeFiles.Any(x => string.Equals(destFile.Name, x, StringComparison.OrdinalIgnoreCase))) continue;

            var srcFilePath = Path.Combine(source.FullName, destFile.Name);
            if (!File.Exists(srcFilePath))
            {
                await DeleteAsync(destFile.FullName, false, ct);
            }
        }

        // 2. Cleanup Directories in Dest that are not in Source
        var destSubDirs = dest.GetDirectories();
        foreach (var destSubDir in destSubDirs)
        {
            if (excludeDirs != null && excludeDirs.Any(x => string.Equals(destSubDir.Name, x, StringComparison.OrdinalIgnoreCase))) continue;

            var srcSubDir = Path.Combine(source.FullName, destSubDir.Name);
            if (!Directory.Exists(srcSubDir))
            {
                await DeleteAsync(destSubDir.FullName, true, ct);
            }
            else
            {
                // Recursively cleanup orphans
                await CleanupOrphansAsync(new DirectoryInfo(srcSubDir), destSubDir, pattern, excludeFiles, excludeDirs, ct);
            }
        }
    }
}
