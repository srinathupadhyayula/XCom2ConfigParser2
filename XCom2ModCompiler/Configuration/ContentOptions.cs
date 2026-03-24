namespace XCom2ModCompiler.Configuration;

public class ContentOptions
{
    public List<string> MissingUncooked { get; init; } = new();
    public List<string> SfStandalone { get; init; } = new();
    public List<string> SfMaps { get; init; } = new();
    public List<CollectionMapDefinition> SfCollectionMaps { get; init; } = new();
}

public class CollectionMapDefinition
{
    public string Name { get; init; } = "";
    public List<string> Packages { get; init; } = new();
}
