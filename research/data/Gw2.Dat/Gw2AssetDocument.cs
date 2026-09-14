namespace Gw2.Dat;

public sealed record Gw2ParsedChunk(Gw2PackChunk Chunk, Gw2PackObject? Root, string? UnsupportedReason);

/// <summary>All versioned chunks of an asset, with unsupported chunks retained losslessly.</summary>
public sealed record Gw2AssetDocument(Gw2PackFile Pack, IReadOnlyList<Gw2ParsedChunk> Chunks)
{
    public bool IsFullyParsed => Chunks.All(c => c.Root is not null);

    public static Gw2AssetDocument Parse(ReadOnlyMemory<byte> bytes, Gw2PackSchemaCatalog? catalog = null, Gw2PackReadLimits? limits = null)
    {
        var pack = Gw2PackFile.Parse(bytes);
        var reader = new Gw2PackReader(catalog);
        var chunks = new List<Gw2ParsedChunk>();
        foreach (var chunk in pack.Chunks)
        {
            try { chunks.Add(new(chunk, reader.Read(pack, chunk, limits), null)); }
            catch (NotSupportedException ex) { chunks.Add(new(chunk, null, ex.Message)); }
        }
        return new(pack, chunks.AsReadOnly());
    }

    public IEnumerable<Gw2PackObject> EnumerateObjects()
    {
        var seen = new HashSet<Gw2PackObject>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<object?>();
        foreach (var chunk in Chunks) pending.Push(chunk.Root);
        while (pending.TryPop(out var value))
        {
            if (value is Gw2PackObject obj && seen.Add(obj))
            {
                yield return obj;
                foreach (var member in obj.Members) pending.Push(member.Value);
            }
            else if (value is object?[] array)
                foreach (var element in array) pending.Push(element);
        }
    }

    /// <summary>Exact encoded file references encountered in parsed fields; no byte-pattern guessing.</summary>
    public IEnumerable<uint> EnumerateFileIds()
    {
        var ids = new HashSet<uint>();
        foreach (var obj in EnumerateObjects())
            foreach (var member in obj.Members)
                foreach (var id in References(member.Value))
                    if (ids.Add(id)) yield return id;

        static IEnumerable<uint> References(object? value)
        {
            if (value is Gw2FileReference reference && reference != default) yield return reference.FileId;
            else if (value is object?[] array)
                foreach (var item in array)
                    foreach (var id in References(item)) yield return id;
        }
    }

    /// <summary>Authored model effect structures, including clouds, emitters, streaks and lightning.</summary>
    public IEnumerable<Gw2PackObject> EnumerateModelEffects() => EnumerateObjects().Where(o =>
        o.TypeName.StartsWith("ModelParticle", StringComparison.Ordinal) ||
        o.TypeName.StartsWith("ModelCloud", StringComparison.Ordinal) ||
        o.TypeName.StartsWith("ModelStreak", StringComparison.Ordinal) ||
        o.TypeName.StartsWith("ModelLightning", StringComparison.Ordinal));
}
