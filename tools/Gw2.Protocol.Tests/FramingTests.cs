using System;
using System.IO;
using System.Text.Json;
using Gw2.Protocol.Framing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Gw2.Protocol.Tests
{
    [TestClass]
    public class FramingTests
    {
        private static string Hex(ReadOnlySpan<byte> b) => Convert.ToHexString(b).ToLowerInvariant();

        [TestMethod]
        public void Deframe_Captured0264Frame_YieldsMessageStream()
        {
            using var doc = JsonDocument.Parse(Fixtures.ReadBytes("0x264-wire.json"));
            byte[] frame = Convert.FromHexString(
                doc.RootElement.GetProperty("transportFrameHex").GetString());

            byte[] stream = TransportFrame.Deframe(frame, out int consumed);
            Assert.AreEqual(frame.Length, consumed);
            Assert.AreEqual("640286240200016402ab24010001", Hex(stream));
        }

        [TestMethod]
        public void Lz4_DecompressesHandBuiltBlock()
        {
            // token 0x10 (1 literal, match ext 0), literal 'A', offset 1, match length 4 -> "AAAAA".
            byte[] block = { 0x10, 0x41, 0x01, 0x00 };
            Assert.AreEqual("4141414141", Hex(Lz4Block.Decompress(block, 5)));
        }

        [TestMethod]
        public void Deframe_RawFrame()
        {
            byte[] frame = { 0x00, 0x00, 0x03, 0x00, 0xAA, 0xBB, 0xCC };
            byte[] stream = TransportFrame.Deframe(frame, out int consumed);
            Assert.AreEqual(7, consumed);
            Assert.AreEqual("aabbcc", Hex(stream));
        }

        [TestMethod]
        public void Deframe_PartialFrame_IsNotConsumed()
        {
            // Header promises a 4-byte payload that is absent.
            byte[] frame = { 0x00, 0x00, 0x04, 0x00, 0xAA };
            byte[] stream = TransportFrame.Deframe(frame, out int consumed);
            Assert.AreEqual(0, consumed);
            Assert.AreEqual(0, stream.Length);
        }

        [TestMethod]
        public void Lz4_MalformedInput_Throws()
        {
            Assert.ThrowsExactly<FormatException>(
                () => Lz4Block.Decompress(new byte[] { 0x10, 0x41, 0x02, 0x00 }, 5)); // offset > output
            Assert.ThrowsExactly<FormatException>(
                () => Lz4Block.Decompress(new byte[] { 0x00, 0x41, 0x01, 0x00 }, 5)); // no literals, bad match
            Assert.ThrowsExactly<FormatException>(
                () => Lz4Block.Decompress(new byte[] { 0x10, 0x41, 0x01, 0x00 }, 4)); // length mismatch
        }
    }
}
