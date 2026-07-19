using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace HumanFortress.Jobs.Transport.Planning;

internal delegate ValueTask TransportPlanningWorkerHook(
    int workerIndex,
    CancellationToken cancellationToken);

/// <summary>
/// Production composition entry for serial or parallel pure planning. Parallel
/// result collection is intentionally unordered; the resolver restores the
/// sole canonical authority order after all workers finish.
/// </summary>
internal static class TransportPlanningPipeline
{
    internal static TransportPlanResolution Plan(TransportPlanningSnapshot snapshot)
    {
        return TransportPlanningWorkerAdapter
            .PlanAsync(snapshot, workerCount: 1)
            .GetAwaiter()
            .GetResult();
    }

    internal static ValueTask<TransportPlanResolution> PlanAsync(
        TransportPlanningSnapshot snapshot,
        int workerCount,
        TransportPlanningWorkerHook? beforeWorker = null,
        CancellationToken cancellationToken = default)
    {
        return TransportPlanningWorkerAdapter.PlanAsync(
            snapshot,
            workerCount,
            beforeWorker,
            cancellationToken);
    }
}

internal static class TransportPlanningWorkerAdapter
{
    internal static async ValueTask<TransportPlanResolution> PlanAsync(
        TransportPlanningSnapshot snapshot,
        int workerCount,
        TransportPlanningWorkerHook? beforeWorker = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (workerCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(workerCount), "Worker count must be positive.");

        var workItems = BuildCanonicalWork(snapshot);
        if (workItems.IsEmpty)
            return TransportIntentResolver.Resolve(Array.Empty<TransportIntent>());

        int effectiveWorkers = Math.Min(workerCount, workItems.Length);
        var shards = Partition(workItems, effectiveWorkers);
        if (effectiveWorkers == 1)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (beforeWorker != null)
                await beforeWorker(0, cancellationToken).ConfigureAwait(false);
            var batch = TransportPurePlanner.Plan(snapshot, shards[0]);
            return TransportIntentResolver.Resolve(batch.Intents, batch.Rejections);
        }

        var completedBatches = new ConcurrentBag<TransportIntentBatch>();
        var tasks = new Task[effectiveWorkers];
        for (int workerIndex = 0; workerIndex < effectiveWorkers; workerIndex++)
        {
            int capturedWorker = workerIndex;
            tasks[workerIndex] = Task.Run(
                async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (beforeWorker != null)
                    {
                        await beforeWorker(capturedWorker, cancellationToken)
                            .ConfigureAwait(false);
                    }

                    completedBatches.Add(TransportPurePlanner.Plan(
                        snapshot,
                        shards[capturedWorker]));
                },
                cancellationToken);
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
        return TransportIntentResolver.Resolve(
            completedBatches.SelectMany(static batch => batch.Intents),
            completedBatches.SelectMany(static batch => batch.Rejections));
    }

    internal static ImmutableArray<TransportPlanningWorkItem> BuildCanonicalWork(
        TransportPlanningSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var work = new List<TransportPlanningWorkItem>(
            snapshot.ActiveJobs.Length
            + snapshot.QueuedRequests.Length
            + snapshot.BacklogRequests.Length);
        int stableSourceOrder = 0;
        foreach (var active in snapshot.ActiveJobs)
        {
            work.Add(new TransportPlanningWorkItem(
                stableSourceOrder++,
                TransportPlanningWorkKind.ActiveJob,
                TransportPendingSource.Queue,
                default,
                active));
        }

        foreach (var queued in snapshot.QueuedRequests)
        {
            work.Add(new TransportPlanningWorkItem(
                stableSourceOrder++,
                TransportPlanningWorkKind.QueuedRequest,
                TransportPendingSource.Queue,
                queued.Request,
                default));
        }

        foreach (var backlog in snapshot.BacklogRequests)
        {
            work.Add(new TransportPlanningWorkItem(
                stableSourceOrder++,
                TransportPlanningWorkKind.BacklogRequest,
                TransportPendingSource.Backlog,
                backlog.Request,
                default));
        }

        var ordered = work
            .OrderBy(static item => GetOrderKey(item), TransportIntentOrderKeyComparer.Instance)
            .ThenBy(static item => item.Kind)
            .ThenBy(static item => item.CanonicalOrder)
            .ToArray();
        for (int index = 0; index < ordered.Length; index++)
            ordered[index] = ordered[index] with { CanonicalOrder = index };
        return ordered.ToImmutableArray();
    }

    private static ImmutableArray<TransportPlanningWorkItem>[] Partition(
        ImmutableArray<TransportPlanningWorkItem> workItems,
        int workerCount)
    {
        var builders = new ImmutableArray<TransportPlanningWorkItem>.Builder[workerCount];
        for (int workerIndex = 0; workerIndex < workerCount; workerIndex++)
            builders[workerIndex] = ImmutableArray.CreateBuilder<TransportPlanningWorkItem>();

        for (int workIndex = 0; workIndex < workItems.Length; workIndex++)
            builders[workIndex % workerCount].Add(workItems[workIndex]);

        var shards = new ImmutableArray<TransportPlanningWorkItem>[workerCount];
        for (int workerIndex = 0; workerIndex < workerCount; workerIndex++)
            shards[workerIndex] = builders[workerIndex].ToImmutable();
        return shards;
    }

    private static TransportIntentOrderKey GetOrderKey(TransportPlanningWorkItem item)
    {
        if (item.Kind == TransportPlanningWorkKind.ActiveJob)
        {
            return new TransportIntentOrderKey(
                item.ActiveJob.Priority,
                item.ActiveJob.CreatedTick,
                item.ActiveJob.SystemOrder,
                item.ActiveJob.ProducerId,
                item.ActiveJob.ItemId,
                item.ActiveJob.LocalSequence);
        }

        return new TransportIntentOrderKey(
            item.Request.Priority,
            item.Request.CreatedTick,
            item.Request.SystemOrder,
            item.Request.ProducerId,
            item.Request.ItemId,
            item.Request.LocalSequence);
    }
}
