using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Text;

namespace Gw2.Dat;

public sealed class Gw2PackObject
{
    private readonly Dictionary<string, object?> _fields = [];
    private readonly List<KeyValuePair<string, object?>> _members = [];
    internal Gw2PackObject(string typeName, int offset)
        => (TypeName, Offset, Fields) = (typeName, offset, new ReadOnlyDictionary<string, object?>(_fields));
    public string TypeName { get; }
    public int Offset { get; }
    public IReadOnlyDictionary<string, object?> Fields { get; }
    /// <summary>Descriptor order with original names, including repeated native field names.</summary>
    public IReadOnlyList<KeyValuePair<string, object?>> Members => _members.AsReadOnly();
    internal void Add(string name, object? value)
    {
        _members.Add(new(name, value));
        string key = name;
        for (int occurrence = 2; _fields.ContainsKey(key); occurrence++) key = $"{name}#{occurrence}";
        _fields.Add(key, value);
    }
}

public sealed record Gw2PackReadLimits(int MaxValues = 2_000_000, int MaxDepth = 128, int MaxArrayElements = 1_000_000);

/// <summary>Bounds-checked, schema-driven reading of packed model, map, animation and effect data.</summary>
public sealed class Gw2PackReader
{
    private readonly Gw2PackSchemaCatalog _catalog;
    public Gw2PackReader(Gw2PackSchemaCatalog? catalog = null) => _catalog = catalog ?? Gw2PackSchemaCatalog.Default;

