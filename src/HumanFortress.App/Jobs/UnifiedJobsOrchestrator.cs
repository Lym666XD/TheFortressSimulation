using System;
using System.Diagnostics;
using HumanFortress.Core.Time;
using HumanFortress.Jobs.Craft;

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
        private readonly HumanFortress.Simulation.Jobs.ConstructionMaterialsPlanner? _cmPlanner;
        private readonly HumanFortress.Simulation.Orders.MiningSystem _miningPlanner;
        private readonly HumanFortress.Simulation.Orders.ConstructionSystem _constructionPlanner;
        private readonly CraftPlanner? _craftPlanner;

        private readonly TransportJobSystem _haulJobs;
        private readonly MiningJobSystem _miningJobs;
        private readonly ConstructionJobSystem _constructionJobs;
        private readonly CraftJobSystem? _craftJobs;

        private readonly SchedulerTunings _tunings;

        private readonly Stopwatch _sw = new();

        public record struct LastStats(
            ulong Tick,
            long PlanMsTotal,
            long ApplyMsTotal,
            int IntakeHaul,
            int IntakeMining,
            int IntakeConstruction,
            int IntakeCraft,
            long HaulPlanMs,
            long MiningPlanMs,
            long ConstructionPlanMs,
            long CraftPlanMs,
            long HaulApplyMs,
            long MiningApplyMs,
            long ConstructionApplyMs,
            long CraftApplyMs
        );

        private LastStats _last;
        public LastStats GetLastStats() => _last;

        public UnifiedJobsOrchestrator(
            HumanFortress.Simulation.Orders.HaulingSystem haulPlanner,
            HumanFortress.Simulation.Jobs.ConstructionMaterialsPlanner? cmPlanner,
            HumanFortress.Simulation.Orders.MiningSystem miningPlanner,
            HumanFortress.Simulation.Orders.ConstructionSystem constructionPlanner,
            CraftPlanner? craftPlanner,
            TransportJobSystem haulJobs,
            MiningJobSystem miningJobs,
            ConstructionJobSystem constructionJobs,
            CraftJobSystem? craftJobs,
            SchedulerTunings tunings)
        {
            _haulPlanner = haulPlanner;
            _cmPlanner = cmPlanner;
            _miningPlanner = miningPlanner;
            _constructionPlanner = constructionPlanner;
            _craftPlanner = craftPlanner;
            _haulJobs = haulJobs;
            _miningJobs = miningJobs;
            _constructionJobs = constructionJobs;
            _craftJobs = craftJobs;
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
            _cmPlanner?.ReadTick(tick);
            _constructionPlanner.ReadTick(tick);
            var tConstr = _sw.ElapsedMilliseconds;
            _craftPlanner?.ReadTick(tick);
            var tCraft = _sw.ElapsedMilliseconds;
            var planMs = tCraft - t0;

            Logger.Log($"[SCHED][{tick}] Plan: mining={tMining - t0}ms haul={tHaul - tMining}ms construction={tConstr - tHaul}ms craft={tCraft - tConstr}ms total={planMs}ms");

            // Store partial; Apply stats filled in WriteTick
            _last = _last with
            {
                Tick = tick,
                PlanMsTotal = planMs,
                HaulPlanMs = (tHaul - tMining),
                MiningPlanMs = (tMining - t0),
                ConstructionPlanMs = (tConstr - tHaul),
                CraftPlanMs = (tCraft - tConstr)
            };
        }

        public void WriteTick(ulong tick)
        {
            // Flush planners to their outboxes
            var start = Stopwatch.GetTimestamp();
            _miningPlanner.WriteTick(tick);
            _haulPlanner.WriteTick(tick);
            _cmPlanner?.WriteTick(tick);
            _constructionPlanner.WriteTick(tick);
            _craftPlanner?.WriteTick(tick);

            // Run executors: Read then Write back-to-back to preserve behavior
            var sw = Stopwatch.StartNew();

            var hLimits = _tunings.HaulingLimits;
            int miningBacklog = _miningJobs.GetBacklogCount();
            int reserve = 0;
            int? intakeHint = null;
            if (hLimits.ReserveForMining > 0 && miningBacklog >= Math.Max(1, hLimits.ReserveBacklogThreshold))
                reserve = hLimits.ReserveForMining;
            if (hLimits.BacklogIntakeCap > 0 && miningBacklog >= Math.Max(1, hLimits.BacklogIntakeThreshold))
                intakeHint = hLimits.BacklogIntakeCap;
            _haulJobs.ApplySchedulingHints(intakeHint, null, reserve);
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

            sw.Restart();
            int craftIntake = 0;
            long craftApplyMs = 0;
            if (_craftJobs != null)
            {
                _craftJobs.ReadTick(tick);
                craftIntake = _craftJobs.LastIntakeCount;
                _craftJobs.WriteTick(tick);
                craftApplyMs = sw.ElapsedMilliseconds;
            }

            var applyMs = (haulApplyMs + miningApplyMs + constrApplyMs + craftApplyMs);

            Logger.Log($"[SCHED][{tick}] Apply: haul(intake={haulIntake})={haulApplyMs}ms mining(intake={miningIntake})={miningApplyMs}ms construction(intake={constrIntake})={constrApplyMs}ms craft(intake={craftIntake})={craftApplyMs}ms total={applyMs}ms");

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
                IntakeCraft = craftIntake,
                HaulApplyMs = haulApplyMs,
                MiningApplyMs = miningApplyMs,
                ConstructionApplyMs = constrApplyMs,
                CraftApplyMs = craftApplyMs
            };
        }
    }
}
