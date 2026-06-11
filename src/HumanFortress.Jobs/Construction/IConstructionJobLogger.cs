namespace HumanFortress.Jobs.Construction;

internal interface IConstructionJobLogger
{
    void Log(string message);
}

internal sealed class NullConstructionJobLogger : IConstructionJobLogger
{
    public static readonly NullConstructionJobLogger Instance = new();

    private NullConstructionJobLogger()
    {
    }

    public void Log(string message)
    {
    }
}
