using System;
using System.IO;
using System.Text.Json;
using Gw2.Protocol;
using Gw2.Protocol.MsgPack;
using Gw2.Protocol.Schema;
using Gw2.Protocol.Transport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Gw2.Protocol.Tests
{
    [TestClass]
    public class ProtocolCodecTests
    {
        private static string Hex(ReadOnlySpan<byte> b) => Convert.ToHexString(b).ToLowerInvariant();

        private static ProtocolSchemaCorpus Corpus() => ProtocolSchemaCorpus.FromJson(
            Fixtures.ReadText("chains-recv.json"),
            Fixtures.ReadText("chains-send.json"));

        private static TransportCipherState State(JsonElement cs) => TransportCipherState.FromCapturedState(
            Convert.ToInt32(cs.GetProperty("i").GetString(), 16),
            Convert.ToInt32(cs.GetProperty("j").GetString(), 16),
            Convert.FromHexString(cs.GetProperty("sboxHex").GetString()));

        [TestMethod]
        public void Inbound_EndToEnd_FromCiphertext()
        {
            using var doc = JsonDocument.Parse(Fixtures.ReadBytes("0x264-wire.json"));
            var root = doc.RootElement;
            byte[] wire = Convert.FromHexString(root.GetProperty("wireHex").GetString());

            var codec = new ProtocolCodec(Corpus(), State(root.GetProperty("cipherState")));
            var messages = codec.DecodeInbound(wire);

            Assert.AreEqual(2, messages.Count);
            Assert.AreEqual(0x264, messages[0].MessageId);
            Assert.AreEqual(0x1206UL, (ulong)messages[0].Fields[1].Value);
            Assert.AreEqual(0x122BUL, (ulong)messages[1].Fields[1].Value);
        }

        [TestMethod]
        public void Inbound_BuffersAPacketSplitMidFrame()
        {
            using var doc = JsonDocument.Parse(Fixtures.ReadBytes("0x264-wire.json"));
            var root = doc.RootElement;
            byte[] wire = Convert.FromHexString(root.GetProperty("wireHex").GetString());

            var codec = new ProtocolCodec(Corpus(), State(root.GetProperty("cipherState")));
            Assert.AreEqual(0, codec.DecodeInbound(wire.AsSpan(0, 10)).Count);
            var messages = codec.DecodeInbound(wire.AsSpan(10));
            Assert.AreEqual(2, messages.Count);
        }

        [TestMethod]
        public void Outbound_RoundTripsTheCapturedPacket()
        {
            using var doc = JsonDocument.Parse(Fixtures.ReadBytes("outbound-0x120.json"));
            var root = doc.RootElement;
            byte[] plaintext = Convert.FromHexString(root.GetProperty("plaintextHex").GetString());
            byte[] wire = Convert.FromHexString(root.GetProperty("wireHex").GetString());
            var corpus = Corpus();

            // Decode with the send corpus, then re-encode and re-encrypt.
            var decoded = MessageStreamDecoder.Decode(
                corpus.ForDirection(TrafficDirection.ClientToServer), plaintext);
            Assert.AreEqual(1, decoded.Count);

            Assert.IsTrue(corpus.Send.TryGet(0x120, out MessageSchema sendSchema));
            byte[] encoded = MsgPackWriter.WriteMessage(sendSchema, decoded[0].Fields);
            Assert.AreEqual(Hex(plaintext), Hex(encoded));

            var codec = new OutboundProtocolCodec(corpus, State(root.GetProperty("cipherState")));
            Assert.AreEqual(Hex(wire), Hex(codec.Encode(0x120, decoded[0].Fields)));
        }
    }
}
