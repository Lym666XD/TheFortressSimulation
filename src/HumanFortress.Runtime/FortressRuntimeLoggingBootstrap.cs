namespace HumanFortress.Runtime;

/// <summary>
/// Public bootstrap entry for App-owned logger callback binding.
/// Runtime keeps the lower-layer static callback target list internal.
/// </summary>
public static class FortressRuntimeLoggingBootstrap
{
    public static void BindStaticCallbacks(Func<string, Action<string>> callbackFactory)
    {
        FortressRuntimeLogBindings.BindStaticCallbacks(callbackFactory);
    }
}
