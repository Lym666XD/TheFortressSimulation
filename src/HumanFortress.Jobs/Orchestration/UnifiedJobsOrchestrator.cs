using HumanFortress.Core.Time;
using HumanFortress.Jobs.Configuration;

namespace HumanFortress.Jobs.Orchestration;

/// <summary>
/// Unified orchestrator for the Jobs stage.
/// </summary>
internal sealed class UnifiedJobsOrchestrator : ITick
{
    private readonly ITick _haulPlanner;
    private readonly ITick? _constructionMaterialsPlanner;
    private readonly ITick _miningPlanner;
    private readonly ITick _constructionPlanner;
    private readonly ITick? _craftPlanner;

    private readonly IUnifiedTransportJobExecutor _haulJobs;
    private readonly IUnifiedMiningJobExecutor _miningJobs;
    private readonly IUnifiedConstructionJobExecutor _constructionJobs;
    private readonly IUnifiedCraftJobExecutor? _craftJobs;

    private readonly SchedulerTunings _tunings;
    private readonly Action<string>? _log;

    internal record struct LastStats(
        ulong Tick,
        int PlanStageCount,
        int ApplyStageCount,
        int IntakeHaul,
        int IntakeMining,
        int IntakeConstruction,
        int IntakeCraft,
        int HaulPlanStageCount,
        int MiningPlanStageCount,
        int ConstructionPlanStageCount,
        int CraftPlanStageCount,
        int HaulApplyStageCount,
        int MiningApplyStageCount,
        int ConstructionApplyStageCount,
        int CraftApplyStageCount);

    private LastStats _last;

    internal UnifiedJobsOrchestrator(
        ITick haulPlanner,
        ITick? constructionMaterialsPlanner,
        ITick miningPlanner,
        ITick constructionPlanner,
        ITick? craftPlanner,
        IUnifiedTransportJobExecutor haulJobs,
        IUnifiedMiningJobExecutor miningJobs,
        IUnifiedConstructionJobExecutor constructionJobs,
        IUnifiedCraftJobExecutor? craftJobs,
        SchedulerTunings tunings,
        Action<string>? log = null)
    {
        _haulPlanner = haulPlanner;
        _constructionMaterialsPlanner = constructionMaterialsPlanner;
        _miningPlanner = miningPlanner;
        _constructionPlanner = constructionPlanner;
        _craftPlanner = craftPlanner;
        _haulJobs = haulJobs;
        _miningJobs = miningJobs;
        _constructionJobs = constructionJobs;
        _craftJobs = craftJobs;
        _tunings = tunings;
        _log = log;
    }

    internal int Priority => HumanFortress.Core.Simulation.UpdateOrder.Priority.Items;

    internal string SystemId => "Jobs.UnifiedOrchestrator";

    internal LastStats GetLastStats() => _last;

    int ITick.Priority => Priority;

    string ITick.SystemId => SystemId;

    void ITick.ReadTick(ulong tick) => ReadTick(tick);

    void ITick.WriteTick(ulong tick) => WriteTick(tick);

    internal void ReadTick(ulong tick)
    {
        _miningPlanner.ReadTick(tick);
        _haulPlanner.ReadTick(tick);
        _constructionMaterialsPlanner?.ReadTick(tick);
        _constructionPlanner.ReadTick(tick);
        _craftPlanner?.ReadTick(tick);
        var haulPlanStageCount = 1;
        var miningPlanStageCount = 1;
        var constructionPlanStageCount = _constructionMaterialsPlanner == null ? 1 : 2;
        var craftPlanStageCount = _craftPlanner == null ? 0 : 1;
        var planStageCount = haulPlanStageCount
            + miningPlanStageCount
            + constructionPlanStageCount
            + craftPlanStageCount;

        _log?.Invoke($"[SCHED][{tick}] Plan: mining={miningPlanStageCount} haul={haulPlanStageCount} construction={constructionPlanStageCount} craft={craftPlanStageCount} total={planStageCount}");

        _last = _last with
        {
            Tick = tick,
            PlanStageCount = planStageCount,
            HaulPlanStageCount = haulPlanStageCount,
            MiningPlanStageCount = miningPlanStageCount,
            ConstructionPlanStageCount = constructionPlanStageCount,
            CraftPlanStageCount = craftPlanStageCount
        };
    }

