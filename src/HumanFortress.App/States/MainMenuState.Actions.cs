namespace HumanFortress.App.States;

internal sealed partial class MainMenuState
{
    private void ExecuteMenuItem(MenuItem item)
    {
        switch (item)
        {
            case MenuItem.NewWorld:
                _navigator.ShowWorldGeneration();
                break;

            case MenuItem.LoadWorld:
                _uiStore.AddToast("WIP: Load World feature coming soon!", _tick + 180);
                break;

            case MenuItem.Settings:
                _currentPage = PageMode.Settings;
                DrawMenu();
                break;

            case MenuItem.Credits:
                _currentPage = PageMode.Credits;
                DrawMenu();
                break;

            case MenuItem.Exit:
                Environment.Exit(0);
                break;
        }
    }
}
