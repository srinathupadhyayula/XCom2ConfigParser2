using Shouldly;
using XCom2ConfigParser2.StructValidation;

namespace XCom2ConfigParser2.Tests.StructValidation;

public class CachedStructDefTests
{
    [Fact]
    public void CachedStructDef_DefaultConstructor_InitializesWithDefaults()
    {
        // Act
        var def = new CachedStructDef();

        // Assert
        def.StructName.ShouldBeEmpty();
        def.SourceFile.ShouldBeEmpty();
        def.SourceHash.ShouldBeEmpty();
        def.Fields.ShouldBeEmpty();
        def.NestedStructs.ShouldBeEmpty();
        def.ResolvedNestedStructs.ShouldBeFalse();
    }

    [Fact]
    public void CachedStructDef_WithProperties_SerializesToJson()
    {
        // Arrange
        var def = new CachedStructDef
        {
            StructName = "SDLReplacement",
            SourceFile = "Src/Mod/Classes/MyClass.uc",
            SourceHash = "sha256:abc123",
            LastIndexed = new DateTime(2026, 3, 22, 10, 30, 0, DateTimeKind.Utc),
            Fields = new List<StructField>
            {
                new() { Name = "List", TypeName = "XComSpawnList[]" },
                new() { Name = "Count", TypeName = "int" }
            },
            NestedStructs = new List<string> { "XComSpawnList" },
            ResolvedNestedStructs = true
        };

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(def, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });

        // Assert
        json.ShouldContain("SDLReplacement");
        json.ShouldContain("XComSpawnList");
        json.ShouldContain("sha256:abc123");
    }

    [Fact]
    public void CachedStructDef_DeserializesFromJson_Correctly()
    {
        // Arrange
        var json = """
        {
            "StructName": "SDLReplacement",
            "SourceFile": "Src/Mod/Classes/MyClass.uc",
            "SourceHash": "sha256:abc123",
            "LastIndexed": "2026-03-22T10:30:00Z",
            "Fields": [
                {
                    "Name": "List",
                    "TypeName": "XComSpawnList[]",
                    "IsStruct": true,
                    "BaseType": "XComSpawnList"
                }
            ],
            "NestedStructs": ["XComSpawnList"],
            "ResolvedNestedStructs": true
        }
        """;

        // Act
        var def = System.Text.Json.JsonSerializer.Deserialize<CachedStructDef>(json);

        // Assert
        def.ShouldNotBeNull();
        def!.StructName.ShouldBe("SDLReplacement");
        def.SourceFile.ShouldBe("Src/Mod/Classes/MyClass.uc");
        def.SourceHash.ShouldBe("sha256:abc123");
        def.Fields.Count.ShouldBe(1);
        def.Fields[0].Name.ShouldBe("List");
        def.Fields[0].IsStruct.ShouldBeTrue();
        def.NestedStructs.ShouldContain("XComSpawnList");
        def.ResolvedNestedStructs.ShouldBeTrue();
    }
}
