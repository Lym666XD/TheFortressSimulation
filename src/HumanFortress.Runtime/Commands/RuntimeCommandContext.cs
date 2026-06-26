using HumanFortress.Core.Commands;

namespace HumanFortress.Runtime.Commands;

internal static class RuntimeCommandContext
{
    internal static T Require<T>(ISimulationContext context, string commandType)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(context);

        return context as T
            ?? throw new InvalidOperationException(
                $"Command '{commandType}' requires runtime context role '{typeof(T).Name}'.");
    }
}
