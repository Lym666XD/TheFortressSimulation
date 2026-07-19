using HumanFortress.App.Rendering;
using HumanFortress.App.Session;
using HumanFortress.Contracts.Runtime;
using SadConsole;

namespace HumanFortress.App.States;

internal sealed class FortressStateInitializer
{
    private readonly ScreenObject _owner;
    private readonly FortressViewState _view;
    private readonly FortressViewportState _viewport;
    private readonly FortressViewContextFactory _viewContexts;
    private readonly FortressSessionLoadCoordinator _sessionLoadCoordinator;
    private readonly Func<int> _fortressSizeProvider;
    private readonly Action _drawUi;

    public FortressStateInitializer(
        ScreenObject owner,
        FortressViewState view,
        FortressViewportState viewport,
        FortressViewContextFactory viewContexts,
        FortressSessionLoadCoordinator sessionLoadCoordinator,
        Func<int> fortressSizeProvider,
        Action drawUi)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
        _viewContexts = viewContexts ?? throw new ArgumentNullException(nameof(viewContexts));
        _sessionLoadCoordinator = sessionLoadCoordinator ?? throw new ArgumentNullException(nameof(sessionLoadCoordinator));
        _fortressSizeProvider = fortressSizeProvider ?? throw new ArgumentNullException(nameof(fortressSizeProvider));
        _drawUi = drawUi ?? throw new ArgumentNullException(nameof(drawUi));
    }

    public bool TryInitialize()
    {
        try
        {
            Logger.Log("[FortressState] Initialize started");

            var gameHost = GameHost.Instance;
            if (gameHost == null)
            {
                Logger.Log("[FortressState] ERROR: GameHost.Instance is null!");
                return false;
            }

            Logger.Log($"[FortressState] GameHost screen size: {gameHost.ScreenCellsX}x{gameHost.ScreenCellsY}");

            var bootstrappedView = FortressViewBootstrapper.Create(
                _owner,
                gameHost,
                _viewContexts.CreateBootstrap());
            _view.Apply(
                bootstrappedView.MapSurface,
                bootstrappedView.UiSurface,
                bootstrappedView.SelectionTool);

            int fortressSize = _fortressSizeProvider();
            Logger.Log($"[FortressState] FortressSize = {fortressSize}");

            var worldAvailability = _sessionLoadCoordinator.Load(_viewport.CurrentZ);
            var surface = new RuntimeRect(
                bootstrappedView.MapSurface.Position.X,
                bootstrappedView.MapSurface.Position.Y,
                bootstrappedView.MapSurface.Surface.Width,
                bootstrappedView.MapSurface.Surface.Height);
            _viewport.Initialize(worldAvailability.WorldBounds, surface);
            bootstrappedView.SelectionTool.SetWorldBounds(worldAvailability.WorldBounds);
            Logger.Log($"[FortressState] Camera position set to {_viewport.CameraPosition}, cursor at {_viewport.CursorPosition}");
            _drawUi();

            Logger.Log("[FortressState] Initialize completed successfully");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("UI.FortressState", $"[FortressState] ERROR in Initialize: {ex.Message}", ex);
            throw;
        }
    }
}
