using System.Buffers.Binary;
using System.Text;

namespace Gw2.Dat;

public sealed record Gw2TextureMip(int Width, int Height, uint EncodingFlags, ReadOnlyMemory<byte> EncodedBytes);

/// <summary>ArenaNet texture container and mip chain, after archive decompression.</summary>
public sealed record Gw2Texture(string Container, string Format, int Width, int Height, IReadOnlyList<Gw2TextureMip> Mips)
{
    public static bool LooksLike(ReadOnlySpan<byte> bytes) => bytes.Length >= 4 &&
        (bytes[..4].SequenceEqual("ATEX"u8) || bytes[..4].SequenceEqual("ATTX"u8) ||
         bytes[..4].SequenceEqual("ATEC"u8) || bytes[..4].SequenceEqual("ATEP"u8) ||
         bytes[..4].SequenceEqual("ATEU"u8) || bytes[..4].SequenceEqual("ATET"u8));

    public static Gw2Texture Parse(ReadOnlyMemory<byte> data)
    {
        var span = data.Span;
        if (span.Length < 12 || !LooksLike(span)) throw new InvalidDataException("Missing ArenaNet texture header");
        int width = BinaryPrimitives.ReadUInt16LittleEndian(span[8..]);
        int height = BinaryPrimitives.ReadUInt16LittleEndian(span[10..]);
        if (width == 0 || height == 0) throw new InvalidDataException("Zero-sized texture");
        int w = width, h = height, offset = 12;
        var mips = new List<Gw2TextureMip>();
        int maxMips = 1 + System.Numerics.BitOperations.Log2((uint)Math.Max(width, height));
        while (offset < span.Length)
        {
            if (span.Length - offset < 8 || mips.Count >= maxMips) throw new InvalidDataException("Invalid texture mip chain");
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(span[offset..]);
            if (size < 8 || size > span.Length - offset || size % 4 != 0) throw new InvalidDataException("Invalid texture mip size");
            mips.Add(new(w, h, BinaryPrimitives.ReadUInt32LittleEndian(span[(offset + 4)..]), data.Slice(offset, (int)size)));
            offset += (int)size;
            w = Math.Max(1, w / 2); h = Math.Max(1, h / 2);
        }
        if (mips.Count == 0) throw new InvalidDataException("Texture has no mip levels");
        return new(Encoding.ASCII.GetString(span[..4]), Encoding.ASCII.GetString(span[4..8]), width, height, mips.AsReadOnly());
    }

