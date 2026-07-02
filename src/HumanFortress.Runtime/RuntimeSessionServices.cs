using System.Threading;
using HumanFortress.Core.Commands;
using HumanFortress.Core.Events;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Simulation.Items;

namespace HumanFortress.Runtime;

internal sealed class RuntimeSessionServices
{
    private long _nextCommandIdentitySequence;

    internal RuntimeSessionServices()
        : this(
            new TickScheduler(),
            new CommandQueue(),
            new EventBus(),
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
            new EventBus(),
            diffLog,
            itemsDiffLog)
    {
    }

    internal RuntimeSessionServices(
        TickScheduler tickScheduler,
        CommandQueue commandQueue,
        IEventBus eventBus,
        DiffLog diffLog,
        ItemsDiffLog itemsDiffLog)
    {
        TickScheduler = tickScheduler ?? throw new ArgumentNullException(nameof(tickScheduler));
        CommandQueue = commandQueue ?? throw new ArgumentNullException(nameof(commandQueue));
        EventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        DiffLog = diffLog ?? throw new ArgumentNullException(nameof(diffLog));
        ItemsDiffLog = itemsDiffLog ?? throw new ArgumentNullException(nameof(itemsDiffLog));
        MutationDiffs = new RuntimeMutationDiffLogs(items: ItemsDiffLog);
    }

    internal TickScheduler TickScheduler { get; }
    internal CommandQueue CommandQueue { get; }
    internal IEventBus EventBus { get; }
    internal DiffLog DiffLog { get; }
    internal ItemsDiffLog ItemsDiffLog { get; }
    internal RuntimeMutationDiffLogs MutationDiffs { get; }

    internal long NextCommandIdentitySequence()
    {
        return Interlocked.Increment(ref _nextCommandIdentitySequence);
    }

    internal void ResetForNewSession()
    {
        TickScheduler.ResetForNewSession();
        CommandQueue.Clear();
        DiffLog.Clear();
        MutationDiffs.Clear();
        _nextCommandIdentitySequence = 0;
    }
}
