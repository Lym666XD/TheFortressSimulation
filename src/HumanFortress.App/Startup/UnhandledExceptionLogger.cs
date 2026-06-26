namespace HumanFortress.App.Startup;

internal static class UnhandledExceptionLogger
{
    public static void Bind()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            Logger.Log("[FATAL ERROR] Unhandled exception:");
            Logger.Log($"Message: {ex?.Message}");
            Logger.Log($"Stack trace: {ex?.StackTrace}");
            Logger.Log($"Inner exception: {ex?.InnerException?.Message}");
            Logger.Close();
        };
    }
}
