using HumanFortress.Core.Commands;
using HumanFortress.Core.Simulation;
using HumanFortress.Runtime;

namespace HumanFortress.Runtime.Commands;

internal sealed partial class UpdateWorkshopQueueCommand
{
    void ICommand.Execute(ISimulationContext context)
    {
        var runtimeContext = RuntimeCommandContext.Require<IRuntimeWorkshopCommandTargetContext>(context, CommandType);
        var target = runtimeContext.Workshops;

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

    byte[] ICommand.Serialize() => Array.Empty<byte>();
}
