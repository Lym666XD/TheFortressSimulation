namespace HumanFortress.Jobs.Craft;

internal static class CraftPathSeed
{
    public static uint From(Guid workerId, Guid workshopGuid)
    {
        unchecked
        {
            var workerBytes = workerId.ToByteArray();
            var workshopBytes = workshopGuid.ToByteArray();
            uint hash = 2166136261;
            foreach (var value in workerBytes)
            {
                hash = (hash ^ value) * 16777619;
            }

            foreach (var value in workshopBytes)
            {
                hash = (hash ^ value) * 16777619;
            }

            return hash;
        }
    }
}
