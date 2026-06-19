using HumanFortress.Core.Commands;
using HumanFortress.Core.Simulation;
using HumanFortress.Runtime;

namespace HumanFortress.Runtime.Commands;

public sealed class SetProfessionWeightCommand : ICommand
{
    private readonly Guid _workerId;
    private readonly string _professionId;
    private readonly int _weight;

    public SetProfessionWeightCommand(ulong tick, Guid workerId, string professionId, int weight)
    {
        Tick = tick;
        _workerId = workerId;
        _professionId = professionId ?? throw new ArgumentNullException(nameof(professionId));
        _weight = weight;
    }

    public ulong Tick { get; }
    public Guid CommandId { get; } = Guid.NewGuid();
    public string CommandType => "professions.set_weight";

    public void Execute(ISimulationContext context)
    {
        if (context is IProfessionAssignmentCommandTarget target)
        {
            target.SetProfessionWeight(_workerId, _professionId, _weight);
        }
    }

    public byte[] Serialize()
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(_workerId.ToByteArray());
        bw.Write(_professionId);
        bw.Write(_weight);
        return ms.ToArray();
    }
}
