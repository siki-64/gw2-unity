using System.Buffers.Binary;

namespace Gw2.Dat;

/// <summary>
/// Managed decoder for ArenaNet's CmpDecompress Method 0 and Method 1 streams.
/// </summary>
public static class Gw2DatMethod0Decoder
{
    private const int CrcChunkSize = 0x10000;
    private const int CrcSize = 4;

    private static readonly byte[] LengthExtra =
    [
        0, 0, 0, 0, 0, 0, 0, 0,
        1, 1, 1, 1,
        2, 2, 2, 2,
        3, 3, 3, 3,
        4, 4, 4, 4,
        5, 5, 5, 5,
        0, 0, 0, 0,
    ];

    private static readonly ushort[] LengthBase =
    [
        0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7,
        0x8, 0xa, 0xc, 0xe,
        0x10, 0x14, 0x18, 0x1c,
        0x20, 0x28, 0x30, 0x38,
        0x40, 0x50, 0x60, 0x70,
        0x80, 0xa0, 0xc0, 0xe0,
        0xff, 0x0, 0x0, 0x0,
    ];

    private static readonly byte[] DistanceExtra =
    [
        0, 0, 0, 0,
        1, 1, 2, 2,
        3, 3, 4, 4,
        5, 5, 6, 6,
        7, 7, 8, 8,
        9, 9, 10, 10,
        11, 11, 12, 12,
        13, 13, 14, 14,
    ];

    private static readonly uint[] DistanceBase =
    [
        0x0, 0x1, 0x2, 0x3, 0x4, 0x6, 0x8, 0xc,
        0x10, 0x18, 0x20, 0x30,
        0x40, 0x60, 0x80, 0xc0,
        0x100, 0x180, 0x200, 0x300,
        0x400, 0x600, 0x800, 0xc00,
        0x1000, 0x1800, 0x2000, 0x3000,
        0x4000, 0x6000, 0x0, 0x0,
    ];

    // Method 1 uses the same 16-bit distance-base table for its signed source
    // deltas, but has a separate extra-bit table.  The current-build Ghidra
    // data at 0x1420A23B0 contains four copies of each extra-bit count.
    private const int SpecialDistanceExtraCount = 0x80;

    private static readonly MetaRow[] MetaTable =
    [
        new(0xa0000000u, 2, 3),
        new(0x60000000u, 6, 4),
        new(0x40000000u, 10, 5),
        new(0x20000000u, 18, 6),
        new(0x12000000u, 25, 7),
        new(0x0c000000u, 31, 8),
        new(0x07000000u, 41, 9),
        new(0x03000000u, 57, 10),
        new(0x01600000u, 70, 11),
        new(0x00f00000u, 77, 12),
        new(0x00c00000u, 83, 13),
        new(0x00b00000u, 87, 14),
        new(0x00a00000u, 95, 15),
        new(0x00000000u, 255, 16),
    ];

