using System.Buffers.Binary;
using System.Numerics;

namespace Gw2.Core.Dat;

public sealed record Gw2VertexAttribute(string Name, int Offset, int Size);

/// <summary>Packed model FVF vertex data; preserves source coordinates and all opaque attributes.</summary>
public sealed class Gw2VertexBuffer
{
    public Gw2VertexBuffer(uint format, uint count, ReadOnlyMemory<byte> bytes)
    {
        Format = format; Count = count; Bytes = bytes;
        var attributes = new List<Gw2VertexAttribute>();
        int stride = 0;
        void Add(string name, int size) { attributes.Add(new(name, stride, size)); stride += size; }
        if ((format & 0xC0000000) != 0) throw new NotSupportedException($"Unknown vertex format bits {format:X8}");
        if ((format & 1) != 0) Add("Position", 12);
        if ((format & 2) != 0) Add("Weights", 4);
        if ((format & 4) != 0) Add("BoneIndices", 4);
        if ((format & 8) != 0) Add("Normal", 12);
        if ((format & 0x10) != 0) Add("Color", 4);
        if ((format & 0x20) != 0) Add("Tangent", 12);
        if ((format & 0x40) != 0) Add("Bitangent", 12);
        if ((format & 0x80) != 0) Add("TangentFrame", 12);
        for (int i = 0; i < 8; i++) if ((format & (1u << (8 + i))) != 0) Add($"UV32_{i}", 8);
        for (int i = 0; i < 8; i++) if ((format & (1u << (16 + i))) != 0) Add($"UV16_{i}", 4);
        if ((format & 0x01000000) != 0) Add("Opaque24", 48);
        if ((format & 0x02000000) != 0) Add("Opaque25", 4);
        if ((format & 0x04000000) != 0) Add("Opaque26", 4);
        if ((format & 0x08000000) != 0) Add("Opaque27", 16);
        if ((format & 0x10000000) != 0) Add("PositionHalf", 6);
        if ((format & 0x20000000) != 0) Add("Opaque29", 12);
        if ((ulong)count * (uint)stride != (ulong)bytes.Length || (count != 0 && stride == 0))
            throw new InvalidDataException("Vertex count, FVF stride and buffer size disagree");
        Stride = stride; Attributes = attributes.AsReadOnly();
    }
    public uint Format { get; }
    public uint Count { get; }
    public int Stride { get; }
    public ReadOnlyMemory<byte> Bytes { get; }
    public IReadOnlyList<Gw2VertexAttribute> Attributes { get; }
    public Vector3 ReadPosition(uint index)
    {
        if (index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
        var field = Attributes.FirstOrDefault(a => a.Name is "Position" or "PositionHalf")
            ?? throw new NotSupportedException("Vertex has no position attribute");
        var span = Bytes.Span.Slice(checked((int)index * Stride + field.Offset), field.Size);
        return field.Size == 12
            ? new(BinaryPrimitives.ReadSingleLittleEndian(span), BinaryPrimitives.ReadSingleLittleEndian(span[4..]), BinaryPrimitives.ReadSingleLittleEndian(span[8..]))
            : new((float)BinaryPrimitives.ReadHalfLittleEndian(span), (float)BinaryPrimitives.ReadHalfLittleEndian(span[2..]), (float)BinaryPrimitives.ReadHalfLittleEndian(span[4..]));
    }
    public static Gw2VertexBuffer FromGeometry(Gw2PackObject geometry)
    {
        var vertices = (Gw2PackObject)geometry.Fields["verts"]!;
        var mesh = (Gw2PackObject)vertices.Fields["mesh"]!;
        return new((uint)mesh.Fields["fvf"]!, (uint)vertices.Fields["vertexCount"]!,
            mesh.Fields["vertices"] is ReadOnlyMemory<byte> data ? data : ReadOnlyMemory<byte>.Empty);
    }
}
