using SadConsole.Input;

namespace HumanFortress.App.States
{
    internal sealed partial class EmbarkPrepState
    {
        public override bool ProcessKeyboard(Keyboard keyboard)
        {
            Logger.Debug("UI.EmbarkPrep", $"EmbarkPrepState ProcessKeyboard called, HasKeyPressed: {keyboard.KeysPressed.Count > 0}");

            if (keyboard.IsKeyPressed(Keys.Escape))
            {
                _navigator.ShowWorldMap();
                return true;
            }

            if (keyboard.IsKeyPressed(Keys.Up))
            {
                _selectedOption = Math.Max(0, _selectedOption - 1);
                DrawUI();
                return true;
            }

            if (keyboard.IsKeyPressed(Keys.Down))
            {
                _selectedOption = Math.Min(0, _selectedOption + 1);
                DrawUI();
                return true;
            }

            if (keyboard.IsKeyPressed(Keys.Left) || keyboard.IsKeyPressed(Keys.Right))
            {
                UpdateSelectedOption(keyboard);
                return true;
            }

            if (keyboard.IsKeyPressed(Keys.Enter))
            {
                StartEmbark();
                return true;
            }

            return false;
        }

        private void UpdateSelectedOption(Keyboard keyboard)
        {
            if (_selectedOption != 0)
                return;

            int currentIndex = Array.IndexOf(_sizeOptions, _fortressSize);
            if (keyboard.IsKeyPressed(Keys.Right))
                currentIndex = Math.Min(_sizeOptions.Length - 1, currentIndex + 1);
            else
                currentIndex = Math.Max(0, currentIndex - 1);

            _fortressSize = _sizeOptions[currentIndex];
            DrawUI();
        }
    }
}
