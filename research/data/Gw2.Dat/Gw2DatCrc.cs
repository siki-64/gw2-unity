namespace Gw2.Dat;

/// <summary>Incremental reflected CRC-32 routines used by ArenaNet archive services.</summary>
public static class Gw2DatCrc
{
    private static readonly uint[] IeeeTable = CreateTable(0xEDB88320);
    private static readonly uint[] CastagnoliTable = CreateTable(0x82F63B78);

    public static uint Compute(ReadOnlySpan<byte> data, uint previous = 0) => Update(data, previous, IeeeTable);
    public static uint ComputeCrc32C(ReadOnlySpan<byte> data, uint previous = 0) => Update(data, previous, CastagnoliTable);

    public static bool Matches(ReadOnlySpan<byte> data, uint expected) =>
        ComputeCrc32C(data) == expected || Compute(data) == expected;

    /// <summary>Checks independent 64 KiB blocks, including the final partial block.</summary>
    public static void VerifyBlocks(ReadOnlySpan<byte> raw)
    {
        for (var offset = 0; offset < raw.Length;)
        {
            var length = Math.Min(65536, raw.Length - offset);
            if (length < 4)
                throw new InvalidDataException($"Incomplete archive CRC at byte {offset}");
            var block = raw.Slice(offset, length);
            var expected = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(block[^4..]);
            if (!Matches(block[..^4], expected))
                throw new InvalidDataException($"Archive block CRC mismatch at byte {offset}");
            offset += length;
        }
    }

    private static uint Update(ReadOnlySpan<byte> data, uint previous, uint[] table)
    {
        var crc = ~previous;
        foreach (var value in data)
            crc = (crc >> 8) ^ table[(crc ^ value) & 255];
        return ~crc;
    }

    private static uint[] CreateTable(uint polynomial)
    {
        var table = new uint[256];
        for (uint i = 0; i < table.Length; i++)
        {
            var value = i;
            for (var bit = 0; bit < 8; bit++)
                value = (value >> 1) ^ ((value & 1) == 0 ? 0 : polynomial);
            table[i] = value;
        }
        return table;
    }
}
