namespace HumanFortress.Jobs.Mining;

internal interface IMiningJobCompletionSink
{
    void RecordJobCompletion(Guid workerId, string jobTag);
}
