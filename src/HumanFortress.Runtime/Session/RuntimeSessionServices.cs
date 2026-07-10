using HumanFortress.Core.Commands;
using HumanFortress.Core.Events;
using HumanFortress.Core.Random;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Contracts.Diagnostics;
using HumanFortress.Runtime.Diff;
using HumanFortress.Simulation.Items;

namespace HumanFortress.Runtime.Session;

internal sealed class RuntimeSessionServices
{
    private const ulong DefaultRngSeed = 0x4855464f52545245UL;

    private long _nextCommandIdentitySequence;
    private readonly object _identitySequenceLock = new();

    internal RuntimeSessionServices()
        : this(
            new TickScheduler(DiagnosticHub.Sink),
            new CommandQueue(DiagnosticHub.Sink),
            new EventBus(DiagnosticHub.Sink),
            new DiffLog(),
            new ItemsDiffLog())
    {
    }

    internal RuntimeSessionServices(
        TickScheduler tickScheduler,
        CommandQueue commandQueue,
        DiffLog diffLog,
        ItemsDiffLog itemsDiffLog)
        : this(
            tickScheduler,
            commandQueue,
            new EventBus(DiagnosticHub.Sink),
            diffLog,
            itemsDiffLog)
    {
    }

    internal RuntimeSessionServices(
        TickScheduler tickScheduler,
        CommandQueue commandQueue,
        IEventBus eventBus,
        DiffLog diffLog,
        ItemsDiffLog itemsDiffLog,
        RngStreamManager? rngStreams = null)
    {
        TickScheduler = tickScheduler ?? throw new ArgumentNullException(nameof(tickScheduler));
        CommandQueue = commandQueue ?? throw new ArgumentNullException(nameof(commandQueue));
        EventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        DiffLog = diffLog ?? throw new ArgumentNullException(nameof(diffLog));
        ItemsDiffLog = itemsDiffLog ?? throw new ArgumentNullException(nameof(itemsDiffLog));
        RngStreams = rngStreams ?? new RngStreamManager(DefaultRngSeed);
        MutationDiffs = new RuntimeMutationDiffLogs(items: ItemsDiffLog);
    }

    internal TickScheduler TickScheduler { get; }
    internal CommandQueue CommandQueue { get; }
    internal IEventBus EventBus { get; }
    internal DiffLog DiffLog { get; }
    internal ItemsDiffLog ItemsDiffLog { get; }
    internal RngStreamManager RngStreams { get; }
    internal RuntimeMutationDiffLogs MutationDiffs { get; }

    internal long NextCommandIdentitySequence()
    {
        lock (_identitySequenceLock)
        {
            _nextCommandIdentitySequence++;
            return _nextCommandIdentitySequence;
        }
    }

    internal void AdvanceCommandIdentitySequenceTo(long sequence)
    {
        if (sequence <= 0)
            return;

        lock (_identitySequenceLock)
        {
            if (_nextCommandIdentitySequence < sequence)
                _nextCommandIdentitySequence = sequence;
        }
    }

    internal void ResetForNewSession()
    {
        TickScheduler.ResetForNewSession();
        CommandQueue.Clear();
        DiffLog.Clear();
        MutationDiffs.Clear();
        RngStreams.ClearStreams();
        lock (_identitySequenceLock)
        {
            _nextCommandIdentitySequence = 0;
        }
    }
}
