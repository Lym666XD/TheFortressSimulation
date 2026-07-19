namespace HumanFortress.Contracts.Time;

public enum TickSchedulerStopStatus
{
    Stopped,
    AlreadyStopped,
    TimedOut,
    SelfStopRequested
}

public enum TickSchedulerExecutionPhase
{
    Stopped,
    Starting,
    Paused,
    Sleeping,
    PreTick,
    Read,
    Barrier,
    Write,
    PostTick,
    AdvancingTick
}

public readonly record struct TickSchedulerExecutionPosition(
    ulong Tick,
    TickSchedulerExecutionPhase Phase,
    string? SystemId = null);

public readonly record struct TickSchedulerStopResult(
    TickSchedulerStopStatus Status,
    TickSchedulerExecutionPosition Position)
{
    public bool HasStopped => Status is TickSchedulerStopStatus.Stopped
        or TickSchedulerStopStatus.AlreadyStopped;

    public ulong Tick => Position.Tick;

    public TickSchedulerExecutionPhase Phase => Position.Phase;

    public string? SystemId => Position.SystemId;
}