    private static readonly byte[] MetaValues =
    [
        0x8, 0x9, 0xa, 0x0, 0x7, 0xb, 0xc, 0x6, 0x29, 0x2a, 0xe0, 0x4, 0x5, 0x20, 0x28, 0x2b,
        0x2c, 0x40, 0x4a, 0x3, 0xd, 0x25, 0x26, 0x27, 0x48, 0x49, 0x24, 0x47, 0x4b, 0x4c, 0x69, 0x6a,
        0x23, 0x46, 0x60, 0x63, 0x67, 0x68, 0x88, 0x89, 0xa0, 0xe8, 0x1, 0x2, 0x2d, 0x43, 0x44, 0x45,
        0x65, 0x66, 0x80, 0x87, 0x8a, 0xa8, 0xa9, 0xc0, 0xc9, 0xe9, 0xe, 0x4d, 0x64, 0x6b, 0x6c, 0x84,
        0x85, 0x8b, 0xa4, 0xa5, 0xaa, 0xc8, 0xe5, 0x83, 0x86, 0xa6, 0xa7, 0xc7, 0xca, 0xe7, 0x22, 0x2e,
        0x8c, 0xc4, 0xe4, 0xe6, 0x4e, 0x6d, 0xc6, 0xec, 0xf, 0x10, 0x11, 0x8d, 0xab, 0xac, 0xcc, 0xea,
        0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1a, 0x1b, 0x1c, 0x1d, 0x1e, 0x1f, 0x21, 0x2f,
        0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3a, 0x3b, 0x3c, 0x3d, 0x3e, 0x3f,
        0x41, 0x42, 0x4f, 0x50, 0x51, 0x52, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59, 0x5a, 0x5b, 0x5c,
        0x5d, 0x5e, 0x5f, 0x61, 0x62, 0x6e, 0x6f, 0x70, 0x71, 0x72, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78,
        0x79, 0x7a, 0x7b, 0x7c, 0x7d, 0x7e, 0x7f, 0x81, 0x82, 0x8e, 0x8f, 0x90, 0x91, 0x92, 0x93, 0x94,
        0x95, 0x96, 0x97, 0x98, 0x99, 0x9a, 0x9b, 0x9c, 0x9d, 0x9e, 0x9f, 0xa1, 0xa2, 0xa3, 0xad, 0xae,
        0xaf, 0xb0, 0xb1, 0xb2, 0xb3, 0xb4, 0xb5, 0xb6, 0xb7, 0xb8, 0xb9, 0xba, 0xbb, 0xbc, 0xbd, 0xbe,
        0xbf, 0xc1, 0xc2, 0xc3, 0xc5, 0xcb, 0xcd, 0xce, 0xcf, 0xd0, 0xd1, 0xd2, 0xd3, 0xd4, 0xd5, 0xd6,
        0xd7, 0xd8, 0xd9, 0xda, 0xdb, 0xdc, 0xdd, 0xde, 0xdf, 0xe1, 0xe2, 0xe3, 0xeb, 0xed, 0xee, 0xef,
        0xf0, 0xf1, 0xf2, 0xf3, 0xf4, 0xf5, 0xf6, 0xf7, 0xf8, 0xf9, 0xfa, 0xfb, 0xfc, 0xfd, 0xfe, 0xff,
    ];

    /// Removes the periodic CRC words from a raw MFT entry.
    public static byte[] StripCrc32(ReadOnlySpan<byte> raw)
    {
        if (raw.Length == 0)
            return [];

        if (raw.Length == CrcChunkSize)
            return raw[..^CrcSize].ToArray();

        if (raw.Length < CrcChunkSize)
            return raw.Length > CrcSize ? raw[..^CrcSize].ToArray() : [];

        var blockCount = raw.Length / CrcChunkSize;
        var intermediateLength = checked(raw.Length - blockCount * CrcSize);
        var remainderLength = raw.Length % CrcChunkSize;
        if (remainderLength is > 0 and < CrcSize)
            throw new InvalidDataException("truncated GW2 CRC word");
        var outputLength = intermediateLength - (remainderLength == 0 ? 0 : CrcSize);
        var output = new byte[outputLength];
        var written = 0;

        for (var block = 0; block < blockCount && written < output.Length; block++)
        {
            var source = raw.Slice(block * CrcChunkSize, CrcChunkSize - CrcSize);
            var count = Math.Min(source.Length, output.Length - written);
            source[..count].CopyTo(output.AsSpan(written));
            written += count;
        }

        var remainderOffset = blockCount * CrcChunkSize;
        if (remainderOffset < raw.Length && written < output.Length)
        {
            var remainder = raw[remainderOffset..];
            var count = Math.Min(remainder.Length, output.Length - written);
            remainder[..count].CopyTo(output.AsSpan(written));
        }

        return output;
    }

    /// Decodes the full raw MFT entry to the supplied destination stream.
    public static void DecodeEntry(ReadOnlySpan<byte> rawEntry, ushort compression, Stream destination)
        => DecodeEntry(rawEntry, compression, destination, []);

