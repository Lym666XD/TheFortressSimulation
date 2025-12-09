using SadConsole;
using SadRogue.Primitives;
using System.Linq;

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
            // Inline Work drawer now renders scheduler stats directly; keep this overlay dormant to avoid double-drawing.
            return;

            var surf = overlay.Surface;
            int height = System.Math.Max(8, (int)(surf.Height * 0.7));
            int y0 = surf.Height - 1 - height; // drawer top row

            // Right-aligned block, 3-4 lines
            int xStart = System.Math.Max(2, surf.Width - 56);

            var gsm = HumanFortress.App.GameStates.GameStateManager.Instance;
            var schedOpt = gsm.JobsOrchestrator?.GetLastStats();
            var debug = gsm.GetJobsDebugData(tick);
            bool debugOn = debug.HasValue && debug.Value.Tunings != null && debug.Value.Tunings.DebugPanel;

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

            // Optional debug block (peek + active jobs)
            if (debugOn && debug.HasValue)
            {
                int y = y0 + 7;
                var dbg = debug.Value;
                if (dbg.Transport.HasValue)
                {
                    var tdbg = dbg.Transport.Value;
                    surf.Print(xStart, y++, "[HAUL DEBUG] Active (max 5):", Color.LightCyan);
                    if (tdbg.AllowedActive >= 0 || tdbg.ReservedSlots > 0)
                    {
                        string limitStr = tdbg.AllowedActive >= 0 ? tdbg.AllowedActive.ToString() : "unlimited";
                        surf.Print(xStart, y++, $"  IntakeCap:{tdbg.IntakeBudget} MaxActive:{limitStr} Reserve:{tdbg.ReservedSlots}", Color.DarkCyan);
                    }
                    foreach (var aj in tdbg.Active.Take(5))
                    {
                        var w = aj.CreatureId.ToString("N")[..6];
                        var it = aj.ItemId.ToString("N")[..6];
                        var seedStr = tdbg.SeedsIncluded ? $" seed={aj.Seed}" : "";
                        surf.Print(xStart, y++, $"  W:{w} It:{it} {aj.Stage} {aj.FromOrCurrent.X},{aj.FromOrCurrent.Y},{aj.FromOrCurrent.Z}->{aj.Dest.X},{aj.Dest.Y},{aj.Dest.Z}{seedStr}", Color.Gray);
                    }
                    if (tdbg.PendingPeek.Count > 0)
                    {
                        surf.Print(xStart, y++, $"  Peek({tdbg.PendingPeek.Count}):", Color.LightCyan);
                        foreach (var rq in tdbg.PendingPeek.Take(5))
                        {
                            var reqId = rq.RequestorId.Length > 6 ? rq.RequestorId.Substring(0, 6) : rq.RequestorId;
                            surf.Print(xStart, y++, $"   {reqId} {rq.Reason} {rq.Priority} {rq.From.X},{rq.From.Y},{rq.FromZ}->{rq.To.X},{rq.To.Y},{rq.ToZ}", Color.DarkGray);
                        }
                    }
                    if (tdbg.ShardCounts.Count > 0)
                    {
                        var shardLine = string.Join(" ", tdbg.ShardCounts.Take(6).Select(kv => $"{kv.Key}:{kv.Value}"));
                        surf.Print(xStart, y++, $"  Shards: {shardLine}", Color.DarkCyan);
                    }
                }

                if (debug.Value.Mining.HasValue)
                {
                    var mdbg = debug.Value.Mining.Value;
                    surf.Print(xStart, y++, "[MINING DEBUG] Active (max 5):", Color.LightCyan);
                    foreach (var aj in mdbg.Active.Take(5))
                    {
                        var w = aj.WorkerId.ToString("N")[..6];
                        var seedStr = mdbg.SeedsIncluded ? $" seed={aj.Seed}" : "";
                        surf.Print(xStart, y++, $"  W:{w} {aj.Stage} {aj.Target.X},{aj.Target.Y},{aj.Z} adj=({aj.Adjacent.X},{aj.Adjacent.Y}) prog={aj.ProgressTicks}/{aj.RequiredTicks}{seedStr}", Color.Gray);
                    }
                }
            }
        }
    }
}
