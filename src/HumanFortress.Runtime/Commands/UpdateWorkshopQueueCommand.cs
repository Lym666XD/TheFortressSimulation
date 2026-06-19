using System;
using HumanFortress.Core.Commands;
using HumanFortress.Core.Simulation;
using HumanFortress.Runtime;

namespace HumanFortress.Runtime.Commands;

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
        if (context is not IWorkshopQueueCommandTarget target) return;

        switch (_operation)
        {
            case WorkshopQueueOperation.AddRecipe:
                if (!string.IsNullOrWhiteSpace(_recipeId))
                    target.AddWorkshopRecipe(_workshopGuid, _recipeId, context.CurrentTick);
                break;

            case WorkshopQueueOperation.RemoveEntry:
                if (_entryId.HasValue)
                    target.RemoveWorkshopQueueEntry(_workshopGuid, _entryId.Value);
                break;

            case WorkshopQueueOperation.MoveEntry:
                if (_entryId.HasValue && _moveOffset.HasValue)
                    target.MoveWorkshopQueueEntry(_workshopGuid, _entryId.Value, _moveOffset.Value);
                break;

            case WorkshopQueueOperation.ClearQueue:
                target.ClearWorkshopQueue(_workshopGuid);
                break;

            case WorkshopQueueOperation.SetWorkerSlots:
                if (_intValue.HasValue)
                    target.SetWorkshopWorkerSlots(_workshopGuid, _intValue.Value);
                break;

            case WorkshopQueueOperation.ToggleAutoStockpile:
                target.SetWorkshopAutoStockpile(_workshopGuid, _boolValue);
                break;

            case WorkshopQueueOperation.ToggleAutoSupply:
                target.SetWorkshopAutoSupply(_workshopGuid, _boolValue);
                break;
        }
    }

    public byte[] Serialize() => Array.Empty<byte>();
}
