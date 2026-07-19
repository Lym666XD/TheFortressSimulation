using HumanFortress.Runtime.Composition;

namespace HumanFortress.Runtime;

/// <summary>
/// Compatibility bootstrap for objects created outside Runtime composition.
/// Active Runtime sessions own an independent diagnostic sink.
/// </summary>
public static class FortressRuntimeLoggingBootstrap
{
    public static void BindStaticCallbacks(Func<string, Action<string>> callbackFactory)
    {
        FortressRuntimeLogBindings.BindStaticCallbacks(callbackFactory);
    }
}
