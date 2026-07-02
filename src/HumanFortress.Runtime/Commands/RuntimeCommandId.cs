using System.IO;
using System.Security.Cryptography;
using System.Text;
using HumanFortress.Core.Commands;

namespace HumanFortress.Runtime.Commands;

internal static class RuntimeCommandId
{
    private const string Domain = "HumanFortress.Runtime.CommandId.v1";

    internal static Guid Create(ICommand command)
    {
        return Create(command, sequence: null);
    }

    internal static Guid Create(ICommand command, long sequence)
    {
        if (sequence <= 0)
            throw new ArgumentOutOfRangeException(nameof(sequence), "Command identity sequence must be positive.");

        return Create(command, (long?)sequence);
    }

    private static Guid Create(ICommand command, long? sequence)
    {
        ArgumentNullException.ThrowIfNull(command);

        var payload = command.Serialize();
        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Domain);
            writer.Write(command.Tick);
            writer.Write(command.CommandType);
            writer.Write(sequence.HasValue);
            if (sequence.HasValue)
            {
                writer.Write(sequence.Value);
            }

            writer.Write(payload.Length);
            writer.Write(payload);
        }

        var hash = SHA256.HashData(ms.ToArray());
        var idBytes = new byte[16];
        Array.Copy(hash, idBytes, idBytes.Length);
        return new Guid(idBytes);
    }
}
