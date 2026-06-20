using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Simulation.Placeables;
using HumanFortress.Simulation.World;

namespace HumanFortress.App.Runtime;

internal static class FortressWorkshopPanelContextResolver
{
    public static FortressWorkshopPanelContext? Resolve(World? world, Guid guid, IConstructionCatalog? constructions)
    {
        if (world == null)
            return null;

        foreach (var chunk in world.GetAllChunks())
        {
            var placeableData = chunk.GetPlaceableData();
            if (placeableData == null)
                continue;

            foreach (var placeable in placeableData.GetAllOwnedPlaceables())
            {
                if (placeable.Guid != guid)
                    continue;

                var definition = constructions?.GetConstruction(placeable.DefinitionId);
                if (placeable.Workshop == null)
                {
                    placeable.Workshop = new WorkshopState();
                    int maxWorkers = Math.Max(1, definition?.Io?.InputSlots ?? 1);
                    placeable.Workshop.ConfigureWorkers(1, maxWorkers);
                }

                return new FortressWorkshopPanelContext(placeable.Workshop, definition?.Id ?? placeable.DefinitionId);
            }
        }

        return null;
    }
}
