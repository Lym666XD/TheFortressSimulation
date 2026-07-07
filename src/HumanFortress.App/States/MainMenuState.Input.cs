using SadConsole.Input;

namespace HumanFortress.App.States;

internal sealed partial class MainMenuState
{
    public override bool ProcessKeyboard(Keyboard keyboard)
    {
        if (keyboard.IsKeyPressed(Keys.Escape))
        {
            if (_currentPage != PageMode.MainMenu)
            {
                _currentPage = PageMode.MainMenu;
                DrawMenu();
                return true;
            }

            Environment.Exit(0);
            return true;
        }

        if (_currentPage != PageMode.MainMenu)
            return false;

        if (keyboard.IsKeyPressed(Keys.Up))
        {
            _selectedItem = (MenuItem)(((int)_selectedItem - 1 + 5) % 5);
            DrawMenu();
            return true;
        }
        else if (keyboard.IsKeyPressed(Keys.Down))
        {
            _selectedItem = (MenuItem)(((int)_selectedItem + 1) % 5);
            DrawMenu();
            return true;
        }
        else if (keyboard.IsKeyPressed(Keys.Enter) || keyboard.IsKeyPressed(Keys.Space))
        {
            ExecuteMenuItem(_selectedItem);
            return true;
        }
        else if (keyboard.IsKeyPressed(Keys.Q))
        {
            Environment.Exit(0);
            return true;
        }

        if (keyboard.IsKeyPressed(Keys.N))
        {
            ExecuteMenuItem(MenuItem.NewWorld);
            return true;
        }
        else if (keyboard.IsKeyPressed(Keys.L))
        {
            ExecuteMenuItem(MenuItem.LoadWorld);
            return true;
        }

        return false;
    }
}