    /// <summary>
    /// Decodes an MFT entry, optionally supplying the previous source buffer
    /// required by a Method-1 delta stream.
    /// </summary>
    public static void DecodeEntry(
        ReadOnlySpan<byte> rawEntry,
        ushort compression,
        Stream destination,
        ReadOnlySpan<byte> oldSource)
    {
        ArgumentNullException.ThrowIfNull(destination);

        if (compression == 0)
        {
            destination.Write(StripCrc32(rawEntry));
            return;
        }

        if (compression != 8)
            throw new NotSupportedException($"GW2 compression flag 0x{compression:X4} is not supported");
        var stripped = StripCrc32(rawEntry);
        if (stripped.Length < 8)
            throw new InvalidDataException("compressed GW2 entry is missing its 8-byte header");

        var expectedSize = BinaryPrimitives.ReadUInt32LittleEndian(stripped.AsSpan(4, 4));
        DecodeCompressed(stripped.AsSpan(8), expectedSize, oldSource, destination);
    }

    /// <summary>
    /// Decodes a stripped CmpDecompress bitstream. Method 0 is the ordinary
    /// Huffman/LZ stream; Method 1 is the patch/delta stream and reads from
    /// <paramref name="oldSource"/> for source-copy tokens.
    /// </summary>
    public static void DecodeCompressed(
        ReadOnlySpan<byte> compressed,
        uint outputSize,
        ReadOnlySpan<byte> oldSource,
        Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        var reader = new BitReader(compressed);
        var method = reader.Read(4);
        switch (method)
        {
            case 0:
                DecodeMethod0Core(reader, outputSize, destination);
                break;
            case 1:
                DecodeMethod1Core(reader, outputSize, oldSource, destination);
                break;
            default:
                throw new NotSupportedException($"GW2 compression Method {method} is not supported");
        }
    }

    /// Decodes an already stripped Method-0 bitstream.
    public static void DecodeMethod0(ReadOnlySpan<byte> compressed, uint outputSize, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var reader = new BitReader(compressed);
        var method = reader.Read(4);
        if (method != 0)
            throw new NotSupportedException($"GW2 compression Method {method} is not supported; Method 1 is delta compression");

        DecodeMethod0Core(reader, outputSize, destination);
    }

    private static void DecodeMethod0Core(BitReader reader, uint outputSize, Stream destination)
    {
        var minMatchAdd = reader.Read(4) + 1u;
        using var output = new DecoderOutput(destination, HistorySize);

        while (output.Count < outputSize)
        {
            var literalTable = HuffmanTable.Build(reader);
            var distanceTable = HuffmanTable.Build(reader);
            var blockSymbols = (reader.Read(4) + 1u) << 12;

            for (uint symbolIndex = 0; symbolIndex < blockSymbols && output.Count < outputSize; symbolIndex++)
            {
                var symbol = literalTable.Decode(reader);
                if (symbol < 0x100)
                {
                    output.Append((byte)symbol);
                    continue;
                }

                var lengthIndex = symbol - 0x100;
                if (lengthIndex >= LengthExtra.Length)
                    throw new InvalidDataException($"GW2 length symbol 0x{symbol:X} is out of range");

                var extraLengthBits = LengthExtra[lengthIndex];
                var length = (uint)LengthBase[lengthIndex] +
                             (extraLengthBits == 0 ? 0u : reader.Read(extraLengthBits)) +
                             minMatchAdd;

                var distanceSymbol = distanceTable.Decode(reader);
                if (distanceSymbol >= DistanceExtra.Length)
                    throw new InvalidDataException($"GW2 distance symbol 0x{distanceSymbol:X} is out of range");

                var extraDistanceBits = DistanceExtra[distanceSymbol];
                var backDistance = DistanceBase[distanceSymbol] +
                                   (extraDistanceBits == 0 ? 0u : reader.Read(extraDistanceBits)) + 1u;
                if (backDistance > output.Count)
                    throw new InvalidDataException("GW2 back-reference distance is outside the decoded history");

                for (uint copyIndex = 0; copyIndex < length && output.Count < outputSize; copyIndex++)
                    output.Append(output.ReadBack(backDistance));
            }
        }
    }

