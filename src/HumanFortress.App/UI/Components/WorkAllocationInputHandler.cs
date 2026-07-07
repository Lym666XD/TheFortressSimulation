using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.App.UI.Components;

internal sealed partial class WorkAllocationInputHandler
{
    private readonly UIStateManager _uiStateManager;
    private readonly int _screenWidth;
    private readonly int _screenHeight;
    private readonly Func<WorkforceDebugData> _workforceProvider;
    private readonly Action<Guid, string, int> _setProfessionWeight;
    private readonly Action<string, ulong> _addToast;

    public WorkAllocationInputHandler(
        UIStateManager uiStateManager,
        int screenWidth,
        int screenHeight,
        Func<WorkforceDebugData> workforceProvider,
        Action<Guid, string, int> setProfessionWeight,
        Action<string, ulong> addToast)
    {
        _uiStateManager = uiStateManager ?? throw new ArgumentNullException(nameof(uiStateManager));
        _screenWidth = screenWidth;
        _screenHeight = screenHeight;
        _workforceProvider = workforceProvider ?? throw new ArgumentNullException(nameof(workforceProvider));
        _setProfessionWeight = setProfessionWeight ?? throw new ArgumentNullException(nameof(setProfessionWeight));
        _addToast = addToast ?? throw new ArgumentNullException(nameof(addToast));
    }

    public static WorkforceDebugData CreateEmptyWorkforce()
    {
        return new WorkforceDebugData(
            Array.Empty<ProfessionDefinitionView>(),
            Array.Empty<ProfessionRosterEntryView>(),
            0,
            0);
    }
}
