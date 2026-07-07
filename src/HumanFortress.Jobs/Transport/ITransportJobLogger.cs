namespace HumanFortress.Jobs.Transport;

internal interface ITransportJobLogger
{
    void Log(string message);
}

internal sealed class NullTransportJobLogger : ITransportJobLogger
{
    internal static readonly NullTransportJobLogger Instance = new();

    private NullTransportJobLogger()
    {
    }

    void ITransportJobLogger.Log(string message)
    {
    }
}
