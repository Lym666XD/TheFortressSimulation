using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.States;

internal sealed partial class MainMenuState
{
    private void DrawMenu()
    {
        _menuSurface.Surface.Clear();

        switch (_currentPage)
        {
            case PageMode.MainMenu:
                DrawMainMenu();
                break;
            case PageMode.Settings:
                DrawSettingsPage();
                break;
            case PageMode.Credits:
                DrawCreditsPage();
                break;
        }
    }
}
