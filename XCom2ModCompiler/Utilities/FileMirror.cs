using System.Diagnostics;

namespace XCom2ModCompiler.Utilities;

/// <summary>
/// Defines a contract for file mirroring and synchronization operations that maintain parity with system build tools.
/// </summary>
public interface IFileMirrorParity
{
    /// <summary>
    /// Mirrors a source directory to a destination directory, optionally excluding specific files or folders.
    /// </summary>
    /// <param name="source">The absolute path to the source directory.</param>
    /// <param name="destination">The absolute path to the destination directory.</param>
    /// <param name="pattern">The file pattern to match (e.g., "*.*").</param>
    /// <param name="excludeFiles">An optional array of filenames to exclude from the mirror.</param>
    /// <param name="excludeDirs">An optional array of directory names to exclude from the mirror.</param>
    /// <param name="ct">A cancellation token to abort the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task MirrorAsync(string source, string destination, string pattern = "*.*", string[]? excludeFiles = null, string[]? excludeDirs = null, CancellationToken ct = default(CancellationToken));

    /// <summary>
    /// Mirrors a source directory using raw Robocopy-style arguments.
    /// </summary>
    /// <param name="source">The source directory.</param>
    /// <param name="destination">The destination directory.</param>
    /// <param name="pattern">The file pattern.</param>
    /// <param name="args">The raw Robocopy CLI arguments.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task MirrorWithArgsAsync(string source, string destination, string pattern = "*.*", string args = "/MIR /R:3 /W:5 /NFL /NDL /NJH /NJS /nc /ns /np", CancellationToken ct = default(CancellationToken));

    /// <summary>
    /// Low-level copy operation for individual files.
    /// </summary>
    /// <param name="source">The source file path.</param>
    /// <param name="destination">The destination file path.</param>
    /// <param name="overwrite">Whether to overwrite the destination if it exists.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CopyAsync(string source, string destination, bool overwrite = true, CancellationToken ct = default(CancellationToken));

    /// <summary>
    /// Deletes a file or directory.
    /// </summary>
    /// <param name="path">The path to delete.</param>
    /// <param name="recursive">Whether to perform a recursive deletion for directories.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteAsync(string path, bool recursive = true, CancellationToken ct = default(CancellationToken));
}

/// <summary>
/// Implements <see cref="IFileMirrorParity"/> using the native Windows 'robocopy.exe' utility.
/// Provides high-performance directory synchronization with robust error handling for Robocopy exit codes.
/// </summary>
public class RobocopyFileMirror : IFileMirrorParity
{
    private readonly IProcessRunner _processRunner;

    /// <summary>
    /// Initializes a new instance of the <see cref="RobocopyFileMirror"/> class.
    /// </summary>
    /// <param name="processRunner">The process runner used to invoke robocopy.exe.</param>
    public RobocopyFileMirror(IProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    /// <inheritdoc/>
    public async Task MirrorAsync(string source, string destination, string pattern = "*.*", string[]? excludeFiles = null, string[]? excludeDirs = null, CancellationToken ct = default(CancellationToken))
    {
        if (!Directory.Exists(source)) return;

        var args = $"\"{source}\" \"{destination}\" {pattern} /MIR /R:3 /W:5 /NFL /NDL /NJH /NJS /nc /ns /np";
        
        if (excludeFiles != null && excludeFiles.Length > 0)
        {
            args += $" /XF {string.Join(" ", excludeFiles.Select(x => $"\"{x}\""))}";
        }

        if (excludeDirs != null && excludeDirs.Length > 0)
        {
            args += $" /XD {string.Join(" ", excludeDirs.Select(x => $"\"{x}\""))}";
        }

        await RunRobocopyAsync(args, source, destination, ct);
    }

    /// <inheritdoc/>
    public async Task MirrorWithArgsAsync(string source, string destination, string pattern = "*.*", string args = "/MIR /R:3 /W:5 /NFL /NDL /NJH /NJS /nc /ns /np", CancellationToken ct = default(CancellationToken))
    {
        if (!Directory.Exists(source)) return;

        var fullArgs = $"\"{source}\" \"{destination}\" {pattern} {args}";
        await RunRobocopyAsync(fullArgs, source, destination, ct);
    }

    /// <summary>
    /// Invokes robocopy.exe and interprets its specialized exit codes.
    /// </summary>
    /// <param name="args">The command line arguments for Robocopy.</param>
    /// <param name="source">The source path for logging.</param>
    /// <param name="destination">The destination path for logging.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <exception cref="Exception">Thrown if Robocopy returns an exit code indicating failure (>= 8).</exception>
    private async Task RunRobocopyAsync(string args, string source, string destination, CancellationToken ct)
    {
        Console.WriteLine($"Running: robocopy.exe {args}");

        // Robocopy exit codes < 8 are generally success (1 = copied successfully, 0 = no change, etc)
        var exitCode = await _processRunner.RunProcessAsync("robocopy.exe", args, null, ct);
        
        Console.WriteLine($"Robocopy completed with exit code {exitCode} (source: {source}, dest: {destination})");
        
        if (exitCode >= 8)
        {
            throw new Exception($"Robocopy failed with exit code {exitCode} when mirroring {source} to {destination}");
        }
    }

    /// <inheritdoc/>
    public Task CopyAsync(string source, string destination, bool overwrite = true, CancellationToken ct = default(CancellationToken))
    {
        if (File.Exists(source))
        {
            var destDir = Path.GetDirectoryName(destination);
            if (destDir != null && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }
            File.Copy(source, destination, overwrite);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DeleteAsync(string path, bool recursive = true, CancellationToken ct = default(CancellationToken))
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive);
        }
        else if (File.Exists(path))
        {
            File.Delete(path);
        }
        return Task.CompletedTask;
    }
}
