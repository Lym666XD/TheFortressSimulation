namespace HumanFortress.Runtime.Checkpoints;

internal sealed class RuntimeCheckpointGenerationLease
{
    private int _isValid = 1;

    internal RuntimeCheckpointGenerationLease(ulong generation)
    {
        if (generation == 0)
            throw new ArgumentOutOfRangeException(nameof(generation));

        Generation = generation;
    }

    internal ulong Generation { get; }

    internal bool IsValid => Volatile.Read(ref _isValid) == 1;

    internal bool Invalidate()
    {
        return Interlocked.Exchange(ref _isValid, 0) == 1;
    }
}
