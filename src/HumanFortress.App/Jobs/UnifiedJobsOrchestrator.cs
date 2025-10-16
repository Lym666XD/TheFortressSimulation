using System.Diagnostics;
using HumanFortress.Core.Time;

namespace HumanFortress.App.Jobs
{
    /// <summary>
    /// Unified orchestrator for the Jobs stage (v1: single-threaded, no behavior changes).
    /// - Read: run planners (Hauling/Mining/Construction) in deterministic order.
    /// - Write: flush planners, then run executors' Read/Write back-to-back.
    /// Logs per-subsystem timings and intake counts.
    /// </summary>
    public sealed class UnifiedJobsOrchestrator : ITick
    {
        private readonly HumanFortress.Simulation.Orders.HaulingSystem _haulPlanner;
        private readonly HumanFortress.Simulation.Orders.MiningSystem _miningPlanner;
        private readonly HumanFortress.Simulation.Orders.ConstructionSystem _constructionPlanner;

        private readonly HaulJobSystem _haulJobs;
        private readonly MiningJobSystem _miningJobs;
        private readonly ConstructionJobSystem _constructionJobs;

        private readonly SchedulerTunings _tunings;

        private readonly Stopwatch _sw = new();

        public record struct LastStats(
            ulong Tick,
            long PlanMsTotal,
            long ApplyMsTotal,
            int IntakeHaul,
            int IntakeMining,
            int IntakeConstruction,
            long HaulPlanMs,
            long MiningPlanMs,
            long ConstructionPlanMs,
            long HaulApplyMs,
            long MiningApplyMs,
            long ConstructionApplyMs
        );

        private LastStats _last;
        public LastStats GetLastStats() => _last;

        public UnifiedJobsOrchestrator(
            HumanFortress.Simulation.Orders.HaulingSystem haulPlanner,
            HumanFortress.Simulation.Orders.MiningSystem miningPlanner,
            HumanFortress.Simulation.Orders.ConstructionSystem constructionPlanner,
            HaulJobSystem haulJobs,
            MiningJobSystem miningJobs,
            ConstructionJobSystem constructionJobs,
            SchedulerTunings tunings)
        {
            _haulPlanner = haulPlanner;
            _miningPlanner = miningPlanner;
            _constructionPlanner = constructionPlanner;
            _haulJobs = haulJobs;
            _miningJobs = miningJobs;
            _constructionJobs = constructionJobs;
            _tunings = tunings;
        }

        public int Priority => HumanFortress.Core.Simulation.UpdateOrder.Priority.Items; // runs early; orchestrates Jobs inside
        public string SystemId => "Jobs.UnifiedOrchestrator";

        public void ReadTick(ulong tick)
        {
            // Plan (single-threaded, deterministic order)
            _sw.Restart();
            var t0 = _sw.ElapsedMilliseconds;
            _miningPlanner.ReadTick(tick); // keep relative order (mining first if planning more expensive)
            var tMining = _sw.ElapsedMilliseconds;
            _haulPlanner.ReadTick(tick);
            var tHaul = _sw.ElapsedMilliseconds;
            _constructionPlanner.ReadTick(tick);
            var tConstr = _sw.ElapsedMilliseconds;
            var planMs = tConstr - t0;

            Logger.Log($"[SCHED][{tick}] Plan: mining={tMining - t0}ms haul={tHaul - tMining}ms construction={tConstr - tHaul}ms total={planMs}ms");

            // Store partial; Apply stats filled in WriteTick
            _last = _last with
            {
                Tick = tick,
                PlanMsTotal = planMs,
                HaulPlanMs = (tHaul - tMining),
                MiningPlanMs = (tMining - t0),
                ConstructionPlanMs = (tConstr - tHaul)
            };
        }

        public void WriteTick(ulong tick)
        {
            // Flush planners to their outboxes
            var start = Stopwatch.GetTimestamp();
            _miningPlanner.WriteTick(tick);
            _haulPlanner.WriteTick(tick);
            _constructionPlanner.WriteTick(tick);

            // Run executors: Read then Write back-to-back to preserve behavior
            var sw = Stopwatch.StartNew();

            _haulJobs.ReadTick(tick);
            var haulIntake = _haulJobs.LastIntakeCount;
            _haulJobs.WriteTick(tick);
            var haulApplyMs = sw.ElapsedMilliseconds;

            sw.Restart();
            _miningJobs.ReadTick(tick);
            var miningIntake = _miningJobs.LastIntakeCount;
            _miningJobs.WriteTick(tick);
            var miningApplyMs = sw.ElapsedMilliseconds;

            sw.Restart();
            _constructionJobs.ReadTick(tick);
            var constrIntake = _constructionJobs.LastIntakeCount;
            _constructionJobs.WriteTick(tick);
            var constrApplyMs = sw.ElapsedMilliseconds;

            var applyMs = (haulApplyMs + miningApplyMs + constrApplyMs);

            Logger.Log($"[SCHED][{tick}] Apply: haul(intake={haulIntake})={haulApplyMs}ms mining(intake={miningIntake})={miningApplyMs}ms construction(intake={constrIntake})={constrApplyMs}ms total={applyMs}ms");

            // Per-job stats snapshot (v1.1)
            try
            {
                var h = _haulJobs.GetLastStatsSnapshot();
                var m = _miningJobs.GetLastStatsSnapshot();
                Logger.Log($"[SCHED][{tick}] [HAUL] active={h.Active} backlog={h.Backlog} carry={h.CarryoverOld} +done={h.CompletedDelta} +req={h.RequeuedDelta} +nopath={h.NoPathDelta}");
                Logger.Log($"[SCHED][{tick}] [MINE] active={m.Active} backlog={m.Backlog} deferred={m.Deferred} reserved={m.ReservedTiles} carry={m.CarryoverOld}");
            }
            catch { }

            _last = _last with
            {
                ApplyMsTotal = applyMs,
                IntakeHaul = haulIntake,
                IntakeMining = miningIntake,
                IntakeConstruction = constrIntake,
                HaulApplyMs = haulApplyMs,
                MiningApplyMs = miningApplyMs,
                ConstructionApplyMs = constrApplyMs
            };
        }
    }
}
