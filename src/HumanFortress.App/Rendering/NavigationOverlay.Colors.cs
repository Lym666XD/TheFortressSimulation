using SadRogue.Primitives;

namespace HumanFortress.App.Rendering;

internal sealed partial class NavigationOverlay
{
    private static Color ParseColor(string hexColor)
    {
        try
        {
            if (hexColor.StartsWith("#"))
                hexColor = hexColor.Substring(1);

            if (hexColor.Length == 6)
            {
                int r = Convert.ToInt32(hexColor.Substring(0, 2), 16);
                int g = Convert.ToInt32(hexColor.Substring(2, 2), 16);
                int b = Convert.ToInt32(hexColor.Substring(4, 2), 16);
                return new Color(r, g, b);
            }
        }
        catch
        {
        }

        return Color.White;
    }
}
