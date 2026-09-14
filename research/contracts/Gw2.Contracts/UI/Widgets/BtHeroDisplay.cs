using System.Runtime.InteropServices;

namespace Gw2.Contracts;

[StructLayout(LayoutKind.Explicit, Size = 0xA8)]
internal struct BtHeroDisplay
{
    [FieldOffset(0x00)] internal nint Vtable;
    [FieldOffset(0x08)] internal Interface008Storage Interface008;
    [StructLayout(LayoutKind.Sequential, Size = 0x50)] internal struct Interface008Storage { }
    [FieldOffset(0x58)] internal nint Interface58;
    [FieldOffset(0x60)] internal nint Interface60;
    [FieldOffset(0x68)] internal nint Interface68;
    [FieldOffset(0x70)] internal nint Unknown70;
    [FieldOffset(0x78)] internal int Unknown78;
    [FieldOffset(0x7C)] internal uint Unknown7C;
    [FieldOffset(0x80)] internal nint Unknown80;
    [FieldOffset(0x88)] internal ushort Unknown88;
    [FieldOffset(0x8A)] internal ushort Unknown8A;
    [FieldOffset(0x8C)] internal uint Unknown8C;
    [FieldOffset(0x90)] internal nint Unknown90;
    [FieldOffset(0x98)] internal nint Unknown98;
    [FieldOffset(0xA0)] internal int UnknownA0;
    [FieldOffset(0xA4)] internal uint UnknownA4;
}
