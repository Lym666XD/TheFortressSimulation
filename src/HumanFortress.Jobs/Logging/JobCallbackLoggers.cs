using HumanFortress.Jobs.Construction;
using HumanFortress.Jobs.Mining;
using HumanFortress.Jobs.Transport;

namespace HumanFortress.App.Jobs;

internal sealed class TransportCallbackJobLogger : ITransportJobLogger
{
    private readonly Action<string>? _log;

    public TransportCallbackJobLogger(Action<string>? log)
    {
        _log = log;
    }

    public void Log(string message)
    {
        _log?.Invoke(message);
    }
}

internal sealed class MiningCallbackJobLogger : IMiningJobLogger
{
    private readonly Action<string>? _log;

    public MiningCallbackJobLogger(Action<string>? log)
    {
        _log = log;
    }

    public void Log(string message)
    {
        _log?.Invoke(message);
    }
}

internal sealed class ConstructionCallbackJobLogger : IConstructionJobLogger
{
    private readonly Action<string>? _log;

    public ConstructionCallbackJobLogger(Action<string>? log)
    {
        _log = log;
    }

    public void Log(string message)
    {
        _log?.Invoke(message);
    }
}
