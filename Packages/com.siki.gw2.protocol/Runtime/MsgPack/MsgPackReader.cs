using System;
using System.Collections.Generic;
using System.Text;
using Gw2.Protocol.Schema;

namespace Gw2.Protocol.MsgPack
{
    /// <summary>
    /// The message-schema reader (<c>MsgPack::ReadFields</c>), ported from the
    /// recovered semantics. It walks a <see cref="FieldDefinition"/> chain over the message
    /// stream and fails closed: any overrun, unknown type, or missing nested chain throws
    /// <see cref="FormatException"/> rather than returning partial data.
    /// <para>
    /// Field type numbers are build-local; do not carry them to another build.
    /// </para>
    /// </summary>
    public static class MsgPackReader
    {
        public const int MaxDepth = 16;

        /// <summary>Decode one chain starting at <paramref name="offset"/>; advances it.</summary>
        public static DecodedField[] ReadFields(FieldDefinition[] chain, ReadOnlySpan<byte> data,
                                                ref int offset, int depth = 0)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            if (depth > MaxDepth) throw new FormatException("msgpack: nesting too deep");

            var fields = new List<DecodedField>(chain.Length);
            for (int i = 0; i < chain.Length; i++)
            {
                FieldDefinition f = chain[i];
                if (f.FieldType == 0 || f.FieldType == 0x18) break; // MP_NONE / MP_SRV_END
                fields.Add(ReadField(f, data, ref offset, depth));
            }
            return fields.ToArray();
        }

        private static DecodedField ReadField(FieldDefinition f, ReadOnlySpan<byte> data,
                                              ref int offset, int depth)
        {
            int ft = f.FieldType;
            bool present = true;
            object value;

            switch (ft)
            {
                case 0x01: // MP_MSGID
                case 0x03: // u16
                    value = (ulong)ReadU16(data, ref offset);
                    break;
                case 0x02: // u8
                    value = (ulong)ReadU8(data, ref offset);
                    break;
                case 0x06: // u32
                case 0x19:
                case 0x17:
                    value = (ulong)ReadU32(data, ref offset);
                    break;
                case 0x05:
                case 0x07:
                case 0x1a:
                    value = ReadBytes(data, ref offset, 8);
                    break;
                case 0x08:
                    value = ReadBytes(data, ref offset, 0x0C);
                    break;
                case 0x09:
                case 0x0b:
                    value = ReadBytes(data, ref offset, 0x10);
                    break;
                case 0x0c:
                    value = ReadBytes(data, ref offset, 0x1C);
                    break;
                case 0x04: // base-128 varint -> u32 slot
                    value = ReadVarint(data, ref offset);
                    break;
                case 0x0a: // array header + count varint
                    byte[] header = ReadBytes(data, ref offset, 0x0C);
                    value = new DecodedArrayHeader(header, (int)ReadVarint(data, ref offset));
                    break;
                case 0x0d: // utf-16, NUL-terminated; param = max u16 elements incl. NUL
                {
                    int start = offset;
                    value = ReadCString16(data, ref offset);
                    RequireMax(ft, offset - start, f.Param * 2);
                    break;
                }
                case 0x0e: // utf-8/ascii, NUL-terminated; param = max bytes incl. NUL
                {
                    int start = offset;
                    value = ReadCString8(data, ref offset);
                    RequireMax(ft, offset - start, f.Param);
                    break;
                }
                case 0x13: // fixed byte region, length = param
                    value = ReadBytes(data, ref offset, f.Param);
                    break;
                case 0x14: // 1-byte length prefix + bytes; param = max length (low u16)
                {
                    int n = ReadU8(data, ref offset);
                    RequireMax(ft, n, f.Param & 0xFFFF);
                    value = ReadBytes(data, ref offset, n);
                    break;
                }
                case 0x15: // 2-byte length prefix + bytes; param = max length (low u16)
                {
                    int n = ReadU16(data, ref offset);
                    RequireMax(ft, n, f.Param & 0xFFFF);
                    value = ReadBytes(data, ref offset, n);
                    break;
                }
                case 0x0f: // MP_OPTIONAL: present flag + nested chain
                    present = ReadU8(data, ref offset) != 0;
                    value = present ? (object)ReadSubchain(f, data, ref offset, depth) : null;
                    break;
                case 0x10: // struct array, fixed param count
                    value = ReadArray(f, f.Param, data, ref offset, depth);
                    break;
                case 0x11: // struct array, 1-byte count; must be <= param
                {
                    int n = ReadU8(data, ref offset);
                    RequireMax(ft, n, f.Param);
                    value = ReadArray(f, n, data, ref offset, depth);
                    break;
                }
                case 0x12: // struct array, 2-byte count; must be <= param
                {
                    int n = ReadU16(data, ref offset);
                    RequireMax(ft, n, f.Param);
                    value = ReadArray(f, n, data, ref offset, depth);
                    break;
                }
                case 0x16: // MP_SRV_ALIGN with data present
                    throw new FormatException("msgpack: MP_SRV_ALIGN has data");
                default:
                    throw new FormatException($"msgpack: unknown fieldType 0x{ft:x}");
            }

            return new DecodedField(ft, present, value);
        }

