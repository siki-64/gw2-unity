using System;
using System.IO;
using System.Text.Json;
using Gw2.Protocol.Framing;
using Gw2.Protocol.MsgPack;
using Gw2.Protocol.Schema;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Gw2.Protocol.Tests
{
    [TestClass]
    public class MessageSchemaJsonTests
    {
        private static string CorpusJson =>
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "chains-recv.json"));

        private static string WireJson =>
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "0x264-wire.json"));

        [TestMethod]
        public void LoadsCorpus()
        {
            Assert.AreEqual(205780, MessageSchemaJson.ReadBuild(CorpusJson));
            var schemas = MessageSchemaJson.Parse(CorpusJson);
            Assert.AreEqual(1241, schemas.Count);

            Assert.IsTrue(schemas.TryGet(0x264, out var s));
            Assert.AreEqual(5, s.Fields.Length);
            Assert.AreEqual(1, s.Fields[0].FieldType);
            Assert.AreEqual(0x264, s.Fields[0].Param);
            Assert.AreEqual(4, s.Fields[1].FieldType);
            Assert.AreEqual(2, s.Fields[2].FieldType);
            Assert.AreEqual(2, s.Fields[3].FieldType);
            Assert.AreEqual(4, s.Fields[4].FieldType);
        }

        [TestMethod]
        public void DecodesCaptured0264WithTheCorpus()
        {
            var schemas = MessageSchemaJson.Parse(CorpusJson);

            using var doc = JsonDocument.Parse(WireJson);
            byte[] frame = Convert.FromHexString(
                doc.RootElement.GetProperty("transportFrameHex").GetString());
            byte[] stream = TransportFrame.Deframe(frame, out _);

            var messages = MessageStreamDecoder.Decode(schemas, stream);
            Assert.AreEqual(2, messages.Count);
            Assert.AreEqual(0x264, messages[0].MessageId);
            Assert.AreEqual(0x1206UL, (ulong)messages[0].Fields[1].Value);
            Assert.AreEqual(0x122BUL, (ulong)messages[1].Fields[1].Value);
        }

        [TestMethod]
        public void RejectsMalformedCorpus()
        {
            Assert.ThrowsExactly<FormatException>(
                () => MessageSchemaJson.Parse("{\"build\":205780}"));
            Assert.Throws<JsonException>(
                () => MessageSchemaJson.Parse("{ not json"));
        }
    }
}
