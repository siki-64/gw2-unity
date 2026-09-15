using System;
using System.Collections.Generic;
using Gw2.Protocol.MsgPack;
using Gw2.Protocol.Schema;
using Gw2.Protocol.Transport;

namespace Gw2.Protocol
{
    /// <summary>
    /// Outbound codec for a game connection: schema encode -> transport cipher. There is no
    /// compression container on the outbound side (build 205.780), and outbound uses the **send**
    /// corpus, which has different chains from recv for the same id.
    /// </summary>
    public sealed class OutboundProtocolCodec
    {
        private readonly ProtocolSchemaCorpus _corpus;
        private readonly TransportCipherState _cipher;

        public OutboundProtocolCodec(ProtocolSchemaCorpus corpus, TransportCipherState cipherState)
        {
            _corpus = corpus ?? throw new ArgumentNullException(nameof(corpus));
            _cipher = cipherState ?? throw new ArgumentNullException(nameof(cipherState));
        }

        public MessageSchemaSet SendSchemas => _corpus.Send;

        /// <summary>
        /// Encode a message (the field list is chain-aligned and includes MP_MSGID as field 0) and
        /// encrypt it, returning the wire bytes. Advances the outbound cipher state.
        /// </summary>
        public byte[] Encode(int messageId, IReadOnlyList<DecodedField> fields)
        {
            if (!_corpus.Send.TryGet(messageId, out MessageSchema schema))
                throw new FormatException($"msgpack: unknown send message id 0x{messageId:x}");
            byte[] plaintext = MsgPackWriter.WriteMessage(schema, fields);
            return TransportCipher.Crypt(_cipher, plaintext);
        }
    }
}
