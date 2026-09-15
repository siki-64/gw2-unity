using System;
using System.IO;
using System.Text.Json;
using Gw2.Protocol.MsgPack;
using Gw2.Protocol.Schema;
using Gw2.Protocol.Transport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Gw2.Protocol.Tests
{
    [TestClass]
    public class OutboundTests
    {
        private static string Hex(ReadOnlySpan<byte> b) => Convert.ToHexString(b).ToLowerInvariant();

        [TestMethod]
        public void Captured0x120_DecryptsAndDecodesWithTheSendChain()
        {
            using var doc = JsonDocument.Parse(File.ReadAllBytes(
                Path.Combine(AppContext.BaseDirectory, "fixtures", "outbound-0x120.json")));
            var root = doc.RootElement;
            byte[] plaintext = Convert.FromHexString(root.GetProperty("plaintextHex").GetString());
            byte[] wire = Convert.FromHexString(root.GetProperty("wireHex").GetString());
            var cs = root.GetProperty("cipherState");
            int i = Convert.ToInt32(cs.GetProperty("i").GetString(), 16);
            int j = Convert.ToInt32(cs.GetProperty("j").GetString(), 16);
            byte[] sbox = Convert.FromHexString(cs.GetProperty("sboxHex").GetString());

            // The outbound cipher: Crypt(state, plaintext) must equal the captured wire bytes.
            var state = TransportCipherState.FromCapturedState(i, j, sbox);
            Assert.AreEqual(Hex(wire), Hex(TransportCipher.Crypt(state, plaintext)));

            // Outbound uses the send-side chain (table A): [MP_MSGID(0x120), varint, u8].
            var schema = MessageSchema.FromChain(new[]
            {
                new FieldDefinition(1, 0x120),
                new FieldDefinition(4),
                new FieldDefinition(2),
            });
            var messages = MessageStreamDecoder.Decode(new MessageSchemaSet(new[] { schema }), plaintext);

            Assert.AreEqual(1, messages.Count);
            Assert.AreEqual(0x120, messages[0].MessageId);
            Assert.AreEqual(6, messages[0].WireLength);
            Assert.AreEqual(0x4788UL, (ulong)messages[0].Fields[1].Value);
            Assert.AreEqual(1UL, (ulong)messages[0].Fields[2].Value);
        }
    }
}
