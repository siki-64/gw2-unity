using System.Buffers.Binary;
using System.Text;

namespace Gw2.Core.Dat;

public sealed record Gw2PackChunk(string Type, ushort Version, int Offset, int HeaderSize,
    ReadOnlyMemory<byte> Bytes, int RelocationTableOffset)
{
    public ReadOnlyMemory<byte> Payload => Bytes[HeaderSize..];
}

/// <summary>Validated PF container. Unknown chunks remain available without guessing their schema.</summary>
public sealed class Gw2PackFile
{
    private Gw2PackFile(ushort flags, string type, IReadOnlyList<Gw2PackChunk> chunks)
        => (Flags, Type, Chunks) = (flags, type, chunks);

    public ushort Flags { get; }
    public int PointerSize => (Flags & 4) != 0 ? 8 : 4;
    public string Type { get; }
    public IReadOnlyList<Gw2PackChunk> Chunks { get; }

    public static Gw2PackFile Parse(ReadOnlyMemory<byte> data)
    {
        var span = data.Span;
        if (span.Length < 12 || !span[..2].SequenceEqual("PF"u8))
            throw new InvalidDataException("Missing PF header");
        if (BinaryPrimitives.ReadUInt16LittleEndian(span[4..]) != 0)
            throw new NotSupportedException("Big-endian PF files are unsupported");
        int offset = BinaryPrimitives.ReadUInt16LittleEndian(span[6..]);
        if (offset < 12 || offset > span.Length)
            throw new InvalidDataException("Invalid PF header size");
        var chunks = new List<Gw2PackChunk>();
        while (offset < span.Length)
        {
            if (span.Length - offset < 16) throw new InvalidDataException("Truncated PF chunk header");
            var header = span[offset..];
            ulong size = (ulong)BinaryPrimitives.ReadUInt32LittleEndian(header[4..]) + 8;
            int headerSize = BinaryPrimitives.ReadUInt16LittleEndian(header[10..]);
            if (headerSize < 16 || size < (uint)headerSize || size > (ulong)header.Length)
                throw new InvalidDataException($"Invalid PF chunk size at {offset}");
            uint relativeTable = BinaryPrimitives.ReadUInt32LittleEndian(header[12..]);
            ulong table = relativeTable == 0 ? 0 : (ulong)relativeTable + 16;
            if (table != 0 && (table < (uint)headerSize || table > size))
                throw new InvalidDataException("PF relocation table is outside the chunk");
            chunks.Add(new(Encoding.ASCII.GetString(header[..4]),
                BinaryPrimitives.ReadUInt16LittleEndian(header[8..]), offset, headerSize,
                data.Slice(offset, (int)size), (int)table));
            offset += (int)size;
        }
        return new(BinaryPrimitives.ReadUInt16LittleEndian(span[2..]), Encoding.ASCII.GetString(span[8..12]), chunks.AsReadOnly());
    }
}
