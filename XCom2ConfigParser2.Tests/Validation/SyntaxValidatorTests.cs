using Shouldly;
using XCom2ConfigParser2.Core;
using XCom2ConfigParser2.Parser;
using XCom2ConfigParser2.Validation;

namespace XCom2ConfigParser2.Tests.Validation;

public class SyntaxValidatorTests
{
    private readonly SyntaxValidator _validator = new();

    [Fact]
    public void Validate_ValidSectionHeader_NoErrors()
    {
        var errors = Validate("[Engine.Engine]");
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_SectionWithTabs_NoErrors()
    {
        var errors = Validate("[Specialist\tX2SoldierClassTemplate]");
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_SectionWithMultipleSpaces_NoErrors()
    {
        var errors = Validate("[Specialist      X2SoldierClassTemplate]");
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_InvalidSectionHeader_MalformedHeaderError()
    {
        var errors = Validate("[Invalid Header ]");
        errors.ShouldContain(e => e.Code == ErrorCode.MalformedHeader);
    }

    [Fact]
    public void Validate_ValidKvp_NoErrors()
    {
        var errors = Validate("Property=Value");
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_InvalidIdentifier_InvalidIdentError()
    {
        var errors = Validate("123Invalid=Value");
        errors.ShouldContain(e => e.Code == ErrorCode.InvalidIdentifier);
    }

    [Fact]
    public void Validate_BooleanValue_NoErrors()
    {
        var errors = Validate("Property=True");
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_NumericValue_NoErrors()
    {
        var errors = Validate("Property=42");
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_FloatValue_NoErrors()
    {
        var errors = Validate("Property=3.14");
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_StringValue_NoErrors()
    {
        var errors = Validate("Property=\"Hello World\"");
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_ValidStruct_NoErrors()
    {
        var errors = Validate("Property=(Name=Value)");
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_InvalidStruct_StructParseError()
    {
        var errors = Validate("Property=(Name=Value"); // Missing closing paren
        errors.ShouldContain(e => e.Code == ErrorCode.StructParseError);
    }

    [Fact]
    public void Validate_ValidArray_NoErrors()
    {
        var errors = Validate("Property=(Item1, Item2, Item3)");
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_UnknownDirective_OtherError()
    {
        var errors = Validate("InvalidDirective");
        errors.ShouldContain(e => e.Code == ErrorCode.Other);
    }

    [Fact]
    public void Validate_EmptyValue_BadValueError()
    {
        var errors = Validate("Property=");
        errors.ShouldContain(e => e.Code == ErrorCode.BadValue);
    }

    [Fact]
    public void Validate_DiagnosticHasCorrectLocation()
    {
        var errors = Validate("123Invalid=Value");
        var error = errors.Where(e => e.Code == ErrorCode.InvalidIdentifier).ShouldHaveSingleItem();
        error.Location.Start.Line.ShouldBe(1);
        error.Location.Start.Column.ShouldBe(1);
    }

    [Fact]
    public void Validate_NamedArrayIndex_NoErrors()
    {
        var errors = Validate("CharacterBaseStats[eStat_Will]=50");
        errors.ShouldBeEmpty();
    }

    private IReadOnlyList<Diagnostic> Validate(string text)
    {
        var lines = LineSplitter.Split(text);
        var merged = LineSplitter.GetMergedLines(text, lines);
        var directives = DirectiveTokenizer.Tokenize(text, merged);
        return _validator.Validate(text, directives, "test.ini");
    }
}
