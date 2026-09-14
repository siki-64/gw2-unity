using System.Runtime.InteropServices;

namespace Gw2.Contracts;

[StructLayout(LayoutKind.Explicit, Size = 0x80)]
internal struct BtHeroInfoFrame
{
    [FieldOffset(0x00)] internal nint Vtable;
    [FieldOffset(0x08)] internal Interface008Storage Interface008;
    [StructLayout(LayoutKind.Sequential, Size = 0x50)] internal struct Interface008Storage { }
    [FieldOffset(0x58)] internal nint Interface58;
    [FieldOffset(0x60)] internal int Unknown60;
    [FieldOffset(0x64)] internal uint Unknown64;
    [FieldOffset(0x68)] internal nint Unknown68;
    [FieldOffset(0x70)] internal int Unknown70;
    [FieldOffset(0x74)] internal uint Unknown74;
    [FieldOffset(0x78)] internal ushort Unknown78;
    [FieldOffset(0x7A)] internal ushort Unknown7A;
    [FieldOffset(0x7C)] internal uint Unknown7C;
}
