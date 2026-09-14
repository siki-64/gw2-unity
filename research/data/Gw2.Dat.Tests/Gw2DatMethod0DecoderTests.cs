using Gw2.Core.Dat;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Gw2.Dat.Tests;

[TestClass]
public sealed class Gw2DatMethod0DecoderTests
{
    [TestMethod]
    public void ExactMultipleOfCrcBlocksPreservesAllPayloadBytes()
    {
        var raw = Enumerable.Range(0, 0x20000).Select(static i => (byte)i).ToArray();
        var expected = raw[..0xFFFC].Concat(raw[0x10000..0x1FFFC]).ToArray();
        CollectionAssert.AreEqual(expected, Gw2DatMethod0Decoder.StripCrc32(raw));
    }

    [TestMethod]
    public void TruncatedStreamCannotSynthesizeZeroBits()
    {
        using var output = new MemoryStream();
        Assert.Throws<InvalidDataException>(() =>
            Gw2DatMethod0Decoder.DecodeMethod0([], 1, output));
    }

    [TestMethod]
    public void MethodZeroDecodesLiteralsAndRejectsTruncation()
    {
        var writer = new BitWriter();
        writer.Write(0, 4);
        writer.Write(0, 4);
        WriteUniformTable(writer, 286, 10);
        WriteSingletonTable(writer);
        writer.Write(0, 4);
        writer.Write(1023 - 65, 10);
        writer.Write(1023 - 66, 10);
        var compressed = writer.Finish();
        using var output = new MemoryStream();
        Gw2DatMethod0Decoder.DecodeMethod0(compressed, 2, output);
        CollectionAssert.AreEqual(new byte[] { 65, 66 }, output.ToArray());
        Assert.Throws<InvalidDataException>(() =>
            Gw2DatMethod0Decoder.DecodeMethod0(compressed[..^4], 2, Stream.Null));
    }

    [TestMethod]
    public void StripCrc32RemovesChunkAndTrailingWords()
    {
        var raw = Enumerable.Range(0, 0x10000 + 16).Select(static value => (byte)value).ToArray();

        var stripped = Gw2DatMethod0Decoder.StripCrc32(raw);
        var expected = raw[..0xFFFC].Concat(raw[0x10000..0x1000C]).ToArray();

        CollectionAssert.AreEqual(expected, stripped);
    }

    [TestMethod]
    public void MethodOneIsRejectedExplicitly()
    {
        using var output = new MemoryStream();

        var exception = Assert.Throws<NotSupportedException>(() =>
            Gw2DatMethod0Decoder.DecodeMethod0([0, 0, 0, 0x10], 1, output));

        StringAssert.Contains(exception.Message, "Method 1");
    }

    [TestMethod]
    public void MethodOneCopiesFromTheOldSourceUsingTheDeltaToken()
    {
        var writer = new BitWriter();
        writer.Write(1, 4); // Method 1.
        writer.Write(0, 4); // regular match add = 1.
        writer.Write(0, 4); // source match add = 1.

        // The first table needs symbol 0x11D, the first source-copy symbol.
        // A complete ten-bit table gives every symbol a valid code and keeps
        // the fixture independent of any real patch payload.
        WriteUniformTable(writer, 0x11E, 10);
        WriteSingletonTable(writer);
        WriteSingletonTable(writer);
        writer.Write(0, 4); // one 0x1000-symbol block.
        writer.Write(738, 10); // symbol 285 (1023 - 285).
        writer.Write(1, 1); // source-distance symbol 0: delta +0.

        using var output = new MemoryStream();
        Gw2DatMethod0Decoder.DecodeMethod1(writer.Finish(), 1, [0xA5], output);

        CollectionAssert.AreEqual(new byte[] { 0xA5 }, output.ToArray());
    }

    private static void WriteSingletonTable(BitWriter writer)
    {
        writer.Write(1, 16);
        writer.Write(27, 10); // fixed meta code for RLE value 0x01.
    }

    private static void WriteUniformTable(BitWriter writer, ushort symbolCount, int codeLength)
    {
        Assert.AreEqual(286, symbolCount);
        Assert.AreEqual(10, codeLength);
        writer.Write(symbolCount, 16);
        for (var i = 0; i < 35; i++)
            writer.Write(80, 15); // meta code for RLE value 0xEA (8 x length 10).
        writer.Write(13, 11); // meta code for RLE value 0xAA (6 x length 10).
    }

    private sealed class BitWriter
    {
        private readonly List<byte> _bytes = [];
        private uint _word;
        private int _bits;

        public void Write(uint value, int bitCount)
        {
            for (var shift = bitCount - 1; shift >= 0; shift--)
            {
                _word |= ((value >> shift) & 1u) << (31 - _bits);
                _bits++;
                if (_bits == 32)
                {
                    FlushWord();
                    _bits = 0;
                    _word = 0;
                }
            }
        }

        public byte[] Finish()
        {
            if (_bits != 0)
                FlushWord();
            return [.. _bytes];
        }

        private void FlushWord()
        {
            _bytes.Add((byte)_word);
            _bytes.Add((byte)(_word >> 8));
            _bytes.Add((byte)(_word >> 16));
            _bytes.Add((byte)(_word >> 24));
        }
    }
}