    internal void WriteTick(ulong tick)
    {
        _miningPlanner.WriteTick(tick);
        _haulPlanner.WriteTick(tick);
        _constructionMaterialsPlanner?.WriteTick(tick);
        _constructionPlanner.WriteTick(tick);
        _craftPlanner?.WriteTick(tick);

        var hLimits = _tunings.HaulingLimits;
        int miningBacklog = _miningJobs.GetBacklogCount();
        int reserve = 0;
        int? intakeHint = null;
        if (hLimits.ReserveForMining > 0 && miningBacklog >= Math.Max(1, hLimits.ReserveBacklogThreshold))
        {
            reserve = hLimits.ReserveForMining;
        }

        if (hLimits.BacklogIntakeCap > 0 && miningBacklog >= Math.Max(1, hLimits.BacklogIntakeThreshold))
        {
            intakeHint = hLimits.BacklogIntakeCap;
        }

        _haulJobs.ApplySchedulingHints(intakeHint, null, reserve);
        _haulJobs.ReadTick(tick);
        var haulIntake = _haulJobs.LastIntakeCount;
        _haulJobs.WriteTick(tick);
        var haulApplyStageCount = 1;

        _miningJobs.ReadTick(tick);
        var miningIntake = _miningJobs.LastIntakeCount;
        _miningJobs.WriteTick(tick);
        var miningApplyStageCount = 1;

        _constructionJobs.ReadTick(tick);
        var constructionIntake = _constructionJobs.LastIntakeCount;
        _constructionJobs.WriteTick(tick);
        var constructionApplyStageCount = 1;

        int craftIntake = 0;
        int craftApplyStageCount = 0;
        if (_craftJobs != null)
        {
            _craftJobs.ReadTick(tick);
            craftIntake = _craftJobs.LastIntakeCount;
            _craftJobs.WriteTick(tick);
            craftApplyStageCount = 1;
        }

        var applyStageCount = haulApplyStageCount
            + miningApplyStageCount
            + constructionApplyStageCount
            + craftApplyStageCount;

        _log?.Invoke($"[SCHED][{tick}] Apply: haul(intake={haulIntake})={haulApplyStageCount} mining(intake={miningIntake})={miningApplyStageCount} construction(intake={constructionIntake})={constructionApplyStageCount} craft(intake={craftIntake})={craftApplyStageCount} total={applyStageCount}");

        try
        {
            var haul = _haulJobs.GetLastStatsSnapshot();
            var mining = _miningJobs.GetLastStatsSnapshot();
            _log?.Invoke($"[SCHED][{tick}] [HAUL] active={haul.Active} backlog={haul.Backlog} carry={haul.CarryoverOld} +done={haul.CompletedDelta} +req={haul.RequeuedDelta} +nopath={haul.NoPathDelta}");
            _log?.Invoke($"[SCHED][{tick}] [MINE] active={mining.Active} backlog={mining.Backlog} deferred={mining.Deferred} reserved={mining.ReservedTiles} carry={mining.CarryoverOld}");
        }
        catch
        {
            // Diagnostics are best-effort; scheduling behavior must not depend on stats formatting.
        }

        _last = _last with
        {
            ApplyStageCount = applyStageCount,
            IntakeHaul = haulIntake,
            IntakeMining = miningIntake,
            IntakeConstruction = constructionIntake,
            IntakeCraft = craftIntake,
            HaulApplyStageCount = haulApplyStageCount,
            MiningApplyStageCount = miningApplyStageCount,
            ConstructionApplyStageCount = constructionApplyStageCount,
            CraftApplyStageCount = craftApplyStageCount
        };
    }
}
