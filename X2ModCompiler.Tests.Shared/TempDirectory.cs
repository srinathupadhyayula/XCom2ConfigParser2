using System.Runtime.CompilerServices;

namespace X2ModCompiler.Tests.Shared;

public sealed class TempDirectory : IAsyncDisposable
{
    public string Path { get; }

    public TempDirectory([CallerMemberName] string testName = "Test")
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "X2ModCompilerTests", $"{testName}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public string Combine(params string[] paths) => System.IO.Path.Combine(Path, System.IO.Path.Combine(paths));

    public void CreateFile(string relativePath, string content = "")
    {
        var fullPath = Combine(relativePath);
        var dir = System.IO.Path.GetDirectoryName(fullPath);
        if (dir != null) Directory.CreateDirectory(dir);
        File.WriteAllText(fullPath, content);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                // Simple retry for robustness against transient locks
                for (int i = 0; i < 3; i++)
                {
                    try { Directory.Delete(Path, true); break; }
                    catch { await Task.Delay(100); }
                }
            }
        }
        catch { /* Ignore cleanup errors in tests */ }
    }
}
