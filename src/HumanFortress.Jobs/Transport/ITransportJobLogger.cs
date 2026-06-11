namespace HumanFortress.Jobs.Transport;

internal interface ITransportJobLogger
{
    void Log(string message);
}

internal sealed class NullTransportJobLogger : ITransportJobLogger
{
    public static readonly NullTransportJobLogger Instance = new();

    private NullTransportJobLogger()
    {
    }

    public void Log(string message)
    {
    }
}
