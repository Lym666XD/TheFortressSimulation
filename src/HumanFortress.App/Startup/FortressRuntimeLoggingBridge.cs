using HumanFortress.Runtime;

namespace HumanFortress.App.Startup;

internal static class FortressRuntimeLoggingBridge
{
    internal static void BindStaticCallbacks(Func<string, Action<string>> createLogCallback)
    {
        ArgumentNullException.ThrowIfNull(createLogCallback);

        FortressRuntimeLoggingBootstrap.BindStaticCallbacks(createLogCallback);
    }
}
