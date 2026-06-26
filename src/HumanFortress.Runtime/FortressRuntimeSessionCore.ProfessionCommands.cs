using HumanFortress.Runtime.Commands;

namespace HumanFortress.Runtime;

internal sealed partial class FortressRuntimeSessionCore
{
    void IFortressRuntimeSessionProfessionCommandPort.SetProfessionWeight(Guid workerId, string professionId, int weight)
    {
        EnqueueCurrentTickCommand(RuntimeProfessionCommandFactory.SetProfessionWeight(workerId, professionId, weight));
    }
}
