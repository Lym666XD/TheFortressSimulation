using HumanFortress.Content.Loading;
using HumanFortress.Content.Registry;
using HumanFortress.Contracts.Content.Loading;
using HumanFortress.Contracts.Diagnostics;

namespace HumanFortress.Runtime;

public static class FortressRuntimeContentLoader
{
    public static FortressContentLoadReport LoadStartupContent(string baseDir)
    {
        ContentRegistryDiagnostics.DiagnosticSink = DiagnosticHub.Sink;
        return FortressContentLoader
            .Load(baseDir, includeCoreCatalogs: false)
            .ToReport();
    }

    public static ContentFileResolution ResolveRegistryFile(string baseDir, string fileName)
    {
        return FortressContentLoader.ResolveRegistryFile(baseDir, fileName);
    }
}