    /// <summary>
    /// Decodes a stripped Method-1 bitstream. The initial method nibble is
    /// still part of <paramref name="compressed"/>; the old source is the
    /// uncompressed buffer that the game passes as CmpDecompress's fifth
    /// argument during patch application.
    /// </summary>
    public static void DecodeMethod1(
        ReadOnlySpan<byte> compressed,
        uint outputSize,
        ReadOnlySpan<byte> oldSource,
        Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var reader = new BitReader(compressed);
        var method = reader.Read(4);
        if (method != 1)
            throw new InvalidDataException($"GW2 compression stream is Method {method}, not Method 1");

        DecodeMethod1Core(reader, outputSize, oldSource, destination);
    }

    private static void DecodeMethod1Core(
        BitReader reader,
        uint outputSize,
        ReadOnlySpan<byte> oldSource,
        Stream destination)
    {
        var minMatchAdd = reader.Read(4) + 1u;
        var sourceMatchAdd = reader.Read(4) + 1u;
        using var output = new DecoderOutput(destination, HistorySize);

        // local_1C48 and local_1C34 in FUN_140DA0650.  The former is not the
        // number of bytes emitted: for a nearby source span the game advances
        // the source predictor to sourcePosition + length, and for a jump it
        // advances the previous predictor by length.
        long sourcePredictor = 0;
        long previousSourcePosition = 0;

        while (output.Count < outputSize)
        {
            var literalTable = HuffmanTable.Build(reader);
            var distanceTable = HuffmanTable.Build(reader);
            var sourceDistanceTable = HuffmanTable.Build(reader);
            var blockSymbols = checked((reader.Read(4) + 1u) << 12);

            for (uint symbolIndex = 0; symbolIndex < blockSymbols && output.Count < outputSize; symbolIndex++)
            {
                var symbol = literalTable.Decode(reader);
                if (symbol < 0x11D)
                {
                    if (symbol < 0x100)
                    {
                        output.Append((byte)symbol);
                        sourcePredictor = output.Count;
                        continue;
                    }

                    var lengthIndex = (uint)(symbol - 0x100);
                    var length = DecodeLength(reader, lengthIndex, minMatchAdd);
                    var distanceSymbol = distanceTable.Decode(reader);
                    var backDistance = DecodeRegularDistance(reader, distanceSymbol);
                    if (backDistance > output.Count)
                        throw new InvalidDataException("GW2 Method-1 back-reference distance is outside the decoded history");
                    EnsureOutputRoom(output.Count, length, outputSize);

                    for (uint copyIndex = 0; copyIndex < length; copyIndex++)
                        output.Append(output.ReadBack(backDistance));
                    sourcePredictor = output.Count;
                    continue;
                }

                var sourceLengthIndex = (uint)(symbol - 0x11D);
                var sourceLength = DecodeLength(reader, sourceLengthIndex, sourceMatchAdd);
                var sourceDistanceSymbol = sourceDistanceTable.Decode(reader);
                var sourceDistance = DecodeSpecialDistance(reader, sourceDistanceSymbol);

                var signedDelta = (sourceDistanceSymbol & 1) == 0
                    ? (long)sourceDistance
                    : -(long)sourceDistance;
                var sourcePosition = checked(sourcePredictor + signedDelta);
                if (sourcePosition < 0 || sourcePosition > oldSource.Length)
                    throw new InvalidDataException("GW2 Method-1 source position is outside the old source buffer");
                if (sourceLength > (uint)(oldSource.Length - sourcePosition))
                    throw new InvalidDataException("GW2 Method-1 source copy exceeds the old source buffer");
                EnsureOutputRoom(output.Count, sourceLength, outputSize);

                for (uint copyIndex = 0; copyIndex < sourceLength; copyIndex++)
                    output.Append(oldSource[(int)(sourcePosition + copyIndex)]);

                var sourceEnd = checked(sourcePosition + sourceLength);
                var threshold = (long)sourceMatchAdd + 0xFF;
                var distanceFromPrevious = Math.Abs(sourcePosition - previousSourcePosition);
                sourcePredictor = distanceFromPrevious > threshold
                    ? checked(sourcePredictor + sourceLength)
                    : sourceEnd;
                previousSourcePosition = sourcePosition;
            }
        }
    }

