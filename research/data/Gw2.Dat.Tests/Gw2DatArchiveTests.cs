using System.Buffers.Binary;
using System.Text;
using Gw2.Dat;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Gw2.Dat.Tests;

[TestClass]
public sealed class Gw2DatArchiveTests
{
    [TestMethod]
    public void ReadsAtocAndScansDecodedRecordsWithPositionalIo()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gw2-dat-{Guid.NewGuid():N}.dat");
        try
        {
            File.WriteAllBytes(path, CreateArchive());

            using var archive = Gw2DatArchive.Open(path, verifyChecksums: false);
            Assert.AreEqual((uint)3, archive.MftEntryCount);
            var atoc = archive.ReadAtoc();
            Assert.AreEqual((uint)2, atoc[0x123]);

            using var copied = new MemoryStream();
            archive.CopyRawEntry(2, copied);
            CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("PF\0\0\0\0\0\0cntc"), copied.ToArray());

            var scan = new Gw2DatScanner(archive)
                .ScanRecords(new Gw2DatScanOptions(RecordCount: 3))
                .ToArray();
            Assert.AreEqual(3, scan.Length);
            Assert.AreEqual(Gw2DatRecordKind.Atoc, scan[1].Kind);
            Assert.AreEqual(Gw2DatRecordKind.ContentStore, scan[2].Kind);
            Assert.AreEqual(12, scan[2].DecodedSize);
            Assert.IsNull(scan[2].Error);
            Assert.AreEqual(Gw2DatRecordKind.ContentStore,
                new Gw2DatScanner(archive).ScanRecords().Last().Kind);
            using var asset = new MemoryStream();
            archive.DecodeFileId(0x123, asset);
            CollectionAssert.AreEqual(copied.ToArray(), asset.ToArray());
            Assert.Throws<KeyNotFoundException>(() => archive.DecodeFileId(0x999, Stream.Null));
            Parallel.For(0, 100, _ =>
            {
                Assert.AreEqual((ulong)288, archive.ReadMftEntry(2).Offset);
                CollectionAssert.AreEqual(copied.ToArray(), archive.ReadRawEntry(2));
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void RejectsMftOutsideArchive()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gw2-dat-{Guid.NewGuid():N}.dat");
        try
        {
            var bytes = CreateArchive();
            BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(24), ulong.MaxValue);
            File.WriteAllBytes(path, bytes);
            Assert.Throws<InvalidDataException>(() => Gw2DatArchive.Open(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static byte[] CreateArchive()
    {
        var data = new byte[320];
        data[0] = 1;
        data[1] = (byte)'A';
        data[2] = (byte)'N';
        data[3] = 0x1A;
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), 40);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(12), 0x1000);
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(24), 64);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(32), 96);

        data[64] = (byte)'M';
        data[65] = (byte)'f';
        data[66] = (byte)'t';
        data[67] = 0x1A;
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(76), 4);

        WriteMftEntry(data, 88, 256, 4);
        WriteMftEntry(data, 112, 272, 8);
        WriteMftEntry(data, 136, 288, 12);

        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(272), 0x123);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(276), 3);
        Encoding.ASCII.GetBytes("PF\0\0\0\0\0\0cntc").CopyTo(data, 288);
        return data;
    }

    private static void WriteMftEntry(byte[] data, int offset, ulong recordOffset, uint size)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(offset), recordOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset + 8), size);
    }
}
