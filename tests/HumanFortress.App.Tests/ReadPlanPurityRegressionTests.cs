using HumanFortress.Core.Determinism;
using HumanFortress.Core.Time;
using HumanFortress.Jobs.Configuration;
using HumanFortress.Jobs.Mining;
using HumanFortress.Jobs.Orchestration;
using HumanFortress.Jobs.Transport;
using HumanFortress.Runtime.Jobs;
using HumanFortress.Simulation.Jobs;
using HumanFortress.Simulation.Orders;

namespace HumanFortress.App.Tests;

internal static class ReadPlanPurityRegressionTests
{
    internal static void RunAll()
    {
        TestReadPlanPreservesAuthorityVersionAndFingerprint();
        TestFailedReadPlanSkipsEveryCompatibilityMutation();
        TestLegacyFamiliesExposeHonestCompatibilityContracts();
        Console.WriteLine("[PASS] Read/Plan purity regression tests");
    }

    private static void TestReadPlanPreservesAuthorityVersionAndFingerprint()
    {
        var authority = new AuthorityProbe();
        var plan = new PurePlanProbe(authority);
        var orchestrator = CreateOrchestrator(authority, plan);
        var beforeVersion = authority.Version;
        var beforeFingerprint = authority.Fingerprint;

        orchestrator.ReadTick(17);

        RegressionAssert.True(
            plan.CallCount == 1
            && plan.ObservedVersion == beforeVersion
            && plan.ObservedFingerprint == beforeFingerprint
            && authority.Version == beforeVersion
            && authority.Fingerprint == beforeFingerprint,
            "A production-style Read/Plan pass changed authoritative version or fingerprint.");

        orchestrator.WriteTick(17);
        var stats = orchestrator.GetLastStats();
        RegressionAssert.True(
            authority.Version == beforeVersion + 18
            && authority.Fingerprint != beforeFingerprint
            && stats.PlanStageCount == 1
            && stats.ApplyStageCount == 9,
            "Sequential compatibility mutation did not remain confined to serialized Write.");
    }

    private static void TestFailedReadPlanSkipsEveryCompatibilityMutation()
    {
        var authority = new AuthorityProbe();
        var orchestrator = CreateOrchestrator(authority, new FailingPlanProbe());
        var scheduler = new TickScheduler();
        scheduler.RegisterSystem(orchestrator);
        var beforeVersion = authority.Version;
        var beforeFingerprint = authority.Fingerprint;

        scheduler.ExecuteSingleTick();

        RegressionAssert.True(
            authority.Version == beforeVersion
            && authority.Fingerprint == beforeFingerprint,
            "A failed Read/Plan stage allowed its serialized compatibility Write to execute.");
    }

    private static void TestLegacyFamiliesExposeHonestCompatibilityContracts()
    {
        var compatibility = typeof(ISequentialCompatibilityStage);
        var tick = typeof(ITick);
        var compatibilityFamilies = new[]
        {
            typeof(HaulingSystem),
            typeof(MiningSystem),
            typeof(ConstructionMaterialsPlanner),
            typeof(ConstructionSystem),
            typeof(HumanFortress.Jobs.Craft.CraftPlanner),
            typeof(TransportJobSystem),
            typeof(MiningJobSystem),
            typeof(ConstructionJobSystem),
            typeof(CraftJobSystem)
        };

        RegressionAssert.True(
            compatibilityFamilies.All(compatibility.IsAssignableFrom)
            && compatibilityFamilies.All(type => !tick.IsAssignableFrom(type))
            && tick.IsAssignableFrom(typeof(BuildableConstructionSystem))
            && compatibility.IsAssignableFrom(typeof(BuildableConstructionSystem)),
            "Legacy mutable families must be compatibility stages, not simulated ITick Read/Write systems.");
    }

