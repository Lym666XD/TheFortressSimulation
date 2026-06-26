using HumanFortress.Core.Commands;
using HumanFortress.Core.Simulation;
using HumanFortress.Runtime;

namespace HumanFortress.Runtime.Commands;

internal sealed class SetProfessionWeightCommand : ICommand
{
    private readonly Guid _workerId;
    private readonly string _professionId;
    private readonly int _weight;

    internal SetProfessionWeightCommand(ulong tick, Guid workerId, string professionId, int weight)
    {
        Tick = tick;
        _workerId = workerId;
        _professionId = professionId ?? throw new ArgumentNullException(nameof(professionId));
        _weight = weight;
    }

    internal ulong Tick { get; }
    private Guid CommandId { get; } = Guid.NewGuid();
    private string CommandType => "professions.set_weight";

    ulong ICommand.Tick => Tick;
    Guid ICommand.CommandId => CommandId;
    string ICommand.CommandType => CommandType;

    void ICommand.Execute(ISimulationContext context)
    {
        var runtimeContext = RuntimeCommandContext.Require<IRuntimeProfessionCommandTargetContext>(context, CommandType);

        runtimeContext.Professions.SetProfessionWeight(_workerId, _professionId, _weight);
    }

    byte[] ICommand.Serialize()
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(_workerId.ToByteArray());
        bw.Write(_professionId);
        bw.Write(_weight);
        return ms.ToArray();
    }
}
