using System;
using HumanFortress.Core.Commands;
using HumanFortress.Core.Content.Registry;
using HumanFortress.Core.Simulation;
using HumanFortress.Simulation.Placeables;
using HumanFortress.Simulation.World;

namespace HumanFortress.App.Commands;

public enum WorkshopQueueOperation
{
    AddRecipe,
    RemoveEntry,
    MoveEntry,
    ClearQueue,
    SetWorkerSlots,
    ToggleAutoStockpile,
    ToggleAutoSupply
}

/// <summary>
/// Command that mutates a workshop queue or automation flags.
/// Executed on simulation thread for determinism.
/// </summary>
public sealed class UpdateWorkshopQueueCommand : ICommand
{
    public ulong Tick { get; }
    public Guid CommandId { get; } = Guid.NewGuid();
    public string CommandType => "workshops.queue.update";

    private readonly Guid _workshopGuid;
    private readonly WorkshopQueueOperation _operation;
    private readonly string? _recipeId;
    private readonly Guid? _entryId;
    private readonly int? _intValue;
    private readonly int? _moveOffset;
    private readonly bool? _boolValue;

    public UpdateWorkshopQueueCommand(
        ulong tick,
        Guid workshopGuid,
        WorkshopQueueOperation op,
        string? recipeId = null,
        Guid? entryId = null,
        int? intValue = null,
        int? moveOffset = null,
        bool? boolValue = null)
    {
        Tick = tick;
        _workshopGuid = workshopGuid;
        _operation = op;
        _recipeId = recipeId;
        _entryId = entryId;
        _intValue = intValue;
        _moveOffset = moveOffset;
        _boolValue = boolValue;
    }

    public void Execute(ISimulationContext context)
    {
        if (context.World is not World world) return;
        var placeable = FindWorkshop(world, _workshopGuid, out var def);
        if (placeable == null) return;

        placeable.Workshop ??= new WorkshopState();
        if (def != null && placeable.Workshop.MaxWorkers <= 1)
        {
            int maxWorkers = Math.Max(1, def.Io?.InputSlots ?? 1);
            placeable.Workshop.ConfigureWorkers(placeable.Workshop.AllowedWorkers, maxWorkers);
        }

        var state = placeable.Workshop;
        if (state == null) return;

        switch (_operation)
        {
            case WorkshopQueueOperation.AddRecipe:
                if (string.IsNullOrWhiteSpace(_recipeId)) return;
                var recipe = RecipeRegistry.Instance.GetRecipe(_recipeId);
                if (recipe == null) return;
                state.AddEntry(recipe.Id, recipe.Name);
                break;

            case WorkshopQueueOperation.RemoveEntry:
                if (_entryId.HasValue)
                    state.RemoveEntry(_entryId.Value);
                break;

            case WorkshopQueueOperation.MoveEntry:
                if (_entryId.HasValue && _moveOffset.HasValue)
                    state.MoveEntry(_entryId.Value, _moveOffset.Value);
                break;

            case WorkshopQueueOperation.ClearQueue:
                state.ClearQueue();
                break;

            case WorkshopQueueOperation.SetWorkerSlots:
                if (_intValue.HasValue)
                    state.SetAllowedWorkers(_intValue.Value);
                break;

            case WorkshopQueueOperation.ToggleAutoStockpile:
                state.AutoStockpileOutputs = _boolValue ?? !state.AutoStockpileOutputs;
                break;

            case WorkshopQueueOperation.ToggleAutoSupply:
                state.AutoRequestMaterials = _boolValue ?? !state.AutoRequestMaterials;
                break;
        }
    }

    public byte[] Serialize() => Array.Empty<byte>();

    private static PlaceableInstance? FindWorkshop(World world, Guid guid, out ConstructionDefinition? def)
    {
        var registry = ConstructionRegistry.Instance;
        foreach (var chunk in world.GetAllChunks())
        {
            var pd = chunk.GetPlaceableData();
            if (pd == null) continue;
            foreach (var placeable in pd.GetAllOwnedPlaceables())
            {
                if (placeable.Guid != guid) continue;
                def = registry.GetConstruction(placeable.DefinitionId);
                return placeable;
            }
        }
        def = null;
        return null;
    }
}