    private static UnifiedJobsOrchestrator CreateOrchestrator(
        AuthorityProbe authority,
        IReadPlanStage readPlanStage)
    {
        var haulPlanner = new CompatibilityPlannerProbe(authority);
        var constructionMaterialsPlanner = new CompatibilityPlannerProbe(authority);
        var miningPlanner = new CompatibilityPlannerProbe(authority);
        var constructionPlanner = new CompatibilityPlannerProbe(authority);
        var craftPlanner = new CompatibilityPlannerProbe(authority);
        return new UnifiedJobsOrchestrator(
            haulPlanner,
            constructionMaterialsPlanner,
            miningPlanner,
            constructionPlanner,
            craftPlanner,
            new TransportCompatibilityProbe(authority),
            new MiningCompatibilityProbe(authority),
            new ConstructionCompatibilityProbe(authority),
            new CraftCompatibilityProbe(authority),
            new SchedulerTunings(),
            readPlanStages: new[] { readPlanStage });
    }

    private sealed class AuthorityProbe
    {
        private int _value = 41;

        internal int Version { get; private set; } = 7;

        internal string Fingerprint => ReplayHashBuilder.Compute(hash =>
        {
            hash.AddInt32(Version);
            hash.AddInt32(_value);
        });

        internal void Mutate()
        {
            Version++;
            _value += 13;
        }
    }

    private sealed class PurePlanProbe : IReadPlanStage
    {
        private readonly AuthorityProbe _authority;

        internal PurePlanProbe(AuthorityProbe authority) => _authority = authority;

        internal int CallCount { get; private set; }
        internal int ObservedVersion { get; private set; }
        internal string? ObservedFingerprint { get; private set; }

        public void ReadPlan(ulong tick)
        {
            CallCount++;
            ObservedVersion = _authority.Version;
            ObservedFingerprint = _authority.Fingerprint;
        }
    }

    private sealed class FailingPlanProbe : IReadPlanStage
    {
        public void ReadPlan(ulong tick) => throw new InvalidOperationException("injected plan failure");
    }

    private class CompatibilityPlannerProbe : ISequentialCompatibilityStage
    {
        protected readonly AuthorityProbe Authority;

        internal CompatibilityPlannerProbe(AuthorityProbe authority) => Authority = authority;

        public void PrepareSequentialCompatibility(ulong tick) => Authority.Mutate();

        public void ApplySequentialCompatibility(ulong tick) => Authority.Mutate();
    }

    private abstract class CompatibilityJobProbe : CompatibilityPlannerProbe, IUnifiedJobExecutor
    {
        protected CompatibilityJobProbe(AuthorityProbe authority) : base(authority)
        {
        }

        public int LastIntakeCount => 1;
    }

    private sealed class TransportCompatibilityProbe : CompatibilityJobProbe, IUnifiedTransportJobExecutor
    {
        internal TransportCompatibilityProbe(AuthorityProbe authority) : base(authority)
        {
        }

        public TransportJobStatsSnapshot GetLastStatsSnapshot() => new(1, 0, 0, 0, 0, 0, 0);

        public void ApplySchedulingHints(int? intakeCap, int? maxActiveCap, int reserveSlots)
        {
        }
    }

    private sealed class MiningCompatibilityProbe : CompatibilityJobProbe, IUnifiedMiningJobExecutor
    {
        internal MiningCompatibilityProbe(AuthorityProbe authority) : base(authority)
        {
        }

        public int GetBacklogCount() => 0;

        public MiningJobStatsSnapshot GetLastStatsSnapshot() => new(1, 0, 0, 0, 0, 0);
    }

    private sealed class ConstructionCompatibilityProbe : CompatibilityJobProbe, IUnifiedConstructionJobExecutor
    {
        internal ConstructionCompatibilityProbe(AuthorityProbe authority) : base(authority)
        {
        }
    }

    private sealed class CraftCompatibilityProbe : CompatibilityJobProbe, IUnifiedCraftJobExecutor
    {
        internal CraftCompatibilityProbe(AuthorityProbe authority) : base(authority)
        {
        }
    }
}
