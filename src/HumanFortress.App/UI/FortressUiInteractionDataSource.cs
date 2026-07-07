using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.App.UI;

internal sealed class FortressUiInteractionDataSource
{
    public FortressUiInteractionDataSource(
        Func<SimulationDebugMenuData> debugMenuProvider,
        Func<WorkforceDebugData> workforceProvider,
        Action<Guid, string, int> setProfessionWeight)
    {
        DebugMenuProvider = debugMenuProvider ?? throw new ArgumentNullException(nameof(debugMenuProvider));
        WorkforceProvider = workforceProvider ?? throw new ArgumentNullException(nameof(workforceProvider));
        SetProfessionWeight = setProfessionWeight ?? throw new ArgumentNullException(nameof(setProfessionWeight));
    }

    public Func<SimulationDebugMenuData> DebugMenuProvider { get; }

    public Func<WorkforceDebugData> WorkforceProvider { get; }

    public Action<Guid, string, int> SetProfessionWeight { get; }
}