    private static uint DecodeLength(BitReader reader, uint lengthIndex, uint matchAdd)
    {
        if (lengthIndex >= LengthExtra.Length)
            throw new InvalidDataException($"GW2 Method-1 length symbol index {lengthIndex} is out of range");

        var extraBits = LengthExtra[lengthIndex];
        return checked((uint)LengthBase[lengthIndex] +
                       (extraBits == 0 ? 0u : reader.Read(extraBits)) +
                       matchAdd);
    }

    private static uint DecodeRegularDistance(BitReader reader, ushort distanceSymbol)
    {
        if (distanceSymbol >= DistanceExtra.Length)
            throw new InvalidDataException($"GW2 Method-1 distance symbol 0x{distanceSymbol:X} is out of range");

        var extraBits = DistanceExtra[distanceSymbol];
        return checked(DistanceBase[distanceSymbol] +
                       (extraBits == 0 ? 0u : reader.Read(extraBits)) + 1u);
    }

    private static uint DecodeSpecialDistance(BitReader reader, ushort distanceSymbol)
    {
        uint distance;
        if (distanceSymbol < 0x3C)
        {
            distance = DistanceBase[distanceSymbol >> 1];
        }
        else
        {
            var shift = (distanceSymbol >> 2) - 2;
            distance = checked((uint)(((distanceSymbol & 2) + 4) << shift));
        }

        if (distanceSymbol >= SpecialDistanceExtraCount)
            throw new InvalidDataException($"GW2 Method-1 source distance symbol 0x{distanceSymbol:X} is out of range");

        var extraBits = distanceSymbol < 8
            ? 0
            : distanceSymbol < 0x7C
                ? distanceSymbol / 4 - 1
                : 0;
        return checked(distance + (extraBits == 0 ? 0u : reader.Read(extraBits)));
    }

    private static void EnsureOutputRoom(uint currentCount, uint length, uint outputSize)
    {
        if (length > outputSize - currentCount)
            throw new InvalidDataException("GW2 Method-1 decoded output exceeds the requested size");
    }

    private const int HistorySize = 0xA000;

    private readonly record struct MetaRow(uint Mask, int Offset, int BitLength);

    private readonly record struct HuffmanKey(int Length, uint Code);

    private sealed class BitReader
    {
        private readonly ReadOnlyMemory<byte> _data;
        private readonly int _endByte;
        private int _position;
        private ulong _accumulator;
        private int _bits;
        private long _consumedBits;

        public BitReader(ReadOnlySpan<byte> data)
        {
            _data = data.ToArray();
            _endByte = _data.Length / 4 * 4;
        }

        public uint Read(int bitCount)
        {
            if (bitCount == 0)
                return 0;

            if (_consumedBits + bitCount > (long)_endByte * 8)
                throw new InvalidDataException("truncated GW2 compression bitstream");
            _consumedBits += bitCount;

            Refill(bitCount);
            _bits -= bitCount;
            var value = (uint)((_accumulator >> _bits) & Mask(bitCount));
            _accumulator &= Mask(_bits);
            return value;
        }

        public uint Peek(int bitCount)
        {
            Refill(bitCount);
            return (uint)((_accumulator >> (_bits - bitCount)) & Mask(bitCount));
        }

        private void Refill(int bitCount)
        {
            while (_bits < bitCount)
            {
                uint word = 0;
                if (_position + 4 <= _endByte)
                {
                    word = BinaryPrimitives.ReadUInt32LittleEndian(_data.Span[_position..]);
                    _position += 4;
                }

                _accumulator = unchecked((_accumulator << 32) | word);
                _bits += 32;
            }
        }

        private static ulong Mask(int bitCount) => bitCount switch
        {
            <= 0 => 0,
            >= 64 => ulong.MaxValue,
            _ => (1ul << bitCount) - 1,
        };
    }

    private sealed class HuffmanTable
    {
        private readonly Dictionary<HuffmanKey, ushort> _symbols;

        private HuffmanTable(Dictionary<HuffmanKey, ushort> symbols) => _symbols = symbols;

