using System;
using SadConsole;
using SadConsole.Input;
using SadRogue.Primitives;
using HumanFortress.App.GameStates;
using HumanFortress.App.Session;

namespace HumanFortress.App.States
{
    internal sealed partial class EmbarkPrepState : ScreenObject
    {
        private readonly IAppStateNavigator _navigator;
        private readonly FortressSessionContext _session;
        private readonly SadConsole.Console _mainConsole;
        private int _fortressSize = FortressSessionSizeRules.DefaultFortressSize;
        private readonly int[] _sizeOptions = FortressSessionSizeRules.CreateSizeOptions();
        private int _selectedOption = 0; // Default to 2x2
        
        private Point SelectedTile => _session.SelectedTile;

        internal EmbarkPrepState(IAppStateNavigator navigator, FortressSessionContext session)
        {
            _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
            _session = session ?? throw new ArgumentNullException(nameof(session));

            // Create a root surface
            var rootSurface = new ScreenSurface(GameHost.Instance.ScreenCellsX, GameHost.Instance.ScreenCellsY);
            rootSurface.UseMouse = false;
            rootSurface.UseKeyboard = false;

            _mainConsole = new SadConsole.Console(80, 50);
            _mainConsole.Position = new Point(10, 5);
            _mainConsole.UseMouse = false;
            _mainConsole.UseKeyboard = false;

            // Add console to root surface
            rootSurface.Children.Add(_mainConsole);

            // Add root as the only child
            Children.Add(rootSurface);

            // Make this ScreenObject focusable
            IsFocused = true;
            UseKeyboard = true;
            UseMouse = false;

            DrawUI();
        }
    }
}
