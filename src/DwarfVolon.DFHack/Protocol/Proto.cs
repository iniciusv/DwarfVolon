using System.Text;

namespace DwarfVolon.DFHack.Protocol;

/// <summary>
/// Codificação protobuf mínima: só o que as mensagens básicas do DFHack usam
/// (varint, string e mensagem aninhada). Evita depender de geração de código.
/// </summary>
internal sealed class ProtoWriter
{
    private readonly MemoryStream _buffer = new();

    public ProtoWriter String(int field, string? value)
    {
        if (value is null) return this;
        Tag(field, 2);
        var bytes = Encoding.UTF8.GetBytes(value);
        Varint((ulong)bytes.Length);
        _buffer.Write(bytes);
        return this;
    }

    public ProtoWriter Int(int field, long value)
    {
        Tag(field, 0);
        Varint(unchecked((ulong)value));
        return this;
    }

    public byte[] ToArray() => _buffer.ToArray();

    private void Tag(int field, int wireType) => Varint((ulong)((field << 3) | wireType));

    private void Varint(ulong value)
    {
        while (value >= 0x80)
        {
            _buffer.WriteByte((byte)(value | 0x80));
            value >>= 7;
        }
        _buffer.WriteByte((byte)value);
    }
}

/// <summary>Um campo lido de uma mensagem protobuf.</summary>
internal readonly record struct ProtoField(int Number, int WireType, ulong Varint, ReadOnlyMemory<byte> Bytes)
{
    public string AsString() => Encoding.UTF8.GetString(Bytes.Span);
    public long AsInt64() => unchecked((long)Varint);
}

internal static class ProtoReader
{
    public static IEnumerable<ProtoField> Read(ReadOnlyMemory<byte> data)
    {
        var pos = 0;
        while (pos < data.Length)
        {
            var key = ReadVarint(data.Span, ref pos);
            var number = (int)(key >> 3);
            var wire = (int)(key & 7);
            switch (wire)
            {
                case 0:
                    yield return new ProtoField(number, wire, ReadVarint(data.Span, ref pos), default);
                    break;
                case 1:
                    yield return new ProtoField(number, wire, BitConverter.ToUInt64(data.Span.Slice(pos, 8)), default);
                    pos += 8;
                    break;
                case 2:
                    var len = (int)ReadVarint(data.Span, ref pos);
                    yield return new ProtoField(number, wire, 0, data.Slice(pos, len));
                    pos += len;
                    break;
                case 5:
                    yield return new ProtoField(number, wire, BitConverter.ToUInt32(data.Span.Slice(pos, 4)), default);
                    pos += 4;
                    break;
                default:
                    throw new InvalidDataException($"Tipo de campo protobuf não suportado: {wire}");
            }
        }
    }

    private static ulong ReadVarint(ReadOnlySpan<byte> span, ref int pos)
    {
        ulong result = 0;
        var shift = 0;
        while (true)
        {
            var b = span[pos++];
            result |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) return result;
            shift += 7;
            if (shift > 63) throw new InvalidDataException("Varint protobuf inválido.");
        }
    }
}
