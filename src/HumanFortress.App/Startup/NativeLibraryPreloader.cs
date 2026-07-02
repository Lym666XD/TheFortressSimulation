namespace HumanFortress.App.Startup;

internal static class NativeLibraryPreloader
{
    internal static void TryPreload(string fullPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        try
        {
            if (File.Exists(fullPath))
            {
                System.Runtime.InteropServices.NativeLibrary.Load(fullPath);
                Logger.Log($"[NATIVE] Preloaded {fullPath}");
            }
            else
            {
                Logger.Log($"[NATIVE] Missing native library: {fullPath}");
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"[NATIVE] Failed to load {fullPath}: {ex.Message}");
        }
    }
}
