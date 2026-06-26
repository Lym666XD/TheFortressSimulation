namespace HumanFortress.App.Session;

internal static class FortressSessionSizeRules
{
    public const int DefaultFortressSize = 2;
    public const int MinFortressSize = 2;
    public const int MaxFortressSize = 8;

    internal static int Normalize(int fortressSize)
    {
        return fortressSize is >= MinFortressSize and <= MaxFortressSize
            ? fortressSize
            : DefaultFortressSize;
    }

    internal static bool IsValid(int fortressSize)
    {
        return fortressSize is >= MinFortressSize and <= MaxFortressSize;
    }

    internal static int[] CreateSizeOptions()
    {
        var count = MaxFortressSize - MinFortressSize + 1;
        var options = new int[count];
        for (int i = 0; i < options.Length; i++)
        {
            options[i] = MinFortressSize + i;
        }

        return options;
    }
}
