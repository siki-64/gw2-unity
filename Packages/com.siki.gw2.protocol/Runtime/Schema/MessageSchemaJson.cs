#nullable enable
using System;
using System.Text.Json;

namespace Gw2.Protocol.Schema
{
    /// <summary>
    /// Loads a build's message-schema corpus from the JSON emitted by
    /// <c>tools/re/ExtractSchemaCorpus.py</c>:
    /// <code>
    /// { "build": 207032, "messages": { "0x264": [ {"t":1,"p":612}, {"t":4}, ... ] } }
    /// </code>
    /// </summary>
    /// <remarks>
    /// The corpus is build-stamped data read from one image; do not reuse it for another build.
    /// This loader is engine-independent and takes the JSON text, so the Unity layer can supply
    /// it from a TextAsset while offline tooling supplies it from disk.
    /// </remarks>
    public static class MessageSchemaJson
    {
        public static MessageSchemaSet Parse(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            using JsonDocument doc = JsonDocument.Parse(json);
            return ParseRoot(doc.RootElement);
        }

        public static MessageSchemaSet Parse(ReadOnlyMemory<byte> utf8Json)
        {
            using JsonDocument doc = JsonDocument.Parse(utf8Json);
            return ParseRoot(doc.RootElement);
        }

        /// <summary>The build stamp recorded in the corpus, if present.</summary>
        public static int ReadBuild(string json)
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("build", out JsonElement b) ? b.GetInt32() : 0;
        }

        private static MessageSchemaSet ParseRoot(JsonElement root)
        {
            if (!root.TryGetProperty("messages", out JsonElement messages)
                || messages.ValueKind != JsonValueKind.Object)
                throw new FormatException("schema corpus: missing 'messages' object");

            var set = new MessageSchemaSet(Array.Empty<MessageSchema>());
            foreach (JsonProperty prop in messages.EnumerateObject())
            {
                string key = prop.Name;
                if (!key.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    throw new FormatException($"schema corpus: id '{key}' is not 0x-prefixed");
                int id = Convert.ToInt32(key, 16);
                FieldDefinition[] chain = ParseChain(prop.Value);
                if (chain.Length > 0 && chain[0].FieldType == 1 && chain[0].Param != id)
                    throw new FormatException($"schema corpus: key {key} disagrees with its MP_MSGID param");
                set.Add(MessageSchema.FromChain(chain));
            }
            return set;
        }

        private static FieldDefinition[] ParseChain(JsonElement array)
        {
            if (array.ValueKind != JsonValueKind.Array)
                throw new FormatException("schema corpus: chain is not an array");
            var fields = new FieldDefinition[array.GetArrayLength()];
            int i = 0;
            foreach (JsonElement f in array.EnumerateArray())
            {
                int type = f.GetProperty("t").GetInt32();
                int param = f.TryGetProperty("p", out JsonElement p) ? p.GetInt32() : 0;
                FieldDefinition[]? reference = null;
                if (f.TryGetProperty("r", out JsonElement r)) reference = ParseChain(r);
                fields[i++] = new FieldDefinition(type, param, reference);
            }
            return fields;
        }
    }
}
