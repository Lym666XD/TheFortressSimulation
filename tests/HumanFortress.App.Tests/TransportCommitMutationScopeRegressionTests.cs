using HumanFortress.Contracts.Jobs;
using HumanFortress.Contracts.Navigation;
using HumanFortress.Core.Simulation;
using HumanFortress.Jobs.Diff;
using HumanFortress.Jobs.Profession;
using HumanFortress.Jobs.Transport;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Stockpile;
using HumanFortress.Simulation.World;

internal static class TransportCommitMutationScopeRegressionTests
{
    internal static void RunAll()
    {
        TestRollbackRestoresEveryTransportSideEffectOwner();
        TestCommitRetainsEveryTransportSideEffect();
        Console.WriteLine("[PASS] Transport commit side-effect mutation scopes");
    }

    private static void TestRollbackRestoresEveryTransportSideEffectOwner()
    {
        var fixture = CreateFixture();
        using (fixture.Coordinator.BeginMutationScope())
        {
            EmitTransportSideEffects(fixture);
        }

        var profession = fixture.Assignments.GetReplaySnapshot();
        RegressionAssert.True(
            fixture.CoreDiff.MergeAndSort().Count == 1
            && fixture.ItemDiff.MergeAndSort().Count == 1
            && fixture.StockpileDiff.MergeAndSort().Count == 1
            && profession.Workers.All(static worker => worker.SkillLevels.Count == 0),
            "An uncommitted transport mutation scope retained diff or profession completion side effects.");

        fixture.ItemDiff.Add(
            ItemsDiffOp.AddItem,
            new HumanFortress.Simulation.World.ChunkKey(0, 0, 0),
            localIndex: 2,
            itemId: "after-rollback",
            quantity: 1,
            priority: 1,
            systemId: "test");
        fixture.StockpileDiff.AddReserveSlot(
            new HumanFortress.Simulation.World.ChunkKey(0, 0, 0),
            zoneId: 2,
            priority: 1,
            systemId: "test");

        RegressionAssert.True(
            fixture.ItemDiff.MergeAndSort().Single(static diff => diff.ItemId == "after-rollback").LocalSeq == 1
            && fixture.StockpileDiff.MergeAndSort().Single(static diff => diff.ZoneId == 2).LocalSeq == 1,
            "Transport side-effect rollback did not restore typed diff local sequences.");
    }

    private static void TestCommitRetainsEveryTransportSideEffect()
    {
        var fixture = CreateFixture();
        using (var scope = fixture.Coordinator.BeginMutationScope())
        {
            EmitTransportSideEffects(fixture);
            scope.Commit();
        }

        var profession = fixture.Assignments.GetReplaySnapshot();
        RegressionAssert.True(
            fixture.CoreDiff.MergeAndSort().Count == 2
            && fixture.ItemDiff.MergeAndSort().Count == 2
            && fixture.StockpileDiff.MergeAndSort().Count == 2
            && profession.Workers.Single(static worker => worker.SkillLevels.Count > 0)
                .SkillLevels.Single(static skill => skill.Id == "hauling").Value == 1,
            "A committed transport mutation scope discarded diff or profession completion side effects.");
    }

    private static Fixture CreateFixture()
    {
        var coreDiff = new DiffLog();
        var itemDiff = new ItemsDiffLog();
        var stockpileDiff = new StockpileDiffLog();
        var world = new World(sizeInChunks: 2, maxZ: 10);
        var assignments = new ProfessionAssignments(new TestProfessionRegistry());
        var transportDiff = new TransportDiffEmitter(
            coreDiff,
            itemDiff,
            priority: 1,
            systemId: "test.transport");
        var stockpileEmitter = new TransportStockpileIndexEmitter(
            world,
            stockpileDiff,
            priority: 1,
            systemId: "test.transport");
        var completionSink = new TransportProfessionCompletionSink(assignments, "hauling");

        coreDiff.AddOp(new DiffOp(
            DiffOpType.SetTerrain,
            new DiffTarget(0, 0),
            "baseline",
            priority: 1));
        itemDiff.Add(
            ItemsDiffOp.AddItem,
            new HumanFortress.Simulation.World.ChunkKey(0, 0, 0),
            localIndex: 0,
            itemId: "baseline",
            quantity: 1,
            priority: 1,
            systemId: "baseline");
        stockpileDiff.AddReserveSlot(
            new HumanFortress.Simulation.World.ChunkKey(0, 0, 0),
            zoneId: 1,
            priority: 1,
            systemId: "baseline");

        return new Fixture(
            coreDiff,
            itemDiff,
            stockpileDiff,
            assignments,
            transportDiff,
            completionSink,
            new TransportCommitMutationCoordinator(
                transportDiff,
                stockpileEmitter,
                completionSink));
    }

    private static void EmitTransportSideEffects(Fixture fixture)
    {
        var workerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var sourceItemId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var splitItemId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        fixture.TransportDiff.MoveCreature(workerId, new Point3(1, 1, 0));
        fixture.TransportDiff.SplitStack(
            sourceItemId,
            splitItemId,
            sourceX: 1,
            sourceY: 1,
            sourceZ: 0,
            quantity: 1,
            sourceReservation: default,
            stagedReservation: default);
        fixture.StockpileDiff.AddReserveSlot(
            new HumanFortress.Simulation.World.ChunkKey(0, 0, 0),
            zoneId: 2,
            priority: 1,
            systemId: "test.transport");
        ((ITransportJobCompletionSink)fixture.CompletionSink)
            .RecordJobCompletion(workerId, "hauling");
    }

    private sealed record Fixture(
        DiffLog CoreDiff,
        ItemsDiffLog ItemDiff,
        StockpileDiffLog StockpileDiff,
        ProfessionAssignments Assignments,
        TransportDiffEmitter TransportDiff,
        TransportProfessionCompletionSink CompletionSink,
        TransportCommitMutationCoordinator Coordinator);

    internal sealed class TestProfessionRegistry : IProfessionRegistry
    {
        private static readonly ProfessionDefinition Hauler = new(
            "hauler",
            "Hauler",
            new[] { "hauling" },
            IsDefault: true);

        public IReadOnlyList<ProfessionDefinition> Definitions { get; } = new[] { Hauler };

        public ProfessionDefinition DefaultProfession => Hauler;

        public IReadOnlyList<ProfessionDefinition> GetProfessionsForJob(string jobTag) =>
            string.Equals(jobTag, "hauling", StringComparison.Ordinal)
                ? Definitions
                : Array.Empty<ProfessionDefinition>();
    }
}
