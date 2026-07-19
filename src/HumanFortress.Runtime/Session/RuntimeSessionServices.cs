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
    internal const ulong DefaultRngSeed = 0x4855464f52545245UL;

    private long _nextCommandIdentitySequence;
    private readonly object _identitySequenceLock = new();

    internal RuntimeSessionServices()
        : this(DiagnosticHub.Sink)
    {
    }

    internal RuntimeSessionServices(
        IDiagnosticSink diagnostics,
        ulong rngSeed = DefaultRngSeed)
        : this(
            new TickScheduler(diagnostics),
            new CommandQueue(diagnostics),
            new EventBus(diagnostics),
            new DiffLog(),
            new ItemsDiffLog(),
            rngStreams: new RngStreamManager(rngSeed),
            diagnostics: diagnostics)
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
            itemsDiffLog,
            diagnostics: DiagnosticHub.Sink)
    {
    }

    internal RuntimeSessionServices(
        TickScheduler tickScheduler,
        CommandQueue commandQueue,
        IEventBus eventBus,
        DiffLog diffLog,
        ItemsDiffLog itemsDiffLog,
        RngStreamManager? rngStreams = null,
        IDiagnosticSink? diagnostics = null)
    {
        TickScheduler = tickScheduler ?? throw new ArgumentNullException(nameof(tickScheduler));
        CommandQueue = commandQueue ?? throw new ArgumentNullException(nameof(commandQueue));
        EventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        DiffLog = diffLog ?? throw new ArgumentNullException(nameof(diffLog));
        ItemsDiffLog = itemsDiffLog ?? throw new ArgumentNullException(nameof(itemsDiffLog));
        Diagnostics = diagnostics ?? DiagnosticHub.Sink;
        RngStreams = rngStreams ?? new RngStreamManager(DefaultRngSeed);
        MutationDiffs = new RuntimeMutationDiffLogs(items: ItemsDiffLog);
    }

    internal TickScheduler TickScheduler { get; }
    internal CommandQueue CommandQueue { get; }
    internal IEventBus EventBus { get; }
    internal DiffLog DiffLog { get; }
    internal ItemsDiffLog ItemsDiffLog { get; }
    internal IDiagnosticSink Diagnostics { get; }
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
