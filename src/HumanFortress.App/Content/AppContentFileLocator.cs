using HumanFortress.Contracts.Content.Loading;
using HumanFortress.Runtime;

namespace HumanFortress.App.Content;

internal static class AppContentFileLocator
{
    internal static ContentFileResolution ResolveRegistryFile(string baseDir, string fileName)
    {
        return FortressRuntimeContentLoader.ResolveRegistryFile(baseDir, fileName);
    }
}