        public static HuffmanTable Build(BitReader reader)
        {
            var symbolCount = reader.Read(16);
            if (symbolCount > 4096)
                throw new InvalidDataException($"GW2 Huffman table has an unreasonable symbol count: {symbolCount}");

            var codeLengths = new byte[symbolCount];
            var index = (int)symbolCount - 1;
            while (index >= 0)
            {
                var rle = DecodeMeta(reader);
                var repeat = (rle >> 5) + 1;
                var length = rle & 0x1F;
                if (length != 0 || symbolCount < 2)
                {
                    for (var i = 0; i < repeat; i++)
                    {
                        if (index < 0)
                            throw new InvalidDataException("GW2 Huffman code-length RLE underflow");
                        codeLengths[index--] = (byte)length;
                    }
                }
                else
                {
                    index -= (int)repeat;
                    if (index < -1)
                        throw new InvalidDataException("GW2 Huffman code-length RLE overflow");
                }
            }

            var maxLength = codeLengths.Length == 0 ? 0 : codeLengths.Max();
            if (maxLength > 31)
                throw new InvalidDataException("GW2 Huffman code length is invalid");

            const uint Nil = uint.MaxValue;
            var heads = Enumerable.Repeat(Nil, maxLength + 1).ToArray();
            var next = new uint[symbolCount];
            index = (int)symbolCount - 1;
            while (index >= 0)
            {
                var length = codeLengths[index];
                if (length != 0)
                {
                    next[index] = heads[length];
                    heads[length] = (uint)index;
                }
                index--;
            }

            var symbols = new Dictionary<HuffmanKey, ushort>();
            uint code = 0;
            for (var length = 0; length <= maxLength; length++)
            {
                var symbol = heads[length];
                var codeLimit = 1u << length;
                while (symbol != Nil && code < codeLimit && symbol < symbolCount)
                {
                    symbols.Add(new HuffmanKey(length, code), (ushort)symbol);
                    code = unchecked(code - 1);
                    symbol = next[symbol];
                }

                code = unchecked(2u * code + 1u);
            }

            return new HuffmanTable(symbols);
        }

        public ushort Decode(BitReader reader)
        {
            uint code = 0;
            for (var length = 1; length <= 31; length++)
            {
                code = (code << 1) | reader.Read(1);
                if (_symbols.TryGetValue(new HuffmanKey(length, code), out var symbol))
                    return symbol;
            }

            throw new InvalidDataException("GW2 Huffman symbol could not be decoded");
        }

        private static byte DecodeMeta(BitReader reader)
        {
            var value = reader.Peek(32);
            foreach (var row in MetaTable)
            {
                if (value < row.Mask)
                    continue;

                var relative = (value - row.Mask) >> (32 - row.BitLength);
                var index = row.Offset - (int)relative;
                if ((uint)index >= MetaValues.Length)
                    throw new InvalidDataException("GW2 meta-Huffman index is invalid");
                reader.Read(row.BitLength);
                return MetaValues[index];
            }

            throw new InvalidDataException("GW2 meta-Huffman symbol could not be decoded");
        }
    }

    private sealed class DecoderOutput : IDisposable
    {
        private readonly Stream _destination;
        private readonly byte[] _history;
        private readonly byte[] _pending = new byte[64 * 1024];
        private int _pendingCount;

        public DecoderOutput(Stream destination, int historySize)
        {
            _destination = destination;
            _history = new byte[historySize];
        }

        public uint Count { get; private set; }

        public void Append(byte value)
        {
            _history[Count % (uint)_history.Length] = value;
            _pending[_pendingCount++] = value;
            Count++;
            if (_pendingCount == _pending.Length)
                FlushPending();
        }

        public byte ReadBack(uint backDistance)
        {
            if (backDistance == 0 || backDistance > Count)
                throw new InvalidDataException("GW2 back-reference distance is invalid");
            return _history[(Count - backDistance) % (uint)_history.Length];
        }

        public void Dispose() => FlushPending();

        private void FlushPending()
        {
            if (_pendingCount == 0)
                return;
            _destination.Write(_pending.AsSpan(0, _pendingCount));
            _pendingCount = 0;
        }
    }
}
