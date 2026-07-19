namespace HumanFortress.Core.Time;

/// <summary>
/// Represents a tickable system that participates in the fixed-step simulation loop.
/// </summary>
public interface ITick
{
    /// <summary>
    /// Called during the read phase of a tick. Systems can read state and emit
    /// tick-local immutable intents, but cannot consume queues or mutate authority.
    /// The current scheduler executes systems in deterministic registered-system order.
    /// </summary>
    /// <param name="tick">Current simulation tick number</param>
    void ReadTick(ulong tick);

    /// <summary>
    /// Called during the serialized write phase. Systems may resolve and commit
    /// intents or execute an explicitly declared sequential compatibility stage.
    /// </summary>
    /// <param name="tick">Current simulation tick number</param>
    void WriteTick(ulong tick);

    /// <summary>
    /// Priority for system execution order within a phase. Lower values execute first.
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// System identifier for debugging and error reporting.
    /// </summary>
    string SystemId { get; }
}
