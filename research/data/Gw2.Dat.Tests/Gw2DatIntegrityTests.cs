using System.Buffers.Binary;
using Gw2.Dat;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Gw2.Dat.Tests;

[TestClass]
public sealed class Gw2DatIntegrityTests
{
    [TestMethod]
    public void StandardCheckVectorsAndIncrementalUpdates()
    {
        Assert.AreEqual(0xCBF43926u, Gw2DatCrc.Compute("123456789"u8));
        Assert.AreEqual(0xE3069283u, Gw2DatCrc.ComputeCrc32C("123456789"u8));
        Assert.AreEqual(Gw2DatCrc.Compute("123456789"u8), Gw2DatCrc.Compute("56789"u8, Gw2DatCrc.Compute("1234"u8)));
        Assert.AreEqual(Gw2DatCrc.ComputeCrc32C("123456789"u8), Gw2DatCrc.ComputeCrc32C("56789"u8, Gw2DatCrc.ComputeCrc32C("1234"u8)));
    }

    [TestMethod]
    public void VerifiesHeaderMftAtocAndUncompressedMultiBlockAsset()
    {
        var data = CreateArchive();
        WithFile(data, path =>
        {
            using var archive = Gw2DatArchive.Open(path);
            Assert.AreEqual(16u, archive.MftEntryCount);
            Assert.AreEqual(15u, archive.FileIndex[123]);
            archive.VerifyEntry(1);
            archive.VerifyEntry(2);
            archive.VerifyEntry(15);
            using var output = new MemoryStream();
            archive.DecodeFileId(123, output);
            CollectionAssert.AreEqual(Gw2DatMethod0Decoder.StripCrc32(data.AsSpan(4096)), output.ToArray());
            CollectionAssert.AreEqual(output.ToArray()[..12], archive.ReadDecodedPrefix(15));
        });
    }

    [TestMethod]
    public void RejectsCorruptionAtEachIntegrityLayer()
    {
        foreach (var location in new[] { 12, 64 + 120 })
        {
            var data = CreateArchive(); data[location] ^= 1;
            WithFile(data, path => Assert.Throws<InvalidDataException>(() => Gw2DatArchive.Open(path)));
        }
        var atoc = CreateArchive(); atoc[512] ^= 1;
        WithFile(atoc, path => { using var archive = Gw2DatArchive.Open(path); Assert.Throws<InvalidDataException>(() => archive.ReadAtoc()); });
        var payload = CreateArchive(); payload[4100] ^= 1;
        WithFile(payload, path =>
        {
            using var archive = Gw2DatArchive.Open(path);
            using var output = new MemoryStream();
            Assert.Throws<InvalidDataException>(() => archive.DecodeEntry(15, output));
            Assert.AreEqual(0L, output.Length);
        });
    }

    [TestMethod]
    public void ChecksBothBlockAlgorithmsAndRejectsTruncation()
    {
        foreach (bool legacy in new[] { false, true })
        {
            var bytes = new byte[8]; "ABCD"u8.CopyTo(bytes);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), legacy ? Gw2DatCrc.Compute(bytes.AsSpan(0,4)) : Gw2DatCrc.ComputeCrc32C(bytes.AsSpan(0,4)));
            Gw2DatCrc.VerifyBlocks(bytes);
            bytes[0] ^= 1;
            Assert.Throws<InvalidDataException>(() => Gw2DatCrc.VerifyBlocks(bytes));
        }
        Assert.Throws<InvalidDataException>(() => Gw2DatCrc.VerifyBlocks([1,2,3]));
    }

    private static byte[] CreateArchive()
    {
        var bytes = new byte[4096 + 65536 + 12];
        bytes[0] = 0x97; "AN\x1A"u8.CopyTo(bytes.AsSpan(1));
        U32(4, 40); U32(12, 512); U32(16, Gw2DatCrc.ComputeCrc32C(bytes.AsSpan(0,16)));
        U64(24, 64); U32(32, 17 * 24);
        "Mft\x1A"u8.CopyTo(bytes.AsSpan(64)); U32(76, 17);
        Entry(0, 0, 40, 0); Entry(1, 512, 8, 0); Entry(2, 64, 17 * 24, 0);
        U32(512, 123); U32(516, 16);
        U32(64 + 48 + 20, Gw2DatCrc.ComputeCrc32C(bytes.AsSpan(512,8)));
        for (int i = 4096; i < bytes.Length; i++) bytes[i] = (byte)i;
        U32(4096 + 65532, Gw2DatCrc.ComputeCrc32C(bytes.AsSpan(4096,65532)));
        U32(bytes.Length-4, Gw2DatCrc.ComputeCrc32C(bytes.AsSpan(4096+65536,8)));
        Entry(15, 4096, (uint)(bytes.Length-4096), Gw2DatCrc.ComputeCrc32C(bytes.AsSpan(4096)));
        var crc = Gw2DatCrc.ComputeCrc32C(bytes.AsSpan(64,72));
        U32(64+72+20, Gw2DatCrc.ComputeCrc32C(bytes.AsSpan(64+96,17*24-96),crc));
        return bytes;
        void U32(int offset,uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset),value);
        void U64(int offset,ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(offset),value);
        void Entry(int record,ulong offset,uint size,uint checksum)
        {
            int at=64+(record+1)*24; U64(at,offset); U32(at+8,size);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(at+14),3); U32(at+20,checksum);
        }
    }
    private static void WithFile(byte[] bytes, Action<string> action)
    {
        var path = Path.Combine(Path.GetTempPath(), $"gw2-integrity-{Guid.NewGuid():N}.dat");
        try { File.WriteAllBytes(path, bytes); action(path); }
        finally { File.Delete(path); }
    }
}
