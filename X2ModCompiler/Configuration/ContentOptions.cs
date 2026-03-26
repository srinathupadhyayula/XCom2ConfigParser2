namespace X2ModCompiler.Configuration;

/// <summary>
/// Defines fine-grained asset configuration for a mod, typically loaded from an external JSON definition.
/// These options determine how shared assets, maps, and collection packages are handled during the build and cook phases.
/// </summary>
public class ContentOptions
{
    /// <summary>
    /// Gets a list of base game packages that are required by the mod but not and must be copied into the mod's CookedPCConsole staging area.
    /// </summary>
    public List<string> MissingUncooked { get; init; } = new();

    /// <summary>
    /// Gets a list of 'Seek Free' (SF) standalone packages that should be included in the mod's distribution.
    /// </summary>
    public List<string> SfStandalone { get; init; } = new();

    /// <summary>
    /// Gets a list of map packages (.pck) that require seek-free compilation for the game's map loading system.
    /// </summary>
    public List<string> SfMaps { get; init; } = new();

    /// <summary>
    /// Gets a list of collection map definitions, which aggregate multiple packages into optimized seek-free maps.
    /// </summary>
    public List<CollectionMapDefinition> SfCollectionMaps { get; init; } = new();
}

/// <summary>
/// Represents a definition for a Seek-Free (SF) collection map.
/// Aggregates multiple dependent packages into a single optimized collection for efficient runtime loading.
/// </summary>
public class CollectionMapDefinition
{
    /// <summary>
    /// Gets the name of the resulting collection map package.
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// Gets the list of packages to be included in this collection.
    /// </summary>
    public List<string> Packages { get; init; } = new();
}
