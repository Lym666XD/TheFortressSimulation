using HumanFortress.Content.Definitions;
using HumanFortress.Core.Content.Registry;
using HumanFortress.Simulation.World;
using IoPath = System.IO.Path;

internal static class DefinitionCatalogTestSupport
{
    public static string CoreDataPath => IoPath.Combine(AppContext.BaseDirectory, "data", "core");

    public static ItemDefinitionCatalogLoadResult LoadItems(World world, string? dataPath = null)
    {
        ArgumentNullException.ThrowIfNull(world);

        world.Items.SetDependencies(world, ContentRegistry.Instance);
        var result = ItemDefinitionCatalogLoader.Load(dataPath ?? CoreDataPath);
        world.Items.SetDefinitionCatalog(result.Catalog);
        return result;
    }

    public static CreatureDefinitionCatalogLoadResult LoadCreatures(World world, string? dataPath = null)
    {
        ArgumentNullException.ThrowIfNull(world);

        var result = CreatureDefinitionCatalogLoader.Load(dataPath ?? CoreDataPath);
        world.Creatures.SetDefinitionCatalog(result.Catalog);
        return result;
    }
}
