using System;
using SadConsole;
using SadRogue.Primitives;
using HumanFortress.App.GameStates;
using HumanFortress.App.UI;

namespace HumanFortress.App.States
{
    internal sealed partial class MainMenuState : ScreenObject
    {
        private readonly ScreenSurface _surface;
        private readonly MenuSurface _menuSurface;
        private readonly IAppStateNavigator _navigator;
        private readonly UiStore _uiStore = new UiStore();
        private ulong _tick = 0;

        private enum MenuItem
        {
            NewWorld = 0,
            LoadWorld = 1,
            Settings = 2,
            Credits = 3,
            Exit = 4
        }

        private enum PageMode
        {
            MainMenu,
            Settings,
            Credits
        }

        private MenuItem _selectedItem = MenuItem.NewWorld;
        private MenuItem? _hoveredItem = null;
        private PageMode _currentPage = PageMode.MainMenu;
        private const int MENU_START_Y = 22;
        private const int MENU_ITEM_HEIGHT = 2;
        private const int MENU_WIDTH = 30;

        internal MainMenuState(IAppStateNavigator navigator)
        {
            _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));

            // Create root surface
            _surface = new ScreenSurface(GameHost.Instance.ScreenCellsX, GameHost.Instance.ScreenCellsY);
            _surface.UseMouse = false;
            _surface.UseKeyboard = false;

            // Create menu surface with mouse support - same pattern as MapScreenSurface
            _menuSurface = new MenuSurface(GameHost.Instance.ScreenCellsX, GameHost.Instance.ScreenCellsY);
            _menuSurface.Position = new Point(0, 0);

            // Hook up mouse events
            _menuSurface.MouseMovedLocal += OnMouseMoved;
            _menuSurface.LeftClickedLocal += OnMouseClicked;
            _menuSurface.RightClickedLocal += OnRightClicked;

            // Add menu surface to root surface
            _surface.Children.Add(_menuSurface);

            // Add surface as child
            Children.Add(_surface);

            // Make this focusable for keyboard
            IsFocused = true;
            UseKeyboard = true;
            UseMouse = false; // Let child handle mouse

            DrawMenu();
        }

        public override void Update(TimeSpan delta)
        {
            base.Update(delta);
            _tick++;

            // Update menu if needed for animations
            if (_tick % 20 == 0)
            {
                DrawMenu();
            }
        }
    }
}
