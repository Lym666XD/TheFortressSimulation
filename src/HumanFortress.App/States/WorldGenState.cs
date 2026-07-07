using System;
using SadConsole;
using SadRogue.Primitives;
using HumanFortress.App.GameStates;
using HumanFortress.App.Session;
using HumanFortress.App.UI;
using HumanFortress.App.WorldGeneration;
using HumanFortress.Contracts.WorldGen;

namespace HumanFortress.App.States
{
    internal sealed partial class WorldGenState : ScreenObject
    {
        private readonly ScreenSurface _surface;
        private readonly MenuSurface _menuSurface;
        private readonly SadConsole.Console _progressConsole;
        private readonly IAppStateNavigator _navigator;
        private readonly FortressSessionContext _session;
        private readonly IWorldGenerationService _worldGeneration;
        private WorldGenerationSettings _settings;
        private bool _isGenerating;

        // UI State
        private enum UIElement
        {
            Name = 0,
            Seed = 1,
            Size = 2,
            Difficulty = 3,
            PresetBeginner = 4,
            PresetStandard = 5,
            PresetChallenge = 6,
            ButtonGenerate = 7,
            ButtonRandomAll = 8,
            ButtonBack = 9
        }

        private UIElement _selectedElement = UIElement.Name;
        private UIElement? _hoveredElement = null;
        private bool _isEditingName = false;
        private string _nameBuffer = "";

        internal WorldGenState(
            IAppStateNavigator navigator,
            FortressSessionContext session,
            IWorldGenerationService worldGeneration)
        {
            _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _worldGeneration = worldGeneration ?? throw new ArgumentNullException(nameof(worldGeneration));

            // Create main surface that fills the screen
            _surface = new ScreenSurface(GameHost.Instance.ScreenCellsX, GameHost.Instance.ScreenCellsY);
            _surface.UseMouse = false;
            _surface.UseKeyboard = false;

            // Create menu surface with mouse support
            _menuSurface = new MenuSurface(GameHost.Instance.ScreenCellsX, GameHost.Instance.ScreenCellsY);
            _menuSurface.Position = new Point(0, 0);
            _menuSurface.MouseMovedLocal += OnMouseMoved;
            _menuSurface.LeftClickedLocal += OnMouseClicked;
            _menuSurface.RightClickedLocal += OnRightClicked;

            _progressConsole = new SadConsole.Console(80, 10);
            _progressConsole.Position = new Point(20, 35);

            // Add consoles to the surface
            _surface.Children.Add(_menuSurface);
            _surface.Children.Add(_progressConsole);

            // Add the surface as the only child
            Children.Add(_surface);

            // Make this ScreenObject focusable
            IsFocused = true;
            UseKeyboard = true;
            UseMouse = false;

            _settings = WorldGenerationSettingsDefaults.CreateDefault();
            _nameBuffer = _settings.Name;
            _worldGeneration.ProgressChanged += OnProgressChanged;

            DrawUI();
        }
    }
}
