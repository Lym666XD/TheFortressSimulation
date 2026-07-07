using SadConsole.Input;

namespace HumanFortress.App.States;

internal sealed partial class WorldGenState
{
    private bool ProcessNameEditKeyboard(Keyboard keyboard)
    {
        if (keyboard.IsKeyPressed(Keys.Escape))
        {
            _isEditingName = false;
            DrawUI();
            return true;
        }

        if (keyboard.IsKeyPressed(Keys.Enter))
        {
            _settings.Name = _nameBuffer;
            _isEditingName = false;
            DrawUI();
            return true;
        }

        if (keyboard.IsKeyPressed(Keys.Back))
        {
            if (_nameBuffer.Length > 0)
            {
                _nameBuffer = _nameBuffer.Substring(0, _nameBuffer.Length - 1);
                DrawUI();
            }
            return true;
        }

        foreach (var asciiKey in keyboard.KeysPressed)
        {
            char c = GetCharFromKey(asciiKey.Key);
            if (c != '\0' && _nameBuffer.Length < 20)
            {
                _nameBuffer += c;
                DrawUI();
                break;
            }
        }

        return true;
    }

    private static char GetCharFromKey(Keys key)
    {
        if (key >= Keys.A && key <= Keys.Z)
            return (char)('a' + (key - Keys.A));

        if (key >= Keys.D0 && key <= Keys.D9)
            return (char)('0' + (key - Keys.D0));

        return key == Keys.Space ? ' ' : '\0';
    }
}
