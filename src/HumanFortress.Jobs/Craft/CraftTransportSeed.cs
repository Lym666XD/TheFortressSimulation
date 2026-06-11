namespace HumanFortress.Jobs.Craft;

internal static class CraftTransportSeed
{
    public static uint From(Guid itemId)
    {
        unchecked
        {
            var bytes = itemId.ToByteArray();
            uint hash = 2166136261;
            foreach (var value in bytes)
            {
                hash = (hash ^ value) * 16777619;
            }

            return hash;
        }
    }
}
