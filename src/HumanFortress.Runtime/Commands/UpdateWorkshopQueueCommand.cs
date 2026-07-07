using System;
using HumanFortress.Core.Commands;

namespace HumanFortress.Runtime.Commands;

/// <summary>
/// Command that mutates a workshop queue or automation flags.
/// Executed on simulation thread for determinism.
/// </summary>
internal sealed partial class UpdateWorkshopQueueCommand : ICommand
{
    internal ulong Tick { get; }
    private Guid CommandId => RuntimeCommandId.Create(this);
    private string CommandType => "workshops.queue.update";

    ulong ICommand.Tick => Tick;
    Guid ICommand.CommandId => CommandId;
    string ICommand.CommandType => CommandType;

    private readonly Guid _workshopGuid;
    private readonly WorkshopQueueOperation _operation;
    private readonly string? _recipeId;
    private readonly Guid? _entryId;
    private readonly int? _intValue;
    private readonly int? _moveOffset;
    private readonly bool? _boolValue;

    internal UpdateWorkshopQueueCommand(
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
}