    public Gw2PackObject Read(Gw2PackFile pack, Gw2PackChunk chunk, Gw2PackReadLimits? limits = null)
    {
        var roots = _catalog.FindRoots(chunk.Type, chunk.Version);
        if (roots.Count > 1)
        {
            string? prefix = pack.Type switch
            {
                "MODL" => "ModelFile", "AMAT" => "Amat", "cntc" => "PackContent",
                "mMet" => "PackMapMetadata", "prlt" => "ContentPortalManifest", "ARMF" => "PackAssetManifest",
                "emoc" => "PackEmoteAnimations", _ => null
            };
            var selected = prefix is null ? [] : roots.Where(r => r.Name.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
            if (selected.Length == 1) return Read(chunk, pack.PointerSize, selected[0].Id, limits);
        }
        return Read(chunk, pack.PointerSize, limits: limits);
    }

    public Gw2PackObject Read(Gw2PackChunk chunk, int pointerSize, string? schemaId = null, Gw2PackReadLimits? limits = null)
    {
        if (pointerSize is not (4 or 8)) throw new ArgumentOutOfRangeException(nameof(pointerSize));
        var candidates = _catalog.FindRoots(chunk.Type, chunk.Version);
        if (schemaId is null)
        {
            if (candidates.Count != 1)
                throw new NotSupportedException($"Chunk {chunk.Type} v{chunk.Version} has {candidates.Count} matching schemas; an exact schema is required");
            schemaId = candidates[0].Id;
        }
        else if (!candidates.Any(c => c.Id == schemaId))
            throw new ArgumentException("Schema is not registered for this chunk version", nameof(schemaId));
        limits ??= new();
        if (limits.MaxValues <= 0 || limits.MaxDepth <= 0 || limits.MaxArrayElements <= 0)
            throw new ArgumentOutOfRangeException(nameof(limits));
        var context = new Context(_catalog, chunk, pointerSize, limits);
        return context.ReadObject(schemaId, chunk.HeaderSize, 0);
    }

    private sealed class Context(Gw2PackSchemaCatalog catalog, Gw2PackChunk chunk, int pointerSize, Gw2PackReadLimits limits)
    {
        private readonly Dictionary<(string, int), Gw2PackObject> _objects = [];
        private readonly Dictionary<string, int> _sizes = [];
        private readonly HashSet<string> _sizing = [];
        private int _values;
        private int End => chunk.RelocationTableOffset == 0 ? chunk.Bytes.Length : chunk.RelocationTableOffset;
        private ReadOnlySpan<byte> Bytes(int offset, int size)
        {
            if (offset < chunk.HeaderSize || size < 0 || offset > End || size > End - offset)
                throw new InvalidDataException($"{chunk.Type}: field at {offset} ({size} bytes) is outside chunk data");
            return chunk.Bytes.Span.Slice(offset, size);
        }
        private void Budget(int depth)
        {
            if (++_values > limits.MaxValues || depth > limits.MaxDepth)
                throw new InvalidDataException("Pack object traversal limit exceeded");
        }
        private int Target(int offset)
        {
            long relative = pointerSize == 8 ? BinaryPrimitives.ReadInt64LittleEndian(Bytes(offset, 8))
                : BinaryPrimitives.ReadInt32LittleEndian(Bytes(offset, 4));
            if (relative == 0) return 0;
            if (relative < chunk.HeaderSize - (long)offset || relative >= End - (long)offset)
                throw new InvalidDataException($"{chunk.Type}: relative pointer at {offset} is outside chunk data");
            return (int)(offset + relative);
        }
        private int Size(string id)
        {
            if (_sizes.TryGetValue(id, out var size)) return size;
            if (!_sizing.Add(id)) throw new InvalidDataException("Recursive inline pack schema");
            try
            {
                size = 0;
                foreach (var field in catalog.GetType(id).Fields) size = checked(size + FieldSize(field));
                _sizes[id] = size;
                return size;
            }
            finally { _sizing.Remove(id); }
        }
        private int FieldSize(Gw2PackFieldDefinition f) => f.Kind switch
        {
            1 => checked((int)f.Count * Size(Child(f))),
            2 or 3 => 4 + pointerSize,
            5 => 1,
            6 or 10 or 12 or 36 => 4,
            7 or 13 or 17 or 24 or 37 => 8,
            14 => 12,
            15 or 22 or 25 => 16,
            11 or 16 or 18 or 19 => pointerSize,
            20 or 29 => Size(Child(f)),
            21 => 2,
            23 => 3,
            26 or 27 => 6,
            _ => throw new NotSupportedException($"Pack field {f.Name} uses unsupported kind 0x{f.Kind:X}")
        };
        private static string Child(Gw2PackFieldDefinition field) => field.Type
            ?? throw new InvalidDataException($"Missing element schema for {field.Name}");

        public Gw2PackObject ReadObject(string id, int offset, int depth)
        {
            Budget(depth);
            if (_objects.TryGetValue((id, offset), out var existing)) return existing;
            Bytes(offset, Size(id));
            var type = catalog.GetType(id);
            var result = new Gw2PackObject(type.Name, offset);
            _objects[(id, offset)] = result;
            foreach (var field in type.Fields)
            {
                result.Add(field.Name, ReadField(field, offset, depth + 1));
                offset = checked(offset + FieldSize(field));
            }
            return result;
        }
        private object? Element(string id, int offset, int depth)
        {
            var type = catalog.GetType(id);
            return type.Fields.Count == 1 && type.Fields[0].Name.Length == 0
                ? ReadField(type.Fields[0], offset, depth + 1) : ReadObject(id, offset, depth + 1);
        }
        private object Array(string id, int offset, int count, bool pointers, int depth)
        {
            var fields = catalog.GetType(id).Fields;
            bool blob = !pointers && fields.Count == 1 && fields[0].Kind == 5 && fields[0].Name == "";
            if (count < 0 || (!blob && count > limits.MaxArrayElements)) throw new InvalidDataException("Pack array count exceeds limit");
            int stride = pointers ? pointerSize : Size(id);
            Bytes(offset, checked(count * stride));
            if (blob)
                return chunk.Bytes.Slice(offset, count);
            var result = new object?[count];
            for (int i = 0; i < count; i++)
            {
                int at = offset + i * stride;
                if (pointers) at = Target(at);
                result[i] = at == 0 ? null : Element(id, at, depth);
            }
            return result;
        }
        private object? ReadField(Gw2PackFieldDefinition f, int at, int depth)
        {
            Budget(depth);
            switch (f.Kind)
            {
                case 1: return Array(Child(f), at, checked((int)f.Count), false, depth);
                case 2: case 3:
                    var count = BinaryPrimitives.ReadUInt32LittleEndian(Bytes(at, 4));
                    if (count == 0) return System.Array.Empty<object>();
                    if (count > int.MaxValue) throw new InvalidDataException("Pack array count exceeds supported size");
                    var target = Target(at + 4);
                    if (target == 0) throw new InvalidDataException("Nonempty pack array has null pointer");
                    return Array(Child(f), target, (int)count, f.Kind == 3, depth);
                case 5: return Bytes(at, 1)[0];
                case 6: case 22: case 23: return chunk.Bytes.Slice(at, FieldSize(f));
                case 7: return BinaryPrimitives.ReadDoubleLittleEndian(Bytes(at, 8));
                case 10: case 36: return BinaryPrimitives.ReadUInt32LittleEndian(Bytes(at, 4));
                case 12: return BinaryPrimitives.ReadSingleLittleEndian(Bytes(at, 4));
                case 13: case 14: case 15:
                    var floats = new float[FieldSize(f) / 4];
                    for (int i = 0; i < floats.Length; i++) floats[i] = BinaryPrimitives.ReadSingleLittleEndian(Bytes(at + i * 4, 4));
                    return floats;
                case 17: case 37: return BinaryPrimitives.ReadUInt64LittleEndian(Bytes(at, 8));
                case 21: return BinaryPrimitives.ReadUInt16LittleEndian(Bytes(at, 2));
                case 24: case 25:
                    var dwords = new uint[FieldSize(f) / 4];
                    for (int i = 0; i < dwords.Length; i++) dwords[i] = BinaryPrimitives.ReadUInt32LittleEndian(Bytes(at + i * 4, 4));
                    return dwords;
                case 26: return new ushort[] {
                    BinaryPrimitives.ReadUInt16LittleEndian(Bytes(at, 2)), BinaryPrimitives.ReadUInt16LittleEndian(Bytes(at+2, 2)),
                    BinaryPrimitives.ReadUInt16LittleEndian(Bytes(at+4, 2)) };
                case 27: return Gw2FileReference.Parse(Bytes(at, 6));
                case 11:
                    target = Target(at);
                    return target == 0 ? null : Gw2FileReference.Parse(Bytes(target, 6));
                case 16:
                    target = Target(at);
                    return target == 0 ? null : Element(Child(f), target, depth);
                case 18: case 19:
                    target = Target(at);
                    if (target == 0) return null;
                    int unit = f.Kind == 18 ? 2 : 1, end = target;
                    while (true)
                    {
                        var value = Bytes(end, unit);
                        if (value[0] == 0 && (unit == 1 || value[1] == 0)) break;
                        end = checked(end + unit);
                    }
                    return (unit == 2 ? Encoding.Unicode : Encoding.UTF8).GetString(Bytes(target, end - target));
                case 20: case 29: return Element(Child(f), at, depth);
                default: throw new NotSupportedException($"Pack field {f.Name} kind 0x{f.Kind:X} is unsupported");
            }
        }
    }
}

public readonly record struct Gw2FileReference(ushort Low, ushort High, ushort Terminator)
{
    public uint FileId => Low >= 0x100 && High >= 0x100 && Terminator == 0
        ? checked(0xFF00u * (uint)(High - 0x100) + Low - 0x100u + 1)
        : throw new InvalidDataException("Invalid GW2 encoded file reference");
    public static Gw2FileReference Parse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 6) throw new InvalidDataException("Truncated GW2 file reference");
        return new(BinaryPrimitives.ReadUInt16LittleEndian(bytes), BinaryPrimitives.ReadUInt16LittleEndian(bytes[2..]),
            BinaryPrimitives.ReadUInt16LittleEndian(bytes[4..]));
    }
}
