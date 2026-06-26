using HumanFortress.Simulation.Placeables;

namespace HumanFortress.Runtime;

internal sealed partial class WorkshopQueueCommandTarget
{
    private bool TryGetWorkshopState(Guid workshopGuid, out WorkshopState state)
    {
        foreach (var chunk in _world.GetAllChunks())
        {
            var placeableData = chunk.GetPlaceableData();
            if (placeableData == null) continue;

            foreach (var placeable in placeableData.GetAllOwnedPlaceables())
            {
                if (placeable.Guid != workshopGuid) continue;

                placeable.Workshop ??= new WorkshopState();
                var definition = _constructions.GetConstruction(placeable.DefinitionId);
                if (definition != null && placeable.Workshop.MaxWorkers <= 1)
                {
                    int maxWorkers = Math.Max(1, definition.Io?.InputSlots ?? 1);
                    placeable.Workshop.ConfigureWorkers(placeable.Workshop.AllowedWorkers, maxWorkers);
                }

                state = placeable.Workshop;
                return true;
            }
        }

        state = null!;
        return false;
    }
}
