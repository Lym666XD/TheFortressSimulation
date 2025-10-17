using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI
{
    /// <summary>
    /// Lightweight overlay that augments the Work drawer (F3) with scheduler and queue details
    /// without modifying the core UiRenderer (v1 safe path).
    /// </summary>
    public static class WorkDrawerOverlay
    {
        public static void DrawWorkSchedulerOverlay(
            ScreenSurface overlay,
            UiStore ui,
            ulong tick,
            HumanFortress.Simulation.World.World? world)
        {
            if (world == null) return;
            if (ui.OpenDrawer != DrawerId.Work) return;
            // Tab index 1 = "All Orders" in current UiRenderer
            if (ui.DrawerTab != 1) return;

            var surf = overlay.Surface;
            int height = System.Math.Max(8, (int)(surf.Height * 0.7));
            int y0 = surf.Height - 1 - height; // drawer top row

            // Right-aligned block, 3-4 lines
            int xStart = System.Math.Max(2, surf.Width - 56);

            var gsm = HumanFortress.App.GameStates.GameStateManager.Instance;
            var schedOpt = gsm.JobsOrchestrator?.GetLastStats();

            if (schedOpt.HasValue)
            {
                var s = schedOpt.Value;
                surf.Print(xStart, y0 + 1, $"[SCHED] Plan={s.PlanMsTotal}ms Apply={s.ApplyMsTotal}ms", Color.Cyan);
                surf.Print(xStart, y0 + 2, $"[Intake] Haul:{s.IntakeHaul} Mining:{s.IntakeMining} Constr:{s.IntakeConstruction}", Color.DarkCyan);
            }

            int haulBacklog = gsm.TransportJobs?.GetBacklogCount() ?? 0;
            int miningBacklog = gsm.MiningJobs?.GetBacklogCount() ?? 0;
            int miningDeferred = gsm.MiningJobs?.GetDeferredCount() ?? 0;
            int miningReserved = gsm.MiningJobs?.GetReservedTileCount() ?? 0;

            // Pull per-job snapshots (v1.1)
            var hstats = gsm.TransportJobs?.GetLastStatsSnapshot();
            var mstats = gsm.MiningJobs?.GetLastStatsSnapshot();

            // Display concise queue/backpressure view
            if (hstats.HasValue)
            {
                var hs = hstats.Value;
                surf.Print(xStart, y0 + 3, $"[HAUL] Backlog:{hs.Backlog} Carry:{hs.CarryoverOld} Active:{hs.Active}", Color.Cyan);
                surf.Print(xStart, y0 + 4, $"       +Done:{hs.CompletedDelta} +ReQ:{hs.RequeuedDelta} +NoPath:{hs.NoPathDelta}", Color.DarkCyan);
            }
            else
            {
                surf.Print(xStart, y0 + 3, $"[HAUL] Backlog:{haulBacklog}", Color.Cyan);
            }

            if (mstats.HasValue)
            {
                var ms = mstats.Value;
                surf.Print(xStart, y0 + 5, $"[MINING] Backlog:{ms.Backlog} Deferred:{ms.Deferred} Reserved:{ms.ReservedTiles}", Color.Cyan);
                surf.Print(xStart, y0 + 6, $"         Carry:{ms.CarryoverOld} Active:{ms.Active}", Color.DarkCyan);
            }
            else
            {
                surf.Print(xStart, y0 + 5, $"[MINING] Backlog:{miningBacklog} Deferred:{miningDeferred} Reserved:{miningReserved}", Color.Cyan);
            }
        }
    }
}
