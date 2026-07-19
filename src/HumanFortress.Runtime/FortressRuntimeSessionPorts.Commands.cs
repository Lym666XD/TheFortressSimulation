using HumanFortress.Contracts.Runtime;

namespace HumanFortress.Runtime;

public interface IFortressRuntimeSessionPlacementCommandPort
{
    void QueueHaulOrder(RuntimeRect rect, int z, int priority = 50);

    void QueueAdvancedMiningOrder(
        RuntimeRect rect,
        int zMin,
        int zMax,
        RuntimeMiningAction action,
        int priority = 50);

    void QueueConstructionOrder(
        RuntimeRect rect,
        int zMin,
        int zMax,
        RuntimeConstructionShape shape,
        string? resultMaterialId,
        RuntimeConstructionMaterialRequirement[] materialRequirements,
        int priority = 50);

    void QueueBuildableConstructionOrder(
        string constructionId,
        RuntimePoint anchor,
        int z,
        int priority = 50);

    void QueueCreateZone(string defId, RuntimeRect rect, int z);
    void QueueDeleteZone(int zoneId);
    void QueueCreateStockpile(RuntimeRect rect, int z, string presetId);
    void QueueDeleteStockpile(int zoneId);
}

public interface IFortressRuntimeSessionDebugCommandPort
{
    void QueueCreatureSpawn(string creatureId, RuntimePoint position, int z, string factionId);
    void QueueItemSpawn(string itemId, RuntimePoint position, int z, int quantity = 1);
}

public interface IFortressRuntimeSessionSimulationControlPort
{
    SimulationStatus ToggleSimulationPause();
    SimulationStatus CycleSimulationSpeedDown();
    SimulationStatus CycleSimulationSpeedUp();
}

public interface IFortressRuntimeSessionProfessionCommandPort
{
    void SetProfessionWeight(Guid workerId, string professionId, int weight);
}

public interface IFortressRuntimeSessionWorkshopCommandPort
{
    void QueueAddWorkshopRecipe(Guid workshopId, string recipeId);
    void QueueRemoveWorkshopQueueEntry(Guid workshopId, Guid entryId);
    void QueueMoveWorkshopQueueEntry(Guid workshopId, Guid entryId, int moveOffset);
    void QueueSetWorkshopWorkerSlots(Guid workshopId, int workerSlots);
    void QueueToggleWorkshopAutoSupply(Guid workshopId);
    void QueueToggleWorkshopAutoStockpile(Guid workshopId);
}
