using SadConsole;
using SadConsole.Input;

namespace HumanFortress.App.Runtime;

internal delegate bool FortressMouseWheelHandler(int scrollWheelValueChange, Keyboard? keyboard);

internal static class FortressMouseWheelPoller
{
    public static void Poll(FortressMouseWheelHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        try
        {
            var gameHost = GameHost.Instance;
            if (gameHost?.Mouse != null)
                handler(gameHost.Mouse.ScrollWheelValueChange, gameHost.Keyboard);
        }
        catch
        {
        }
    }
}
