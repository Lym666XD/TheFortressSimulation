using HumanFortress.App.Input;
using HumanFortress.App.Jobs;
using HumanFortress.App.Rendering;
using HumanFortress.App.UI;
using HumanFortress.Navigation;
using HumanFortress.Simulation.World;

namespace HumanFortress.App.Runtime;

internal static class FortressSessionRuntimeBootstrapper
{
    public static FortressSessionRuntimeBindings Configure(
        World world,
        NavigationManager? navigationManager,
        FortressRuntimeAccess runtime,
        UiStore ui,
        Func<ulong> uiTickProvider,
        bool autoDig,
        int currentZ,
        InputBindingsService bindings,
        OrdersRegistryService ordersRegistry,
        string baseDir)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(uiTickProvider);
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(ordersRegistry);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDir);

        BindWorkshopCompletionNotifications(ui, uiTickProvider);

        Logger.Log("[GenerateFortressMap] Creating NavigationOverlay");
        var navigationOverlay = new NavigationOverlay(runtime.NavigationTuning);
        if (navigationManager != null)
        {
            navigationOverlay.SetNavigationManager(navigationManager);
        }

        if (autoDig)
        {
            FortressAutoDigBootstrapper.EnqueueAfterWorldFill(world, runtime, currentZ);
        }

        var uiServices = FortressUiServicesFactory.Create(
            world,
            bindings,
            ordersRegistry,
            baseDir);

        return new FortressSessionRuntimeBindings(
            navigationOverlay,
            LoadOverlayFromSnapshot(baseDir),
            uiServices);
    }

    private static void BindWorkshopCompletionNotifications(UiStore ui, Func<ulong> uiTickProvider)
    {
        ConstructionJobSystem.UiNotifyWorkshopComplete = (x, y, z, rect, id, simTick) =>
        {
            try
            {
                var uiTick = uiTickProvider();
                ui.AddHighlight("workshop:complete", rect, z, z, uiTick + 60);
                ui.AddToast($"[BUILD] {id} completed at ({x},{y},{z})", uiTick + 180);
            }
            catch
            {
            }
        };
    }

    private static bool LoadOverlayFromSnapshot(string baseDir)
    {
        try
        {
            string path = System.IO.Path.Combine(baseDir, "configs", "game_config.txt");
            if (!System.IO.File.Exists(path))
                return false;

            var lines = System.IO.File.ReadAllLines(path);
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.StartsWith('#') || line.Length == 0)
                    continue;

                int eq = line.IndexOf('=');
                if (eq <= 0)
                    continue;

                var key = line.Substring(0, eq).Trim();
                var val = line.Substring(eq + 1).Trim();
                if (key.Equals("overlay_from_snapshot", StringComparison.OrdinalIgnoreCase))
                {
                    return val.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                           val == "1" ||
                           val.Equals("yes", StringComparison.OrdinalIgnoreCase);
                }
            }
        }
        catch
        {
        }

        return false;
    }
}
