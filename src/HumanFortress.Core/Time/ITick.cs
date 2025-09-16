namespace HumanFortress.Core.Time;

/// <summary>
/// Represents a tickable system that participates in the fixed-step simulation loop.
/// </summary>
public interface ITick
{
    /// <summary>
    /// Called during the read phase of a tick. Systems can read state but cannot modify it.
    /// Multiple systems may execute this phase in parallel.
    /// </summary>
    /// <param name="tick">Current simulation tick number</param>
    void ReadTick(ulong tick);

    /// <summary>
    /// Called during the write phase of a tick. Systems can modify state through
    /// the diff-log or chunk-actor patterns only.
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