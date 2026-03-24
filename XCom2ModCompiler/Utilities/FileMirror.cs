using System.Diagnostics;

namespace XCom2ModCompiler.Utilities;

public interface IFileMirrorParity
{
    Task MirrorAsync(string source, string destination, string pattern = "*.*", string[]? excludeFiles = null, string[]? excludeDirs = null, CancellationToken ct = default(CancellationToken));
    Task MirrorWithArgsAsync(string source, string destination, string pattern = "*.*", string args = "/MIR /R:3 /W:5 /NFL /NDL /NJH /NJS /nc /ns /np", CancellationToken ct = default(CancellationToken));
    Task CopyAsync(string source, string destination, bool overwrite = true, CancellationToken ct = default(CancellationToken));
    Task DeleteAsync(string path, bool recursive = true, CancellationToken ct = default(CancellationToken));
}

public class RobocopyFileMirror : IFileMirrorParity
{
    private readonly IProcessRunner _processRunner;

    public RobocopyFileMirror(IProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

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

    public async Task MirrorWithArgsAsync(string source, string destination, string pattern = "*.*", string args = "/MIR /R:3 /W:5 /NFL /NDL /NJH /NJS /nc /ns /np", CancellationToken ct = default(CancellationToken))
    {
        if (!Directory.Exists(source)) return;

        var fullArgs = $"\"{source}\" \"{destination}\" {pattern} {args}";
        await RunRobocopyAsync(fullArgs, source, destination, ct);
    }

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
