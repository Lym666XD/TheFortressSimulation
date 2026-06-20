using System.Diagnostics;
using HumanFortress.Core.Time;

namespace HumanFortress.Jobs;

/// <summary>
/// Unified orchestrator for the Jobs stage.
/// </summary>
public sealed class UnifiedJobsOrchestrator : ITick
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
        long CraftApplyMs);

    private LastStats _last;

    public UnifiedJobsOrchestrator(
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

    public int Priority => HumanFortress.Core.Simulation.UpdateOrder.Priority.Items;

    public string SystemId => "Jobs.UnifiedOrchestrator";

    public LastStats GetLastStats() => _last;

    public void ReadTick(ulong tick)
    {
        _sw.Restart();
        var t0 = _sw.ElapsedMilliseconds;
        _miningPlanner.ReadTick(tick);
        var tMining = _sw.ElapsedMilliseconds;
        _haulPlanner.ReadTick(tick);
        var tHaul = _sw.ElapsedMilliseconds;
        _constructionMaterialsPlanner?.ReadTick(tick);
        _constructionPlanner.ReadTick(tick);
        var tConstr = _sw.ElapsedMilliseconds;
        _craftPlanner?.ReadTick(tick);
        var tCraft = _sw.ElapsedMilliseconds;
        var planMs = tCraft - t0;

        _log?.Invoke($"[SCHED][{tick}] Plan: mining={tMining - t0}ms haul={tHaul - tMining}ms construction={tConstr - tHaul}ms craft={tCraft - tConstr}ms total={planMs}ms");

        _last = _last with
        {
            Tick = tick,
            PlanMsTotal = planMs,
            HaulPlanMs = tHaul - tMining,
            MiningPlanMs = tMining - t0,
            ConstructionPlanMs = tConstr - tHaul,
            CraftPlanMs = tCraft - tConstr
        };
    }

    public void WriteTick(ulong tick)
    {
        _miningPlanner.WriteTick(tick);
        _haulPlanner.WriteTick(tick);
        _constructionMaterialsPlanner?.WriteTick(tick);
        _constructionPlanner.WriteTick(tick);
        _craftPlanner?.WriteTick(tick);

        var sw = Stopwatch.StartNew();

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
        var haulApplyMs = sw.ElapsedMilliseconds;

        sw.Restart();
        _miningJobs.ReadTick(tick);
        var miningIntake = _miningJobs.LastIntakeCount;
        _miningJobs.WriteTick(tick);
        var miningApplyMs = sw.ElapsedMilliseconds;

        sw.Restart();
        _constructionJobs.ReadTick(tick);
        var constructionIntake = _constructionJobs.LastIntakeCount;
        _constructionJobs.WriteTick(tick);
        var constructionApplyMs = sw.ElapsedMilliseconds;

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

        var applyMs = haulApplyMs + miningApplyMs + constructionApplyMs + craftApplyMs;

        _log?.Invoke($"[SCHED][{tick}] Apply: haul(intake={haulIntake})={haulApplyMs}ms mining(intake={miningIntake})={miningApplyMs}ms construction(intake={constructionIntake})={constructionApplyMs}ms craft(intake={craftIntake})={craftApplyMs}ms total={applyMs}ms");

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
            ApplyMsTotal = applyMs,
            IntakeHaul = haulIntake,
            IntakeMining = miningIntake,
            IntakeConstruction = constructionIntake,
            IntakeCraft = craftIntake,
            HaulApplyMs = haulApplyMs,
            MiningApplyMs = miningApplyMs,
            ConstructionApplyMs = constructionApplyMs,
            CraftApplyMs = craftApplyMs
        };
    }
}
