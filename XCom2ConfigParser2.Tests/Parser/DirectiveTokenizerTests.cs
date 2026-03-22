using Shouldly;
using XCom2ConfigParser2.Core;
using XCom2ConfigParser2.Parser;

namespace XCom2ConfigParser2.Tests.Parser;

public class DirectiveTokenizerTests
{
    [Fact]
    public void Tokenize_SectionHeader_ReturnsSectionDirective()
    {
        var text = "[Engine.Engine]";
        var lines = LineSplitter.Split(text);
        var merged = LineSplitter.GetMergedLines(text, lines);

        var directives = DirectiveTokenizer.Tokenize(text, merged);

        directives.Count.ShouldBe(1);
        directives[0].Type.ShouldBe(DirectiveType.SectionHeader);
    }

    [Fact]
    public void Tokenize_Kvp_ReturnsKvpDirective()
    {
        var text = "Property=Value";
        var lines = LineSplitter.Split(text);
        var merged = LineSplitter.GetMergedLines(text, lines);

        var directives = DirectiveTokenizer.Tokenize(text, merged);

        directives.Count.ShouldBe(1);
        directives[0].Type.ShouldBe(DirectiveType.Kvp);
        directives[0].Kvp!.Value.Operation.ShouldBe(KvpOperation.Set);
    }

    [Fact]
    public void Tokenize_KvpWithPlusPrefix_ReturnsInsertUnique()
    {
        var text = "+Property=Value";
        var lines = LineSplitter.Split(text);
        var merged = LineSplitter.GetMergedLines(text, lines);

        var directives = DirectiveTokenizer.Tokenize(text, merged);

        directives.Count.ShouldBe(1);
        directives[0].Kvp!.Value.Operation.ShouldBe(KvpOperation.InsertUnique);
    }

    [Fact]
    public void Tokenize_KvpWithDotPrefix_ReturnsInsert()
    {
        var text = ".Property=Value";
        var lines = LineSplitter.Split(text);
        var merged = LineSplitter.GetMergedLines(text, lines);

        var directives = DirectiveTokenizer.Tokenize(text, merged);

        directives.Count.ShouldBe(1);
        directives[0].Kvp!.Value.Operation.ShouldBe(KvpOperation.Insert);
    }

    [Fact]
    public void Tokenize_KvpWithMinusPrefix_ReturnsRemove()
    {
        var text = "-Property=Value";
        var lines = LineSplitter.Split(text);
        var merged = LineSplitter.GetMergedLines(text, lines);

        var directives = DirectiveTokenizer.Tokenize(text, merged);

        directives.Count.ShouldBe(1);
        directives[0].Kvp!.Value.Operation.ShouldBe(KvpOperation.Remove);
    }

    [Fact]
    public void Tokenize_KvpWithExclaimPrefix_ReturnsClear()
    {
        var text = "!Property=Value";
        var lines = LineSplitter.Split(text);
        var merged = LineSplitter.GetMergedLines(text, lines);

        var directives = DirectiveTokenizer.Tokenize(text, merged);

        directives.Count.ShouldBe(1);
        directives[0].Kvp!.Value.Operation.ShouldBe(KvpOperation.Clear);
    }

    [Fact]
    public void Tokenize_CommentLine_SkipsComment()
    {
        var text = "; This is a comment";
        var lines = LineSplitter.Split(text);
        var merged = LineSplitter.GetMergedLines(text, lines);

        var directives = DirectiveTokenizer.Tokenize(text, merged);

        directives.ShouldBeEmpty();
    }

    [Fact]
    public void Tokenize_EmptyLine_SkipsLine()
    {
        var text = "   ";
        var lines = LineSplitter.Split(text);
        var merged = LineSplitter.GetMergedLines(text, lines);

        var directives = DirectiveTokenizer.Tokenize(text, merged);

        directives.ShouldBeEmpty();
    }

    [Fact]
    public void Tokenize_SlashSlashComment_ReturnsUnknownDirective()
    {
        var text = "// Invalid comment";
        var lines = LineSplitter.Split(text);
        var merged = LineSplitter.GetMergedLines(text, lines);

        var directives = DirectiveTokenizer.Tokenize(text, merged);

        directives.Count.ShouldBe(1);
        directives[0].Type.ShouldBe(DirectiveType.Unknown);
    }

    [Fact]
    public void Tokenize_UnknownLine_ReturnsUnknownDirective()
    {
        var text = "InvalidLineWithoutEquals";
        var lines = LineSplitter.Split(text);
        var merged = LineSplitter.GetMergedLines(text, lines);

        var directives = DirectiveTokenizer.Tokenize(text, merged);

        directives.Count.ShouldBe(1);
        directives[0].Type.ShouldBe(DirectiveType.Unknown);
    }

    [Fact]
    public void Tokenize_MultipleDirectives_ReturnsAllDirectives()
    {
        var text = "[Section]\nProperty=Value\n+Array=Item";
        var lines = LineSplitter.Split(text);
        var merged = LineSplitter.GetMergedLines(text, lines);

        var directives = DirectiveTokenizer.Tokenize(text, merged);

        directives.Count.ShouldBe(3);
        directives[0].Type.ShouldBe(DirectiveType.SectionHeader);
        directives[1].Type.ShouldBe(DirectiveType.Kvp);
        directives[2].Type.ShouldBe(DirectiveType.Kvp);
    }
}
