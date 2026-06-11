namespace HumanFortress.Jobs.Transport;

internal interface ITransportJobCompletionSink
{
    void RecordJobCompletion(Guid workerId, string jobTag);
}

internal sealed class NullTransportJobCompletionSink : ITransportJobCompletionSink
{
    public static readonly NullTransportJobCompletionSink Instance = new();

    private NullTransportJobCompletionSink()
    {
    }

    public void RecordJobCompletion(Guid workerId, string jobTag)
    {
    }
}
