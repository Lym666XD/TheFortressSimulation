using HumanFortress.Contracts.Navigation;
using HumanFortress.Simulation.Jobs;

namespace HumanFortress.Jobs.Transport;

internal sealed class TransportDeliveryHandler
{
    private readonly TransportDestinationValidator _destinationValidator;
    private readonly ITransportItemDiffEmitter _diffEmitter;
    private readonly TransportJobFinalizer _jobFinalizer;
    private readonly ITransportJobCompletionSink _completionSink;
    private readonly ITransportJobLogger _logger;
    private readonly string _jobTag;

    internal TransportDeliveryHandler(
        TransportDestinationValidator destinationValidator,
        ITransportItemDiffEmitter diffEmitter,
        TransportJobFinalizer jobFinalizer,
        ITransportJobCompletionSink? completionSink,
        ITransportJobLogger? logger,
        string jobTag)
    {
        _destinationValidator = destinationValidator ?? throw new ArgumentNullException(nameof(destinationValidator));
        _diffEmitter = diffEmitter ?? throw new ArgumentNullException(nameof(diffEmitter));
        _jobFinalizer = jobFinalizer ?? throw new ArgumentNullException(nameof(jobFinalizer));
        _completionSink = completionSink ?? NullTransportJobCompletionSink.Instance;
        _logger = logger ?? NullTransportJobLogger.Instance;
        _jobTag = jobTag ?? throw new ArgumentNullException(nameof(jobTag));
    }

    internal void HandleArrivedAtDestination(ActiveJob job, ulong tick, Point3 workerPosition, ICollection<ActiveJob> finished)
    {
        if (!_destinationValidator.ValidateDestination(job.Dest.X, job.Dest.Y, job.Dest.Z, job.Reason))
        {
            _logger.Log($"[TRANS-JOBS][{tick}] Drop job item={job.ItemId} dest=({job.Dest.X},{job.Dest.Y},{job.Dest.Z}) reason={job.Reason} validation=failed");
            _diffEmitter.UnmarkCarried(job.ItemId, workerPosition);
            _jobFinalizer.Finish(job, finished);
            return;
        }

        _diffEmitter.MoveItem(job.ItemId, job.Dest);
        _diffEmitter.UnmarkCarried(job.ItemId, job.Dest);
        _jobFinalizer.Finish(job, finished);
        JobStats.Completed++;
        _logger.Log($"[TRANS-JOBS][{tick}] Completed item={job.ItemId} to=({job.Dest.X},{job.Dest.Y},{job.Dest.Z}) reason={job.Reason} by worker={job.CreatureId}");
        _completionSink.RecordJobCompletion(job.CreatureId, _jobTag);
    }
}
