namespace HumanFortress.Jobs.Mining;

internal interface IMiningJobLogger
{
    void Log(string message);
}

internal sealed class NullMiningJobLogger : IMiningJobLogger
{
    public static readonly NullMiningJobLogger Instance = new();

    private NullMiningJobLogger()
    {
    }

    public void Log(string message)
    {
    }
}
