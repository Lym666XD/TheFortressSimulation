using HumanFortress.Core.Time;
using HumanFortress.Jobs.Configuration;

namespace HumanFortress.Jobs.Orchestration;

/// <summary>
/// Unified orchestrator for the Jobs stage.
/// </summary>
internal sealed class UnifiedJobsOrchestrator : ITick
{
    private readonly ISequentialCompatibilityStage _haulPlanner;
    private readonly ISequentialCompatibilityStage? _constructionMaterialsPlanner;
    private readonly ISequentialCompatibilityStage _miningPlanner;
    private readonly ISequentialCompatibilityStage _constructionPlanner;
    private readonly ISequentialCompatibilityStage? _craftPlanner;

    private readonly IUnifiedTransportJobExecutor _haulJobs;
    private readonly IUnifiedMiningJobExecutor _miningJobs;
    private readonly IUnifiedConstructionJobExecutor _constructionJobs;
    private readonly IUnifiedCraftJobExecutor? _craftJobs;

    private readonly SchedulerTunings _tunings;
    private readonly Action<string>? _log;
    private readonly IReadOnlyList<IReadPlanStage> _readPlanStages;

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
        ISequentialCompatibilityStage haulPlanner,
        ISequentialCompatibilityStage? constructionMaterialsPlanner,
        ISequentialCompatibilityStage miningPlanner,
        ISequentialCompatibilityStage constructionPlanner,
        ISequentialCompatibilityStage? craftPlanner,
        IUnifiedTransportJobExecutor haulJobs,
        IUnifiedMiningJobExecutor miningJobs,
        IUnifiedConstructionJobExecutor constructionJobs,
        IUnifiedCraftJobExecutor? craftJobs,
        SchedulerTunings tunings,
        Action<string>? log = null,
        IReadOnlyList<IReadPlanStage>? readPlanStages = null)
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
        _readPlanStages = readPlanStages ?? Array.Empty<IReadPlanStage>();
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
        foreach (var stage in _readPlanStages)
        {
            stage.ReadPlan(tick);
        }
    }

    internal void WriteTick(ulong tick)
    {
        // These legacy planner families still consume queues and mutate authority.
        // Keep their old two-pass order, but execute both passes in serialized Write.
        _miningPlanner.PrepareSequentialCompatibility(tick);
        _haulPlanner.PrepareSequentialCompatibility(tick);
        _constructionMaterialsPlanner?.PrepareSequentialCompatibility(tick);
        _constructionPlanner.PrepareSequentialCompatibility(tick);
        _craftPlanner?.PrepareSequentialCompatibility(tick);

        _miningPlanner.ApplySequentialCompatibility(tick);
        _haulPlanner.ApplySequentialCompatibility(tick);
        _constructionMaterialsPlanner?.ApplySequentialCompatibility(tick);
        _constructionPlanner.ApplySequentialCompatibility(tick);
        _craftPlanner?.ApplySequentialCompatibility(tick);

        var haulPlannerApplyStageCount = 1;
        var miningPlannerApplyStageCount = 1;
        var constructionPlannerApplyStageCount = _constructionMaterialsPlanner == null ? 1 : 2;
        var craftPlannerApplyStageCount = _craftPlanner == null ? 0 : 1;

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
        _haulJobs.PrepareSequentialCompatibility(tick);
        var haulIntake = _haulJobs.LastIntakeCount;
        _haulJobs.ApplySequentialCompatibility(tick);
        var haulApplyStageCount = haulPlannerApplyStageCount + 1;

        _miningJobs.PrepareSequentialCompatibility(tick);
        var miningIntake = _miningJobs.LastIntakeCount;
        _miningJobs.ApplySequentialCompatibility(tick);
        var miningApplyStageCount = miningPlannerApplyStageCount + 1;

        _constructionJobs.PrepareSequentialCompatibility(tick);
        var constructionIntake = _constructionJobs.LastIntakeCount;
        _constructionJobs.ApplySequentialCompatibility(tick);
        var constructionApplyStageCount = constructionPlannerApplyStageCount + 1;

        int craftIntake = 0;
        int craftApplyStageCount = 0;
        if (_craftJobs != null)
        {
            _craftJobs.PrepareSequentialCompatibility(tick);
            craftIntake = _craftJobs.LastIntakeCount;
            _craftJobs.ApplySequentialCompatibility(tick);
            craftApplyStageCount = craftPlannerApplyStageCount + 1;
        }

        var applyStageCount = haulApplyStageCount
            + miningApplyStageCount
            + constructionApplyStageCount
            + craftApplyStageCount;

        var planStageCount = _readPlanStages.Count;
        _log?.Invoke($"[SCHED][{tick}] PurePlan: total={planStageCount}");
        _log?.Invoke($"[SCHED][{tick}] SequentialCompatibilityApply: haul(intake={haulIntake})={haulApplyStageCount} mining(intake={miningIntake})={miningApplyStageCount} construction(intake={constructionIntake})={constructionApplyStageCount} craft(intake={craftIntake})={craftApplyStageCount} total={applyStageCount}");

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
            Tick = tick,
            PlanStageCount = planStageCount,
            ApplyStageCount = applyStageCount,
            IntakeHaul = haulIntake,
            IntakeMining = miningIntake,
            IntakeConstruction = constructionIntake,
            IntakeCraft = craftIntake,
            HaulPlanStageCount = 0,
            MiningPlanStageCount = 0,
            ConstructionPlanStageCount = 0,
            CraftPlanStageCount = 0,
            HaulApplyStageCount = haulApplyStageCount,
            MiningApplyStageCount = miningApplyStageCount,
            ConstructionApplyStageCount = constructionApplyStageCount,
            CraftApplyStageCount = craftApplyStageCount
        };
    }
}
