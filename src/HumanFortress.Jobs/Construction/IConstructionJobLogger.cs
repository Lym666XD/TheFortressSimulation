namespace HumanFortress.Jobs.Construction;

internal interface IConstructionJobLogger
{
    void Log(string message);
}

internal sealed class NullConstructionJobLogger : IConstructionJobLogger
{
    internal static readonly NullConstructionJobLogger Instance = new();

    private NullConstructionJobLogger()
    {
    }

    void IConstructionJobLogger.Log(string message)
    {
    }
}
