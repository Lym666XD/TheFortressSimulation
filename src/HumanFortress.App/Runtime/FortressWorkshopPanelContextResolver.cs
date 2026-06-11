using HumanFortress.Core.Content.Registry;
using HumanFortress.Simulation.Placeables;
using HumanFortress.Simulation.World;

namespace HumanFortress.App.Runtime;

internal static class FortressWorkshopPanelContextResolver
{
    public static FortressWorkshopPanelContext? Resolve(World? world, Guid guid)
    {
        if (world == null)
            return null;

        var registry = ContentRegistry.Instance.Constructions;
        foreach (var chunk in world.GetAllChunks())
        {
            var placeableData = chunk.GetPlaceableData();
            if (placeableData == null)
                continue;

            foreach (var placeable in placeableData.GetAllOwnedPlaceables())
            {
                if (placeable.Guid != guid)
                    continue;

                var definition = registry.GetConstruction(placeable.DefinitionId);
                if (placeable.Workshop == null)
                {
                    placeable.Workshop = new WorkshopState();
                    int maxWorkers = Math.Max(1, definition?.Io?.InputSlots ?? 1);
                    placeable.Workshop.ConfigureWorkers(1, maxWorkers);
                }

                return new FortressWorkshopPanelContext(placeable.Workshop, definition?.Id);
            }
        }

        return null;
    }
}
