#nullable enable
using System;
using System.Collections.Generic;

namespace Gw2.Protocol.MsgPack
{
    /// <summary>
    /// One decoded field value. <see cref="FieldType"/> is the schema field type.
    /// <para>
    /// <see cref="Value"/> types by field kind: unsigned integers as <see cref="ulong"/>,
    /// fixed blobs (<c>0x05/0x07/0x08/0x09/0x0b/0x0c/0x1a</c>) as <see cref="byte"/>[],
    /// strings as <see cref="string"/>, byte arrays as <see cref="byte"/>[],
    /// <c>MP_ARRAY</c> as <see cref="DecodedArrayHeader"/>, optional (<c>0x0f</c>) as a
    /// <see cref="DecodedField"/>[] or <c>null</c> when absent, and nested arrays
    /// (<c>0x10/0x11/0x12</c>) as <see cref="IReadOnlyList{T}"/> of <see cref="DecodedField"/>[].
    /// </para>
    /// </summary>
    public sealed class DecodedField
    {
        public DecodedField(int fieldType, bool present, object? value)
        {
            FieldType = fieldType;
            Present = present;
            Value = value;
        }

        public int FieldType { get; }

        /// <summary>False for an absent optional field; true otherwise.</summary>
        public bool Present { get; }

        public object? Value { get; }
    }

    /// <summary>Result of a field type <c>0x0a</c>: a 12-byte header and a base-128 count.</summary>
    public sealed class DecodedArrayHeader
    {
        public DecodedArrayHeader(byte[] header, int count)
        {
            Header = header;
            Count = count;
        }

        public byte[] Header { get; }
        public int Count { get; }
    }

    /// <summary>One decoded message: its id, its span in the message stream, and its fields.</summary>
    public sealed class DecodedMessage
    {
        public DecodedMessage(int messageId, int wireOffset, int wireLength,
                              IReadOnlyList<DecodedField> fields)
        {
            MessageId = messageId;
            WireOffset = wireOffset;
            WireLength = wireLength;
            Fields = fields;
        }

        public int MessageId { get; }

        /// <summary>Offset of the message start within the message stream.</summary>
        public int WireOffset { get; }

        /// <summary>Number of message-stream bytes consumed.</summary>
        public int WireLength { get; }

        public IReadOnlyList<DecodedField> Fields { get; }
    }
}
