using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace HumanFortress.Core.Determinism;

/// <summary>
/// Stable field-oriented hash builder for deterministic replay checkpoints.
/// Authoritative snapshot builders decide which fields to append; this type
/// only owns canonical primitive encoding.
/// </summary>
public sealed class ReplayHashBuilder : IDisposable
{
    public const string Algorithm = "sha256-v1";

    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private bool _finished;

    public static string Compute(Action<ReplayHashBuilder> build)
    {
        ArgumentNullException.ThrowIfNull(build);

        using var builder = new ReplayHashBuilder();
        build(builder);
        return builder.FinishHex();
    }

    public void AddBoolean(bool value)
    {
        AddByte(value ? (byte)1 : (byte)0);
    }

    public void AddByte(byte value)
    {
        ThrowIfFinished();
        Span<byte> buffer = stackalloc byte[1];
        buffer[0] = value;
        _hash.AppendData(buffer);
    }

    public void AddInt32(int value)
    {
        ThrowIfFinished();
        Span<byte> buffer = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
        _hash.AppendData(buffer);
    }

    public void AddUInt32(uint value)
    {
        ThrowIfFinished();
        Span<byte> buffer = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        _hash.AppendData(buffer);
    }

    public void AddInt64(long value)
    {
        ThrowIfFinished();
        Span<byte> buffer = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
        _hash.AppendData(buffer);
    }

    public void AddUInt64(ulong value)
    {
        ThrowIfFinished();
        Span<byte> buffer = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(buffer, value);
        _hash.AppendData(buffer);
    }

    public void AddGuid(Guid value)
    {
        ThrowIfFinished();
        Span<byte> buffer = stackalloc byte[16];
        value.TryWriteBytes(buffer);
        _hash.AppendData(buffer);
    }

    public void AddString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        AddNullableString(value);
    }

    public void AddNullableString(string? value)
    {
        ThrowIfFinished();
        if (value == null)
        {
            AddInt32(-1);
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(value);
        AddBytes(bytes);
    }

    public void AddBytes(ReadOnlySpan<byte> value)
    {
        ThrowIfFinished();
        AddInt32(value.Length);
        _hash.AppendData(value);
    }

    public string FinishHex()
    {
        ThrowIfFinished();
        _finished = true;
        return Convert.ToHexString(_hash.GetHashAndReset()).ToLowerInvariant();
    }

    public void Dispose()
    {
        _hash.Dispose();
    }

    private void ThrowIfFinished()
    {
        if (_finished)
            throw new InvalidOperationException("Replay hash builder has already been finalized.");
    }
}
