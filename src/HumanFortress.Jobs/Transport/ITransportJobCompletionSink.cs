namespace HumanFortress.Jobs.Transport;

internal interface ITransportJobCompletionSink
{
    void RecordJobCompletion(Guid workerId, string jobTag);
}

internal sealed class NullTransportJobCompletionSink : ITransportJobCompletionSink
{
    internal static readonly NullTransportJobCompletionSink Instance = new();

    private NullTransportJobCompletionSink()
    {
    }

    void ITransportJobCompletionSink.RecordJobCompletion(Guid workerId, string jobTag)
    {
    }
}
