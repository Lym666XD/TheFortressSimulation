namespace HumanFortress.Jobs.Mining;

internal interface IMiningJobLogger
{
    void Log(string message);
}

internal sealed class NullMiningJobLogger : IMiningJobLogger
{
    internal static readonly NullMiningJobLogger Instance = new();

    private NullMiningJobLogger()
    {
    }

    void IMiningJobLogger.Log(string message)
    {
    }
}
