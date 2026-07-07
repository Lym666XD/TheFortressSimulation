using System.IO;
using System.Text;

namespace HumanFortress.Runtime.Commands;

internal static class RuntimeCommandPayload
{
    internal const int CurrentVersion = 1;

    internal static BinaryWriter CreateWriter(Stream stream)
    {
        var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(CurrentVersion);
        return writer;
    }

    internal static void ReadAndValidateVersion(BinaryReader reader, string commandType)
    {
        var version = reader.ReadInt32();
        if (version != CurrentVersion)
            throw new InvalidDataException($"Unsupported payload version '{version}' for '{commandType}'.");
    }
}
