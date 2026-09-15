using System;
using System.Collections.Generic;
using Gw2.Protocol.Framing;
using Gw2.Protocol.MsgPack;
using Gw2.Protocol.Schema;
using Gw2.Protocol.Transport;

namespace Gw2.Protocol
{
    /// <summary>
    /// End-to-end inbound codec for one game connection: transport cipher -> frame container
    /// (with LZ4) -> direction-aware schema decode, in one call per packet.
    /// <para>
    /// It is stateful: the transport cipher state advances per packet, frames may span packets, and
    /// a message may span frames, so undecoded tails are buffered across calls. Inbound uses the
    /// recv corpus; outbound uses <see cref="OutboundProtocolCodec"/>.
    /// </para>
    /// </summary>
    public sealed class ProtocolCodec
    {
        private readonly ProtocolSchemaCorpus _corpus;
        private readonly TransportCipherState _cipher;
        private readonly List<byte> _transport = new List<byte>(); // decrypted, not yet deframed
        private readonly List<byte> _stream = new List<byte>();    // message stream

        public ProtocolCodec(ProtocolSchemaCorpus corpus, TransportCipherState cipherState)
        {
            _corpus = corpus ?? throw new ArgumentNullException(nameof(corpus));
            _cipher = cipherState ?? throw new ArgumentNullException(nameof(cipherState));
        }

        public MessageSchemaSet RecvSchemas => _corpus.Recv;

        /// <summary>
        /// Decrypt one inbound packet and return every message it completes. A trailing partial
        /// frame, or a partial message, is retained for the next call.
        /// </summary>
        public IReadOnlyList<DecodedMessage> DecodeInbound(ReadOnlySpan<byte> ciphertext)
        {
            // 1. Decrypt (advances the persistent cipher state).
            _transport.AddRange(TransportCipher.Crypt(_cipher, ciphertext));

            // 2. Deframe every complete frame; leave a partial trailing frame buffered.
            int off = 0;
            while (off + 4 <= _transport.Count)
            {
                int compLen = _transport[off] | (_transport[off + 1] << 8);
                int decodedLen = _transport[off + 2] | (_transport[off + 3] << 8);
                int size = compLen != 0 ? compLen : decodedLen;
                if (off + 4 + size > _transport.Count) break;

                if (compLen == 0)
                {
                    for (int k = 0; k < decodedLen; k++) _stream.Add(_transport[off + 4 + k]);
                }
                else
                {
                    byte[] payload = _transport.GetRange(off + 4, size).ToArray();
                    _stream.AddRange(Lz4Block.Decompress(payload, decodedLen));
                }
                off += 4 + size;
            }
            if (off > 0) _transport.RemoveRange(0, off);

            // 3. Decode complete messages; keep a truncated tail buffered.
            byte[] buffer = _stream.ToArray();
            IReadOnlyList<DecodedMessage> messages =
                MessageStreamDecoder.TryDecode(_corpus.Recv, buffer, out int consumed);
            if (consumed > 0) _stream.RemoveRange(0, consumed);
            return messages;
        }
    }
}
