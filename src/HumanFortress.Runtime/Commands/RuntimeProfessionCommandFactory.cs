using HumanFortress.Core.Commands;

namespace HumanFortress.Runtime.Commands;

internal static class RuntimeProfessionCommandFactory
{
    internal static Func<ulong, ICommand> SetProfessionWeight(
        Guid workerId,
        string professionId,
        int weight)
    {
        return tick => new SetProfessionWeightCommand(
            tick,
            workerId,
            professionId,
            weight);
    }
}
