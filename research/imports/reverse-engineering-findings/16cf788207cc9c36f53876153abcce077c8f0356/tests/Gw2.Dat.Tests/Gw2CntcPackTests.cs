using System.Buffers.Binary;
using System.Text;
using Gw2.Core.Dat;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Gw2.Dat.Tests;

[TestClass]
public sealed class Gw2CntcPackTests
{
    [TestMethod]
    public void ParsesObjectsAndAllIndexedFixupKinds()
    {
        var pack = Gw2CntcPack.Parse(CreatePack());

        Assert.AreEqual((uint)12, pack.MainOffset);
        Assert.AreEqual((uint)0x80, pack.ContentByteCount);
        Assert.AreEqual(2, pack.Objects.Count);

        var novelty = AssertEx.Single(pack.EnumerateObjects(0x23));
        Assert.AreEqual((uint)0, novelty.Offset);
        Assert.AreEqual((uint)0x40, novelty.EndOffset);
        Assert.AreEqual((uint)95159, pack.ReadContentUInt32(novelty, 0x28));
        Assert.AreEqual("000102030405060708090A0B0C0D0E0F", pack.ReadContentKey(novelty).ToString());

        CollectionAssert.AreEqual(
            new uint[] { 123 },
            pack.EnumerateObjectFileIds(novelty).ToArray());
        CollectionAssert.AreEqual(
            new[] { "Champion" },
            pack.EnumerateObjectStrings(novelty).ToArray());

        var local = AssertEx.Single(pack.EnumerateLocalFixups(novelty));
        Assert.AreEqual((uint)0x10, local.RelocationOffset);
        Assert.AreEqual((uint)0x40, local.TargetOffset);

        var external = AssertEx.Single(pack.EnumerateExternalFixups(novelty));
        Assert.AreEqual((uint)10, external.TargetFileIndex);
        Assert.AreEqual((uint)0x48, external.TargetOffset);

        Assert.IsTrue(pack.TryFindOwningObject(0x48, out var owner));
        Assert.AreEqual((uint)0x40, owner.Offset);
        Assert.AreEqual((uint)0x40, owner.Type);

        var graph = pack.TraverseLocalObjectGraph(novelty);
        Assert.AreEqual(2, graph.Count);
        Assert.AreEqual((uint)0x40, graph[1].Offset);
    }

    private static byte[] CreatePack()
    {
        var data = new byte[0x500];
        data[0] = (byte)'P';
        data[1] = (byte)'F';
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(6), 12);
        Encoding.ASCII.GetBytes("cntc").CopyTo(data, 8);
        Encoding.ASCII.GetBytes("Main").CopyTo(data, 12);

        SetArray(data, 3, 2, 0x100);
        SetArray(data, 4, 1, 0x140);
        SetArray(data, 5, 1, 0x150);
        SetArray(data, 6, 1, 0x160);
        SetArray(data, 7, 1, 0x170);
        SetArray(data, 8, 0, 0x180);
        SetArray(data, 9, 1, 0x1A0);
        SetArray(data, 10, 0x80, 0x300);

        WriteIndexEntry(data, 0x100, 0x23, 0);
        WriteIndexEntry(data, 0x110, 0x40, 0x40);

        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0x140), 0x10);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0x150), 0x20);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0x154), 10);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0x160), 0x24);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0x170), 0x30);

        for (var i = 0; i < 16; i++)
            data[0x300 + i] = (byte)i;
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0x300 + 0x10), 0x40);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0x300 + 0x20), 0x48);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0x300 + 0x24), 123);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0x300 + 0x28), 95159);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0x300 + 0x30), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0x300 + 0x28 + 0x40), 9876);

        BinaryPrimitives.WriteInt64LittleEndian(data.AsSpan(0x1A0), 0x380 - (0x1A0 + 8));
        Encoding.Unicode.GetBytes("Champion\0").CopyTo(data, 0x380);
        return data;
    }

    private static void SetArray(byte[] data, int index, uint count, uint target)
    {
        var descriptor = 32 + index * 12;
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(descriptor), count);
        BinaryPrimitives.WriteInt64LittleEndian(
            data.AsSpan(descriptor + 4),
            checked((long)target - (descriptor + 4)));
    }

    private static void WriteIndexEntry(byte[] data, int offset, uint type, uint contentOffset)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), type);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset + 4), contentOffset);
    }

    private static class AssertEx
    {
        public static T Single<T>(IEnumerable<T> values)
        {
            var array = values.ToArray();
            Assert.AreEqual(1, array.Length);
            return array[0];
        }
    }
}
