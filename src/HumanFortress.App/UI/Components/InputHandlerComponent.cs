using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadConsole.Components;

namespace HumanFortress.App.UI.Components;

/// <summary>
/// SadConsole component that handles top-level UI input dispatch.
/// </summary>
internal sealed partial class InputHandlerComponent : IComponent
{
    private readonly UIStateManager _uiStateManager;
    private readonly Func<ulong> _uiTickProvider;
    private readonly DrawerMouseInputHandler _drawerMouseInput;
    private readonly DebugMenuInputHandler _debugMenuInput;
    private readonly WorkAllocationInputHandler _workAllocation;

    public InputHandlerComponent(
        UIStateManager uiStateManager,
        int screenWidth,
        int screenHeight,
        Func<ulong>? uiTickProvider = null,
        Func<SimulationDebugMenuData>? debugMenuProvider = null,
        Func<WorkforceDebugData>? workforceProvider = null,
        Action<Guid, string, int>? setProfessionWeight = null)
    {
        _uiStateManager = uiStateManager;
        _uiTickProvider = uiTickProvider ?? (() => 0UL);
        _drawerMouseInput = new DrawerMouseInputHandler(
            _uiStateManager,
            screenWidth,
            screenHeight,
            AddToast);
        _debugMenuInput = new DebugMenuInputHandler(
            _uiStateManager,
            screenWidth,
            screenHeight,
            debugMenuProvider ?? DebugMenuInputHandler.CreateEmptyDebugMenuData,
            AddToast);
        _workAllocation = new WorkAllocationInputHandler(
            _uiStateManager,
            screenWidth,
            screenHeight,
            workforceProvider ?? WorkAllocationInputHandler.CreateEmptyWorkforce,
            setProfessionWeight ?? ((_, _, _) => { }),
            AddToast);
    }

    public uint SortOrder { get; set; } = 0;
    public bool IsUpdate => false;
    public bool IsRender => false;
    public bool IsMouse => true;
    public bool IsKeyboard => true;

    public void OnAdded(IScreenObject host)
    {
    }

    public void OnRemoved(IScreenObject host)
    {
    }

    public void Render(IScreenObject host, TimeSpan delta)
    {
    }

    public void Update(IScreenObject host, TimeSpan delta)
    {
    }

    private void AddToast(string text, ulong durationTicks)
    {
        _uiStateManager.AddToast(text, _uiTickProvider() + durationTicks);
    }
}
