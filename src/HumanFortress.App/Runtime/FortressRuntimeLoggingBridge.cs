using HumanFortress.Runtime;

namespace HumanFortress.App.Runtime;

internal static class FortressRuntimeLoggingBridge
{
    internal static void BindStaticCallbacks(Func<string, Action<string>> createLogCallback)
    {
        ArgumentNullException.ThrowIfNull(createLogCallback);

        FortressRuntimeLoggingBootstrap.BindStaticCallbacks(createLogCallback);
    }
}
