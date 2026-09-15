using System;
using System.Collections.Generic;
using Gw2.Protocol.Schema;

namespace Gw2.Protocol.MsgPack
{
    /// <summary>
    /// Decodes a mode-3 message stream: a concatenation of <c>[u16 msgId][schema fields]</c>
    /// records with no per-message length. The leading id selects the schema;
    /// the field chain consumes exactly the message.
    /// </summary>
    public static class MessageStreamDecoder
    {
        /// <summary>The validator's per-message ceiling (<c>MSG_MAX_BUFFER_SIZE</c>).</summary>
        public const int MaxMessageBytes = 0x2000;

        /// <summary>
        /// Decode every message in <paramref name="stream"/>. Throws <see cref="FormatException"/>
        /// on an unknown id, a truncated message, or a schema violation; it never returns partial
        /// state for a malformed stream.
        /// </summary>
        public static IReadOnlyList<DecodedMessage> Decode(MessageSchemaSet schemas,
                                                           ReadOnlySpan<byte> stream)
        {
            if (schemas == null) throw new ArgumentNullException(nameof(schemas));

            var messages = new List<DecodedMessage>();
            int offset = 0;
            while (offset < stream.Length)
            {
                if (offset + 2 > stream.Length)
                    throw new FormatException($"msgpack: truncated message id at offset {offset}");

                int messageId = stream[offset] | (stream[offset + 1] << 8);
                if (!schemas.TryGet(messageId, out MessageSchema schema))
                    throw new FormatException($"msgpack: unknown message id 0x{messageId:x} at offset {offset}");

                int start = offset;
                DecodedField[] fields = MsgPackReader.ReadFields(schema.Fields, stream, ref offset);
                int length = offset - start;
                if (length > MaxMessageBytes)
                    throw new FormatException($"msgpack: message 0x{messageId:x} exceeds {MaxMessageBytes} bytes");

                messages.Add(new DecodedMessage(messageId, start, length, fields));
            }
            return messages;
        }

        /// <summary>
        /// Streaming decode for callers that buffer across packets: decode every complete message,
        /// then leave a truncated tail (a message that has not fully arrived) unconsumed. An
        /// unknown id is still an error.
        /// </summary>
        public static IReadOnlyList<DecodedMessage> TryDecode(MessageSchemaSet schemas,
                                                              ReadOnlySpan<byte> stream,
                                                              out int consumed)
        {
            if (schemas == null) throw new ArgumentNullException(nameof(schemas));

            var messages = new List<DecodedMessage>();
            int offset = 0;
            while (offset < stream.Length)
            {
                if (offset + 2 > stream.Length) break; // partial id
                int messageId = stream[offset] | (stream[offset + 1] << 8);
                if (!schemas.TryGet(messageId, out MessageSchema schema))
                    throw new FormatException($"msgpack: unknown message id 0x{messageId:x} at offset {offset}");

                int start = offset;
                DecodedField[] fields;
                try
                {
                    fields = MsgPackReader.ReadFields(schema.Fields, stream, ref offset);
                }
                catch (MsgPackTruncatedException)
                {
                    offset = start; // wait for more bytes; re-decoded next call
                    break;
                }

                int length = offset - start;
                if (length > MaxMessageBytes)
                    throw new FormatException($"msgpack: message 0x{messageId:x} exceeds {MaxMessageBytes} bytes");
                messages.Add(new DecodedMessage(messageId, start, length, fields));
            }
            consumed = offset;
            return messages;
        }
    }
}
