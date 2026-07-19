using HumanFortress.Contracts.Diagnostics;
using HumanFortress.Runtime.Diagnostics;

namespace HumanFortress.Runtime.Composition;

internal static class FortressRuntimeLogBindings
{
    internal const string ConstructionMaterialsCategory = "Jobs.ConstructionMaterials";

    internal static void BindStaticCallbacks(Func<string, Action<string>> callbackFactory)
    {
        ArgumentNullException.ThrowIfNull(callbackFactory);

        // Compatibility bootstrap for callers that still create lower-level objects
        // directly. Composed Runtime sessions capture their own sink and never read
        // this process-global fallback after construction.
        DiagnosticHub.Sink = new CallbackFactoryDiagnosticSink(callbackFactory);
    }
}
