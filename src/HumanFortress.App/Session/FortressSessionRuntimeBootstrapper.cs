using HumanFortress.App.Input;
using HumanFortress.App.Rendering;
using HumanFortress.App.UI;

namespace HumanFortress.App.Session;

internal static class FortressSessionRuntimeBootstrapper
{
    internal static FortressSessionRuntimeBindings Configure(
        FortressSessionRuntimePorts runtime,
        UiStore ui,
        Func<ulong> uiTickProvider,
        bool autoDig,
        int currentZ,
        InputBindingsService bindings,
        OrdersRegistryService ordersRegistry,
        string baseDir)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(uiTickProvider);
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(ordersRegistry);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDir);

        BindWorkshopCompletionNotifications(runtime, ui, uiTickProvider);

        Logger.Log("[GenerateFortressMap] Creating NavigationOverlay");
        var navigationOverlay = new NavigationOverlay();

        if (autoDig)
        {
            runtime.EnqueueStartupAutoDig(currentZ);
        }

        var uiServices = FortressUiServicesFactory.Create(
            bindings,
            ordersRegistry,
            baseDir);

        return new FortressSessionRuntimeBindings(
            navigationOverlay,
            uiServices);
    }

    private static void BindWorkshopCompletionNotifications(
        FortressSessionRuntimePorts runtime,
        UiStore ui,
        Func<ulong> uiTickProvider)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        // This handler is App/UI-owned, but the notifier and job-system sink are Runtime-owned.
        runtime.SetWorkshopCompletionHandler(notification =>
        {
            try
            {
                var uiTick = uiTickProvider();
                ui.AddHighlight(
                    "workshop:complete",
                    notification.Footprint,
                    notification.ChunkZ,
                    notification.ChunkZ,
                    uiTick + 60);
                var message =
                    $"[BUILD] {notification.ConstructionId} completed at " +
                    $"({notification.ChunkX},{notification.ChunkY},{notification.ChunkZ})";
                ui.AddToast(message, uiTick + 180);
            }
            catch
            {
            }
        });
    }
}
