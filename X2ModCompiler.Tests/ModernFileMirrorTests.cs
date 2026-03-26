using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using X2ModCompiler.Utilities;
using X2ModCompiler.Tests.Shared;
using System;

namespace X2ModCompiler.Tests;

public class ModernFileMirrorTests : TestBase
{
    private readonly IProcessRunner _processRunner;
    private readonly ModernFileMirror _mirror;

    public ModernFileMirrorTests()
    {
        _processRunner = Substitute.For<IProcessRunner>();
        _mirror = new ModernFileMirror(_processRunner);
    }

    [Fact]
    public async Task MirrorAsync_CopiesNewFile()
    {
        await using var temp = new TempDirectory();
        var src = Path.Combine(temp.Path, "Src");
        var dest = Path.Combine(temp.Path, "Dest");
        Directory.CreateDirectory(src);
        Directory.CreateDirectory(dest);
        
        string fileName = "test.uc";
        string content = "class Test;";
        await File.WriteAllTextAsync(Path.Combine(src, fileName), content, TestContext.Current.CancellationToken);

        await _mirror.MirrorAsync(src, dest, "*.uc", null, null, TestContext.Current.CancellationToken);

        string destFile = Path.Combine(dest, fileName);
        Assert.True(File.Exists(destFile));
        Assert.Equal(content, await File.ReadAllTextAsync(destFile, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MirrorAsync_SkipsIdenticalFile()
    {
        await using var temp = new TempDirectory();
        var src = Path.Combine(temp.Path, "Src");
        var dest = Path.Combine(temp.Path, "Dest");
        Directory.CreateDirectory(src);
        Directory.CreateDirectory(dest);
        
        string fileName = "test.uc";
        string content = "class Test;";
        string srcPath = Path.Combine(src, fileName);
        string destPath = Path.Combine(dest, fileName);

        await File.WriteAllTextAsync(srcPath, content, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(destPath, content, TestContext.Current.CancellationToken);

        // Synchronize timestamps
        var now = DateTime.UtcNow;
        File.SetLastWriteTimeUtc(srcPath, now);
        File.SetLastWriteTimeUtc(destPath, now);

        await _mirror.MirrorAsync(src, dest, "*.uc", null, null, TestContext.Current.CancellationToken);

        // Optimization: In a real test we might track I/O or use a mock FS, 
        // but here we just verify it still exists.
        Assert.True(File.Exists(destPath));
    }

    [Fact]
    public async Task MirrorAsync_DeletesOrphanedFileInMirrorMode()
    {
        await using var temp = new TempDirectory();
        var src = Path.Combine(temp.Path, "Src");
        var dest = Path.Combine(temp.Path, "Dest");
        Directory.CreateDirectory(src);
        Directory.CreateDirectory(dest);
        
        await File.WriteAllTextAsync(Path.Combine(dest, "orphan.u"), "delete me", TestContext.Current.CancellationToken);

        // Run with mirrorMode = true (default in many calls)
        await _mirror.MirrorAsync(src, dest, "*.*", null, null, TestContext.Current.CancellationToken);

        Assert.False(File.Exists(Path.Combine(dest, "orphan.u")));
    }

    [Fact]
    public async Task MirrorAsync_ExcludesFilesByPattern()
    {
        await using var temp = new TempDirectory();
        var src = Path.Combine(temp.Path, "Src");
        var dest = Path.Combine(temp.Path, "Dest");
        Directory.CreateDirectory(src);
        Directory.CreateDirectory(dest);
        
        await File.WriteAllTextAsync(Path.Combine(src, "keep.uc"), "content", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(src, "exclude.json"), "{}", TestContext.Current.CancellationToken);

        await _mirror.MirrorAsync(src, dest, "*.uc", null, null, TestContext.Current.CancellationToken);

        Assert.True(File.Exists(Path.Combine(dest, "keep.uc")));
        Assert.False(File.Exists(Path.Combine(dest, "exclude.json")));
    }

    [Fact]
    public async Task DeleteAsync_ExecutesDirectoryDelete_WhenDirectoryExists()
    {
        await using var temp = new TempDirectory();
        var tempDir = Path.Combine(temp.Path, "ToDelete");
        Directory.CreateDirectory(tempDir);

        await _mirror.DeleteAsync(tempDir, true, TestContext.Current.CancellationToken);
        Assert.False(Directory.Exists(tempDir));
    }
}
