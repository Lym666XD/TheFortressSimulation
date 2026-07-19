internal sealed record TestSuiteDefinition(string Id, Action Run);

internal static class TestSuiteCatalog
{
    internal const string TransportConstructionCraft = "transport-construction-craft";
    internal const string MiningItemsDiff = "mining-items-diff";
    internal const string NavigationPath = "navigation-path";
    internal const string ViewportGeometry = "viewport-geometry";
    internal const string RuntimeCheckpoint = "runtime-checkpoint";
    internal const string TickSchedulerLifecycle = "tick-scheduler-lifecycle";
    internal const string ContentIdentity = "content-identity";
    internal const string CoreRuntime = "core-runtime";
    internal const string ArchitectureBoundary = "architecture-boundary";
    internal const string DeterministicAuthority = "deterministic-authority";
    internal const string WorldGenEvidence = "worldgen-evidence";
    internal const string PhaseValidation = "phase-validation";

    internal static IReadOnlyList<TestSuiteDefinition> All { get; } = Array.AsReadOnly(
        new[]
        {
            new TestSuiteDefinition(TransportConstructionCraft, TransportConstructionCraftRegressionTests.RunAll),
            new TestSuiteDefinition(MiningItemsDiff, MiningItemsDiffRegressionTests.RunAll),
            new TestSuiteDefinition(NavigationPath, NavigationPathRegressionTests.RunAll),
            new TestSuiteDefinition(ViewportGeometry, ViewportGeometryRegressionTests.RunAll),
            new TestSuiteDefinition(RuntimeCheckpoint, RuntimeCheckpointBehaviorTests.RunAll),
            new TestSuiteDefinition(TickSchedulerLifecycle, TickSchedulerLifecycleRegressionTests.RunAll),
            new TestSuiteDefinition(ContentIdentity, ContentPipelineGateRegressionTests.RunAll),
            new TestSuiteDefinition(CoreRuntime, CoreRuntimeSmokeTests.RunAll),
            new TestSuiteDefinition(ArchitectureBoundary, ArchitectureBoundarySmokeTests.RunAll),
            new TestSuiteDefinition(DeterministicAuthority, DeterministicAuthoritySmokeTests.RunAll),
            new TestSuiteDefinition(WorldGenEvidence, WorldGenEvidenceRegressionTests.RunAll),
            new TestSuiteDefinition(PhaseValidation, HumanFortress.App.PhaseTests.RunAllPhaseTests)
        });
}

internal static class LegacySmokeSuiteRunner
{
    internal static void RunSuite(string suiteId)
    {
        var suite = TestSuiteCatalog.All.SingleOrDefault(
            definition => string.Equals(definition.Id, suiteId, StringComparison.Ordinal));
        if (suite == null)
            throw new ArgumentException($"Unknown registered test suite '{suiteId}'.", nameof(suiteId));

        suite.Run();
    }

    internal static void RunAll()
    {
        var failures = new List<Exception>();
        foreach (var suite in TestSuiteCatalog.All)
        {
            try
            {
                suite.Run();
            }
            catch (Exception exception)
            {
                failures.Add(new InvalidOperationException(
                    $"Test suite '{suite.Id}' failed.",
                    exception));
            }
        }

        if (failures.Count > 0)
            throw new AggregateException("One or more end-to-end smoke suites failed.", failures);
    }
}