    /// <summary>Expands ArenaNet mip coding to GPU block-compressed bytes, not RGBA pixels.</summary>
    public byte[] DecodeGpuBlocks(int mipLevel = 0, int maxOutputBytes = 256 * 1024 * 1024)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxOutputBytes);
        if ((uint)mipLevel >= Mips.Count) throw new ArgumentOutOfRangeException(nameof(mipLevel));
        var mip = Mips[mipLevel];
        bool color = Format is "DXT1" or "DXT2" or "DXT3" or "DXT4" or "DXT5" or "DXTL" or "BC7X";
        bool alpha = Format is "DXT2" or "DXT3" or "DXT4" or "DXT5" or "DXTA" or "DXTL" or "DXTN" or "3DCX" or "BC5X" or "BC7X";
        bool two = Format is "DXT2" or "DXT3" or "DXT4" or "DXT5" or "DXTL" or "DXTN" or "3DCX" or "BC5X" or "BC7X";
        bool dual = Format is "DXTN" or "3DCX" or "BC5X";
        if (Format == "BC7X" && mip.EncodingFlags != 0) throw new NotSupportedException("BC7X palette coding is unsupported");
        if (!color && !alpha) throw new NotSupportedException($"GPU expansion for texture format {Format} is unsupported");
        if ((mip.EncodingFlags & ~15u) != 0) throw new NotSupportedException($"Unknown texture encoding flags {mip.EncodingFlags:X}");
        int blocks = checked(((mip.Width + 3) / 4) * ((mip.Height + 3) / 4));
        int stride = two ? 16 : 8;
        if ((long)blocks * stride > maxOutputBytes) throw new InvalidDataException("Texture output exceeds limit");
        var output = new byte[blocks * stride];
        var alphaDone = new bool[blocks];
        var colorDone = new bool[blocks];
        var bits = new TextureBits(mip.EncodedBytes[8..]);

        void FillRuns(bool[] done, ulong value, int component, bool extraChoice, bool white = false)
        {
            int block = 0;
            while (block < blocks)
            {
                int run = bits.Read(1) == 1 ? 1 : bits.Read(1) == 1 ? 18 : 17 - (int)bits.Read(4);
                bool fill = bits.Read(1) != 0;
                ulong selected = fill && extraChoice && bits.Read(1) == 0 ? 0 : value;
                while (run > 0)
                {
                    if (block >= blocks) throw new InvalidDataException("Texture RLE exceeds block count");
                    if (!done[block])
                    {
                        if (fill)
                        {
                            BinaryPrimitives.WriteUInt64LittleEndian(output.AsSpan(block * stride + component, 8), selected);
                            done[block] = true;
                            if (white) alphaDone[block] = true;
                        }
                        run--;
                    }
                    block++;
                }
                while (block < blocks && done[block]) block++;
            }
        }

        if ((mip.EncodingFlags & 1) != 0)
        {
            if (Format != "DXT1") throw new NotSupportedException("White-block coding requires DXT1");
            FillRuns(colorDone, 0xFFFFFFFFFFFFFFFE, 0, false, true);
        }
        if ((mip.EncodingFlags & 2) != 0)
        {
            if (!alpha) throw new InvalidDataException("Alpha coding on texture without alpha plane");
            ulong nibble = bits.Read(4);
            FillRuns(alphaDone, nibble * 0x1111111111111111UL, 0, true);
        }
        if ((mip.EncodingFlags & 4) != 0)
        {
            if (!alpha) throw new InvalidDataException("Alpha coding on texture without alpha plane");
            ulong value = bits.Read(8);
            FillRuns(alphaDone, value * 0x101, 0, true);
        }
        if ((mip.EncodingFlags & 8) != 0)
        {
            if (!color) throw new NotSupportedException("Plain-color coding requires a color plane");
            FillRuns(colorDone, SolidColor(bits.Read(8), bits.Read(8), bits.Read(8), Format == "DXT1"), two ? 8 : 0, false);
        }

        int rawOffset = bits.AlignedByteOffset;
        void CopyWord(int destination)
        {
            var raw = mip.EncodedBytes.Span[8..];
            if (rawOffset > raw.Length - 4) throw new InvalidDataException("Truncated texture block data");
            raw.Slice(rawOffset, 4).CopyTo(output.AsSpan(destination, 4));
            rawOffset += 4;
        }
        if (alpha)
            for (int i = 0; i < blocks; i++)
                if (!alphaDone[i]) { CopyWord(i * stride); CopyWord(i * stride + 4); }
        if (color || dual)
            for (int word = 0; word < 2; word++)
                for (int i = 0; i < blocks; i++)
                    if (!colorDone[i]) CopyWord(i * stride + (two ? 8 : 0) + word * 4);
        if (rawOffset != mip.EncodedBytes.Length - 8) throw new InvalidDataException("Unconsumed texture mip data");
        return output;
    }

    private static ulong SolidColor(uint high, uint green, uint low, bool bc1)
    {
        uint first = 0, second = 0, weight = 0, changes = 0;
        void Channel(uint value, int shift, int bits)
        {
            uint q = (value - (value >> bits)) >> (8 - bits);
            uint expanded = (q << (8 - bits)) + (q >> (2 * bits - 8));
            uint mask = bits == 5 ? 0x11u : 0x1111u;
            uint residual = 12 * (value - expanded) / (8 - ((q & mask) == mask ? 1u : 0));
            uint a = q + (residual >= 6 ? 1u : 0), b = q + (residual is >= 2 and < 6 or >= 10 ? 1u : 0);
            first |= a << shift; second |= b << shift;
            if (a != b) { changes++; weight += a == q ? residual : 12 - residual; }
        }
        Channel(low, 0, 5); Channel(green, 5, 6); Channel(high, 11, 5);
        if (changes != 0) weight = (weight + changes / 2) / changes;
        bool special = bc1 && (weight is 5 or 6 || changes != 0);
        if (changes != 0 && !special)
        {
            if (second == 0xFFFF) { weight = 12; first--; }
            else { weight = 0; second++; }
        }
        if (second >= first) { (first, second) = (second, first); weight = 12 - weight; }
        uint index = special ? 2u : weight < 2 ? 0u : weight < 6 ? 2u : weight < 10 ? 3u : 1u;
        return first | ((ulong)second << 16) | ((ulong)(index * 0x55555555u) << 32);
    }

    private sealed class TextureBits(ReadOnlyMemory<byte> data)
    {
        private int _position;
        public int AlignedByteOffset => (_position + 31) / 32 * 4;
        public uint Read(int count)
        {
            if ((long)_position + count > data.Length / 4L * 32) throw new InvalidDataException("Truncated texture coding bits");
            uint value = 0;
            for (int i = 0; i < count; i++, _position++)
            {
                var word = BinaryPrimitives.ReadUInt32LittleEndian(data.Span[(_position / 32 * 4)..]);
                value = (value << 1) | ((word >> (31 - _position % 32)) & 1);
            }
            return value;
        }
    }
}
