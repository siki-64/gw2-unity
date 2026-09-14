using System.Text.Json;

namespace Gw2.Core.Dat;

public sealed record Gw2PackFieldDefinition(string Name, int Kind, ulong Count, string? Type);
public sealed record Gw2PackTypeDefinition(string Id, string Name, IReadOnlyList<Gw2PackFieldDefinition> Fields);

/// <summary>Versioned format descriptors recovered from an executable, independent of game memory.</summary>
public sealed class Gw2PackSchemaCatalog
{
    private readonly Dictionary<string, Gw2PackTypeDefinition> _types = [];
    private readonly Dictionary<(string Chunk, ushort Version), HashSet<string>> _roots = [];
    private static readonly Lazy<Gw2PackSchemaCatalog> BuiltIn = new(() =>
    {
        using var stream = typeof(Gw2PackSchemaCatalog).Assembly.GetManifestResourceStream("Gw2.Dat.pack-schemas.json")!;
        return Load(stream);
    });
    public static Gw2PackSchemaCatalog Default => BuiltIn.Value;
    public string ExecutableSha256 { get; private init; } = "";
    public IEnumerable<Gw2PackTypeDefinition> Types => _types.Values;
    public Gw2PackTypeDefinition GetType(string id) => _types.TryGetValue(id, out var type)
        ? type : throw new NotSupportedException($"Unknown pack schema {id}");

    public IReadOnlyList<Gw2PackTypeDefinition> FindRoots(string chunk, ushort version) =>
        _roots.TryGetValue((chunk, version), out var roots)
            ? roots.Select(GetType).ToArray() : [];

    public static Gw2PackSchemaCatalog Load(Stream stream)
    {
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        if (root.GetProperty("format").GetString() != "gw2-native-pack-schemas-v1")
            throw new InvalidDataException("Unrecognized pack schema catalog");
        var result = new Gw2PackSchemaCatalog { ExecutableSha256 = root.GetProperty("executableSha256").GetString()! };
        foreach (var type in root.GetProperty("types").EnumerateObject())
        {
            var fields = type.Value.GetProperty("fields").EnumerateArray().Select(field =>
                new Gw2PackFieldDefinition(field.GetProperty("name").GetString()!, field.GetProperty("kind").GetInt32(),
                    field.GetProperty("count").GetUInt64(), field.TryGetProperty("type", out var child) ? child.GetString() : null)).ToArray();
            result._types.Add(type.Name, new(type.Name, type.Value.GetProperty("name").GetString()!, Array.AsReadOnly(fields)));
        }
        foreach (var registration in root.GetProperty("registrations").EnumerateArray())
        {
            var chunk = registration.GetProperty("chunk").GetString()!;
            foreach (var version in registration.GetProperty("versions").EnumerateObject())
            {
                var key = (chunk, ushort.Parse(version.Name, System.Globalization.CultureInfo.InvariantCulture));
                if (!result._roots.TryGetValue(key, out var roots)) result._roots[key] = roots = [];
                roots.Add(version.Value.GetString()!);
            }
        }
        return result;
    }
}
