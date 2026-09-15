#nullable enable
using System;
using System.Collections.Generic;
using Gw2.Protocol.Schema;

namespace Gw2.Protocol.MsgPack
{
    /// <summary>
    /// The message-schema writer (the inverse of <see cref="MsgPackReader"/>).
    /// <para>
    /// It re-encodes field values produced by a decode, so a captured message can be round-tripped.
    /// Unlike the reader it is bounds-agnostic: the client's writer trusts the caller to have sized
    /// the buffer, so malformed values are rejected by type, not by overrun.
    /// </para>
    /// </summary>
    public static class MsgPackWriter
    {
        /// <summary>Encode a whole message: the chain includes MP_MSGID as its first field.</summary>
        public static byte[] WriteMessage(MessageSchema schema, IReadOnlyList<DecodedField> fields)
        {
            if (schema == null) throw new ArgumentNullException(nameof(schema));
            var output = new List<byte>();
            WriteFields(schema.Fields, fields, output);
            return output.ToArray();
        }

        public static void WriteFields(FieldDefinition[] chain, IReadOnlyList<DecodedField> fields,
                                       List<byte> output)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            if (fields == null) throw new ArgumentNullException(nameof(fields));
            if (output == null) throw new ArgumentNullException(nameof(output));

            int index = 0;
            for (int i = 0; i < chain.Length; i++)
            {
                FieldDefinition f = chain[i];
                if (f.FieldType == 0 || f.FieldType == 0x18) break;
                if (index >= fields.Count)
                    throw new FormatException($"msgpack: missing value for field {i} (type 0x{f.FieldType:x})");
                WriteField(f, fields[index], output);
                index++;
            }
        }

        private static void WriteField(FieldDefinition f, DecodedField field, List<byte> output)
        {
            switch (f.FieldType)
            {
                case 0x01:
                case 0x03:
                    WriteU16(output, (int)AsUlong(field));
                    break;
                case 0x02:
                    output.Add((byte)AsUlong(field));
                    break;
                case 0x06:
                case 0x19:
                case 0x17:
                    WriteU32(output, (uint)AsUlong(field));
                    break;
                case 0x04:
                    WriteVarint(output, AsUlong(field));
                    break;
                case 0x05:
                case 0x07:
                case 0x1a:
                    WriteBytes(output, AsBytes(field), 8);
                    break;
                case 0x08:
                    WriteBytes(output, AsBytes(field), 0x0C);
                    break;
                case 0x09:
                case 0x0b:
                    WriteBytes(output, AsBytes(field), 0x10);
                    break;
                case 0x0c:
                    WriteBytes(output, AsBytes(field), 0x1C);
                    break;
                case 0x0d:
                    foreach (byte b in System.Text.Encoding.Unicode.GetBytes(AsString(field))) output.Add(b);
                    output.Add(0);
                    output.Add(0);
                    break;
                case 0x0e:
                    foreach (byte b in System.Text.Encoding.UTF8.GetBytes(AsString(field))) output.Add(b);
                    output.Add(0);
                    break;
                case 0x13:
                    output.AddRange(AsBytes(field));
                    break;
                case 0x14:
                {
                    byte[] b = AsBytes(field);
                    if (b.Length > 0xFF) throw new FormatException("msgpack: 0x14 length exceeds 255");
                    output.Add((byte)b.Length);
                    output.AddRange(b);
                    break;
                }
                case 0x15:
                {
                    byte[] b = AsBytes(field);
                    if (b.Length > 0xFFFF) throw new FormatException("msgpack: 0x15 length exceeds 65535");
                    WriteU16(output, b.Length);
                    output.AddRange(b);
                    break;
                }
                case 0x0a:
                {
                    var header = field.Value as DecodedArrayHeader
                        ?? throw new FormatException("msgpack: 0x0a value is not an array header");
                    if (header.Header.Length != 0x0C) throw new FormatException("msgpack: 0x0a header must be 12 bytes");
                    output.AddRange(header.Header);
                    WriteVarint(output, (ulong)header.Count);
                    break;
                }
                case 0x0f:
                    if (field.Present)
                    {
                        output.Add(1);
                        WriteSubchain(f, (DecodedField[])field.Value!, output);
                    }
                    else
                    {
                        output.Add(0);
                    }
                    break;
                case 0x10:
                    WriteItems(f, AsItems(field), output);
                    break;
                case 0x11:
                {
                    var items = AsItems(field);
                    if (items.Count > 0xFF) throw new FormatException("msgpack: 0x11 count exceeds 255");
                    output.Add((byte)items.Count);
                    WriteItems(f, items, output);
                    break;
                }
                case 0x12:
                {
                    var items = AsItems(field);
                    if (items.Count > 0xFFFF) throw new FormatException("msgpack: 0x12 count exceeds 65535");
                    WriteU16(output, items.Count);
                    WriteItems(f, items, output);
                    break;
                }
                case 0x16:
                    break; // MP_SRV_ALIGN: no data
                default:
                    throw new FormatException($"msgpack: cannot write fieldType 0x{f.FieldType:x}");
            }
        }

        private static void WriteSubchain(FieldDefinition f, DecodedField[] nested, List<byte> output)
        {
            FieldDefinition[]? reference = f.Reference;
            if (reference == null)
                throw new FormatException($"msgpack: fieldType 0x{f.FieldType:x} has no nested chain");
            WriteFields(reference, nested, output);
        }

        private static void WriteItems(FieldDefinition f, IReadOnlyList<DecodedField[]> items, List<byte> output)
        {
            foreach (DecodedField[] item in items) WriteSubchain(f, item, output);
        }

        private static void WriteBytes(List<byte> output, byte[] value, int required)
        {
            if (value.Length != required)
                throw new FormatException($"msgpack: expected {required} bytes, got {value.Length}");
            output.AddRange(value);
        }

        private static void WriteU16(List<byte> output, int value)
        {
            output.Add((byte)value);
            output.Add((byte)(value >> 8));
        }

        private static void WriteU32(List<byte> output, uint value)
        {
            output.Add((byte)value);
            output.Add((byte)(value >> 8));
            output.Add((byte)(value >> 16));
            output.Add((byte)(value >> 24));
        }

        private static void WriteVarint(List<byte> output, ulong value)
        {
            do
            {
                byte b = (byte)(value & 0x7F);
                value >>= 7;
                if (value != 0) b |= 0x80;
                output.Add(b);
            } while (value != 0);
        }

        private static ulong AsUlong(DecodedField field) =>
            field.Value is ulong v ? v : throw new FormatException($"msgpack: field 0x{field.FieldType:x} is not an integer");

        private static byte[] AsBytes(DecodedField field) =>
            field.Value as byte[] ?? throw new FormatException($"msgpack: field 0x{field.FieldType:x} is not bytes");

        private static string AsString(DecodedField field) =>
            field.Value as string ?? throw new FormatException($"msgpack: field 0x{field.FieldType:x} is not a string");

        private static IReadOnlyList<DecodedField[]> AsItems(DecodedField field) =>
            field.Value as IReadOnlyList<DecodedField[]>
            ?? throw new FormatException($"msgpack: field 0x{field.FieldType:x} is not an item array");
    }
}