        private static DecodedField[] ReadSubchain(FieldDefinition f, ReadOnlySpan<byte> data,
                                                   ref int offset, int depth)
        {
            FieldDefinition[] reference = f.Reference;
            if (reference == null)
                throw new FormatException($"msgpack: fieldType 0x{f.FieldType:x} has no nested chain");
            return ReadFields(reference, data, ref offset, depth + 1);
        }

        private static IReadOnlyList<DecodedField[]> ReadArray(FieldDefinition f, int count,
                                                               ReadOnlySpan<byte> data,
                                                               ref int offset, int depth)
        {
            if (count < 0 || count > data.Length)
                throw new FormatException($"msgpack: implausible array count {count}");
            var items = new List<DecodedField[]>(count);
            for (int i = 0; i < count; i++)
                items.Add(ReadSubchain(f, data, ref offset, depth));
            return items;
        }

        /// <summary>
        /// The client bounds every variable-length field by its descriptor <c>param</c>
        /// (<c>MsgPack_ReadFields</c> fails when the wire count/length exceeds it). Enforcing it here
        /// keeps the reader from accepting a message the client rejects.
        /// </summary>
        private static void RequireMax(int fieldType, int value, int max)
        {
            if (value > max)
                throw new FormatException(
                    $"msgpack: fieldType 0x{fieldType:x} declares {value}, exceeding param max {max}");
        }

        private static byte ReadU8(ReadOnlySpan<byte> data, ref int off)
        {
            if (off + 1 > data.Length) throw Overrun(off, 1, data.Length);
            return data[off++];
        }

        private static int ReadU16(ReadOnlySpan<byte> data, ref int off)
        {
            if (off + 2 > data.Length) throw Overrun(off, 2, data.Length);
            int v = data[off] | (data[off + 1] << 8);
            off += 2;
            return v;
        }

        private static uint ReadU32(ReadOnlySpan<byte> data, ref int off)
        {
            if (off + 4 > data.Length) throw Overrun(off, 4, data.Length);
            uint v = (uint)(data[off] | (data[off + 1] << 8) | (data[off + 2] << 16) | (data[off + 3] << 24));
            off += 4;
            return v;
        }

        private static byte[] ReadBytes(ReadOnlySpan<byte> data, ref int off, int n)
        {
            if (n < 0) throw new FormatException($"msgpack: negative length {n}");
            if (off + n > data.Length) throw Overrun(off, n, data.Length);
            byte[] result = data.Slice(off, n).ToArray();
            off += n;
            return result;
        }

        private static ulong ReadVarint(ReadOnlySpan<byte> data, ref int off)
        {
            ulong value = 0;
            int shift = 0;
            for (int i = 0; i < 5; i++)
            {
                if (off >= data.Length) throw new MsgPackTruncatedException("msgpack: varint end of payload");
                byte b = data[off++];
                value |= (ulong)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) return value & 0xFFFFFFFF;
                shift += 7;
            }
            throw new FormatException("msgpack: varint exceeds 5 bytes");
        }

        private static string ReadCString16(ReadOnlySpan<byte> data, ref int off)
        {
            int start = off;
            while (off + 1 < data.Length)
            {
                if (data[off] == 0 && data[off + 1] == 0)
                {
                    string s = Encoding.Unicode.GetString(data.Slice(start, off - start).ToArray());
                    off += 2;
                    return s;
                }
                off += 2;
            }
            throw new MsgPackTruncatedException("msgpack: unterminated utf16 string");
        }

        private static string ReadCString8(ReadOnlySpan<byte> data, ref int off)
        {
            int start = off;
            while (off < data.Length)
            {
                if (data[off] == 0)
                {
                    string s = Encoding.UTF8.GetString(data.Slice(start, off - start).ToArray());
                    off++;
                    return s;
                }
                off++;
            }
            throw new MsgPackTruncatedException("msgpack: unterminated string");
        }

        private static MsgPackTruncatedException Overrun(int off, int n, int length) =>
            new MsgPackTruncatedException($"msgpack: read of {n} bytes at {off} overruns payload of {length}");
    }
}
