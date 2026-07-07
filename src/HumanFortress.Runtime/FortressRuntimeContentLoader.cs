using HumanFortress.Content.Loading;
using HumanFortress.Contracts.Content.Loading;

namespace HumanFortress.Runtime;

public static class FortressRuntimeContentLoader
{
    public static FortressContentLoadReport LoadStartupContent(string baseDir)
    {
        return FortressContentLoader
            .Load(baseDir, includeCoreCatalogs: false)
            .ToReport();
    }

    public static ContentFileResolution ResolveRegistryFile(string baseDir, string fileName)
    {
        return FortressContentLoader.ResolveRegistryFile(baseDir, fileName);
    }
}
