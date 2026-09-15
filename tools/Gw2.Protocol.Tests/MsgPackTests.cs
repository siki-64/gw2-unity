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
    public class MsgPackTests
    {
        // fieldType 1 = MP_MSGID, 4 = base-128 varint, 2 = u8 (msg-dispatch-addenda Addendum 8).
        private static MessageSchema ConfiguredSkillSchema() => MessageSchema.FromChain(new[]
        {
            new FieldDefinition(1, 0x264),
            new FieldDefinition(4),
            new FieldDefinition(2),
            new FieldDefinition(2),
            new FieldDefinition(4),
        });

        [TestMethod]
        public void DecodesCaptured0264MessageStream()
        {
            using var doc = JsonDocument.Parse(File.ReadAllBytes(
                Path.Combine(AppContext.BaseDirectory, "fixtures", "0x264-wire.json")));
            byte[] frame = Convert.FromHexString(
                doc.RootElement.GetProperty("transportFrameHex").GetString());
            byte[] stream = TransportFrame.Deframe(frame, out _);

            var schemas = new MessageSchemaSet(new[] { ConfiguredSkillSchema() });
            var messages = MessageStreamDecoder.Decode(schemas, stream);

            Assert.AreEqual(2, messages.Count);
            Assert.AreEqual(0x264, messages[0].MessageId);
            Assert.AreEqual(7, messages[0].WireLength);
            Assert.AreEqual(0x264UL, (ulong)messages[0].Fields[0].Value);
            Assert.AreEqual(0x1206UL, (ulong)messages[0].Fields[1].Value); // varint 86 24
            Assert.AreEqual(2UL, (ulong)messages[0].Fields[2].Value);
            Assert.AreEqual(0UL, (ulong)messages[0].Fields[3].Value);
            Assert.AreEqual(1UL, (ulong)messages[0].Fields[4].Value);
            Assert.AreEqual(0x122BUL, (ulong)messages[1].Fields[1].Value); // varint AB 24
            Assert.AreEqual(1UL, (ulong)messages[1].Fields[2].Value);
        }

        [TestMethod]
        public void UnknownMessageId_Throws()
        {
            var schemas = new MessageSchemaSet(new MessageSchema[0]);
            Assert.ThrowsExactly<FormatException>(
                () => MessageStreamDecoder.Decode(schemas, new byte[] { 0xAA, 0xBB }));
        }

        [TestMethod]
        public void TruncatedMessage_Throws()
        {
            var schemas = new MessageSchemaSet(new[] { ConfiguredSkillSchema() });
            Assert.ThrowsExactly<MsgPackTruncatedException>(
                () => MessageStreamDecoder.Decode(schemas, new byte[] { 0x64, 0x02 }));
        }

        [TestMethod]
        public void Optional_AbsentThenPresent()
        {
            var schema = MessageSchema.FromChain(new[]
            {
                new FieldDefinition(1, 0x264),
                new FieldDefinition(0x0f, 0, new[] { new FieldDefinition(3) }),
            });
            var schemas = new MessageSchemaSet(new[] { schema });

            var absent = MessageStreamDecoder.Decode(schemas, new byte[] { 0x64, 0x02, 0x00 });
            Assert.IsFalse(absent[0].Fields[1].Present);
            Assert.IsNull(absent[0].Fields[1].Value);

            var present = MessageStreamDecoder.Decode(schemas, new byte[] { 0x64, 0x02, 0x01, 0x34, 0x12 });
            Assert.IsTrue(present[0].Fields[1].Present);
            var nested = (DecodedField[])present[0].Fields[1].Value;
            Assert.AreEqual(0x1234UL, (ulong)nested[0].Value);
        }

        [TestMethod]
        public void StructArray_ByCountByte()
        {
            var schema = MessageSchema.FromChain(new[]
            {
                new FieldDefinition(1, 0x264),
                new FieldDefinition(0x11, 0, new[] { new FieldDefinition(2) }),
            });
            var schemas = new MessageSchemaSet(new[] { schema });
            var messages = MessageStreamDecoder.Decode(schemas, new byte[] { 0x64, 0x02, 0x02, 0x07, 0x09 });

            var items = (System.Collections.Generic.IReadOnlyList<DecodedField[]>)messages[0].Fields[1].Value;
            Assert.AreEqual(2, items.Count);
            Assert.AreEqual(7UL, (ulong)items[0][0].Value);
            Assert.AreEqual(9UL, (ulong)items[1][0].Value);
        }

        [TestMethod]
        public void OverlongVarint_Throws()
        {
            var schema = MessageSchema.FromChain(new[]
            {
                new FieldDefinition(1, 0x264),
                new FieldDefinition(4),
            });
            var schemas = new MessageSchemaSet(new[] { schema });
            Assert.ThrowsExactly<FormatException>(() => MessageStreamDecoder.Decode(
                schemas, new byte[] { 0x64, 0x02, 0x80, 0x80, 0x80, 0x80, 0x80, 0x00 }));
        }
    }
}
