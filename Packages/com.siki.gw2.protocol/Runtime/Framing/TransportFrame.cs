using System;
using System.Collections.Generic;

namespace Gw2.Protocol.Framing
{
    /// <summary>
    /// The inbound transport frame container (build 205.780, <c>MsgConn.cpp</c>).
    /// <para>
    /// After the transport cipher is removed, the buffer is a concatenation of frames:
    /// <c>[u16 compLen][u16 decodedLen][payload]</c>. When <c>compLen == 0</c> the payload is
    /// <c>decodedLen</c> raw bytes; otherwise it is an LZ4 block of <c>compLen</c> bytes that
    /// expands to <c>decodedLen</c>. Concatenating the decoded payloads yields the message
    /// stream that the schema reader consumes.
    /// </para>
    /// </summary>
    public static class TransportFrame
    {
        /// <summary>
        /// Deframe as many complete frames as <paramref name="input"/> holds, returning the
        /// concatenated decoded payloads. <paramref name="consumed"/> is the number of input
        /// bytes used; a trailing partial frame is left unconsumed (0..3 header bytes, or a
        /// payload that runs past the end).
        /// </summary>
        public static byte[] Deframe(ReadOnlySpan<byte> input, out int consumed)
        {
            var stream = new List<byte>();
            int off = 0;
            while (off + 4 <= input.Length)
            {
                int compLen = input[off] | (input[off + 1] << 8);
                int decodedLen = input[off + 2] | (input[off + 3] << 8);
                int payloadSize = compLen != 0 ? compLen : decodedLen;
                if (off + 4 + payloadSize > input.Length) break;

                ReadOnlySpan<byte> payload = input.Slice(off + 4, payloadSize);
                if (compLen == 0)
                {
                    for (int k = 0; k < decodedLen; k++) stream.Add(payload[k]);
                }
                else
                {
                    stream.AddRange(Lz4Block.Decompress(payload, decodedLen));
                }
                off += 4 + payloadSize;
            }
            consumed = off;
            return stream.ToArray();
        }

        /// <summary>Convenience overload for a buffer that is exactly one or more whole frames.</summary>
        public static byte[] Deframe(ReadOnlySpan<byte> input) => Deframe(input, out _);
    }
}
