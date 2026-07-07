using HumanFortress.Jobs.Construction;
using HumanFortress.Jobs.Mining;
using HumanFortress.Jobs.Transport;

namespace HumanFortress.Jobs.Logging;

internal sealed class TransportCallbackJobLogger : ITransportJobLogger
{
    private readonly Action<string>? _log;

    internal TransportCallbackJobLogger(Action<string>? log)
    {
        _log = log;
    }

    void ITransportJobLogger.Log(string message)
    {
        _log?.Invoke(message);
    }
}

internal sealed class MiningCallbackJobLogger : IMiningJobLogger
{
    private readonly Action<string>? _log;

    internal MiningCallbackJobLogger(Action<string>? log)
    {
        _log = log;
    }

    void IMiningJobLogger.Log(string message)
    {
        _log?.Invoke(message);
    }
}

internal sealed class ConstructionCallbackJobLogger : IConstructionJobLogger
{
    private readonly Action<string>? _log;

    internal ConstructionCallbackJobLogger(Action<string>? log)
    {
        _log = log;
    }

    void IConstructionJobLogger.Log(string message)
    {
        _log?.Invoke(message);
    }
}
