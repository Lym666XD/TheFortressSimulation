using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;
using System.Linq;

namespace HumanFortress.App.UI;

internal static partial class UiWorkDrawerRenderer
{
    private static void RenderSchedulerColumn(ICellSurface surf, Rectangle area, string caption, SimulationWorkDrawerData work)
    {
        var jobs = work.Jobs;
        var sched = jobs?.Scheduler;
        var haulStats = jobs?.Transport;
        var miningStats = jobs?.Mining;
        var craftStats = jobs?.Craft;

        int line = area.Y;
        surf.Print(area.X + 1, line++, caption, Color.Yellow);
        if (sched.HasValue)
        {
            var s = sched.Value;
            surf.Print(area.X + 1, line++, $"Plan: {s.PlanMsTotal} ms  Apply: {s.ApplyMsTotal} ms", Color.Cyan);
            surf.Print(area.X + 1, line++, $"Intake H:{s.IntakeHaul} M:{s.IntakeMining} C:{s.IntakeConstruction} Cr:{s.IntakeCraft}", Color.Gray);
            line++;
        }

        if (haulStats.HasValue)
        {
            var hs = haulStats.Value;
            surf.Print(area.X + 1, line++, $"[Haul] Active:{hs.Active} Backlog:{hs.Backlog}", Color.White);
            surf.Print(area.X + 1, line++, $"Carry:{hs.CarryoverOld} +Done:{hs.CompletedDelta} +Retry:{hs.RequeuedDelta}", Color.DarkGray);
        }

        if (miningStats.HasValue)
        {
            var ms = miningStats.Value;
            surf.Print(area.X + 1, line++, $"[Mine] Active:{ms.Active} Backlog:{ms.Backlog} Deferred:{ms.Deferred}", Color.White);
            surf.Print(area.X + 1, line++, $"Carry:{ms.CarryoverOld} Reserved:{ms.ReservedTiles}", Color.DarkGray);
        }

        if (craftStats.HasValue)
        {
            var cs = craftStats.Value;
            surf.Print(area.X + 1, line++, $"[Craft] Active:{cs.Active} Backlog:{cs.Backlog}", Color.White);
            surf.Print(area.X + 1, line++, $"Intake:{cs.Intake} +Done:{cs.CompletedDelta}", Color.DarkGray);
        }

        if (jobs.HasValue && jobs.Value.TransportDebug.HasValue)
        {
            var tdbg = jobs.Value.TransportDebug.Value;
            line++;
            surf.Print(area.X + 1, line++, "Queue peek:", Color.LightCyan);
            foreach (var req in tdbg.PendingPeek.Take(2))
            {
                string reason = req.Reason;
                if (string.IsNullOrWhiteSpace(reason)) reason = "Request";
                surf.Print(area.X + 2, line++, $"{reason} -> ({req.To.X},{req.To.Y},{req.To.Z})", Color.Gray);
            }

            if (tdbg.ShardCounts.Count > 0)
            {
                var shardLine = string.Join(" ", tdbg.ShardCounts.Take(3).Select(kv => $"{kv.Key}:{kv.Value}"));
                surf.Print(area.X + 1, line++, $"Shards: {shardLine}", Color.DarkGray);
            }
        }
    }
}
