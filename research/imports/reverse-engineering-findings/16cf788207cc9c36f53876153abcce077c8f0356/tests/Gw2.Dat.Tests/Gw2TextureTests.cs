using System.Buffers.Binary;
using System.Text;
using Gw2.Core.Dat;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Gw2.Dat.Tests;

[TestClass]
public sealed class Gw2TextureTests
{
    [TestMethod]
    public void ReassemblesPlanarDxtAndBc7Blocks()
    {
        foreach (string format in new[] { "DXT5", "DXTL", "BC7X", "3DCX", "BC5X" })
        {
            var raw=Enumerable.Range(0,32).Select(i=>(byte)i).ToArray();
            var texture=Gw2Texture.Parse(Create(format,8,4,0,raw));
            var expected=raw[..8].Concat(raw[16..20]).Concat(raw[24..28]).Concat(raw[8..16]).Concat(raw[20..24]).Concat(raw[28..32]).ToArray();
            CollectionAssert.AreEqual(expected,texture.DecodeGpuBlocks());
        }
    }
    [TestMethod]
    public void DecodesWhiteAndConstantAlphaRuns()
    {
        var white=Gw2Texture.Parse(Create("DXT1",4,4,1,[0,0,0,0xC0]));
        CollectionAssert.AreEqual(new byte[]{254,255,255,255,255,255,255,255},white.DecodeGpuBlocks());
        var alpha=Gw2Texture.Parse(Create("DXTA",4,4,4,[0,0,0xE0,0xA5]));
        CollectionAssert.AreEqual(new byte[]{0xA5,0xA5,0,0,0,0,0,0},alpha.DecodeGpuBlocks());
    }
    [TestMethod]
    public void RejectsOverlongRunsAndTruncatedPlanes()
    {
        var run=Gw2Texture.Parse(Create("DXT1",4,4,1,[0,0,0,0x60]));
        Assert.Throws<InvalidDataException>(()=>run.DecodeGpuBlocks());
        var shortPlane=Gw2Texture.Parse(Create("DXT5",4,4,0,[0,0,0,0]));
        Assert.Throws<InvalidDataException>(()=>shortPlane.DecodeGpuBlocks());
        var unknown=Gw2Texture.Parse(Create("????",4,4,0,[]));
        Assert.Throws<NotSupportedException>(()=>unknown.DecodeGpuBlocks());
    }
    [TestMethod]
    public void PreservesNonSquareMipDimensions()
    {
        var first=Create("DXT1",2,1,0,new byte[8]);
        var second=Create("DXT1",1,1,0,new byte[8]);
        var texture=Gw2Texture.Parse(first.Concat(second[12..]).ToArray());
        Assert.AreEqual(2,texture.Mips.Count);
        Assert.AreEqual(1,texture.Mips[1].Height);
        Assert.AreEqual(8,texture.DecodeGpuBlocks(1).Length);
    }
    private static byte[] Create(string format,ushort width,ushort height,uint flags,byte[] payload)
    {
        var bytes=new byte[20+payload.Length]; "ATEX"u8.CopyTo(bytes);
        Encoding.ASCII.GetBytes(format).CopyTo(bytes,4);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8),width);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10),height);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12),(uint)(payload.Length+8));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16),flags);payload.CopyTo(bytes,20);
        return bytes;
    }
}
